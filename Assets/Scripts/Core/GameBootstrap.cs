using System.Collections.Generic;
using UnityEngine;
using DungeonCrawler.Classes;
using DungeonCrawler.Enemies;
using DungeonCrawler.Core;
using DungeonCrawler.Abilities;
using DungeonCrawler.World;
using DungeonCrawler.Loot;
using DungeonCrawler.Visuals;
using DungeonCrawler.UI;

namespace DungeonCrawler
{
    // Drop this on an empty GameObject in a test scene and hit Play. Shows a character
    // select screen, then spawns that class into the hub -- a safe plaza with vendors and
    // training dummies. Walking up to the gate and pressing E is what actually builds the
    // abyss encounter (imps across two combat rooms, a guaranteed-loot vault, and the boss)
    // and drops you into it; a return gate near the dungeon's entry room brings you back.
    public class GameBootstrap : MonoBehaviour
    {
        public enum TestClass { Knight, Priest, Paladin, Wizard }
        public TestClass classToTest = TestClass.Knight; // pre-selected highlight only -- CharacterSelectUI always runs
        public bool spawnAbyssEncounter = true;
        public int startingGold = 60;

        // Kept as fields (rather than threaded through method params) so EnterDungeon/
        // ReturnToHub/WireVendors -- all called later, from Interactable.onInteract
        // closures or the gate itself -- can reach the player/economy without re-deriving
        // them each time.
        private GameObject playerGO;
        private PlayerCharacter player;
        private Inventory.InventorySystem playerInventory;
        private PlayerWallet wallet;
        private Vector3 hubEntryPoint;
        private Vector3 currentAreaEntryPoint; // wherever TeleportPlayer last put the player -- see FallRecovery
        private GameObject dungeonRoot;
        private HubLayout hub;

        // Which camps have already been cleared this run -- survives the open world's own
        // Destroy(dungeonRoot) (the camps live inside it, this doesn't) so beating one
        // camp then entering a different biome's dungeon doesn't undo it: without this,
        // leaving via any camp portal destroyed the whole OpenWorld GameObject including
        // the OTHER two already-earned portals, forcing a full re-clear of biomes the
        // player had already finished.
        private bool wastesCleared, frostlandsCleared, marshlandsCleared;

        private void Start()
        {
            // Reset here, before the character select screen builds -- CharacterSelectUI
            // now hosts the Hardcore toggle (see BuildUI), and BeginRun runs AFTER the
            // player has already clicked a class card, so resetting inside BeginRun would
            // wipe out whatever they just picked on that same screen.
            RunModifiers.ResetAll();
            CharacterSelectUI.Show(BeginRun);
        }

