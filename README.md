# Bare-Bones Prototype — Import & Test Guide

## Code review pass (latest update)

Went through every script looking for the kind of mistakes that don't show up until
they bite you. Found and fixed six real issues:

1. **Silent status-effect bug.** `StatusEffectType.ArmorBreak` was enum value `0`, and
   the "does this ability apply a status?" check compared against `default` (also `0`).
   Any ability applying ArmorBreak — the Knight's Shield Slam — would silently do
   nothing. Added an explicit `None = 0` as the sentinel value instead.
2. **Fragile double-init hack.** `GameBootstrap` was calling `player.SendMessage("Awake")`
   to re-run Unity's `Awake()` after assigning `classDefinition` — relying on undocumented
   behavior to redo initialization. Replaced with an explicit `PlayerCharacter.Initialize()`
   method, called once, on purpose.
3. **Dead field.** `AbilityData.manaCost` existed but nothing ever checked or deducted it
   — abilities were free to spam. Added a `Mana` component (mirrors `Health`) and wired
   `AbilityCaster` to actually consume mana on cast.
4. **Death handled by polling.** `LootDropper` checked `CurrentHP <= 0` every single
   frame with a guard flag to avoid firing twice. Replaced with a proper `Health.OnDeath`
   event — cleaner and cheaper, and it's how `EnemyBase` now triggers cleanup too
   (enemies previously just sat in the scene forever after "dying").
5. **Per-frame GC allocation.** `AbilityCaster.Update()` built a new `List<AbilityData>`
   from `Dictionary.Keys` every frame just to tick cooldowns. Cached once in `Init()` instead.
6. **Duplicated state / wrong Unity API version.** `PartyReviveController` had its own
   `reviveRange` that duplicated (and could drift from) `Health.reviveRange`; now reads
   the one source of truth. `AggroController` also used `FindObjectsByType`, a 2023.1+
   only API — switched to the broadly-compatible `FindObjectsOfType` with a note on
   when it's safe to switch back.

Also tightened `Health.currentHP` into a property with a private setter (nothing outside
`Health` should be able to set it directly and skip the downed check), and removed
misleading `[SerializeField]` attributes on `StatBlock`'s dictionaries — Unity's
serializer doesn't support `Dictionary` at all, so the attributes did nothing and
implied a capability that wasn't real.


Everything here is placeholder/crude by design, built to get you into Unity and clicking
"Play" with the core loop working, not final art or production code.

## 1. Import into your project

Copy these folders straight into your Unity project's `Assets/` folder:

```
Assets/
  Scripts/     <- copy contents of Scripts/ here (or anywhere under Assets, Unity doesn't care)
  Sprites/     <- copy contents of Sprites/ here
```

Unity will auto-import the PNGs as Textures. For each one you plan to use as a sprite
(all of them), select it in the Project window and in the Inspector set:
- **Texture Type:** Sprite (2D and UI)
- **Filter Mode:** Point (no filter) — keeps the crude pixel-art crisp instead of blurry
- Click **Apply**

Tip: select the whole `Sprites` folder at once and change these settings in bulk.

## 2. What's in the package

**Sprites** (`Sprites/`)
- `Enemies/Abyss/` — `imp_demon.png`, `imp_demon_spiked.png` (the abyss dungeon's basic
  enemies), `abyss_final_demon.png` (the boss)
- `Equipment/` — one icon per weapon (wand/staff/sword/shield/warhammer/armor) and one
  per stat potion (8 stats + an all-stat potion)
- `UI/` — inventory slot, highlighted slot, and panel background

**Scripts** (`Scripts/`)
- `Core/` — `StatType`, `StatBlock` (the 8-stat, 1/5-per-potion system), `StatusEffect`
  (the 7 RotMG-style effects), `Health`, `GameBootstrap` (spawns a test scene)
- `Classes/` — `ClassDefinition`, `PlayerCharacter`, `DefaultContentFactory` (builds all
  4 classes + their abilities in code — no manual asset setup needed to test)
- `Abilities/` — `AbilityData`, `AbilityCaster`
- `Enemies/` — `EnemyBase`, `ImpDemon`, `AbyssFinalDemon` (2-phase, role-check boss)
- `Inventory/` — `ItemData`, `InventorySystem`, `InventoryUI`

