# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

A Unity 2020.3.30f1 (Built-in Render Pipeline) first-person co-op dungeon crawler
prototype. Solo-testable by design — every system that would eventually be multiplayer
(revive, party) has a documented solo fallback rather than requiring a second player to
exercise. `full-game-scope.md` is the long-term design doc (8-10 dungeons, 4 classes,
RotMG-style 8-stat system, role-check bosses); `README.md` documents an earlier
import/setup snapshot and is stale relative to current code — trust the code and this
file over it.

## Verifying changes (no automated test suite)

There is no CLI build, lint, or test command for this project — Unity's own compiler is
the only verification available, and it only runs inside the Editor. The established
workflow, run after every non-trivial change:

1. Close the running Unity Editor process if one is open (`Get-Process Unity` /
   `CloseMainWindow()`, falling back to `Stop-Process -Force` if it's stuck on a modal
   dialog — e.g. a Package Manager error).
2. Delete `%LOCALAPPDATA%\Unity\Editor\Editor.log` so the next run's log is unambiguous.
3. Relaunch: `Start-Process "C:\Program Files\Unity\Hub\Editor\2020.3.30f1\Editor\Unity.exe" -ArgumentList '-projectPath "<repo path>"'`.
4. Poll the log for the line `Refresh completed` (compilation is done once this appears).
5. Grep the log for `error CS\d+` (0 matches = clean compile) and for `Exception|No script asset` (excluding `ExceptionUtils`, which is a false-positive match on the class name).

This confirms the project **compiles**, not that a feature **works** — Play-mode
behavior always needs a human to actually press Play and test it (see `Assets/Editor/DevBridge.cs`
below for a way to read back what happened without asking them to relay it manually).

Gameplay balance/logic changes still need a real Play-mode pass by the user — don't
script automated Play-mode testing (no headless input-driving, no simulated playthrough);
ask the user to test and describe/report what they see, or read `Logs/play-console.log`
after they do.

### Reading Play-mode output directly

`Assets/Editor/DevBridge.cs` is an Editor-only script (no external dependencies) that
writes to two files under `Logs/` (gitignored):
- `Logs/compile-status.log` — only updates on a *live* in-editor recompile (Unity's
  file-change detection is focus-triggered, not a background watcher, so a fully
  backgrounded kill/relaunch cycle won't exercise it — use the Editor.log workflow above
  for that case instead).
- `Logs/play-console.log` — every `Debug.Log`/warning/error/exception during an actual
  Play-mode session, timestamped, with stack traces on errors. This *does* work reliably
  after a user-driven Play test (the window is naturally focused when they press Play),
  and is the fastest way to see exactly what happened without asking them to copy-paste
  the Console.

Two attempts at third-party Unity MCP servers (`ozankasikci/unity-editor-mcp`,
`akiojin/unity-mcp-server`) both failed: despite documentation claiming Unity 2020.3 LTS
support, one had real Editor-API drift (`PrefabStageUtility` namespace, a newer
`string.Contains` overload) and the other's own `package.json` hard-requires Unity 6000.0.
Don't re-attempt either without checking upstream has actually fixed version compatibility.

## Architecture

**Everything is built at runtime via `AddComponent`, in code — there are no prefabs and
almost no serialized scene state.** `Assets/Scenes/TestScene.unity` holds only a camera,
an EventSystem, and the Canvases that predate this pattern (kept for compatibility, but
`InventoryUI`/etc. now restyle them at runtime rather than relying on their edit-time
config). Placeholder art is procedural (`Texture2D.SetPixel` + `Sprite.Create` for icons/
panels — see `Visuals/PanelSpriteFactory.cs`, `Visuals/IconFactory.cs`; raw waveform
synthesis for SFX — see `Audio/SfxLibrary.cs`) since there's no external art/audio tool
access. Reach for this pattern before adding a new asset type.

**Entry point:** `Core/GameBootstrap.cs`. `Start()` shows `CharacterSelectUI`, whose
callback (`BeginRun`) spawns the player, builds the hub (`World/HubLayout.cs`), and wires
every interactable (vendors, gambling table, claw machine, dungeon gate) to its UI/logic.
Dungeon entry goes through `DungeonSelectUI` → `EnterAbyssDungeon()` / `EnterFrozenCrypt()`
→ shared `PrepareDungeonRoot()`.