        private void BeginRun(TestClass chosenClass)
        {
            classToTest = chosenClass;
            wastesCleared = frostlandsCleared = marshlandsCleared = false;

            var hubGO = new GameObject("Hub");
            hub = hubGO.AddComponent<HubLayout>();
            hubEntryPoint = hub.EntryPoint;

            playerGO = new GameObject("Player_" + classToTest);
            var health = playerGO.AddComponent<Health>();
            var status = playerGO.AddComponent<StatusEffectController>();
            var caster = playerGO.AddComponent<AbilityCaster>();
            player = playerGO.AddComponent<PlayerCharacter>();
            playerInventory = playerGO.AddComponent<Inventory.InventorySystem>();
            player.inventory = playerInventory; // was never actually wired up before -- nothing read it, but it's the documented hook for exactly this kind of lookup
            TeleportPlayer(hubEntryPoint);

            ClassDefinition def = classToTest switch
            {
                TestClass.Knight => DefaultContentFactory.CreateKnight(),
                TestClass.Priest => DefaultContentFactory.CreatePriest(),
                TestClass.Paladin => DefaultContentFactory.CreatePaladin(),
                _ => DefaultContentFactory.CreateWizard(),
            };
            player.Initialize(def); // explicit init call -- see PlayerCharacter for why this replaced a SendMessage("Awake") hack

            // No progression system exists yet to unlock this "for real" (see the full
            // scope doc's "ultimate unlocks later" note) -- force it open so it's testable
            // now instead of the slot 3 sitting permanently, silently uncastable.
            caster.ultimateUnlocked = true;

            wallet = playerGO.AddComponent<PlayerWallet>();
            wallet.Add(startingGold);

            playerGO.AddComponent<PlayerMovement>();
            var abilityInput = playerGO.AddComponent<PlayerAbilityInput>(); // 1/2/3 or RMB cast Basic1/Basic2/Ultimate -- see PlayerAbilityInput for the crosshair targeting rule
            var autoAttack = playerGO.AddComponent<AutoAttack>(); // hold LMB -- free, DEX-scaled attack rate, ATT-scaled damage
            autoAttack.isMelee = def.isMelee;
            if (def.isMelee)
            {
                // Shorter reach in exchange for hitting harder -- a melee class standing
                // in someone's face should feel scarier than the old shared 12-unit/
                // 8-damage default let it.
                autoAttack.castRange = 2.5f;
                autoAttack.baseDamage = 14f;
            }
            else
            {
                autoAttack.castRange = 12f;
                autoAttack.baseDamage = 7f;
                autoAttack.projectileCount = def.rangedShotCount;
            }
            playerGO.AddComponent<PlayerRegen>(); // passive HP/MP regen, scaled by VIT
            playerGO.AddComponent<PlayerInteraction>(); // E -- opens shops, enters/exits the dungeon via Interactable markers
            playerGO.AddComponent<PlayerDash>(); // Left Shift -- directional dodge burst, goes through CharacterController so it can't punch through walls
            var downedRecovery = playerGO.AddComponent<DownedRecovery>(); // solo safety net -- no second player exists to proximity-revive a downed player, see DownedRecovery
            downedRecovery.onRunFailed = RespawnAfterDefeat;
            playerGO.AddComponent<FallRecovery>().onFellOutOfWorld = RecoverFromFall; // geometry-seam safety net -- see FallRecovery

            // First-person: camera parented at eye height, mouse-look yaws the player body
            // (so movement turns with it) and pitches only the camera. The player's own
            // visual capsule/weapon-icon sit on LocalVisualLayer so this camera doesn't end
            // up staring at the inside of its own head. FOV bumped up from Unity's default
            // 60 -- that reads as narrow/zoomed-in for a first-person game.
            var camGO = Camera.main != null ? Camera.main.gameObject : new GameObject("Main Camera", typeof(Camera));
            camGO.tag = "MainCamera";
            camGO.transform.SetParent(playerGO.transform);
            camGO.transform.localPosition = new Vector3(0, 1.6f, 0);
            camGO.transform.localRotation = Quaternion.identity;
            var cam = camGO.GetComponent<Camera>();
            if (cam != null)
            {
                cam.cullingMask &= ~(1 << PlayerCharacter.LocalVisualLayer);
                cam.fieldOfView = 82f;
            }
            var look = camGO.AddComponent<FirstPersonLook>();
            look.playerBody = playerGO.transform;

            var viewmodel = WeaponViewmodel.Attach(camGO.transform, def.weaponSprite);
            abilityInput.viewmodel = viewmodel;
            autoAttack.viewmodel = viewmodel; // same weapon sprite swings for both ability casts and auto-attacks

            // The inventory Canvas lives in the scene at edit time (see TestScene), but the
            // InventorySystem it displays doesn't exist until now -- wire them together.
            // player is needed too, so clicking a potion slot can actually consume it
            // (UsePotionAt takes a StatBlock) and refresh Health/Mana maxes afterward.
            var inventoryUI = FindFirstObjectByType<Inventory.InventoryUI>();
            if (inventoryUI != null)
            {
                inventoryUI.player = player;
                inventoryUI.viewmodel = viewmodel; // so equipping a weapon updates the in-hand sprite too
                inventoryUI.SetInventory(playerInventory);
            }

            PlayerHUD.Build(player, wallet, downedRecovery);
            StatScreenUI.Build(player); // toggle with C
            PauseMenuUI.Build(); // toggle with Escape -- owns cursor lock/timeScale pausing
            DebugTools.Build(player, wallet); // F1-F5 testing hotkeys -- see DebugTools for the list

            hub.GateInteractable.onInteract = EnterOpenWorld;
            WireVendors();
            WireMinigames();

            Debug.Log($"[Bootstrap] Spawned {def.className} in the Hub -- HP {health.maxHP}, abilities: " +
                string.Join(", ", def.abilities.ConvertAll(a => a.abilityName)));
        }

        // Fills in each HubLayout-built VendorNPC's stock (needs a Resources.Load to
        // resolve ItemData references -- see LoadItemPool) and wires its Interactable to
        // open ShopUI with this run's inventory/wallet.
        private void WireVendors()
        {
            var pool = LoadItemPool();

            var alchemistStock = new List<ShopStock>
            {
                Stock(pool, "HP Potion", 20), Stock(pool, "MP Potion", 20), Stock(pool, "ATT Potion", 20),
                Stock(pool, "DEF Potion", 20), Stock(pool, "SPD Potion", 20), Stock(pool, "DEX Potion", 20),
                Stock(pool, "VIT Potion", 20), Stock(pool, "WIS Potion", 20),
            };
            var blacksmithStock = new List<ShopStock>
            {
                Stock(pool, "Generic Armor", 200), Stock(pool, "Knight's Sword", 180), Stock(pool, "Knight's Shield", 150),
                Stock(pool, "Priest's Wand", 180), Stock(pool, "Paladin's Warhammer", 220), Stock(pool, "Wizard's Staff", 200),
            };
            var curiosityStock = new List<ShopStock>
            {
                Stock(pool, "All-Stat Potion", 140),
                new ShopStock { item = Inventory.RingFactory.CreateVitalityBand(), price = 160 },
                new ShopStock { item = Inventory.RingFactory.CreatePowerSignet(), price = 260 },
            };

            alchemistStock.RemoveAll(s => s.item == null);
            blacksmithStock.RemoveAll(s => s.item == null);
            curiosityStock.RemoveAll(s => s.item == null);
            Debug.Log($"[Bootstrap] WireVendors: pool={pool.Count}, alchemist={alchemistStock.Count}, blacksmith={blacksmithStock.Count}, curiosities={curiosityStock.Count}");

            int vendorCount = 0;
            foreach (var vendor in FindObjectsByType<VendorNPC>(FindObjectsSortMode.None))
            {
                vendorCount++;
                vendor.stock = vendor.vendorName switch
                {
                    "Alchemist" => alchemistStock.ToArray(),
                    "Blacksmith" => blacksmithStock.ToArray(),
                    "Curiosities" => curiosityStock.ToArray(),
                    _ => new ShopStock[0],
                };
                Debug.Log($"[Bootstrap] Vendor '{vendor.vendorName}' wired with {vendor.stock.Length} stock entries");

                var interactable = vendor.GetComponent<Interactable>();
                if (interactable == null) continue;
                var v = vendor; // capture per-iteration value, not the loop variable
                interactable.onInteract = () => ShopUI.Show(v, playerInventory, wallet);
            }
            Debug.Log($"[Bootstrap] WireVendors found {vendorCount} VendorNPC(s) in the scene");
        }