## 3. Fastest path to testing

1. Create a new empty scene.
2. Create an empty GameObject, name it `Bootstrap`.
3. Add the `GameBootstrap` component to it.
4. Pick a class in the Inspector dropdown (`classToTest`).
5. Hit Play. This now spawns:
   - A blockout room (floor + 4 walls, dark abyss coloring) so there's an actual bounded space
   - Your player, with movement (WASD/arrow keys via `PlayerMovement`), a follow camera,
     an `InventorySystem`, and a `CapsuleCollider` (needed for AoE detection)
   - 2 imps (one spiked variant) and the final demon, each with an `AggroController`
     (they'll now find and chase the nearest non-downed player automatically) and a
     `LootDropper` (drops nothing yet until you assign a `LootTable` — see below)

Check the Console — it logs the spawned class, its HP, and its 3 abilities.

No ScriptableObject assets need to exist on disk for this — `DefaultContentFactory`
builds Knight/Priest/Paladin/Wizard and all their abilities in code at runtime. Once
you're happy with the numbers, right-click in the Project window → **Create →
DungeonCrawler → Class Definition** / **Ability** to make them into real, editable
`.asset` files instead, and wire sprites onto them in the Inspector.

## 4. Setting up loot (optional, but quick)

1. Project window → **Create → DungeonCrawler → Item** for each equipment/potion piece
   (use the sprites in `Sprites/Equipment/` as icons, set `category` and `potionStat`/
   `primaryStat` appropriately).
2. **Create → DungeonCrawler → Loot Table**, add entries with a drop chance each.
3. On the imp/boss GameObjects (or in `GameBootstrap.SpawnImp`/`SpawnBoss`), assign that
   Loot Table to the `LootDropper.lootTable` field.
4. Drops spawn as bare cubes with a `WorldPickup` component — walk a player with an
   `InventorySystem` and a trigger collider into one to pick it up. Swap the cube for a
   real pickup prefab via `LootDropper.pickupPrefab` whenever you have one.

## 5. Known gaps (intentionally left as stubs — this is bare-bones, not final)

- **Networking (NGO + Relay):** none of this is networked yet. Everything here runs
  local/single-player so you can validate the systems in isolation first. This is the
  single biggest remaining piece of work versus what's in this package.
- **Real dungeon layout:** `BlockoutRoom` is one flat room, not a generator — no
  corridors, no multiple rooms, no procedural layout. Fine for testing combat systems
  in isolation, not a real dungeon yet.
- **Downed/revive input:** `PartyReviveController.TryChannelRevive()` does the actual
  channel/charge-pool logic, but nothing calls it from player input yet — needs a
  "hold E near a downed ally" input hook, and ideally a callback from `Health.TakeDamage`
  to auto-cancel a channel when the reviver gets hit (currently only cancels on
  out-of-range).
- **Inventory UI prefab:** `InventoryUI` expects a simple slot prefab (an Image + child
  Image) that doesn't exist yet — takes 2 minutes to build in the Editor with the
  provided `inventory_slot.png`.
- **No animations, hit feedback, or sound:** sprites are static, there's no hit-flash,
  damage numbers, or status icons over enemies/players yet, and no audio at all.
- **Currency/shop/bounty board:** none of the economy layer from the full scope doc
  exists in code yet — only the potion/equipment item data model.
- **The other 7-9 dungeons:** this package only covers the abyss dungeon's enemies/boss;
  the rest of the ring structure is still just the design doc.
- **Sprites are placeholder geometry**, not final art — swap freely once you have real art.

## 6. Suggested order to build on this

1. Playtest what's here first — movement, aggro, abilities, AoE, and loot pickup should
   all work end-to-end now. Confirm feel/balance before adding more systems.
2. Build the inventory slot prefab and drop `InventoryUI` into a Canvas.
3. Wire revive input (hold-to-channel near a downed ally) using `PartyReviveController`.
4. Add hit feedback (flash sprite red on damage, a floating damage number) — cheap and
   makes testing combat much easier to read with 4 players on screen.
5. Once single-player feels right, layer in NGO + Relay networking per your existing
   technical design doc.