**The `Awake()` vs. explicit `Initialize()`/`Build()` split is load-bearing, not
stylistic.** `AddComponent<T>()` runs `T.Awake()` synchronously, before the caller can
set any fields the component needs (a `ClassDefinition`, a `DungeonTheme`, etc.) — so
components that need caller-supplied data split construction: `Awake()` does only
context-free setup, and an explicit method (`PlayerCharacter.Initialize(def)`,
`DungeonLayout.Build(theme)`) does the rest, called once, on purpose, immediately after
`AddComponent`. Follow this pattern for any new component that needs data from its
spawner — don't reach for `SendMessage("Awake")` or similar re-invocation hacks (a real
bug this codebase already had and fixed once).

**`World/DungeonLayout.cs`** is a single generator shared by both dungeons
(`DungeonTheme.Abyss` / `DungeonTheme.FrozenCrypt`) — palette and hazard type (lava/bones
vs. ice patches/spikes) branch on the theme inside `BuildRoom`/`BuildCircularRoom` rather
than duplicating the room/corridor/platform/tunnel-building code. Adding a third dungeon
theme means: add an enum case, an `ApplyXPalette()` method, a hazard branch, new
enemy/boss classes under `Enemies/`, and a new `EnterX()` method in `GameBootstrap.cs`
that calls the shared `PrepareDungeonRoot()` — not a new layout generator.

**Enemy movement goes through `EnemyBase.Move()`**, which routes through a
`CharacterController` (`controller.Move(delta)`) exactly like the player does — direct
`transform.position +=` on an enemy will walk through walls/floors with no collision at
all. Player teleports (dungeon entry, hub return, respawn) go through
`GameBootstrap.TeleportPlayer()`, which disables the `CharacterController` before writing
`transform.position` and re-enables it after — a `CharacterController`'s internal
collision state can otherwise lag a frame behind a direct position write and silently
undo the teleport on the very next `Move()` call.

**Namespace layout** (all under `DungeonCrawler.*`, folder matches namespace):
`Core` (stats, health/mana, wallet, status effects, debug hotkeys), `Classes` (player
components: movement, dash, jump, auto-attack, ability input, first-person look),
`Abilities` (the 3-slot ability system), `Enemies` (`EnemyBase` + every enemy/boss
subclass + `AggroController` + `Projectile`), `Inventory` (items, rarity, the
buy/sell-aware `InventoryUI`), `Loot` (drop tables, `ItemDropper`), `World` (hub/dungeon
geometry, interactables, hazards), `UI` (all runtime-built Canvases — HUD, shop, pause,
tooltips, dungeon/character select), `Visuals` (procedural sprite/panel/particle-ish
helpers), `Party` (revive, currently unused in solo play).

### Known sharp edges (already hit once, worth not re-discovering)

- **Unity serializes enums as raw ints in `.asset` YAML.** Inserting a new enum value
  anywhere but the end silently reassigns every value after it on every already-saved
  asset (`Inventory/ItemData.cs`'s `ItemCategory` has a comment scar from this — `Ring`
  had to be moved to the end after insertion corrupted saved potion assets). Always
  append.
- **`RectTransform.pivot` must match `anchorMin`/`anchorMax` when they're not centered**,
  or a fresh `RectTransform`'s default `pivot = (0.5, 0.5)` centers the element on the
  anchor point instead of aligning an edge to it — a real, shipped bug in `ShopUI.cs`'s
  item-name label (spilled 84px past the panel edge).
- **`Mask` alpha-tests its Graphic; `RectMask2D` doesn't.** A near-invisible (alpha
  ≈0.001) background used to hide a `Mask`'s own visual can sit at/under the clip
  threshold and discard *everything* inside it, not just the mask graphic itself — this
  silently emptied every scrollable list in `ShopUI.cs` for a while. Use `RectMask2D` for
  plain rectangular clipping; it needs no Graphic and has no alpha behavior to trip over.
- **`Image.fillAmount` on a runtime-built `Type.Filled` Image with no sprite assigned
  doesn't reliably repaint.** Every fill bar in this codebase (HP/MP, stat potion
  progress, ability cooldown sweep) drives width via `RectTransform.anchorMax.x` instead
  (see `PlayerHUD.SetFillFraction`) — the cooldown sweep is the one exception, since a
  radial wipe needs the fill-mesh path and can't be done via anchor resizing; it's given
  an explicit white sprite to sidestep the same failure mode.