        // Wires the tavern's gambling table and the fairground's claw machine, the two
        // "fun things to do" the hub didn't have before -- same split as WireVendors: the
        // gate/table/cabinet geometry and empty data already exist (see HubLayout), this
        // just fills in the claw machine's prize pool and hooks both Interactables up to
        // their UI now that wallet/inventory exist.
        private void WireMinigames()
        {
            if (hub.GambleInteractable != null)
                hub.GambleInteractable.onInteract = () => GambleUI.Show(wallet);

            if (hub.ClawMachine != null)
            {
                var pool = LoadItemPool();
                var prizes = new List<Inventory.ItemData>
                {
                    Stock(pool, "HP Potion", 0).item, Stock(pool, "MP Potion", 0).item,
                    Stock(pool, "ATT Potion", 0).item, Stock(pool, "All-Stat Potion", 0).item,
                };
                prizes.RemoveAll(item => item == null);
                hub.ClawMachine.prizePool = prizes.ToArray();

                var interactable = hub.ClawMachine.GetComponent<Interactable>();
                if (interactable != null)
                    interactable.onInteract = () => ClawMachineUI.Show(hub.ClawMachine, playerInventory, wallet);
            }
        }

        // Reuses the boss loot table purely as a Resources-loadable pool of every ItemData
        // asset -- items themselves live under Assets/Data/Items (not Resources), so this
        // is the same indirection SpawnVaultLoot already relies on to reach them at runtime.
        private Dictionary<string, Inventory.ItemData> LoadItemPool()
        {
            var pool = new Dictionary<string, Inventory.ItemData>();
            var table = Resources.Load<LootTable>("Data/Loot/AbyssBossLootTable");
            if (table == null) return pool;
            foreach (var entry in table.entries)
                if (entry.item != null) pool[entry.item.itemName] = entry.item;
            return pool;
        }

        private static ShopStock Stock(Dictionary<string, Inventory.ItemData> pool, string itemName, int price)
        {
            return pool.TryGetValue(itemName, out var item) ? new ShopStock { item = item, price = price } : default(ShopStock);
        }

        // Shared setup every dungeon needs: tear down whatever was here before, build fresh
        // geometry for the given theme, drop the player at its entry room, and set the
        // scene-global fog to match. Rebuilding fresh every time the gate is used -- not
        // just the first -- is what makes a dungeon "repeatable and farmable" per the
        // design doc, without needing any save/respawn-timer system.
        private DungeonLayout PrepareDungeonRoot(string rootName, DungeonTheme theme, Color fogColor)
        {
            if (dungeonRoot != null) Destroy(dungeonRoot);

            dungeonRoot = new GameObject(rootName);
            var layout = dungeonRoot.AddComponent<DungeonLayout>();
            layout.Build(theme);
            Debug.Log($"[Bootstrap] {rootName} layout built OK -- EntryPoint {layout.EntryPoint}");

            TeleportPlayer(layout.EntryPoint);

            // Dungeon-only atmosphere -- RenderSettings.fog is scene-global, not per-object,
            // so it has to be toggled here rather than baked into DungeonLayout itself
            // (which has no idea when the player leaves for the hub).
            RenderSettings.fog = true;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogMode = FogMode.Linear;
            // Rooms are 28 units wide (half 14) and the circular room is radius 15 -- the
            // old 8/36 distances started fogging before a room's own far wall was even in
            // view, on top of torches that didn't reach that far either (see
            // DungeonLayout.BuildTorch). Pushed out so a room reads clearly from its own
            // center; corridors and anything further off still fade into the dungeon's fog.
            RenderSettings.fogStartDistance = 16f;
            RenderSettings.fogEndDistance = 55f;

            return layout;
        }

        // Replaces the old DungeonSelectUI portal -- occupies the exact same reusable
        // dungeonRoot slot PrepareDungeonRoot builds dungeons into, so entering any of the
        // three camp portals below tears the open world down via the same
        // Destroy(dungeonRoot) call at the top of PrepareDungeonRoot, no new teardown logic
        // needed. Open air, not a dungeon -- no fog, and geometry building (OpenWorldLayout)
        // stays as separated from spawning as DungeonLayout already is: this method owns
        // every enemy/portal placed in the world, the layout only hands back points.
        private void EnterOpenWorld()
        {
            Debug.Log("[Bootstrap] EnterOpenWorld() called");
            if (dungeonRoot != null) Destroy(dungeonRoot);

            dungeonRoot = new GameObject("OpenWorld");
            var world = dungeonRoot.AddComponent<OpenWorldLayout>();
            TeleportPlayer(world.EntryPoint);
            RenderSettings.fog = false; // open air out here, not an enclosed dungeon

            PopulateWastes(world.Wastes);
            PopulateFrostlands(world.Frostlands);
            PopulateMarshlands(world.Marshlands);

            // RotMG's Oryx's Sanctuary inspiration -- three cleared camps (runes) light the
            // shared monument and unlock a bonus pull on top of each camp's own dungeon-unlock payoff.
            if (wastesCleared && frostlandsCleared && marshlandsCleared)
            {
                SpawnMonumentReward(world.MonumentPoint);
            }

            BuildReturnGate(world.EntryPoint);
        }

