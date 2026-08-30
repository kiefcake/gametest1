using UnityEngine;
using UnityEngine.UI;
using DungeonCrawler.Classes;
using DungeonCrawler.Visuals;
using DungeonCrawler.UI;
using DungeonCrawler.Loot;

namespace DungeonCrawler.Inventory
{
    // Grid UI, restyled at runtime with procedurally-drawn rounded panels/slots (see
    // PanelSpriteFactory) instead of the flat placeholder squares baked into the edit-time
    // scene (Sprites/UI/inventory_panel.png etc) -- those still exist on disk but are no
    // longer assigned, since a rounded-and-bordered sprite reads as noticeably more
    // "designed" than a solid square with a 1px edge.
    public class InventoryUI : MonoBehaviour
    {
        public InventorySystem inventory;
        public RectTransform gridParent;
        public GameObject slotPrefab; // simple prefab: Image (slot bg) + child Image (item icon)
        public Sprite slotSprite;
        public Sprite slotHighlightSprite;

        // Set by GameBootstrap alongside SetInventory -- needed to actually consume/equip
        // an item (both take a StatBlock) and to refresh Health/Mana maxes after.
        public PlayerCharacter player;
        // Set by GameBootstrap -- equipping a weapon updates the in-hand viewmodel too,
        // not just the floating icon PlayerCharacter already owns.
        public WeaponViewmodel viewmodel;

        private GameObject[] slotObjects;
        private HoverTooltip[] slotTooltips;
        private Image[] slotIcons;
        private Image[] slotRarityBgs;
        private Sprite slotBgSprite;

        // Weapon / Armor / Ring, in display order -- Ring is new (see InventorySystem),
        // matching both RotMG's real 4-slot convention (Weapon/Ability/Armor/Ring, per
        // RealmEye) and the design doc's own "Accessory/trinket" slot.
        private static readonly ItemCategory[] EquipSlots = { ItemCategory.Weapon, ItemCategory.Armor, ItemCategory.Ring };
        private readonly Image[] equipIcons = new Image[EquipSlots.Length];
        private readonly Image[] equipRarityBgs = new Image[EquipSlots.Length];
        private readonly HoverTooltip[] equipTooltips = new HoverTooltip[EquipSlots.Length];

        private const float PanelWidth = 620f;
        private const float PanelHeight = 620f;
        private const float TitleHeight = 64f; // room for the title plus the left/right-click hint line below it
        private const float EquipRowHeight = 112f;
        private const float GridPadding = 20f;

        private static readonly Color PanelFill = new Color(0.08f, 0.08f, 0.11f, 0.97f);
        private static readonly Color PanelBorder = new Color(0.42f, 0.4f, 0.58f, 1f);
        private static readonly Color SlotFill = new Color(0.15f, 0.15f, 0.2f, 1f);
        private static readonly Color SlotBorder = new Color(0.34f, 0.34f, 0.44f, 1f);

        // This panel lives in the scene at edit time (built by ContentPipelineSetup), which
        // may predate fields added later -- found at runtime via child name instead of a
        // serialized reference so it works against scenes built by either version.
        private GameObject panelRoot;

        private void Awake()
        {
            var panelTransform = transform.Find("InventoryPanel");
            panelRoot = panelTransform != null ? panelTransform.gameObject : null;

            if (panelRoot != null)
            {
                RestylePanel();
                BuildEquipmentSlots();
                panelRoot.SetActive(false); // closed by default -- it used to just sit open the whole time
            }
        }

