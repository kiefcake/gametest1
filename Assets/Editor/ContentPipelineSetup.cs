using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using DungeonCrawler.Core;
using DungeonCrawler.Inventory;
using DungeonCrawler.Loot;

namespace DungeonCrawler.EditorTools
{
    // Second-pass automated setup, on top of AutoTestSceneSetup: moves Sprites under
    // Resources (so purely-code-driven spawners can Resources.Load them -- see
    // SpriteVisual/EnemyBase, since everything here is built via AddComponent in code
    // rather than prefabs), generates ItemData + LootTable assets from the existing
    // equipment sprites, builds an inventory slot prefab, and wires an inventory Canvas
    // into TestScene. Runs once, the first time scripts compile after this file lands.
    //
    // Every step is individually guarded (checked against what's already on disk) rather
    // than gated behind one master flag, so an interrupted run -- Editor closed mid-way,
    // a compile error part-way through a later change -- recovers on the next compile
    // instead of erroring on "asset already exists" or silently skipping the rest.
    [InitializeOnLoad]
    public static class ContentPipelineSetup
    {
        private const string OldSpritesRoot = "Assets/Sprites";
        private const string SpritesRoot = "Assets/Resources/Sprites";
        private const string ItemsRoot = "Assets/Data/Items";
        private const string LootRoot = "Assets/Resources/Data/Loot";
        private const string PrefabsRoot = "Assets/Prefabs/UI";
        private const string ScenePath = "Assets/Scenes/TestScene.unity";

        static ContentPipelineSetup()
        {
            // This is edit-time content generation (asset creation, scene editing) -- it
            // has no business running during Play mode. Entering/exiting Play mode is
            // itself a domain reload, which re-fires this static constructor; without this
            // guard, Run() would fire mid-playtest and WireInventoryCanvas's OpenScene call
            // would throw (Unity refuses scene loads via that API while playing).
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.delayCall += Run;
        }

        private struct ItemSpec
        {
            public string fileName; // under Sprites/Equipment/, no extension
            public string itemName;
            public string description;
            public ItemCategory category;
            public StatType primaryStat;
            public float primaryStatBonus;
            public StatType potionStat;
            public ItemRarity rarity;
            public bool isUnique;
        }

        private static readonly ItemSpec[] ItemSpecs =
        {
            new ItemSpec { fileName = "potion_hp",  itemName = "HP Potion",  description = "Fills 1/5 of your maximum HP pool. 5 potions fully max the stat.", category = ItemCategory.Potion, potionStat = StatType.HP, rarity = ItemRarity.Common },
            new ItemSpec { fileName = "potion_mp",  itemName = "MP Potion",  description = "Fills 1/5 of your maximum MP pool. 5 potions fully max the stat.", category = ItemCategory.Potion, potionStat = StatType.MP, rarity = ItemRarity.Common },
            new ItemSpec { fileName = "potion_att", itemName = "ATT Potion", description = "Boosts damage dealt. Fills 1/5 of the stat's cap; 5 potions max it.", category = ItemCategory.Potion, potionStat = StatType.ATT, rarity = ItemRarity.Common },
            new ItemSpec { fileName = "potion_def", itemName = "DEF Potion", description = "Reduces damage taken. Fills 1/5 of the stat's cap; 5 potions max it.", category = ItemCategory.Potion, potionStat = StatType.DEF, rarity = ItemRarity.Common },
            new ItemSpec { fileName = "potion_spd", itemName = "SPD Potion", description = "Boosts movement speed. Fills 1/5 of the stat's cap; 5 potions max it.", category = ItemCategory.Potion, potionStat = StatType.SPD, rarity = ItemRarity.Common },
            new ItemSpec { fileName = "potion_dex", itemName = "DEX Potion", description = "Boosts attack/cast rate. Fills 1/5 of the stat's cap; 5 potions max it.", category = ItemCategory.Potion, potionStat = StatType.DEX, rarity = ItemRarity.Common },
            new ItemSpec { fileName = "potion_vit", itemName = "VIT Potion", description = "Boosts HP/MP regen rate. Fills 1/5 of the stat's cap; 5 potions max it.", category = ItemCategory.Potion, potionStat = StatType.VIT, rarity = ItemRarity.Common },
            new ItemSpec { fileName = "potion_wis", itemName = "WIS Potion", description = "Boosts heal/buff potency. Fills 1/5 of the stat's cap; 5 potions max it.", category = ItemCategory.Potion, potionStat = StatType.WIS, rarity = ItemRarity.Common },
            new ItemSpec { fileName = "potion_allstat", itemName = "All-Stat Potion", description = "A smaller boost to all 8 stats at once. Rare -- usually a boss-tier drop.", category = ItemCategory.AllStatPotion, rarity = ItemRarity.Rare },
            new ItemSpec { fileName = "armor_generic", itemName = "Generic Armor", description = "Sturdy armor. Equip it to add flat DEF.", category = ItemCategory.Armor, primaryStat = StatType.DEF, primaryStatBonus = 5f, rarity = ItemRarity.Uncommon },
            new ItemSpec { fileName = "sword_knight", itemName = "Knight's Sword", description = "Half of the Knight's sword-and-shield kit. Equip it to add flat ATT.", category = ItemCategory.Weapon, primaryStat = StatType.ATT, primaryStatBonus = 4f, rarity = ItemRarity.Rare, isUnique = true },
            new ItemSpec { fileName = "shield_knight", itemName = "Knight's Shield", description = "The other half of the set. Equip it to add flat DEF.", category = ItemCategory.Weapon, primaryStat = StatType.DEF, primaryStatBonus = 3f, rarity = ItemRarity.Rare, isUnique = true },
            new ItemSpec { fileName = "wand_priest", itemName = "Priest's Wand", description = "A holy focus. Equip it to add flat WIS.", category = ItemCategory.Weapon, primaryStat = StatType.WIS, primaryStatBonus = 4f, rarity = ItemRarity.Rare, isUnique = true },
            new ItemSpec { fileName = "warhammer_paladin", itemName = "Paladin's Warhammer", description = "Heavy melee weapon. Equip it to add flat ATT.", category = ItemCategory.Weapon, primaryStat = StatType.ATT, primaryStatBonus = 6f, rarity = ItemRarity.Epic, isUnique = true },
            new ItemSpec { fileName = "staff_wizard", itemName = "Wizard's Staff", description = "Channels Fireball/Icicle. Equip it to add flat ATT.", category = ItemCategory.Weapon, primaryStat = StatType.ATT, primaryStatBonus = 5f, rarity = ItemRarity.Rare, isUnique = true },
        };