        // Wastes trash alternates regular/scurrier imps across its roam points, spiked imps
        // guard the camp, and the camp's own chief is a scaled-up spiked ImpDemon -- killing
        // it opens a portal straight to the Abyss.
        private void PopulateWastes(OpenWorldLayout.BiomeZone zone)
        {
            if (wastesCleared)
            {
                BuildDungeonPortal(zone.campPortalPoint, zone.dungeonLabel, EnterAbyssDungeon);
                return;
            }

            for (int i = 0; i < zone.roamPoints.Length; i++)
            {
                if (i % 2 == 0) SpawnImp(zone.roamPoints[i], false);
                else SpawnScurrierImp(zone.roamPoints[i]);
            }
            foreach (var p in zone.guardPoints) SpawnImp(p, true);

            SpawnBanditMiniboss<ImpDemon>(zone.minibossPoint, 400f, 1.8f,
                () => { wastesCleared = true; BuildDungeonPortal(zone.campPortalPoint, zone.dungeonLabel, EnterAbyssDungeon); },
                imp => imp.ApplyVariant(true));
        }

        // Frostlands trash/guards/chief are all Frost Skeletons -- killing the chief opens a
        // portal to the Frozen Crypt.
        private void PopulateFrostlands(OpenWorldLayout.BiomeZone zone)
        {
            if (frostlandsCleared)
            {
                BuildDungeonPortal(zone.campPortalPoint, zone.dungeonLabel, EnterFrozenCrypt);
                return;
            }

            foreach (var p in zone.roamPoints) SpawnFrostSkeleton(p);
            foreach (var p in zone.guardPoints) SpawnFrostSkeleton(p);

            SpawnBanditMiniboss<FrostSkeleton>(zone.minibossPoint, 400f, 1.8f,
                () => { frostlandsCleared = true; BuildDungeonPortal(zone.campPortalPoint, zone.dungeonLabel, EnterFrozenCrypt); });
        }

        // Marshlands trash/guards/chief are all Bog Lurkers -- killing the chief opens a
        // portal to the Sunken Ruins.
        private void PopulateMarshlands(OpenWorldLayout.BiomeZone zone)
        {
            if (marshlandsCleared)
            {
                BuildDungeonPortal(zone.campPortalPoint, zone.dungeonLabel, EnterSunkenRuins);
                return;
            }

            foreach (var p in zone.roamPoints) SpawnBogLurker(p);
            foreach (var p in zone.guardPoints) SpawnBogLurker(p);

            SpawnBanditMiniboss<BogLurker>(zone.minibossPoint, 400f, 1.8f,
                () => { marshlandsCleared = true; BuildDungeonPortal(zone.campPortalPoint, zone.dungeonLabel, EnterSunkenRuins); });
        }

        // A single generic "bandit chief" builder shared by all three biomes -- same
        // Health/StatusEffectController/AggroController/LootDropper wiring the SpawnImp-
        // family helpers below already use, just scaled up and handed a death callback
        // instead of nothing. The optional configure callback runs right after
        // AddComponent<T>() and before the stat overrides below, so it can set anything an
        // explicit post-AddComponent method exposes (e.g. ImpDemon.ApplyVariant(true) for
        // the Wastes chief) without that work getting clobbered by the flat maxHp/damage
        // override that follows.
        private void SpawnBanditMiniboss<T>(Vector3 pos, float maxHp, float damageMultiplier, System.Action onDefeated, System.Action<T> configure = null) where T : EnemyBase
        {
            var go = new GameObject(typeof(T).Name + "Chief");
            go.transform.position = pos;
            go.transform.SetParent(dungeonRoot.transform);
            go.transform.localScale = Vector3.one * 1.35f; // visually reads as tougher than the regular version
            var h = go.AddComponent<Health>();
            go.AddComponent<StatusEffectController>();
            var enemy = go.AddComponent<T>();
            configure?.Invoke(enemy);
            // Setting these AFTER configure so the flat override always wins regardless of
            // what configure (or T's own Awake()) did -- e.g. the miniboss's HP is always
            // exactly maxHp, not whatever ImpDemon.ApplyVariant's 1.3x bump computed.
            enemy.attackDamage *= damageMultiplier;
            h.maxHP = maxHp;
            h.SetCurrentHP(maxHp);
            go.AddComponent<AggroController>();
            var loot = go.AddComponent<LootDropper>();
            loot.lootTable = Resources.Load<LootTable>("Data/Loot/AbyssLootTable"); // reuse the trash table, not the boss table -- this is a miniboss, not a dungeon boss
            loot.minGold = 25;
            loot.maxGold = 45;
            h.OnDeath += () => onDefeated?.Invoke();
        }