        private void OnEnable()
        {
            if (inventory != null)
            {
                inventory.OnChanged += Redraw;
                inventory.OnEquipmentChanged += RedrawEquipment;
            }
            BuildGrid();
            Redraw();
            RedrawEquipment();
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.OnChanged -= Redraw;
                inventory.OnEquipmentChanged -= RedrawEquipment;
            }
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.I) || panelRoot == null) return;

            bool open = !panelRoot.activeSelf;
            panelRoot.SetActive(open);

            // Unity's UI input module won't route clicks to a Button while the cursor is
            // locked (see PointerInputModule.Process) -- without this, the panel opened but
            // every slot/equip click silently did nothing. Same fix ShopUI needed.
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }

        // This panel lives in the scene at edit time, but the player (and its
        // InventorySystem) doesn't exist until GameBootstrap spawns it in Play mode --
        // well after this component's own OnEnable already ran with inventory == null.
        // Spawners call this once the InventorySystem exists to (re)wire and redraw.
        public void SetInventory(InventorySystem inv)
        {
            if (inventory != null)
            {
                inventory.OnChanged -= Redraw;
                inventory.OnEquipmentChanged -= RedrawEquipment;
            }
            inventory = inv;
            if (inventory != null)
            {
                inventory.OnChanged += Redraw;
                inventory.OnEquipmentChanged += RedrawEquipment;
            }
            BuildGrid();
            Redraw();
            RedrawEquipment();
        }

        // Recenters and enlarges the edit-time panel, swaps its flat placeholder sprite for
        // a procedurally-drawn rounded one, and adds a title -- all overriding whatever
        // ContentPipelineSetup baked into the scene at edit time (see that class's own
        // comments on why runtime code takes priority over saved scene state here).
        private void RestylePanel()
        {
            var panelRect = panelRoot.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            var panelImage = panelRoot.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.sprite = PanelSpriteFactory.CreateRoundedSprite(PanelFill, PanelBorder);
                panelImage.type = Image.Type.Sliced;
                panelImage.color = Color.white; // the sprite already carries its own fill color
            }

            var font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGO.transform.SetParent(panelRoot.transform, false);
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0, -14);
            titleRect.sizeDelta = new Vector2(PanelWidth - 40, 30);
            var titleText = titleGO.GetComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 22;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
            titleText.text = "INVENTORY (I to close)";

            var hintGO = new GameObject("Hint", typeof(RectTransform), typeof(Text));
            hintGO.transform.SetParent(panelRoot.transform, false);
            var hintRect = hintGO.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, 1f);
            hintRect.anchorMax = new Vector2(0.5f, 1f);
            hintRect.pivot = new Vector2(0.5f, 1f);
            hintRect.anchoredPosition = new Vector2(0, -TitleHeight + 4f);
            hintRect.sizeDelta = new Vector2(PanelWidth - 40, 18);
            var hintText = hintGO.GetComponent<Text>();
            hintText.font = font;
            hintText.fontSize = 12;
            hintText.fontStyle = FontStyle.Italic;
            hintText.alignment = TextAnchor.MiddleCenter;
            hintText.color = new Color(0.65f, 0.65f, 0.7f);
            hintText.text = "Left-click to use/equip -- Right-click to drop";

            if (gridParent != null)
            {
                gridParent.anchorMin = Vector2.zero;
                gridParent.anchorMax = Vector2.one;
                gridParent.offsetMin = new Vector2(GridPadding, GridPadding);
                gridParent.offsetMax = new Vector2(-GridPadding, -(TitleHeight + EquipRowHeight));

                var grid = gridParent.GetComponent<GridLayoutGroup>();
                if (grid != null)
                {
                    grid.cellSize = new Vector2(72, 72);
                    grid.spacing = new Vector2(10, 10);
                }
            }

            slotBgSprite = PanelSpriteFactory.CreateRoundedSprite(SlotFill, SlotBorder, size: 64, radius: 10, borderThickness: 3);
        }

        private void BuildGrid()
        {
            if (gridParent == null || slotPrefab == null || inventory == null) return;
            slotObjects = new GameObject[inventory.SlotCount];
            slotTooltips = new HoverTooltip[inventory.SlotCount];
            slotIcons = new Image[inventory.SlotCount];
            slotRarityBgs = new Image[inventory.SlotCount];

            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var go = Instantiate(slotPrefab, gridParent);
                var bg = go.GetComponent<Image>();
                if (bg != null)
                {
                    bg.sprite = slotBgSprite != null ? slotBgSprite : slotSprite;
                    bg.type = Image.Type.Sliced;
                    bg.color = Color.white;
                }

                slotRarityBgs[i] = BuildRarityBackdrop(go.transform);
                var iconTransform = go.transform.Find("Icon");
                slotIcons[i] = iconTransform != null ? iconTransform.GetComponent<Image>() : null;

                int index = i; // capture per-iteration value, not the loop variable
                var button = go.AddComponent<Button>();
                button.onClick.AddListener(() => OnSlotClicked(index));
                go.AddComponent<RightClickHandler>().onRightClick = () => OnSlotRightClicked(index);
                slotTooltips[i] = go.AddComponent<HoverTooltip>();

                slotObjects[i] = go;
            }
        }

        // A colored diamond/backdrop behind an item's icon, RealmEye/RotMG-style rarity
        // coloring (see RarityColors) -- inserted as the first sibling so it renders behind
        // whatever icon Image already exists in the slot (grid slots have one from the
        // prefab; equip slots get one built alongside it in BuildEquipSlot).
        private Image BuildRarityBackdrop(Transform parent)
        {
            var go = new GameObject("RarityBg", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.1f, 0.1f);
            rect.anchorMax = new Vector2(0.9f, 0.9f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.transform.SetAsFirstSibling();
            var img = go.GetComponent<Image>();
            img.enabled = false;
            return img;
        }

        // Click a potion slot to drink it, or a weapon/armor/ring slot to equip it.
        // Cosmetics don't have any effect system yet, so clicking those is a no-op.
        private void OnSlotClicked(int index)
        {
            if (player == null || inventory == null) return;
            var item = inventory.GetAt(index);
            if (item == null) return;

            if (item.category == ItemCategory.Potion || item.category == ItemCategory.AllStatPotion)
            {
                if (inventory.UsePotionAt(index, player.Stats))
                    player.RefreshDerivedStats();
            }
            else if (item.category == ItemCategory.Weapon || item.category == ItemCategory.Armor || item.category == ItemCategory.Ring)
            {
                bool wasWeapon = item.category == ItemCategory.Weapon;
                if (inventory.Equip(index, player.Stats))
                {
                    player.RefreshDerivedStats();
                    if (wasWeapon)
                    {
                        player.SetWeaponIcon(item.icon);
                        viewmodel?.SetSprite(item.icon);
                    }
                }
            }
        }

        // Right-click a filled slot to drop it on the ground in front of the player --
        // the only way to empty a slot without using/equipping it, and how loot gets left
        // behind for another party member instead of only ever going straight into a bag.
        private void OnSlotRightClicked(int index)
        {
            if (player == null || inventory == null) return;
            var item = inventory.GetAt(index);
            if (item == null) return;

            inventory.RemoveAt(index);
            ItemDropper.Drop(item, player.transform);
        }

        private void Redraw()
        {
            if (slotObjects == null) return;
            for (int i = 0; i < slotObjects.Length; i++)
            {
                var item = inventory.GetAt(i);
                SetSlotVisual(slotIcons[i], slotRarityBgs[i], item);

                if (slotTooltips[i] != null)
                {
                    slotTooltips[i].content = DescribeItem(item);
                    slotTooltips[i].RefreshIfHovering();
                }
            }
        }

        private static void SetSlotVisual(Image icon, Image rarityBg, ItemData item)
        {
            if (icon != null)
            {
                if (item != null) { icon.sprite = item.icon; icon.enabled = true; }
                else icon.enabled = false;
            }
            if (rarityBg != null)
            {
                if (item != null) { rarityBg.color = RarityColors.Get(item.rarity); rarityBg.enabled = true; }
                else rarityBg.enabled = false;
            }
        }

        // RealmEye-style tooltip prefix: "{Rarity} {Name}{ UT}" then the flavor description
        // on its own line -- see a RealmEye character page's item tooltips for the source.
        private static string DescribeItem(ItemData item)
        {
            if (item == null) return null;
            string header = $"{RarityColors.Label(item.rarity)} {item.itemName}{(item.isUnique ? " UT" : "")}";
            return string.IsNullOrEmpty(item.description) ? header : $"{header}\n{item.description}";
        }

        // Builds the equip-slot row, positioned just below the title (see RestylePanel) --
        // done entirely at runtime (not via ContentPipelineSetup) so it works regardless of
        // when the scene's InventoryCanvas was originally built, and to sidestep edit-time
        // asset-creation entirely (see Loot/LootTable.cs for why that's been worth avoiding
        // this session).
        private void BuildEquipmentSlots()
        {
            var rowGO = new GameObject("EquipmentRow", typeof(RectTransform));
            rowGO.transform.SetParent(panelRoot.transform, false);
            var rowRect = rowGO.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0, -TitleHeight);
            rowRect.sizeDelta = new Vector2(0, EquipRowHeight);

            string[] labels = { "weapon", "armor", "ring" };
            float[] xOffsets = { -110f, 0f, 110f };
            for (int i = 0; i < EquipSlots.Length; i++)
            {
                var slot = EquipSlots[i];
                var (icon, rarityBg, tooltip) = BuildEquipSlot(rowGO.transform, labels[i] + "Slot", xOffsets[i], labels[i], () => OnEquipSlotClicked(slot));
                equipIcons[i] = icon;
                equipRarityBgs[i] = rarityBg;
                equipTooltips[i] = tooltip;
            }
        }

        private (Image icon, Image rarityBg, HoverTooltip tooltip) BuildEquipSlot(
            Transform parent, string name, float xOffset, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(xOffset, 0);
            rect.sizeDelta = new Vector2(76, 76);
            var goImage = go.GetComponent<Image>();
            goImage.sprite = slotBgSprite;
            goImage.type = Image.Type.Sliced;
            goImage.color = Color.white;
            go.GetComponent<Button>().onClick.AddListener(onClick);

            var rarityBg = BuildRarityBackdrop(go.transform);

            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(go.transform, false);
            var iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.14f, 0.14f);
            iconRect.anchorMax = new Vector2(0.86f, 0.86f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var icon = iconGO.GetComponent<Image>();
            icon.enabled = false;

            var labelGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGO.transform.SetParent(go.transform, false);
            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 1f);
            labelRect.anchorMax = new Vector2(0.5f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0, 4);
            labelRect.sizeDelta = new Vector2(90, 16);
            var labelText = labelGO.GetComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            labelText.fontSize = 12;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = new Color(0.75f, 0.75f, 0.8f);
            labelText.text = label.ToUpper();

            var tooltip = go.AddComponent<HoverTooltip>();
            return (icon, rarityBg, tooltip);
        }

        // Clicking an equip slot unequips back into the inventory grid.
        private void OnEquipSlotClicked(ItemCategory slot)
        {
            if (player == null || inventory == null) return;
            var item = inventory.GetEquipped(slot);
            if (item == null) return;

            if (inventory.Unequip(slot, player.Stats))
            {
                player.RefreshDerivedStats();
                if (slot == ItemCategory.Weapon)
                {
                    // Fall back to the class's default weapon sprite once nothing's equipped.
                    var fallback = player.classDefinition != null ? player.classDefinition.weaponSprite : null;
                    player.SetWeaponIcon(fallback);
                    viewmodel?.SetSprite(fallback);
                }
            }
        }

        private void RedrawEquipment()
        {
            if (inventory == null) return;
            for (int i = 0; i < EquipSlots.Length; i++)
            {
                var item = inventory.GetEquipped(EquipSlots[i]);
                SetSlotVisual(equipIcons[i], equipRarityBgs[i], item);
                if (equipTooltips[i] != null)
                {
                    equipTooltips[i].content = DescribeItem(item);
                    equipTooltips[i].RefreshIfHovering();
                }
            }
        }
    }
}