        private static void Run()
        {
            if (!Directory.Exists("Assets")) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return; // delayCall is async -- Play could have started since registration

            MoveSpritesUnderResources();
            var items = CreateItemAssets();
            CreateLootTables(items);
            var slotPrefab = CreateInventorySlotPrefab();
            WireInventoryCanvas(slotPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void MoveSpritesUnderResources()
        {
            if (AssetDatabase.IsValidFolder(OldSpritesRoot))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
                string error = AssetDatabase.MoveAsset(OldSpritesRoot, SpritesRoot);
                if (!string.IsNullOrEmpty(error))
                    Debug.LogError("[ContentPipelineSetup] Failed to move Sprites under Resources: " + error);
            }

            if (!AssetDatabase.IsValidFolder(SpritesRoot)) return;

            // Also fixes an earlier miscalibration: AutoTestSceneSetup imported these at
            // 16px/unit assuming tiny pixel-art icons, but the actual source art is
            // 72-288px square -- 100px/unit (Unity's default) is what actually matches
            // SpriteVisual's 1x-scale assumption for enemies/pickups.
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SpritesRoot });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                if (importer.textureType == TextureImporterType.Sprite && Mathf.Approximately(importer.spritePixelsPerUnit, 100f))
                    continue; // already configured correctly

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.spritePixelsPerUnit = 100;
                importer.SaveAndReimport();
            }
        }

        private static Dictionary<string, ItemData> CreateItemAssets()
        {
            var result = new Dictionary<string, ItemData>();
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(ItemsRoot)) AssetDatabase.CreateFolder("Assets/Data", "Items");

            foreach (var spec in ItemSpecs)
            {
                string assetPath = $"{ItemsRoot}/{spec.fileName}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
                if (existing != null)
                {
                    // Descriptions/rarity were added after these assets already existed on
                    // disk -- the guard above means CreateAsset never runs again, so patch
                    // them in directly rather than leaving old items with blank tooltips or
                    // stuck at ItemRarity's default (Common) forever.
                    bool dirty = false;
                    if (string.IsNullOrEmpty(existing.description))
                    {
                        existing.description = spec.description;
                        dirty = true;
                    }
                    if (existing.rarity != spec.rarity || existing.isUnique != spec.isUnique)
                    {
                        existing.rarity = spec.rarity;
                        existing.isUnique = spec.isUnique;
                        dirty = true;
                    }
                    if (dirty) EditorUtility.SetDirty(existing);
                    result[spec.fileName] = existing;
                    continue;
                }

                string spritePath = $"{SpritesRoot}/Equipment/{spec.fileName}.png";
                var icon = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (icon == null)
                {
                    Debug.LogWarning($"[ContentPipelineSetup] No sprite at {spritePath} for item '{spec.itemName}' -- creating it without an icon.");
                }

                var item = ScriptableObject.CreateInstance<ItemData>();
                item.itemName = spec.itemName;
                item.description = spec.description;
                item.category = spec.category;
                item.icon = icon;
                item.primaryStat = spec.primaryStat;
                item.primaryStatBonus = spec.primaryStatBonus;
                item.potionStat = spec.potionStat;
                item.rarity = spec.rarity;
                item.isUnique = spec.isUnique;

                AssetDatabase.CreateAsset(item, assetPath);
                result[spec.fileName] = item;
            }
            return result;
        }

        private static void CreateLootTables(Dictionary<string, ItemData> items)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Data")) AssetDatabase.CreateFolder("Assets/Resources", "Data");
            if (!AssetDatabase.IsValidFolder(LootRoot)) AssetDatabase.CreateFolder("Assets/Resources/Data", "Loot");

            // Common table: the two imps. Modest chances, no weapons -- trash mobs.
            CreateLootTable($"{LootRoot}/AbyssLootTable.asset", new[]
            {
                ("potion_hp", 0.10f), ("potion_mp", 0.10f), ("potion_att", 0.08f), ("potion_def", 0.08f),
                ("potion_spd", 0.08f), ("potion_dex", 0.08f), ("potion_vit", 0.08f), ("potion_wis", 0.08f),
                ("armor_generic", 0.04f),
            }, items);

            // Boss table: much better odds across the board, plus a shot at every weapon
            // piece and the rare all-stat potion -- this is meant to feel like a real drop.
            CreateLootTable($"{LootRoot}/AbyssBossLootTable.asset", new[]
            {
                ("potion_hp", 0.35f), ("potion_mp", 0.35f), ("potion_att", 0.3f), ("potion_def", 0.3f),
                ("potion_spd", 0.3f), ("potion_dex", 0.3f), ("potion_vit", 0.3f), ("potion_wis", 0.3f),
                ("potion_allstat", 0.15f), ("armor_generic", 0.2f),
                ("sword_knight", 0.1f), ("shield_knight", 0.1f), ("wand_priest", 0.1f),
                ("warhammer_paladin", 0.1f), ("staff_wizard", 0.1f),
            }, items);
        }

        private static void CreateLootTable(string assetPath, (string key, float chance)[] entries, Dictionary<string, ItemData> items)
        {
            if (AssetDatabase.LoadAssetAtPath<LootTable>(assetPath) != null) return;

            var table = ScriptableObject.CreateInstance<LootTable>();
            foreach (var (key, chance) in entries)
            {
                if (!items.TryGetValue(key, out var item)) continue;
                table.entries.Add(new LootEntry { item = item, dropChance = chance });
            }
            AssetDatabase.CreateAsset(table, assetPath);
        }

        private static GameObject CreateInventorySlotPrefab()
        {
            string prefabPath = $"{PrefabsRoot}/InventorySlot.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabsRoot)) AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

            var slotSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesRoot}/UI/inventory_slot.png");

            var root = new GameObject("InventorySlot", typeof(RectTransform), typeof(Image));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(56, 56);
            var bg = root.GetComponent<Image>();
            bg.sprite = slotSprite;

            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(root.transform, false);
            var iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.12f, 0.12f);
            iconRect.anchorMax = new Vector2(0.88f, 0.88f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            iconGO.GetComponent<Image>().enabled = false; // InventoryUI.Redraw enables/sets this per-slot

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void WireInventoryCanvas(GameObject slotPrefab)
        {
            if (!File.Exists(ScenePath)) return; // AutoTestSceneSetup hasn't run yet

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (Object.FindFirstObjectByType<InventoryUI>() != null) return; // already wired

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            var canvasGO = new GameObject("InventoryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesRoot}/UI/inventory_panel.png");
            var panelGO = new GameObject("InventoryPanel", typeof(RectTransform), typeof(Image));
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.zero;
            panelRect.pivot = Vector2.zero;
            panelRect.anchoredPosition = new Vector2(20, 20);
            panelRect.sizeDelta = new Vector2(360, 300);
            var panelImage = panelGO.GetComponent<Image>();
            panelImage.sprite = panelSprite;
            panelImage.color = new Color(1f, 1f, 1f, 0.9f);

            var gridGO = new GameObject("SlotGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGO.transform.SetParent(panelGO.transform, false);
            var gridRect = gridGO.GetComponent<RectTransform>();
            gridRect.anchorMin = Vector2.zero;
            gridRect.anchorMax = Vector2.one;
            gridRect.offsetMin = new Vector2(12, 12);
            gridRect.offsetMax = new Vector2(-12, -12);
            var grid = gridGO.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(56, 56);
            grid.spacing = new Vector2(6, 6);

            var invUI = canvasGO.AddComponent<InventoryUI>();
            invUI.gridParent = gridRect;
            invUI.slotPrefab = slotPrefab;
            invUI.slotSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesRoot}/UI/inventory_slot.png");
            invUI.slotHighlightSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesRoot}/UI/inventory_slot_highlight.png");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