        // Same visual/Interactable shape as BuildBossExitGate, but colored to match the
        // target dungeon and wired to actually enter it instead of returning to the hub.
        // This is what each camp miniboss's onDefeated callback builds once it dies --
        // it doesn't exist beforehand, so the dungeon stays locked until its camp is cleared.
        private void BuildDungeonPortal(Vector3 pos, string dungeonLabel, System.Action enterDungeon)
        {
            Color portalColor = dungeonLabel switch
            {
                "The Wastes" => new Color(0.9f, 0.35f, 0.1f),
                "The Frostlands" => new Color(0.35f, 0.75f, 1f),
                "The Marshlands" => new Color(0.3f, 0.85f, 0.5f),
                _ => new Color(0.6f, 0.15f, 0.75f),
            };
            Color glowA = new Color(portalColor.r * 0.7f, portalColor.g * 0.7f, portalColor.b * 0.7f);
            Color glowB = Color.Lerp(portalColor, Color.white, 0.35f);

            var portal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            portal.name = "DungeonPortal";
            var portalCol = portal.GetComponent<Collider>();
            if (portalCol != null) Destroy(portalCol);
            portal.transform.SetParent(dungeonRoot.transform);
            portal.transform.position = pos + new Vector3(0, 1.6f, 0);
            portal.transform.localScale = new Vector3(2.2f, 3.2f, 0.15f);
            var renderer = portal.GetComponent<Renderer>();
            if (renderer != null) renderer.material = new Material(Shader.Find("Standard")) { color = portalColor };
            var glow = portal.AddComponent<PortalGlow>();
            glow.colorA = glowA;
            glow.colorB = glowB;

            var triggerGO = new GameObject("DungeonPortalTrigger");
            triggerGO.transform.SetParent(dungeonRoot.transform);
            triggerGO.transform.position = pos + new Vector3(0, 1f, -1.2f);
            var col = triggerGO.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(3f, 2.4f, 1.8f);

            var interactable = triggerGO.AddComponent<Interactable>();
            interactable.prompt = $"Enter {dungeonLabel} (E)";
            interactable.onInteract = enterDungeon;
        }

        private void EnterAbyssDungeon()
        {
            Debug.Log("[Bootstrap] EnterAbyssDungeon() called");
            var layout = PrepareDungeonRoot("AbyssDungeon", DungeonTheme.Abyss, new Color(0.1f, 0.02f, 0.03f));

            if (spawnAbyssEncounter)
            {
                SpawnImp(layout.CombatPoint + new Vector3(3, 0, 2), false);
                SpawnImp(layout.CombatPoint + new Vector3(-3, 0, 2), true);
                SpawnScurrierImp(layout.CombatPoint + new Vector3(5f, 0, -3f));
                SpawnScurrierImp(layout.CombatPoint + new Vector3(-5f, 0, -3f));
                // Posted on the ramp-up platform (see DungeonLayout.CombatPlatformPoint)
                // instead of the open floor -- has to be climbed up to and engaged, not
                // just shot at from below with no way to close the gap.
                SpawnRangedImp(layout.CombatPlatformPoint);

                SpawnImp(layout.Combat2Point + new Vector3(3.5f, 0, 2f), true);
                SpawnImp(layout.Combat2Point + new Vector3(-3.5f, 0, -1f), true);
                SpawnRangedImp(layout.Combat2Point + new Vector3(-2f, 0, -6));
                SpawnRangedImp(layout.Combat2Point + new Vector3(2f, 0, -6));
                SpawnScurrierImp(layout.Combat2Point + new Vector3(6f, 0, 0));
                // Same idea as the first room's platform -- a caster sniping from up there
                // instead of open ground, so its telegraphed AoE has to actually be dodged
                // while climbing, not just eaten from a safe distance.
                SpawnAbyssMage(layout.Combat2PlatformPoint);

                // A ramp off Combat2Point's west wall dips down into a below-grade side
                // tunnel (see DungeonLayout.TunnelPoint) -- the first slice of real
                // verticality: a small ambush and a reward chest for anyone who notices
                // the opening and climbs down instead of walking straight past it.
                SpawnScurrierImp(layout.TunnelPoint + new Vector3(-1.5f, 0, 1.5f));
                SpawnScurrierImp(layout.TunnelPoint + new Vector3(1.5f, 0, -1.5f));
                SpawnRangedImp(layout.TunnelPoint + new Vector3(0, 0, -2f));
                SpawnTunnelLoot(layout.TunnelPoint + new Vector3(0, 0, 3f));

                SpawnVaultLoot(layout.VaultPoint);
                SpawnBoss(layout.BossPoint);

                // The entry-room gate is a long walk back from the boss room in a
                // five-room dungeon -- an immediate exit right where the run actually
                // ends is what "leave the dungeon" needs to mean in practice.
                BuildBossExitGate(layout.BossPoint + new Vector3(-7f, 0, 7f), "the Abyss");
            }

            BuildReturnGate(layout.EntryPoint);
        }

        // Same shape as EnterAbyssDungeon -- same room graph and encounter layout (via the
        // shared DungeonLayout generator, see World.DungeonTheme), but Frost Skeletons
        // instead of Imps and a Frost Lich instead of the Abyss Demon. RangedImp and
        // AbyssMage are reused as-is on the platforms/tunnel; a dedicated frost ranged enemy
        // is a reasonable next addition but not required to stand this dungeon up.
        private void EnterFrozenCrypt()
        {
            Debug.Log("[Bootstrap] EnterFrozenCrypt() called");
            var layout = PrepareDungeonRoot("FrozenCrypt", DungeonTheme.FrozenCrypt, new Color(0.55f, 0.7f, 0.85f));

            if (spawnAbyssEncounter)
            {
                SpawnFrostSkeleton(layout.CombatPoint + new Vector3(3, 0, 2));
                SpawnFrostSkeleton(layout.CombatPoint + new Vector3(-3, 0, 2));
                SpawnScurrierImp(layout.CombatPoint + new Vector3(5f, 0, -3f));
                SpawnScurrierImp(layout.CombatPoint + new Vector3(-5f, 0, -3f));
                SpawnRangedImp(layout.CombatPlatformPoint);

                SpawnFrostSkeleton(layout.Combat2Point + new Vector3(3.5f, 0, 2f));
                SpawnFrostSkeleton(layout.Combat2Point + new Vector3(-3.5f, 0, -1f));
                SpawnRangedImp(layout.Combat2Point + new Vector3(-2f, 0, -6));
                SpawnRangedImp(layout.Combat2Point + new Vector3(2f, 0, -6));
                SpawnScurrierImp(layout.Combat2Point + new Vector3(6f, 0, 0));
                SpawnAbyssMage(layout.Combat2PlatformPoint);

                SpawnFrostSkeleton(layout.TunnelPoint + new Vector3(-1.5f, 0, 1.5f));
                SpawnFrostSkeleton(layout.TunnelPoint + new Vector3(1.5f, 0, -1.5f));
                SpawnRangedImp(layout.TunnelPoint + new Vector3(0, 0, -2f));
                SpawnTunnelLoot(layout.TunnelPoint + new Vector3(0, 0, 3f));

                SpawnVaultLoot(layout.VaultPoint);
                SpawnFrostLichBoss(layout.BossPoint);

                BuildBossExitGate(layout.BossPoint + new Vector3(-7f, 0, 7f), "the Frozen Crypt");
            }

            BuildReturnGate(layout.EntryPoint);
        }

        // Same shape as EnterFrozenCrypt -- same room graph and encounter layout (via the
        // shared DungeonLayout generator, see World.DungeonTheme), but Bog Lurkers instead
        // of Frost Skeletons/Imps and a Swamp Warden instead of the Frost Lich/Abyss Demon.
        // RangedImp and AbyssMage are reused as-is on the platforms/tunnel, exactly like
        // Frozen Crypt reuses them.
        private void EnterSunkenRuins()
        {
            Debug.Log("[Bootstrap] EnterSunkenRuins() called");
            var layout = PrepareDungeonRoot("SunkenRuins", DungeonTheme.SunkenRuins, new Color(0.09f, 0.16f, 0.14f));

            if (spawnAbyssEncounter)
            {
                SpawnBogLurker(layout.CombatPoint + new Vector3(3, 0, 2));
                SpawnBogLurker(layout.CombatPoint + new Vector3(-3, 0, 2));
                SpawnScurrierImp(layout.CombatPoint + new Vector3(5f, 0, -3f));
                SpawnScurrierImp(layout.CombatPoint + new Vector3(-5f, 0, -3f));
                SpawnRangedImp(layout.CombatPlatformPoint);

                SpawnBogLurker(layout.Combat2Point + new Vector3(3.5f, 0, 2f));
                SpawnBogLurker(layout.Combat2Point + new Vector3(-3.5f, 0, -1f));
                SpawnRangedImp(layout.Combat2Point + new Vector3(-2f, 0, -6));
                SpawnRangedImp(layout.Combat2Point + new Vector3(2f, 0, -6));
                SpawnScurrierImp(layout.Combat2Point + new Vector3(6f, 0, 0));
                SpawnAbyssMage(layout.Combat2PlatformPoint);

                SpawnBogLurker(layout.TunnelPoint + new Vector3(-1.5f, 0, 1.5f));
                SpawnBogLurker(layout.TunnelPoint + new Vector3(1.5f, 0, -1.5f));
                SpawnRangedImp(layout.TunnelPoint + new Vector3(0, 0, -2f));
                SpawnTunnelLoot(layout.TunnelPoint + new Vector3(0, 0, 3f));

                SpawnVaultLoot(layout.VaultPoint);
                SpawnSwampWardenBoss(layout.BossPoint);

                BuildBossExitGate(layout.BossPoint + new Vector3(-7f, 0, 7f), "the Sunken Ruins");
            }

            BuildReturnGate(layout.EntryPoint);
        }

        // A second, victory-flavored exit (green glowing portal, no pillars) right in the
        // boss's own room -- BuildReturnGate stays invisible-trigger-only since it's meant
        // to just always be sitting at the entrance, but this one is the actual payoff for
        // clearing the dungeon, so it gets a visual.
        private void BuildBossExitGate(Vector3 pos, string dungeonLabel)
        {
            var portal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            portal.name = "BossExitPortal";
            var portalCol = portal.GetComponent<Collider>();
            if (portalCol != null) Destroy(portalCol);
            portal.transform.SetParent(dungeonRoot.transform);
            portal.transform.position = pos + new Vector3(0, 1.6f, 0);
            portal.transform.localScale = new Vector3(2.2f, 3.2f, 0.15f);
            var renderer = portal.GetComponent<Renderer>();
            if (renderer != null) renderer.material = new Material(Shader.Find("Standard")) { color = new Color(0.2f, 0.9f, 0.6f) };
            var glow = portal.AddComponent<PortalGlow>();
            glow.colorA = new Color(0.1f, 0.7f, 0.4f);
            glow.colorB = new Color(0.4f, 1f, 0.75f);

            var triggerGO = new GameObject("BossExitTrigger");
            triggerGO.transform.SetParent(dungeonRoot.transform);
            triggerGO.transform.position = pos + new Vector3(0, 1f, -1.2f);
            var col = triggerGO.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(3f, 2.4f, 1.8f);

            var interactable = triggerGO.AddComponent<Interactable>();
            interactable.prompt = $"Leave {dungeonLabel} (E)";
            interactable.onInteract = ReturnToHub;
        }

        // A single guaranteed item, pulled the same way SpawnVaultLoot resolves its pool --
        // the tunnel's own reward for being found at all, not tied to a kill.
        private void SpawnTunnelLoot(Vector3 pos)
        {
            var table = Resources.Load<Loot.LootTable>("Data/Loot/AbyssLootTable");
            if (table == null) return;

            var pool = new List<Inventory.ItemData>();
            foreach (var entry in table.entries)
                if (entry.item != null) pool.Add(entry.item);
            if (pool.Count == 0) return;

            var pick = pool[Random.Range(0, pool.Count)];
            Chest.Spawn(pos, new List<Inventory.ItemData> { pick }).transform.SetParent(dungeonRoot.transform);
        }

        private void BuildReturnGate(Vector3 entryPoint)
        {
            var triggerGO = new GameObject("HubReturnGate");
            triggerGO.transform.SetParent(dungeonRoot.transform);
            triggerGO.transform.position = entryPoint + new Vector3(0, 1f, -2f);
            var col = triggerGO.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(3f, 2.4f, 1.6f);

            var interactable = triggerGO.AddComponent<Interactable>();
            interactable.prompt = "Return to Hub (E)";
            interactable.onInteract = ReturnToHub;
        }

        private void ReturnToHub()
        {
            TeleportPlayer(hubEntryPoint);
            RenderSettings.fog = false;
        }

        // Called by DownedRecovery once a downed solo player has waited out the recovery
        // timer with nobody around to revive them -- the closest thing a squad-of-one has
        // to "full squad wipe ends the run" (see design doc). Sends them back to the hub
        // instead of leaving the screen frozen forever.
        private void RespawnAfterDefeat()
        {
            player.health.Revive(0.5f);
            if (player.mana != null) player.mana.SetMax(player.mana.maxMP, refill: true);
            RunModifiers.ResetAll(); // this counts as the run ending -- see BeginRun's identical reset
            TeleportPlayer(hubEntryPoint);
            RenderSettings.fog = false;
        }

        // A CharacterController's internal collision state can lag one frame behind a
        // direct Transform.position write -- PlayerMovement's Update() now calls
        // controller.Move() unconditionally every frame (needed so gravity keeps applying
        // while standing still), and that very next Move() call was silently sweeping from
        // the controller's stale pre-teleport position, snapping the player back toward
        // where they started. Disabling the controller for the write and re-enabling it
        // forces Unity to resync its internal state to the new position first -- the
        // standard fix for this exact CharacterController + manual teleport interaction.
        private void TeleportPlayer(Vector3 pos)
        {
            var controller = playerGO.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            playerGO.transform.position = pos;
            if (controller != null) controller.enabled = true;

            // Every call site here IS a "safe landing spot" (hub entry, dungeon entry,
            // post-defeat hub return) -- recording it here means FallRecovery always has
            // somewhere sane to snap back to, without threading it through separately.
            currentAreaEntryPoint = pos;
        }

        // Called by FallRecovery when the player drops below the geometry entirely (a
        // wall/floor/ramp seam gap somewhere) -- snap back to wherever they last validly
        // arrived rather than leaving them free-falling forever. Not a failure state (no
        // HP loss): the geometry let them down, not the other way around.
        private void RecoverFromFall()
        {
            TeleportPlayer(currentAreaEntryPoint);
        }

        private void SpawnImp(Vector3 pos, bool spiked)
        {
            var go = new GameObject(spiked ? "SpikedImp" : "Imp");
            go.transform.position = pos;
            go.transform.SetParent(dungeonRoot.transform);
            go.AddComponent<Health>();
            go.AddComponent<StatusEffectController>();
            var imp = go.AddComponent<ImpDemon>();
            imp.ApplyVariant(spiked);
            go.AddComponent<AggroController>();
            var loot = go.AddComponent<LootDropper>();
            loot.lootTable = Resources.Load<Loot.LootTable>("Data/Loot/AbyssLootTable");
            loot.minGold = spiked ? 7 : 4;
            loot.maxGold = spiked ? 12 : 8;
        }

        private void SpawnRangedImp(Vector3 pos)
        {
            var go = new GameObject("ImpShaman");
            go.transform.position = pos;
            go.transform.SetParent(dungeonRoot.transform);
            go.AddComponent<Health>();
            go.AddComponent<StatusEffectController>();
            go.AddComponent<RangedImp>();
            go.AddComponent<AggroController>();
            var loot = go.AddComponent<LootDropper>();
            loot.lootTable = Resources.Load<Loot.LootTable>("Data/Loot/AbyssLootTable");
            loot.minGold = 6;
            loot.maxGold = 10;
        }

        private void SpawnScurrierImp(Vector3 pos)
        {
            var go = new GameObject("ImpScurrier");
            go.transform.position = pos;
            go.transform.SetParent(dungeonRoot.transform);
            go.AddComponent<Health>();
            go.AddComponent<StatusEffectController>();
            go.AddComponent<ScurrierImp>();
            go.AddComponent<AggroController>();
            var loot = go.AddComponent<LootDropper>();
            loot.lootTable = Resources.Load<Loot.LootTable>("Data/Loot/AbyssLootTable");
            loot.minGold = 2;
            loot.maxGold = 5;
        }

        private void SpawnAbyssMage(Vector3 pos)
        {
            var go = new GameObject("AbyssMage");
            go.transform.position = pos;
            go.transform.SetParent(dungeonRoot.transform);
            go.AddComponent<Health>();
            go.AddComponent<StatusEffectController>();
            go.AddComponent<AbyssMage>();
            go.AddComponent<AggroController>();
            var loot = go.AddComponent<LootDropper>();
            loot.lootTable = Resources.Load<Loot.LootTable>("Data/Loot/AbyssLootTable");
            loot.minGold = 8;
            loot.maxGold = 14;
        }

        private void SpawnFrostSkeleton(Vector3 pos)
        {
            var go = new GameObject("FrostSkeleton");
            go.transform.position = pos;
            go.transform.SetParent(dungeonRoot.transform);
            go.AddComponent<Health>();
            go.AddComponent<StatusEffectController>();
            go.AddComponent<FrostSkeleton>();
            go.AddComponent<AggroController>();
            var loot = go.AddComponent<LootDropper>();
            loot.lootTable = Resources.Load<Loot.LootTable>("Data/Loot/AbyssLootTable");
            loot.minGold = 4;
            loot.maxGold = 9;
        }

        private void SpawnFrostLichBoss(Vector3 pos)
        {
            var go = new GameObject("FrostLich");
            go.transform.position = pos;
            go.transform.SetParent(dungeonRoot.transform);
            var h = go.AddComponent<Health>();
            h.maxHP = 1100;
            h.SetCurrentHP(h.maxHP);
            go.AddComponent<StatusEffectController>();
            go.AddComponent<FrostLich>();
            go.AddComponent<AggroController>();
            var loot = go.AddComponent<LootDropper>();
            loot.lootTable = Resources.Load<Loot.LootTable>("Data/Loot/AbyssBossLootTable");
            loot.minGold = 90;
            loot.maxGold = 140;
            loot.dropAsChest = true; // a boss scattering loot on the floor reads worse than it dropping a treasure chest
            h.OnDeath += PlayerProgress.MarkFrozenCryptBossDefeated;
        }

        private void SpawnBogLurker(Vector3 pos) => BogLurker.Spawn(pos).transform.SetParent(dungeonRoot.transform);

        private void SpawnSwampWardenBoss(Vector3 pos)
        {
            var go = new GameObject("SwampWarden");
            go.transform.position = pos;
            go.transform.SetParent(dungeonRoot.transform);
            var h = go.AddComponent<Health>();
            h.maxHP = 1150;
            h.SetCurrentHP(h.maxHP);
            go.AddComponent<StatusEffectController>();
            go.AddComponent<SwampWarden>();
            go.AddComponent<AggroController>();
            var loot = go.AddComponent<LootDropper>();
            loot.lootTable = Resources.Load<Loot.LootTable>("Data/Loot/AbyssBossLootTable");
            loot.minGold = 90;
            loot.maxGold = 140;
            loot.dropAsChest = true; // a boss scattering loot on the floor reads worse than it dropping a treasure chest
            h.OnDeath += PlayerProgress.MarkSunkenRuinsBossDefeated;
        }

        private void SpawnBoss(Vector3 pos)
        {
            var go = new GameObject("AbyssFinalDemon");
            go.transform.position = pos;
            go.transform.SetParent(dungeonRoot.transform);
            var h = go.AddComponent<Health>();
            h.maxHP = 1200;
            h.SetCurrentHP(h.maxHP);
            go.AddComponent<StatusEffectController>();
            go.AddComponent<AbyssFinalDemon>();
            go.AddComponent<AggroController>();
            var loot = go.AddComponent<LootDropper>();
            loot.lootTable = Resources.Load<Loot.LootTable>("Data/Loot/AbyssBossLootTable");
            loot.minGold = 90;
            loot.maxGold = 140;
            loot.dropAsChest = true; // a boss scattering loot on the floor reads worse than it dropping a treasure chest
            h.OnDeath += PlayerProgress.MarkAbyssBossDefeated;
        }

        // Guaranteed loot (no kill required) in the vault room, pulled straight from the
        // boss loot table's item pool rather than needing a separate Resources-loadable
        // item list -- reuses infrastructure that's already proven to load correctly.
        // Handed to a Chest instead of scattered as floor pickups -- a "vault" should have
        // something to actually open.
        private void SpawnVaultLoot(Vector3 pos)
        {
            var table = Resources.Load<Loot.LootTable>("Data/Loot/AbyssBossLootTable");
            if (table == null) return;

            var pool = new List<Inventory.ItemData>();
            foreach (var entry in table.entries)
                if (entry.item != null) pool.Add(entry.item);

            var picks = new List<Inventory.ItemData>();
            for (int i = 0; i < 3 && pool.Count > 0; i++)
            {
                int idx = Random.Range(0, pool.Count);
                picks.Add(pool[idx]);
                pool.RemoveAt(idx);
            }

            Chest.Spawn(pos, picks).transform.SetParent(dungeonRoot.transform);
        }

        // The all-three-camps payoff at OpenWorldLayout.MonumentPoint -- same pool-then-pick
        // pattern as SpawnVaultLoot, just a bigger pull (5 instead of 3) since clearing all
        // three camps is a bigger ask than reaching one vault room.
        private void SpawnMonumentReward(Vector3 pos)
        {
            var table = Resources.Load<Loot.LootTable>("Data/Loot/AbyssBossLootTable");
            if (table == null) return;

            var pool = new List<Inventory.ItemData>();
            foreach (var entry in table.entries)
                if (entry.item != null) pool.Add(entry.item);

            var picks = new List<Inventory.ItemData>();
            for (int i = 0; i < 5 && pool.Count > 0; i++)
            {
                int idx = Random.Range(0, pool.Count);
                picks.Add(pool[idx]);
                pool.RemoveAt(idx);
            }

            Chest.Spawn(pos, picks).transform.SetParent(dungeonRoot.transform);
        }
    }
}
