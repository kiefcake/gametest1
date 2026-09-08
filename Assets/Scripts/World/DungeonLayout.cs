using System.Collections.Generic;
using UnityEngine;
using DungeonCrawler.Classes;
using DungeonCrawler.Core;
using DungeonCrawler.UI;

namespace DungeonCrawler.World
{
    // Which palette/hazard set BuildRoom-family methods use -- lets the exact same room/
    // corridor/circular-room/platform/tunnel generator below serve more than one dungeon
    // without duplicating any of that structural code. Add a case here plus an Apply*
    // Palette method and a hazard branch (see BuildRoom/BuildCircularRoom) for each new
    // dungeon theme.
    public enum DungeonTheme { Abyss, FrozenCrypt, SunkenRuins, SnakePit, WraithboundSanctum }

    // A real (if crude) dungeon: five rooms in a line -- Entry, two Combat rooms, a Vault,
    // and the Boss -- joined by corridors, instead of BlockoutRoom's single flat box. Same
    // placeholder-geometry philosophy (primitives, flat colors), just enough structure
    // that a "dungeon" reads as more than one room. Positions are exposed so GameBootstrap
    // can place the player and enemies without hardcoding coordinates that would drift out
    // of sync with the geometry.
    //
    // The five-room spine's order/lore is fixed (RoomInfoTable below), but the shape of
    // each spine room (circular vs. rectangular) and a random 1-4 extra side branches
    // (see BranchPoints/BuildBranchPocket) off it are re-rolled fresh every single call to
    // Build() -- no two visits to "the same" dungeon are laid out identically, and every
    // generation now has more corridors than the old fixed four-segment chain. This is a
    // randomized spine-plus-branches generator, not a free-form room graph: GameBootstrap
    // still reads exact named points (CombatPoint, VaultPoint, etc.) to place its precise
    // per-room encounters, which a fully free topology would have to give up.
    //
    // Builds in Awake(), not Start(): GameBootstrap does
    // `roomGO.AddComponent<DungeonLayout>()` and immediately reads the room points the
    // same frame. AddComponent() calls Awake() synchronously; Start() would not run until
    // after GameBootstrap.Start() has already returned, leaving those properties at their
    // default (0,0,0).
    public class DungeonLayout : MonoBehaviour
    {
        // Bumped up across the board (28->38 footprint, 3->6 ceiling) -- the old 3-unit
        // ceiling sat below several real boss meshes (Swamp Warden alone is 4.4 tall) and
        // their health bars, which is why boss health bars were invisible: BuildCeiling
        // put an opaque plane right through/above them. See BossRoomWidth/BossRoomWallHeight
        // below for the boss room's own further bump on top of this.
        // Bumped again (38->48 footprint, 6->8 ceiling) per the user's "bigger, taller"
        // request -- the previous bump only fixed a specific ceiling-vs-healthbar clipping
        // bug, this one is a deliberate scale-up of the whole dungeon on top of that.
        public float roomWidth = 48f;
        public float roomDepth = 48f;
        public float circularRoomRadius = 24f;
        public float platformHeight = 3.5f;
        public float platformHalfSize = 3f;
        // Widened from 4 -- generous margin against anything narrowing a passage (the
        // circular room's gap, an enemy or two standing in a doorway during a fight, any
        // future geometry tweak) actually blocking it shut.
        public float corridorWidth = 7f;
        public float corridorLength = 9f;
        public float wallHeight = 8f;
        public float wallThickness = 0.5f;

        // Extra width/height ONLY for the boss room, layered on top of roomWidth/wallHeight
        // right before that one BuildRoom call (see Build()) and restored immediately after.
        // Depth deliberately isn't touched here -- RoomSpacing (roomDepth + corridorLength)
        // already fixes BossPoint's distance from the Vault room, so growing the boss room's
        // OWN depth would eat into the connecting corridor. Width and height have no such
        // spacing constraint, so the boss arena can go bigger in both without touching them.
        private const float BossRoomWidth = 66f;
        private const float BossRoomWallHeight = 13f;

        public Color entryFloorColor = new Color(0.14f, 0.14f, 0.18f);
        public Color combatFloorColor = new Color(0.15f, 0.05f, 0.08f);
        public Color vaultFloorColor = new Color(0.16f, 0.13f, 0.04f);
        public Color bossFloorColor = new Color(0.22f, 0.03f, 0.03f);
        public Color corridorFloorColor = new Color(0.1f, 0.1f, 0.12f);
        public Color wallColor = new Color(0.08f, 0.02f, 0.04f);
        public Color ceilingColor = new Color(0.04f, 0.03f, 0.04f);

        [Header("Atmosphere")]
        public bool buildCeiling = true;
        public bool buildTorches = true;
        public bool buildPillars = true;

        public Vector3 EntryPoint { get; private set; }
        public Vector3 CombatPoint { get; private set; }
        public Vector3 Combat2Point { get; private set; }
        public Vector3 VaultPoint { get; private set; }
        public Vector3 BossPoint { get; private set; }
        // Center of the below-grade side chamber reached via the ramp off Combat2Point's
        // west wall -- see BuildVerticalTunnel. First slice of dungeon verticality: one
        // branch, one level down, not yet a general multi-level graph.
        public Vector3 TunnelPoint { get; private set; }
        // Tops of the two ramp-up sniper platforms (see BuildPlatform) -- exact enemy
        // stand points, not just "somewhere up there," so GameBootstrap can place a ranged
        // enemy precisely on the platform surface instead of guessing its height.
        public Vector3 CombatPlatformPoint { get; private set; }
        public Vector3 Combat2PlatformPoint { get; private set; }

        private float RoomSpacing => roomDepth + corridorLength;
        private DungeonTheme theme;

        // Which of the five room slots along the chain a given RoomInfo/trigger belongs
        // to -- indexes RoomInfoTable rather than duplicating the name/flavor lookup logic
        // per room.
        private enum RoomSlot { Entry, Combat, Combat2, Vault, Boss }

        // Compass direction for the free-form spine path Build() lays out below --
        // North/South/East/West map onto this file's existing world-space convention
        // (+Z/-Z/+X/-X), the same axes BuildWallOrDoor (Z) and BuildEastWallWithGap/
        // BuildWestWallWithGap (X) already build gaps on.
        private enum Dir { North, East, South, West }
        private static Dir Opposite(Dir d) => (Dir)(((int)d + 2) % 4);
        private static Vector2Int CellOffset(Dir d) => d switch
        {
            Dir.North => new Vector2Int(0, 1),
            Dir.South => new Vector2Int(0, -1),
            Dir.East => new Vector2Int(1, 0),
            Dir.West => new Vector2Int(-1, 0),
            _ => Vector2Int.zero,
        };

        // Picks a random compass direction for the next hop of the spine's random walk
        // -- never the exact reverse of the incoming direction (no instant backtrack
        // onto the room you just left), never one that would land on an already-used
        // grid cell (no self-intersecting spine folding a room on top of another), and
        // never one `allowed` rejects (used to keep Combat2's and Vault's own west wall
        // permanently free for the vertical tunnel / treasure alcove they always try to
        // build there -- see the constraint derivation in Build()'s own comment).
        // Falls back to relaxing only the occupied-cell check if truly nothing
        // qualifies, which for a 4-6-hop path on an open grid essentially never fires.
        private static Dir PickPathDir(Vector2Int fromCell, Dir? incoming, HashSet<Vector2Int> occupied, System.Func<Dir, bool> allowed)
        {
            var order = new List<Dir> { Dir.North, Dir.East, Dir.South, Dir.West };
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            foreach (var d in order)
            {
                if (incoming.HasValue && d == Opposite(incoming.Value)) continue;
                if (!allowed(d)) continue;
                if (IsBlocked(fromCell + CellOffset(d), fromCell, occupied)) continue;
                return d;
            }
            foreach (var d in order)
            {
                if (incoming.HasValue && d == Opposite(incoming.Value)) continue;
                if (!allowed(d)) continue;
                return d;
            }
            return incoming ?? Dir.North;
        }

        // A candidate cell is blocked if it's already used, OR if it's orthogonally
        // adjacent to any OTHER already-placed room (any neighbor except `fromCell`,
        // the room this hop is actually leaving). Without the second half of this check,
        // a bent path can legally place two rooms that are nowhere near each other IN
        // THE PATH SEQUENCE into physically adjacent grid cells -- e.g. Entry and Boss
        // ending up one cell apart purely by coincidence of which directions got rolled.
        // That's not a corridor (neither room's path actually uses that shared wall), so
        // nothing opens a door there -- but a side feature that assumes "the space next
        // to me is empty" (BuildBranchPocket's east branch, BuildVerticalTunnel/
        // BuildTreasureAlcove's west attachment) has no way to know a whole other room
        // now sits exactly where it's about to build into. This buffer-zone rule is what
        // guarantees every room's "unused" walls actually face empty space, not a
        // stranger's wall a few units away.
        private static bool IsBlocked(Vector2Int cell, Vector2Int fromCell, HashSet<Vector2Int> occupied)
        {
            if (occupied.Contains(cell)) return true;
            foreach (Dir d in new[] { Dir.North, Dir.South, Dir.East, Dir.West })
            {
                Vector2Int neighbor = cell + CellOffset(d);
                if (neighbor == fromCell) continue;
                if (occupied.Contains(neighbor)) return true;
            }
            return false;
        }

        // A room's actual wall openings are just "which directions does the path touch
        // here" -- at most two (one incoming, one outgoing; Entry/Boss only ever have
        // one of the two). Computing this generically, rather than re-deriving by hand
        // per spine slot, is what lets Build() safely ask "is this room's east/west
        // still free for a branch/tunnel/alcove" without trusting hand-algebra to have
        // covered every path shape (including the optional waypoint detour) correctly.
        private static void OpenFlags(Dir? a, Dir? b, out bool north, out bool south, out bool east, out bool west)
        {
            north = a == Dir.North || b == Dir.North;
            south = a == Dir.South || b == Dir.South;
            east = a == Dir.East || b == Dir.East;
            west = a == Dir.West || b == Dir.West;
        }

        private static Vector3 CellToWorld(Vector2Int cell, float pitch) => new Vector3(cell.x * pitch, 0, cell.y * pitch);

        private readonly struct RoomInfo
        {
            public readonly string name;
            public readonly string flavor;
            public RoomInfo(string name, string flavor) { this.name = name; this.flavor = flavor; }
        }

        // Dark and Darker-style room names/flavor lines for RoomBanner (see UI/RoomBanner.cs)
        // -- one row per dungeon theme, indexed by RoomSlot. Entry fires immediately (no
        // walk-in -- see AnnounceRoom); the other four fire via RoomEntryTrigger once the
        // player actually walks into the room.
        private static readonly Dictionary<DungeonTheme, RoomInfo[]> RoomInfoTable = new Dictionary<DungeonTheme, RoomInfo[]>
        {
            {
                DungeonTheme.Abyss, new[]
                {
                    new RoomInfo("The Threshold", "Cold stone gives way to something warmer, and wrong."),
                    new RoomInfo("The Cinder Ring", "A ring of scorched stone. Something's been fighting here a long time."),
                    new RoomInfo("The Bone Choir", "Old bones underfoot; every step sounds like it's being heard."),
                    new RoomInfo("The Ember Vault", "Whoever built this vault didn't plan on leaving."),
                    new RoomInfo("The Demon's Hearth", "The heat here isn't the floor. It's him."),
                }
            },
            {
                DungeonTheme.FrozenCrypt, new[]
                {
                    new RoomInfo("The Rime Gate", "Frost creeps up the walls like it's trying to seal the door behind you."),
                    new RoomInfo("The Frozen Choir", "A perfect circle of ice, too clean to be natural."),
                    new RoomInfo("The Cracked Nave", "The floor groans. It's holding, for now."),
                    new RoomInfo("The Glacial Hold", "Whatever's kept safe in here has been safe a very long time."),
                    new RoomInfo("The Lich's Sanctum", "The cold has a source, and it's watching you find it."),
                }
            },
            {
                DungeonTheme.SunkenRuins, new[]
                {
                    new RoomInfo("The Silted Steps", "The water's shallow here. It gets deeper."),
                    new RoomInfo("The Drowned Round", "A flooded chamber, ankle-deep and getting worse."),
                    new RoomInfo("The Silt Reliquary", "A shrine, once. The bog's had it longer than the gods did."),
                    new RoomInfo("The Brackish Hoard", "Rust and rot, but the good kind of rust."),
                    new RoomInfo("The Warden's Mire", "The whole room breathes. That's not a good sign."),
                }
            },
            {
                DungeonTheme.SnakePit, new[]
                {
                    new RoomInfo("The Cracked Narthex", "Stone steps worn smooth by scales, not sandals."),
                    new RoomInfo("The Serpent's Coil", "The walls hiss when the wind changes. So do the floors."),
                    new RoomInfo("The Fang Gallery", "Old temple murals, scoured clean by centuries of shedding."),
                    new RoomInfo("The Sunken Reliquary", "Whatever the priests buried here, the snakes buried deeper."),
                    new RoomInfo("Stheno's Sanctum", "She doesn't guard the temple anymore. She is the temple."),
                }
            },
            {
                DungeonTheme.WraithboundSanctum, new[]
                {
                    new RoomInfo("The Grieving Gate", "The names on the archway are worn smooth. Someone kept touching them."),
                    new RoomInfo("The Hollow Choir", "Armor stands empty in rows, and every suit is facing you."),
                    new RoomInfo("The Weeping Nave", "Something here still grieves. It wants company."),
                    new RoomInfo("The Sundered Vault", "Whatever oath this place was sworn on, it broke first."),
                    new RoomInfo("The Wraithbound Throne", "He was buried sitting up. He never got the memo about resting."),
                }
            },
        };

        // Set once Build() finishes, if this run happened to roll one -- null on any run
        // that didn't (roughly 60% of the time, and never at all when VaultPoint itself
        // rolled circular, since circular rooms only support the two opposite corridor
        // gaps, not a third branch). GameBootstrap reads this to decide whether to place a
        // bonus reward there.
        public Vector3? TreasureAlcovePoint { get; private set; }

        // Off-spine side pockets (see BuildBranchPocket) -- a random 1-4 per generation,
        // each its own extra corridor + room branching east off a spine room that happened
        // to roll rectangular. This plus the shape rolls above is what makes a dungeon
        // "randomly generated" rather than a fixed template: room count on the branch path
        // varies, branch position/size varies, and GameBootstrap decides per-theme what
        // (loot, an ambush, or both) shows up in each one.
        public List<Vector3> BranchPoints { get; private set; } = new List<Vector3>();

        // An extra on-critical-path room, sometimes inserted between Combat2 and Vault
        // (see Build()) -- unlike BranchPoints (dead-end side pockets), a waypoint sits
        // directly on the route to the boss, so it's a plain full-size Combat-flavored
        // room rather than a small alcove. 0 or 1 per generation today; a real room
        // count beyond the fixed five-slot spine, however small a step.
        public List<Vector3> WaypointPoints { get; private set; } = new List<Vector3>();

        // Explicit call instead of building in Awake() -- GameBootstrap needs to hand this
        // a theme (which room/enemy content to build) before generation runs, the same
        // reason PlayerCharacter.Initialize() exists instead of doing everything in Awake.
        public void Build(DungeonTheme dungeonTheme = DungeonTheme.Abyss)
        {
            theme = dungeonTheme;
            if (theme == DungeonTheme.FrozenCrypt) ApplyFrozenCryptPalette();
            else if (theme == DungeonTheme.SunkenRuins) ApplySunkenRuinsPalette();
            else if (theme == DungeonTheme.SnakePit) ApplySnakePitPalette();
            else if (theme == DungeonTheme.WraithboundSanctum) ApplyWraithboundPalette();

            BranchPoints = new List<Vector3>();
            WaypointPoints = new List<Vector3>();

            // The spine's five named rooms keep their fixed roles/order and lore (Entry
            // -> Combat -> Combat2 -> Vault -> Boss, same RoomInfoTable text as always),
            // but their grid layout is a real random walk now, not a straight line: each
            // hop picks a random compass direction, never an instant backtrack onto the
            // room just left, never one that would fold the spine back onto an
            // already-placed room. Two directions stay reserved throughout -- Combat2's
            // west (always tries to grow a vertical tunnel there) and Vault's west
            // (always tries to roll a treasure alcove there) -- via the `allowed`
            // constraints on the PickPathDir calls below. Everything downstream (branch
            // eligibility, circular-room eligibility, the boss room's single door) is
            // computed generically from the ACTUAL chosen directions afterward (see
            // OpenFlags), rather than trusted from this derivation -- a mistake here
            // would show up as a missing feature, never as a room missing a wall it
            // structurally needs.
            Vector2Int entryCell = Vector2Int.zero;
            var occupied = new HashSet<Vector2Int> { entryCell };

            Dir d1 = PickPathDir(entryCell, null, occupied, d => true);
            Vector2Int combatCell = entryCell + CellOffset(d1);
            occupied.Add(combatCell);

            Dir d2 = PickPathDir(combatCell, d1, occupied, d => d != Dir.East); // keep Combat2's west free
            Vector2Int combat2Cell = combatCell + CellOffset(d2);
            occupied.Add(combat2Cell);

            // A 40% chance of one extra plain Combat-flavored room wedged between
            // Combat2 and Vault -- WaypointPoints, not BranchPoints: this sits ON the
            // route to the boss instead of a dead-end pocket off it, real room count
            // beyond the fixed five-slot spine rather than just bent corridors.
            bool hasWaypoint = Random.value < 0.4f;
            Dir intoVaultDir;
            Vector2Int vaultCell;
            Vector2Int waypointCell = default;
            Dir waypointIncoming = default, waypointOutgoing = default;

            if (hasWaypoint)
            {
                Dir stepA = PickPathDir(combat2Cell, d2, occupied, d => d != Dir.West); // leaving Combat2 -- keep its west free
                waypointCell = combat2Cell + CellOffset(stepA);
                occupied.Add(waypointCell);
                Dir stepB = PickPathDir(waypointCell, stepA, occupied, d => d != Dir.East); // arriving at Vault -- keep its west free
                vaultCell = waypointCell + CellOffset(stepB);
                occupied.Add(vaultCell);

                waypointIncoming = stepA;
                waypointOutgoing = stepB;
                intoVaultDir = stepB;
            }
            else
            {
                Dir d3 = PickPathDir(combat2Cell, d2, occupied, d => d == Dir.North || d == Dir.South); // direct hop -- protects Combat2's AND Vault's west at once
                vaultCell = combat2Cell + CellOffset(d3);
                occupied.Add(vaultCell);
                intoVaultDir = d3;
            }

            // Boss is the one room whose width gets temporarily bumped for its own build
            // (see BossRoomWidth below) while its DEPTH stays standard specifically so
            // Z-axis spacing (roomDepth + corridorLength) still lines up -- an X-axis
            // approach would need WIDTH-based spacing instead, which `pitch` below can't
            // give it without knowing in advance that the next room is Boss. Restricting
            // this last hop to North/South (exactly like the old fixed spine always was)
            // sidesteps that mismatch entirely rather than trying to special-case pitch
            // for one room; it also automatically keeps Vault's west free (North/South
            // never equals West), so no separate check is needed for that anymore.
            Dir d4 = PickPathDir(vaultCell, intoVaultDir, occupied, d => d == Dir.North || d == Dir.South);
            Vector2Int bossCell = vaultCell + CellOffset(d4);
            occupied.Add(bossCell);

            float pitch = roomWidth + corridorLength; // roomWidth == roomDepth, so one square grid pitch works regardless of hop direction
            EntryPoint = CellToWorld(entryCell, pitch);
            CombatPoint = CellToWorld(combatCell, pitch);
            Combat2Point = CellToWorld(combat2Cell, pitch);
            VaultPoint = CellToWorld(vaultCell, pitch);
            BossPoint = CellToWorld(bossCell, pitch);
            Vector3 waypointPos = hasWaypoint ? CellToWorld(waypointCell, pitch) : Vector3.zero;

            OpenFlags(null, d1, out bool entryN, out bool entryS, out bool entryE, out bool entryW);
            OpenFlags(Opposite(d1), d2, out bool combatN, out bool combatS, out bool combatE, out bool combatW);
            Dir combat2Outgoing = hasWaypoint ? waypointIncoming : intoVaultDir;
            OpenFlags(Opposite(d2), combat2Outgoing, out bool combat2N, out bool combat2S, out bool combat2E, out bool combat2W);
            OpenFlags(Opposite(intoVaultDir), d4, out bool vaultN, out bool vaultS, out bool vaultE, out bool vaultW);
            OpenFlags(Opposite(d4), null, out bool bossN, out bool bossS, out bool bossE, out bool bossW);

            // Entry is always rectangular; per OpenFlags above it only ever needs its
            // east wall for the path if the very first hop happened to go east, which
            // leaves it free to host a branch the rest of the time.
            bool entryBranch = !entryE && Random.value < 0.45f;
            BuildRoom(EntryPoint, entryFloorColor, openNorth: entryN, openSouth: entryS, hazardous: false, openEast: entryE, openWest: entryW, eastBranch: entryBranch);
            if (entryBranch) BranchPoints.Add(BuildBranchPocket(EntryPoint));

            // Procedural shape roll -- Combat and Vault each independently pick circular
            // or rectangular per generation, so no two runs of the same dungeon look
            // identical. Circular rooms only ever open two OPPOSITE gaps (see
            // BuildCircularWallRing), so a room whose real path connections need its
            // east or west wall this generation can't roll circular at all -- computed
            // here from the actual OpenFlags result, not assumed the way the old
            // straight-line spine could get away with.
            bool combatCanBeCircular = !combatE && !combatW;
            bool combatCircular = combatCanBeCircular && Random.value < 0.5f;
            bool combatBranch = !combatCircular && !combatE && Random.value < 0.65f;
            if (combatCircular)
            {
                BuildCircularRoom(CombatPoint, combatFloorColor, circularRoomRadius);
            }
            else
            {
                BuildRoom(CombatPoint, combatFloorColor, openNorth: combatN, openSouth: combatS, hazardous: true, openEast: combatE, openWest: combatW, platform: true, platformIsPrimary: true, eastBranch: combatBranch);
            }
            if (combatBranch) BranchPoints.Add(BuildBranchPocket(CombatPoint));
            BuildRoomEntryTrigger(CombatPoint, RoomSlot.Combat);

            // Combat2 stays rectangular unconditionally -- it needs a platform in a
            // specific corner regardless of shape rolls. Its west wall is provably free
            // by the PickPathDir constraints above (checked again here via !combat2W
            // rather than assumed), so the vertical tunnel almost always gets built;
            // its east branch is likewise usually free unless the optional waypoint
            // detour happened to leave via that exact wall.
            bool combat2Branch = !combat2E && Random.value < 0.8f;
            BuildRoom(Combat2Point, combatFloorColor, openNorth: combat2N, openSouth: combat2S, hazardous: true, openEast: combat2E, openWest: combat2W, westTunnel: !combat2W, platform: true, eastBranch: combat2Branch);
            if (combat2Branch) BranchPoints.Add(BuildBranchPocket(Combat2Point));
            BuildRoomEntryTrigger(Combat2Point, RoomSlot.Combat2);

            if (hasWaypoint)
            {
                // A plain, unnamed Combat-flavored room -- no shape roll, no platform, no
                // branch, no RoomBanner (it has no RoomInfoTable slot). GameBootstrap
                // populates it generically via WaypointPoints, the same way it already
                // does for BranchPoints, just with a fuller on-path encounter.
                OpenFlags(Opposite(waypointIncoming), waypointOutgoing, out bool wN, out bool wS, out bool wE, out bool wW);
                BuildRoom(waypointPos, combatFloorColor, openNorth: wN, openSouth: wS, hazardous: true, openEast: wE, openWest: wW);
                WaypointPoints.Add(waypointPos);
            }

            bool vaultCanBeCircular = !vaultE && !vaultW;
            bool vaultCircular = vaultCanBeCircular && Random.value < 0.5f;
            // A treasure alcove needs a real west-wall gap (see BuildTreasureAlcove) --
            // provably free by the PickPathDir constraints above, checked again here via
            // !vaultW rather than assumed.
            bool treasureAlcove = !vaultCircular && !vaultW && Random.value < 0.4f;
            bool vaultBranch = !vaultCircular && !vaultE && Random.value < 0.65f;
            if (vaultCircular)
                BuildCircularRoom(VaultPoint, vaultFloorColor, circularRoomRadius, buildSniperPlatform: false);
            else
                BuildRoom(VaultPoint, vaultFloorColor, openNorth: vaultN, openSouth: vaultS, hazardous: true, openEast: vaultE, openWest: vaultW, westTunnel: treasureAlcove, eastBranch: vaultBranch);
            if (treasureAlcove) TreasureAlcovePoint = BuildTreasureAlcove(VaultPoint);
            if (vaultBranch) BranchPoints.Add(BuildBranchPocket(VaultPoint));
            BuildRoomEntryTrigger(VaultPoint, RoomSlot.Vault);

            // Wider and taller than every other room -- a grander arena, and enough
            // headroom that the tallest bosses (Swamp Warden at 4.4 units) and their health
            // bars sit well clear of the ceiling. Depth stays at the shared value; see
            // BossRoomWidth/BossRoomWallHeight's own comment for why.
            float savedRoomWidth = roomWidth, savedWallHeight = wallHeight;
            roomWidth = BossRoomWidth;
            wallHeight = BossRoomWallHeight;
            BuildRoom(BossPoint, bossFloorColor, openNorth: bossN, openSouth: bossS, hazardous: true, openEast: bossE, openWest: bossW);
            roomWidth = savedRoomWidth;
            wallHeight = savedWallHeight;
            BuildRoomEntryTrigger(BossPoint, RoomSlot.Boss);

            BuildCorridor(EntryPoint, CombatPoint);
            BuildCorridor(CombatPoint, Combat2Point);
            if (hasWaypoint)
            {
                BuildCorridor(Combat2Point, waypointPos);
                BuildCorridor(waypointPos, VaultPoint);
            }
            else
            {
                BuildCorridor(Combat2Point, VaultPoint);
            }
            BuildCorridor(VaultPoint, BossPoint);

            BuildVerticalTunnel(Combat2Point);

            // The entry room has no walk-in -- GameBootstrap teleports the player directly
            // to EntryPoint -- so it announces itself immediately instead of via a trigger.
            AnnounceRoom(RoomSlot.Entry);
        }

        // Shows a room's RoomBanner (see UI/RoomBanner.cs) directly, looked up from
        // RoomInfoTable for the currently-building theme. Only ever called for Entry today
        // (see the end of Build()) -- the other four rooms announce themselves via
        // RoomEntryTrigger instead, once the player actually walks in.
        private void AnnounceRoom(RoomSlot slot)
        {
            var info = RoomInfoTable[theme][(int)slot];
            RoomBanner.Show(info.name, info.flavor);
        }

        // Places a one-shot RoomEntryTrigger at a room's center -- see RoomEntryTrigger
        // below. Trigger-only SphereCollider, so this adds zero solid geometry and can't
        // block movement or snag a ramp/platform underneath it.
        private void BuildRoomEntryTrigger(Vector3 center, RoomSlot slot)
        {
            var info = RoomInfoTable[theme][(int)slot];
            var triggerGO = new GameObject("RoomEntryTrigger_" + slot);
            triggerGO.transform.SetParent(transform);
            triggerGO.transform.position = center;

            var col = triggerGO.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 6f;

            var trigger = triggerGO.AddComponent<RoomEntryTrigger>();
            trigger.roomName = info.name;
            trigger.flavor = info.flavor;
        }

        // Icy blue/white instead of the Abyss's dark red/black -- overrides the color
        // fields' Abyss-tuned defaults before any geometry reads them.
        private void ApplyFrozenCryptPalette()
        {
            entryFloorColor = new Color(0.58f, 0.68f, 0.78f);
            combatFloorColor = new Color(0.48f, 0.62f, 0.74f);
            vaultFloorColor = new Color(0.55f, 0.63f, 0.7f);
            bossFloorColor = new Color(0.38f, 0.52f, 0.68f);
            corridorFloorColor = new Color(0.5f, 0.6f, 0.7f);
            wallColor = new Color(0.68f, 0.8f, 0.9f);
            ceilingColor = new Color(0.28f, 0.38f, 0.48f);
        }

        // Murky teal/green water and mossy stone instead of the Abyss's dark red/black or
        // the Crypt's ice-blue/white -- brackish brown creeps into the vault floor for the
        // "silted-up ruin" read.
        private void ApplySunkenRuinsPalette()
        {
            entryFloorColor = new Color(0.22f, 0.32f, 0.28f);
            combatFloorColor = new Color(0.14f, 0.26f, 0.22f);
            vaultFloorColor = new Color(0.24f, 0.22f, 0.14f);
            bossFloorColor = new Color(0.08f, 0.18f, 0.15f);
            corridorFloorColor = new Color(0.12f, 0.2f, 0.18f);
            wallColor = new Color(0.18f, 0.28f, 0.22f);
            ceilingColor = new Color(0.06f, 0.12f, 0.1f);
        }

        // Earthy brown stone with red/blue accent trim instead of any of the other three
        // themes' cold-vs-hot palettes -- matches the real Snake Pit's "brown floor tiles,
        // many cracked, brown walls with red and blue drawings" description. The red/blue
        // "drawings" read through wallColor's own baseboard/cap trim bands (see
        // DungeonLayout.AddWallTrim) rather than a literal mural texture this project has
        // no way to paint.
        private void ApplySnakePitPalette()
        {
            entryFloorColor = new Color(0.36f, 0.28f, 0.16f);
            combatFloorColor = new Color(0.3f, 0.22f, 0.12f);
            vaultFloorColor = new Color(0.32f, 0.26f, 0.15f);
            bossFloorColor = new Color(0.24f, 0.16f, 0.09f);
            corridorFloorColor = new Color(0.28f, 0.2f, 0.11f);
            wallColor = new Color(0.4f, 0.3f, 0.18f);
            ceilingColor = new Color(0.16f, 0.11f, 0.06f);
        }

        // Deep purple-black stone with a sickly grave-green glow instead of any of the
        // other four themes' palettes -- a cursed reliquary/crypt, not fire, ice, bog, or
        // earthy temple.
        private void ApplyWraithboundPalette()
        {
            entryFloorColor = new Color(0.14f, 0.1f, 0.18f);
            combatFloorColor = new Color(0.1f, 0.06f, 0.14f);
            vaultFloorColor = new Color(0.16f, 0.12f, 0.18f);
            bossFloorColor = new Color(0.08f, 0.04f, 0.1f);
            corridorFloorColor = new Color(0.1f, 0.07f, 0.14f);
            wallColor = new Color(0.16f, 0.1f, 0.2f);
            ceilingColor = new Color(0.05f, 0.03f, 0.07f);
        }

        private void BuildRoom(Vector3 center, Color floorColor, bool openNorth, bool openSouth, bool hazardous, bool openEast = false, bool openWest = false, bool westTunnel = false, bool platform = false, bool platformIsPrimary = false, bool eastBranch = false)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "RoomFloor";
            floor.transform.SetParent(transform);
            floor.transform.position = center;
            floor.transform.localScale = new Vector3(roomWidth / 10f, 1f, roomDepth / 10f);
            SetColor(floor, floorColor);

            // East/west gaps open for either reason: the spine's own random-walk path
            // needs that wall (openEast/openWest, plain corridor, nothing built beyond
            // it here) or this room is hosting a side-branch pocket / vertical tunnel /
            // treasure alcove there (eastBranch/westTunnel -- Build() only ever sets
            // those when the path itself doesn't already need that same wall, so the two
            // reasons never collide on one room). North/south gaps work the same way via
            // openNorth/openSouth and BuildWallOrDoor, just without a second "why."
            if (eastBranch || openEast)
                BuildEastWallWithGap(center);
            else
                BuildWall(center + new Vector3(roomWidth / 2f, wallHeight / 2f, 0), new Vector3(wallThickness, wallHeight, roomDepth));
            if (westTunnel || openWest)
                BuildWestWallWithGap(center);
            else
                BuildWall(center + new Vector3(-roomWidth / 2f, wallHeight / 2f, 0), new Vector3(wallThickness, wallHeight, roomDepth));

            BuildWallOrDoor(center, roomDepth / 2f, openNorth);
            BuildWallOrDoor(center, -roomDepth / 2f, openSouth);

            if (buildCeiling) BuildCeiling(center);
            if (buildPillars || buildTorches) BuildRoomDecor(center, platform);

            // Entry stays a clean, calm transition room -- lava/bones are reserved for the
            // rooms that are actually fights, so the abyss theming reads as "danger zone,"
            // not just uniform decoration everywhere.
            if (hazardous)
            {
                BuildThemedHazardCluster(center + new Vector3(6f, 0, 4f), center + new Vector3(-8f, 0, -3f), center + new Vector3(5f, 0, -8f));
            }

            // A raised platform in the room's south-east corner, up a ramp -- a ranged
            // enemy posted here (see GameBootstrap) has to actually be climbed up to and
            // engaged, not just shot at from below with no way to close the gap.
            // platformIsPrimary picks which of the two named points GameBootstrap reads:
            // Combat2's platform (the only one this ever built before room shape was
            // randomized) stays the default, and a rectangular Combat room -- previously
            // always circular, now sometimes not -- passes platformIsPrimary so its own
            // sniper spot still ends up in CombatPlatformPoint like GameBootstrap expects
            // regardless of which shape Combat rolled this run.
            if (platform)
            {
                Vector3 platformTop = center + new Vector3(roomWidth / 2f - 5f, platformHeight, -(roomDepth / 2f - 5f));
                if (platformIsPrimary) CombatPlatformPoint = platformTop;
                else Combat2PlatformPoint = platformTop;
                BuildPlatform(platformTop);
                Vector3 rampBottom = platformTop + new Vector3(0, -platformHeight, platformHalfSize + 5f);
                Vector3 rampTop = platformTop + new Vector3(0, 0, platformHalfSize);
                BuildRamp(rampTop, rampBottom, 3f);
            }
        }

        // A ring-walled circular arena instead of a rectangular box -- for room-shape
        // variety, now rollable for either Combat or Vault (see Build()). North/south
        // openings line up with the corridors the same way a rectangular room's doors do
        // (see BuildCircularWallRing); everything else around the ring is solid wall built
        // from short tangent segments.
        //
        // buildSniperPlatform defaults to true (Combat's own historical behavior, back when
        // this was the only room that could ever be circular) -- pass false for a Vault
        // that happens to roll circular, since Vault has no platform in its rectangular
        // form either and GameBootstrap has nothing to spawn on one there. Without this
        // flag, a circular Vault built AFTER Combat in Build()'s call order would
        // unconditionally overwrite CombatPlatformPoint with its own platform's position,
        // silently breaking GameBootstrap's SpawnRangedImp(layout.CombatPlatformPoint) call
        // for that run.
        private void BuildCircularRoom(Vector3 center, Color floorColor, float radius, bool buildSniperPlatform = true)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "RoomFloor";
            floor.transform.SetParent(transform);
            floor.transform.position = center;
            floor.transform.localScale = new Vector3(radius * 2f, 0.05f, radius * 2f);
            SetColor(floor, floorColor);

            // BuildCircularWallRing now builds each kept arc directly from this exact
            // boundary angle (see its own comment) -- no fixed grid to round to, so no
            // fudge-factor margin needed here either. A small +4 keeps the opening
            // comfortably wider than the raw corridor chord, not to dodge a rounding error.
            float gapHalfAngle = Mathf.Asin(Mathf.Clamp01((corridorWidth / 2f) / radius)) * Mathf.Rad2Deg + 4f;
            BuildCircularWallRing(center, radius, new float[] { 0f, 180f }, gapHalfAngle);

            if (buildCeiling)
            {
                var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ceiling.name = "Ceiling";
                var col = ceiling.GetComponent<Collider>();
                if (col != null) Destroy(col);
                ceiling.transform.SetParent(transform);
                ceiling.transform.position = center + new Vector3(0, wallHeight, 0);
                ceiling.transform.localScale = new Vector3(radius * 2f, 0.05f, radius * 2f);
                SetColor(ceiling, ceilingColor);
            }

            if (buildTorches)
            {
                BuildTorch(center + new Vector3(radius - 2f, 1.1f, 0));
                BuildTorch(center + new Vector3(-(radius - 2f), 1.1f, 0));
            }

            BuildThemedHazardCluster(center + new Vector3(-6f, 0, -6f), center + new Vector3(-8f, 0, 5f), center + new Vector3(6f, 0, -8f));

            if (buildSniperPlatform)
            {
                // Sniper platform to the east, well clear of both corridor openings (0 deg
                // and 180 deg) -- the ramp climbs toward the room's own center so it can't
                // run past the wall on the far side.
                Vector3 platformTop = center + new Vector3(radius - 6f, platformHeight, 0);
                CombatPlatformPoint = platformTop;
                BuildPlatform(platformTop);
                Vector3 rampTop = platformTop + new Vector3(-platformHalfSize, 0, 0);
                Vector3 rampBottom = rampTop + new Vector3(-4f, -platformHeight, 0);
                BuildRamp(rampTop, rampBottom, 3f);
            }
        }

        // Builds the ring as two walled arcs (each tangent-segmented, local X the tangent
        // direction / local Z the radial thickness once rotated by its own angle around Y),
        // leaving the two gaps between them where a corridor connects.
        //
        // Rebuilt from scratch -- the previous approach approximated the gaps onto a fixed
        // 24-segment/15-degree grid ("skip whichever segments fall near the gap angle") and
        // needed a padding trick to hide seams between kept segments; two rounds of tuning
        // that padding/margin still left a real, physical wall corner sitting somewhere
        // inside what looked like an open doorway. Rather than find a third magic number,
        // this drops the fixed grid entirely: it only ever needs to handle exactly the two
        // opposite gaps this codebase calls it with (0/180 degrees), so it builds the two
        // KEPT arcs (east side, west side) as their own continuous polylines, each sized to
        // that arc's own exact start/end angle. Every arc's first and last vertex lands
        // exactly on the real gap boundary -- there's no "does this 15-degree slice happen to
        // land near the gap" approximation left to get subtly wrong.
        private void BuildCircularWallRing(Vector3 center, float radius, float[] gapAnglesDeg, float gapHalfAngle)
        {
            float gapA = gapAnglesDeg[0];
            float gapB = gapAnglesDeg.Length > 1 ? gapAnglesDeg[1] : gapA + 180f;

            BuildCircularWallArc(center, radius, gapA + gapHalfAngle, gapB - gapHalfAngle);
            BuildCircularWallArc(center, radius, gapB + gapHalfAngle, gapA + 360f - gapHalfAngle);
        }

        // One continuous stretch of wall from startAngle to endAngle (degrees; startAngle is
        // always less than endAngle here, possibly past 360 to express wrapping through 0),
        // subdivided into straight segments sized to THIS arc's own exact span -- so both
        // ends land precisely on the caller's intended boundary instead of the nearest point
        // on some unrelated fixed grid.
        private void BuildCircularWallArc(Vector3 center, float radius, float startAngle, float endAngle)
        {
            float span = endAngle - startAngle;
            if (span <= 0.01f) return;

            const float targetSegmentAngle = 15f; // roughly matches the old fixed grid's segment size, purely cosmetic
            int segmentCount = Mathf.Max(1, Mathf.RoundToInt(span / targetSegmentAngle));
            float segmentAngle = span / segmentCount;
            // A hairline 1% overlap only to hide the render seam between segments WITHIN
            // this same arc (a few centimeters at 15-unit radius) -- nothing like the old
            // 15% padding, and nowhere near enough to reach past this arc's own start/end
            // angle into the gap on either side.
            float segmentWidth = 2f * radius * Mathf.Sin(segmentAngle * Mathf.Deg2Rad / 2f) * 1.01f;

            float bandHeight = Mathf.Min(0.3f, wallHeight * 0.15f);
            Color baseboard = wallColor * 0.55f; baseboard.a = 1f;
            Color cap = Color.Lerp(wallColor, Color.white, 0.25f);
            float bandFrac = bandHeight / wallHeight; // fraction of wallHeight, since bands are built as scaled children of each unit-cube wall segment

            for (int i = 0; i < segmentCount; i++)
            {
                float angle = startAngle + (i + 0.5f) * segmentAngle; // this segment's own center angle
                float rad = angle * Mathf.Deg2Rad;
                Vector3 pos = center + new Vector3(Mathf.Sin(rad) * radius, wallHeight / 2f, Mathf.Cos(rad) * radius);

                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "CircularWall";
                wall.transform.SetParent(transform);
                wall.transform.position = pos;
                wall.transform.rotation = Quaternion.Euler(0, angle, 0);
                wall.transform.localScale = new Vector3(segmentWidth, wallHeight, wallThickness);
                SetColor(wall, wallColor);

                // Trim bands as scaled children of the segment cube, not AddWallTrim's own
                // world-space padding -- a curved wall's segments are each individually
                // rotated, so parenting is what makes the bands rotate along with them.
                // Local scale here is a fraction of the PARENT's unit-cube size (1,1,1
                // before wall's own localScale is applied), not world units.
                var baseband = BuildTrimBand(Vector3.zero, new Vector3(1.02f, bandFrac, 1.15f), baseboard);
                baseband.transform.SetParent(wall.transform, false);
                baseband.transform.localPosition = new Vector3(0, -0.5f + bandFrac / 2f, 0);

                var capband = BuildTrimBand(Vector3.zero, new Vector3(1.02f, bandFrac, 1.15f), cap);
                capband.transform.SetParent(wall.transform, false);
                capband.transform.localPosition = new Vector3(0, 0.5f - bandFrac / 2f, 0);
            }
        }

        // A glowing hazard pool -- forces the player to actually route around part of the
        // room instead of walking a straight line through every fight, and gives ranged
        // imps something worth kiting behind.
        // A raised stone basin rim (see Models/Props/hazard_basin) around every hazard's
        // flat glow disc -- the disc itself stays a runtime-built plane (PortalGlow
        // animates its color, which needs a plain material, not a prop mesh) so this is
        // purely additive dressing, not a replacement. Skips silently if the resource is
        // missing rather than falling back to a primitive -- the pool still works fine
        // (still damages, still glows) without a rim, unlike a torch or pillar whose
        // absence would leave a much bigger visual hole.
        private void BuildHazardBasinRim(Vector3 pos, float radius)
        {
            var model = Resources.Load<GameObject>("Models/Props/hazard_basin");
            if (model == null) return;
            var rimGO = Instantiate(model, transform);
            rimGO.name = "HazardBasinRim";
            rimGO.transform.position = pos;
            rimGO.transform.localScale = Vector3.one * radius;
        }

        // Re-tints every renderer under a just-instantiated prop model to a solid runtime
        // color, replacing whatever the imported OBJ's baked material color was. Only
        // needed for props shared across every dungeon theme that used to derive their
        // color from the active theme's palette (the pillar, from wallColor) -- unlike
        // single-theme clutter (bones, ice spikes, reeds, ...) which keeps its own baked
        // color regardless of which dungeon it's placed in.
        private void TintRecursive(GameObject go, Color c)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                r.material = new Material(Shader.Find("Standard")) { color = c };
        }

        private void BuildLavaPool(Vector3 pos, float radius)
        {
            var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pool.name = "LavaPool";
            pool.transform.SetParent(transform);
            pool.transform.position = pos + new Vector3(0, 0.03f, 0);
            pool.transform.localScale = new Vector3(radius * 2f, 0.03f, radius * 2f);
            SetColor(pool, new Color(0.9f, 0.35f, 0.05f));

            var glow = pool.AddComponent<PortalGlow>();
            glow.colorA = new Color(0.7f, 0.15f, 0.02f);
            glow.colorB = new Color(1f, 0.6f, 0.1f);
            glow.speed = 0.8f;

            // The Cylinder primitive ships with its own CapsuleCollider -- reused as the
            // hazard's trigger volume rather than destroying and rebuilding one.
            var col = pool.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            pool.AddComponent<LavaHazard>();
            BuildHazardBasinRim(pos, radius);
        }

        // Scattered bones plus a half-buried skull -- real low-poly meshes (see
        // Models/Props/bone_shard, skull) when the resource is present, falling back to
        // the original stretched-capsule/sphere primitives if it's ever missing.
        private void BuildBonePile(Vector3 pos)
        {
            var boneColor = new Color(0.82f, 0.78f, 0.68f);
            var shardModel = Resources.Load<GameObject>("Models/Props/bone_shard");
            int count = Random.Range(3, 6);
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = new Vector3(Random.Range(-0.5f, 0.5f), 0.08f, Random.Range(-0.5f, 0.5f));
                if (shardModel != null)
                {
                    var boneGO = Instantiate(shardModel, transform);
                    boneGO.name = "Bone";
                    boneGO.transform.position = pos + offset;
                    boneGO.transform.rotation = Quaternion.Euler(Random.Range(70f, 110f), Random.Range(0f, 360f), 0f);
                    boneGO.transform.localScale = Vector3.one * Random.Range(0.7f, 1.15f);
                }
                else
                {
                    var bone = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    bone.name = "Bone";
                    var col = bone.GetComponent<Collider>();
                    if (col != null) Destroy(col); // decorative clutter -- shouldn't snag movement
                    bone.transform.SetParent(transform);
                    bone.transform.position = pos + offset;
                    bone.transform.rotation = Quaternion.Euler(Random.Range(70f, 110f), Random.Range(0f, 360f), 0f);
                    bone.transform.localScale = new Vector3(0.08f, Random.Range(0.25f, 0.4f), 0.08f);
                    SetColor(bone, boneColor);
                }
            }

            Vector3 skullOffset = new Vector3(Random.Range(-0.3f, 0.3f), 0.12f, Random.Range(-0.3f, 0.3f));
            var skullModel = Resources.Load<GameObject>("Models/Props/skull");
            if (skullModel != null)
            {
                var skullGO = Instantiate(skullModel, transform);
                skullGO.name = "Skull";
                skullGO.transform.position = pos + skullOffset;
                skullGO.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            }
            else
            {
                var skull = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                skull.name = "Skull";
                var skullCol = skull.GetComponent<Collider>();
                if (skullCol != null) Destroy(skullCol);
                skull.transform.SetParent(transform);
                skull.transform.position = pos + skullOffset;
                skull.transform.localScale = new Vector3(0.32f, 0.28f, 0.36f);
                SetColor(skull, boneColor);
            }
        }

        // Frozen Crypt's equivalent of BuildLavaPool -- same periodic-damage hazard
        // (LavaHazard is generic despite the name, just a damage-over-time trigger volume),
        // reskinned as frostbite-blue instead of fire-orange.
        private void BuildIcePatch(Vector3 pos, float radius)
        {
            var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pool.name = "IcePatch";
            pool.transform.SetParent(transform);
            pool.transform.position = pos + new Vector3(0, 0.03f, 0);
            pool.transform.localScale = new Vector3(radius * 2f, 0.03f, radius * 2f);
            SetColor(pool, new Color(0.55f, 0.85f, 1f));

            var glow = pool.AddComponent<PortalGlow>();
            glow.colorA = new Color(0.35f, 0.65f, 0.9f);
            glow.colorB = new Color(0.75f, 0.95f, 1f);
            glow.speed = 0.8f;

            var col = pool.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            var hazard = pool.AddComponent<LavaHazard>();
            hazard.appliedEffect = StatusEffectType.Slow;
            hazard.effectMagnitude = 0.4f;
            hazard.effectDuration = 1.5f;
            BuildHazardBasinRim(pos, radius);
        }

        // BuildBonePile's icy counterpart -- jagged ice-spike clusters (see
        // Models/Props/ice_spike) instead of scattered bones, falling back to the
        // original stretched-capsule primitive if the resource is ever missing.
        private void BuildIceSpikes(Vector3 pos)
        {
            var iceColor = new Color(0.78f, 0.92f, 1f);
            var model = Resources.Load<GameObject>("Models/Props/ice_spike");
            int count = Random.Range(3, 6);
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = new Vector3(Random.Range(-0.5f, 0.5f), 0.1f, Random.Range(-0.5f, 0.5f));
                if (model != null)
                {
                    var spikeGO = Instantiate(model, transform);
                    spikeGO.name = "IceSpike";
                    spikeGO.transform.position = pos + offset;
                    spikeGO.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
                    spikeGO.transform.localScale = Vector3.one * Random.Range(0.7f, 1.2f);
                }
                else
                {
                    var spike = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    spike.name = "IceSpike";
                    var col = spike.GetComponent<Collider>();
                    if (col != null) Destroy(col); // decorative clutter -- shouldn't snag movement
                    spike.transform.SetParent(transform);
                    spike.transform.position = pos + offset;
                    spike.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
                    spike.transform.localScale = new Vector3(0.1f, Random.Range(0.4f, 0.7f), 0.1f);
                    SetColor(spike, iceColor);
                }
            }
        }

        // Sunken Ruins' equivalent of BuildLavaPool/BuildIcePatch -- same periodic-damage
        // hazard (LavaHazard is generic despite the name, just a damage-over-time trigger
        // volume), reskinned as a sickly green/brown bog. Standing in it is still flat
        // physical damage; the Poison *status effect* comes from BogLurker/SwampWarden
        // attacks, not from this hazard.
        private void BuildPoisonBog(Vector3 pos, float radius)
        {
            var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pool.name = "PoisonBog";
            pool.transform.SetParent(transform);
            pool.transform.position = pos + new Vector3(0, 0.03f, 0);
            pool.transform.localScale = new Vector3(radius * 2f, 0.03f, radius * 2f);
            SetColor(pool, new Color(0.35f, 0.42f, 0.12f));

            var glow = pool.AddComponent<PortalGlow>();
            glow.colorA = new Color(0.25f, 0.32f, 0.08f);
            glow.colorB = new Color(0.55f, 0.58f, 0.2f);
            glow.speed = 0.8f;

            // The Cylinder primitive ships with its own CapsuleCollider -- reused as the
            // hazard's trigger volume rather than destroying and rebuilding one.
            var col = pool.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            var hazard = pool.AddComponent<LavaHazard>();
            hazard.appliedEffect = StatusEffectType.Blind;
            hazard.effectMagnitude = 1f;
            hazard.effectDuration = 1.5f;
            BuildHazardBasinRim(pos, radius);
        }

        // BuildBonePile/BuildIceSpikes' swamp counterpart -- tall reed/rush clusters (see
        // Models/Props/reed) instead of bones or ice spikes, falling back to the original
        // stretched-capsule primitive if the resource is ever missing.
        private void BuildReedCluster(Vector3 pos)
        {
            var reedColor = new Color(0.3f, 0.42f, 0.24f);
            var model = Resources.Load<GameObject>("Models/Props/reed");
            int count = Random.Range(3, 6);
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = new Vector3(Random.Range(-0.5f, 0.5f), 0.1f, Random.Range(-0.5f, 0.5f));
                if (model != null)
                {
                    var reedGO = Instantiate(model, transform);
                    reedGO.name = "Reed";
                    reedGO.transform.position = pos + offset;
                    reedGO.transform.rotation = Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(0f, 360f), Random.Range(-6f, 6f));
                    reedGO.transform.localScale = Vector3.one * Random.Range(0.7f, 1.2f);
                }
                else
                {
                    var reed = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    reed.name = "Reed";
                    var col = reed.GetComponent<Collider>();
                    if (col != null) Destroy(col); // decorative clutter -- shouldn't snag movement
                    reed.transform.SetParent(transform);
                    reed.transform.position = pos + offset;
                    reed.transform.rotation = Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(0f, 360f), Random.Range(-6f, 6f));
                    reed.transform.localScale = new Vector3(0.07f, Random.Range(0.5f, 0.85f), 0.07f);
                    SetColor(reed, reedColor);
                }
            }
        }

        // Single theme-dispatch point for a hazardous room's decor -- BuildRoom and
        // BuildCircularRoom each used to carry their own identical copy of this branch.
        // Every theme but SnakePit uses all three positions (a pool plus two decor
        // clusters); SnakePit only needs two grates, so it uses poolPos and decorB (the
        // rectangular room's own SnakePit branch used to place its second grate at a
        // third, slightly different offset than either decorA or decorB -- normalized to
        // decorB here to match the circular room's own SnakePit branch, which already did
        // this; the couple of units it moves a decorative prop by isn't worth carrying a
        // fourth position parameter just to preserve).
        private void BuildThemedHazardCluster(Vector3 poolPos, Vector3 decorA, Vector3 decorB)
        {
            if (theme == DungeonTheme.FrozenCrypt)
            {
                BuildIcePatch(poolPos, 2.5f);
                BuildIceSpikes(decorA);
                BuildIceSpikes(decorB);
            }
            else if (theme == DungeonTheme.SunkenRuins)
            {
                BuildPoisonBog(poolPos, 2.5f);
                BuildReedCluster(decorA);
                BuildReedCluster(decorB);
            }
            else if (theme == DungeonTheme.SnakePit)
            {
                BuildSnakeGrate(poolPos);
                BuildSnakeGrate(decorB);
            }
            else if (theme == DungeonTheme.WraithboundSanctum)
            {
                BuildWraithCircle(poolPos, 2.5f);
                BuildShatteredReliquary(decorA);
                BuildShatteredReliquary(decorB);
            }
            else
            {
                BuildLavaPool(poolPos, 2.5f);
                BuildBonePile(decorA);
                BuildBonePile(decorB);
            }
        }

        // Wraithbound Sanctum's own hazard patch -- a ring of grave-light that Curses
        // anyone standing in it, same LavaHazard/PortalGlow shape every other dungeon's
        // patch already uses.
        private void BuildWraithCircle(Vector3 pos, float radius)
        {
            var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pool.name = "WraithCircle";
            pool.transform.SetParent(transform);
            pool.transform.position = pos + new Vector3(0, 0.03f, 0);
            pool.transform.localScale = new Vector3(radius * 2f, 0.03f, radius * 2f);
            SetColor(pool, new Color(0.35f, 0.15f, 0.4f));

            var glow = pool.AddComponent<PortalGlow>();
            glow.colorA = new Color(0.2f, 0.06f, 0.25f);
            glow.colorB = new Color(0.55f, 0.85f, 0.5f);
            glow.speed = 0.7f;

            var col = pool.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            var hazard = pool.AddComponent<LavaHazard>();
            hazard.appliedEffect = StatusEffectType.Curse;
            hazard.effectMagnitude = 0.3f;
            hazard.effectDuration = 4f;
            BuildHazardBasinRim(pos, radius);
        }

        // Wraithbound Sanctum's clutter -- cracked reliquary shards (see
        // Models/Props/rubble_shard) and a broken urn (Models/Props/broken_urn) instead
        // of another dungeon's bone pile/ice spikes/reeds, falling back to the original
        // cube/cylinder primitives if either resource is ever missing.
        private void BuildShatteredReliquary(Vector3 pos)
        {
            var stoneColor = new Color(0.3f, 0.26f, 0.34f);
            var shardModel = Resources.Load<GameObject>("Models/Props/rubble_shard");
            int count = Random.Range(3, 6);
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = new Vector3(Random.Range(-0.55f, 0.55f), 0.1f, Random.Range(-0.55f, 0.55f));
                if (shardModel != null)
                {
                    var shardGO = Instantiate(shardModel, transform);
                    shardGO.name = "ReliquaryShard";
                    shardGO.transform.position = pos + offset;
                    shardGO.transform.rotation = Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
                    shardGO.transform.localScale = Vector3.one * Random.Range(0.7f, 1.3f);
                }
                else
                {
                    var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    shard.name = "ReliquaryShard";
                    var col = shard.GetComponent<Collider>();
                    if (col != null) Destroy(col); // decorative clutter -- shouldn't snag movement
                    shard.transform.SetParent(transform);
                    shard.transform.position = pos + offset;
                    shard.transform.rotation = Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
                    shard.transform.localScale = new Vector3(Random.Range(0.2f, 0.4f), Random.Range(0.3f, 0.6f), Random.Range(0.15f, 0.3f));
                    SetColor(shard, stoneColor);
                }
            }

            var urnModel = Resources.Load<GameObject>("Models/Props/broken_urn");
            if (urnModel != null)
            {
                var urnGO = Instantiate(urnModel, transform);
                urnGO.name = "BrokenUrn";
                urnGO.transform.position = pos + new Vector3(0.2f, 0f, -0.15f);
                urnGO.transform.rotation = Quaternion.Euler(18f, Random.Range(0f, 360f), 0f);
                urnGO.transform.localScale = Vector3.one * 0.7f;
            }
            else
            {
                var urn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                urn.name = "BrokenUrn";
                var urnCol = urn.GetComponent<Collider>();
                if (urnCol != null) Destroy(urnCol);
                urn.transform.SetParent(transform);
                urn.transform.position = pos + new Vector3(0.2f, 0.15f, -0.15f);
                urn.transform.rotation = Quaternion.Euler(18f, Random.Range(0f, 360f), 0f);
                urn.transform.localScale = new Vector3(0.28f, 0.22f, 0.28f);
                SetColor(urn, new Color(0.22f, 0.19f, 0.26f));
            }
        }

        // Snake Pit's own room fixture in place of a lava pool/ice patch/poison bog -- the
        // real dungeon's "indestructible Snake Grate tiles which will continually spawn
        // Pit Snakes." A flat marker (no damage of its own -- this isn't a hazard you take
        // damage for standing on, it's a spawner) plus SnakeGrateSpawner (see
        // World/SnakeGrateSpawner.cs) doing the actual periodic spawning.
        private void BuildSnakeGrate(Vector3 pos)
        {
            var model = Resources.Load<GameObject>("Models/Props/snake_grate");
            GameObject grate;
            if (model != null)
            {
                grate = Instantiate(model, transform);
                grate.name = "SnakeGrate";
                grate.transform.position = pos + new Vector3(0, 0.015f, 0);
                // Authored with an outer radius of ~1 (see props.js), matching the old
                // disc's 2-unit diameter -- no extra scale needed.
            }
            else
            {
                grate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                grate.name = "SnakeGrate";
                var col = grate.GetComponent<Collider>();
                if (col != null) Destroy(col); // decorative marker -- the room floor beneath is still solid
                grate.transform.SetParent(transform);
                grate.transform.position = pos + new Vector3(0, 0.015f, 0);
                grate.transform.localScale = new Vector3(2f, 0.015f, 2f);
                SetColor(grate, new Color(0.14f, 0.11f, 0.08f));
            }

            grate.AddComponent<SnakeGrateSpawner>();
        }

        // A same-level branch off Vault's west wall (see Build()'s treasureAlcove roll) --
        // structurally identical to BuildVerticalTunnel's ramp-and-chamber shape except
        // there's no ramp or grade change, just a short corridor stub and a small room,
        // since this is meant to read as an optional side room off the main path rather
        // than a below-grade secret. Returns the alcove's center so GameBootstrap can place
        // a bonus reward there.
        private Vector3 BuildTreasureAlcove(Vector3 roomCenter)
        {
            const float stubLength = 5f;
            const float alcoveHalf = 6f;

            Vector3 gapOuter = roomCenter + new Vector3(-roomWidth / 2f, 0, 0);
            Vector3 stubCenter = gapOuter + new Vector3(-stubLength / 2f, 0, 0);
            Vector3 alcoveCenter = gapOuter + new Vector3(-stubLength - alcoveHalf, 0, 0);

            var stubFloor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            stubFloor.name = "AlcoveStubFloor";
            stubFloor.transform.SetParent(transform);
            stubFloor.transform.position = stubCenter;
            // The stub runs east-west (along X), so X carries its LENGTH and Z its WIDTH --
            // opposite of BuildCorridor's north-south convention where X is width. This was
            // built with the axes swapped (a real, if subtle, bug: the floor undersized the
            // walkable width by about a unit and didn't fully cover the stub's own length),
            // unnoticed because corridorWidth and stubLength happened to be close in value.
            stubFloor.transform.localScale = new Vector3(stubLength / 10f, 1f, corridorWidth / 10f);
            SetColor(stubFloor, corridorFloorColor);
            BuildWall(stubCenter + new Vector3(0, wallHeight / 2f, corridorWidth / 2f), new Vector3(stubLength, wallHeight, wallThickness));
            BuildWall(stubCenter + new Vector3(0, wallHeight / 2f, -corridorWidth / 2f), new Vector3(stubLength, wallHeight, wallThickness));

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "AlcoveFloor";
            floor.transform.SetParent(transform);
            floor.transform.position = alcoveCenter;
            floor.transform.localScale = new Vector3(alcoveHalf * 2f / 10f, 1f, alcoveHalf * 2f / 10f);
            SetColor(floor, vaultFloorColor);

            BuildWall(alcoveCenter + new Vector3(0, wallHeight / 2f, alcoveHalf), new Vector3(alcoveHalf * 2f, wallHeight, wallThickness));
            BuildWall(alcoveCenter + new Vector3(0, wallHeight / 2f, -alcoveHalf), new Vector3(alcoveHalf * 2f, wallHeight, wallThickness));
            BuildWall(alcoveCenter + new Vector3(-alcoveHalf, wallHeight / 2f, 0), new Vector3(wallThickness, wallHeight, alcoveHalf * 2f));

            float sideLength = alcoveHalf - corridorWidth / 2f;
            if (sideLength > 0f)
            {
                float sideOffset = (corridorWidth / 2f + alcoveHalf) / 2f;
                BuildWall(alcoveCenter + new Vector3(alcoveHalf, wallHeight / 2f, sideOffset), new Vector3(wallThickness, wallHeight, sideLength));
                BuildWall(alcoveCenter + new Vector3(alcoveHalf, wallHeight / 2f, -sideOffset), new Vector3(wallThickness, wallHeight, sideLength));
            }

            if (buildCeiling)
            {
                var ceiling = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ceiling.name = "AlcoveCeiling";
                var ceilCol = ceiling.GetComponent<Collider>();
                if (ceilCol != null) Destroy(ceilCol);
                ceiling.transform.SetParent(transform);
                ceiling.transform.position = alcoveCenter + new Vector3(0, wallHeight, 0);
                ceiling.transform.rotation = Quaternion.Euler(180, 0, 0);
                ceiling.transform.localScale = new Vector3(alcoveHalf * 2f / 10f, 1f, alcoveHalf * 2f / 10f);
                SetColor(ceiling, ceilingColor);
            }

            if (buildTorches)
            {
                BuildTorch(alcoveCenter + new Vector3(alcoveHalf - 1.5f, 1.1f, alcoveHalf - 1.5f));
                BuildTorch(alcoveCenter + new Vector3(-(alcoveHalf - 1.5f), 1.1f, -(alcoveHalf - 1.5f)));
            }

            return alcoveCenter;
        }

        // A same-level branch off a room's EAST wall -- the "more corridors, randomly
        // generated" side content: an extra corridor + pocket room that isn't on the
        // critical path, sized randomly (9-13 unit half-width, 5-8 unit connecting stub)
        // so no two branches look identical. Structurally the mirror image of
        // BuildTreasureAlcove (which does the same thing off the WEST wall) but kept as
        // its own method rather than shared: TreasureAlcove is deliberately small and
        // loot-only with a fixed size, this is bigger, randomly sized, and can also host an
        // ambush (see GameBootstrap's per-theme Enter*Dungeon loop over BranchPoints).
        // Returns the pocket's center so the caller can populate it.
        private Vector3 BuildBranchPocket(Vector3 roomCenter)
        {
            float stubLength = Random.Range(5f, 8f);
            float pocketHalf = Random.Range(9f, 13f);

            Vector3 gapOuter = roomCenter + new Vector3(roomWidth / 2f, 0, 0);
            Vector3 stubCenter = gapOuter + new Vector3(stubLength / 2f, 0, 0);
            Vector3 pocketCenter = gapOuter + new Vector3(stubLength + pocketHalf, 0, 0);

            var stubFloor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            stubFloor.name = "BranchStubFloor";
            stubFloor.transform.SetParent(transform);
            stubFloor.transform.position = stubCenter;
            stubFloor.transform.localScale = new Vector3(stubLength / 10f, 1f, corridorWidth / 10f);
            SetColor(stubFloor, corridorFloorColor);
            BuildWall(stubCenter + new Vector3(0, wallHeight / 2f, corridorWidth / 2f), new Vector3(stubLength, wallHeight, wallThickness));
            BuildWall(stubCenter + new Vector3(0, wallHeight / 2f, -corridorWidth / 2f), new Vector3(stubLength, wallHeight, wallThickness));

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "BranchPocketFloor";
            floor.transform.SetParent(transform);
            floor.transform.position = pocketCenter;
            floor.transform.localScale = new Vector3(pocketHalf * 2f / 10f, 1f, pocketHalf * 2f / 10f);
            SetColor(floor, combatFloorColor);

            BuildWall(pocketCenter + new Vector3(0, wallHeight / 2f, pocketHalf), new Vector3(pocketHalf * 2f, wallHeight, wallThickness));
            BuildWall(pocketCenter + new Vector3(0, wallHeight / 2f, -pocketHalf), new Vector3(pocketHalf * 2f, wallHeight, wallThickness));
            // East wall (facing away from the room) is solid; the gap sits on the west
            // side, facing back toward the corridor -- opposite of BuildTreasureAlcove's
            // alcove, which sits west of its room and so gaps its EAST side instead.
            BuildWall(pocketCenter + new Vector3(pocketHalf, wallHeight / 2f, 0), new Vector3(wallThickness, wallHeight, pocketHalf * 2f));

            float sideLength = pocketHalf - corridorWidth / 2f;
            if (sideLength > 0f)
            {
                float sideOffset = (corridorWidth / 2f + pocketHalf) / 2f;
                BuildWall(pocketCenter + new Vector3(-pocketHalf, wallHeight / 2f, sideOffset), new Vector3(wallThickness, wallHeight, sideLength));
                BuildWall(pocketCenter + new Vector3(-pocketHalf, wallHeight / 2f, -sideOffset), new Vector3(wallThickness, wallHeight, sideLength));
            }

            if (buildCeiling)
            {
                var ceiling = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ceiling.name = "BranchPocketCeiling";
                var ceilCol = ceiling.GetComponent<Collider>();
                if (ceilCol != null) Destroy(ceilCol);
                ceiling.transform.SetParent(transform);
                ceiling.transform.position = pocketCenter + new Vector3(0, wallHeight, 0);
                ceiling.transform.rotation = Quaternion.Euler(180, 0, 0);
                ceiling.transform.localScale = new Vector3(pocketHalf * 2f / 10f, 1f, pocketHalf * 2f / 10f);
                SetColor(ceiling, ceilingColor);
            }

            if (buildTorches)
            {
                BuildTorch(pocketCenter + new Vector3(pocketHalf - 1.5f, 1.1f, pocketHalf - 1.5f));
                BuildTorch(pocketCenter + new Vector3(-(pocketHalf - 1.5f), 1.1f, -(pocketHalf - 1.5f)));
            }

            return pocketCenter;
        }

        // A flat, dark ceiling reads better than leaving the room open to the void above --
        // rotated 180 on X so the Plane's single-sided front face points downward, into
        // the room, instead of up into nothing.
        private void BuildCeiling(Vector3 center)
        {
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ceiling.name = "Ceiling";
            var col = ceiling.GetComponent<Collider>();
            if (col != null) Destroy(col);
            ceiling.transform.SetParent(transform);
            ceiling.transform.position = center + new Vector3(0, wallHeight, 0);
            ceiling.transform.rotation = Quaternion.Euler(180, 0, 0);
            ceiling.transform.localScale = new Vector3(roomWidth / 10f, 1f, roomDepth / 10f);
            SetColor(ceiling, ceilingColor);
        }

        // Four corner pillars plus torches on two opposite ones -- breaks up what would
        // otherwise be a flat empty box, and gives every room actual light sources instead
        // of relying purely on the single scene-wide directional light.
        //
        // skipSouthEastPillar is set for any room with a platform (see the platform block
        // below, always placed in the south-east corner): a solid, full-height pillar
        // there sat almost exactly on top of one of the platform's support legs (both
        // within ~0.3 units of the same corner), a real full-collider obstruction tucked
        // under/behind the platform that read as an inexplicable "invisible" wall.
        private void BuildRoomDecor(Vector3 center, bool skipSouthEastPillar = false)
        {
            float inset = 2.2f;
            Vector3[] corners =
            {
                center + new Vector3(roomWidth / 2f - inset, 0, roomDepth / 2f - inset),
                center + new Vector3(-(roomWidth / 2f - inset), 0, roomDepth / 2f - inset),
                center + new Vector3(roomWidth / 2f - inset, 0, -(roomDepth / 2f - inset)),
                center + new Vector3(-(roomWidth / 2f - inset), 0, -(roomDepth / 2f - inset)),
            };
            const int southEastIndex = 2;

            for (int i = 0; i < corners.Length; i++)
            {
                if (buildPillars && !(skipSouthEastPillar && i == southEastIndex)) BuildPillar(corners[i]);
                if (buildTorches && i % 2 == 0) BuildTorch(corners[i] + new Vector3(0, 1.1f, 0));
            }
        }

        // Modeled at exactly 8 units tall (see Models/Props/pillar, and props.js's own
        // comment on why) -- the standard non-boss wallHeight, so the common case needs
        // no runtime rescale at all; only the boss room's own taller wallHeight bump
        // stretches it further, same as the old cube did unconditionally.
        private void BuildPillar(Vector3 basePos)
        {
            var model = Resources.Load<GameObject>("Models/Props/pillar");
            if (model != null)
            {
                var pillarGO = Instantiate(model, transform);
                pillarGO.name = "Pillar";
                pillarGO.transform.position = basePos;
                pillarGO.transform.localScale = new Vector3(1f, wallHeight / 8f, 1f);
                TintRecursive(pillarGO, new Color(wallColor.r + 0.03f, wallColor.g + 0.03f, wallColor.b + 0.03f));
            }
            else
            {
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = "Pillar";
                pillar.transform.SetParent(transform);
                pillar.transform.position = basePos + new Vector3(0, wallHeight / 2f, 0);
                pillar.transform.localScale = new Vector3(0.7f, wallHeight, 0.7f);
                SetColor(pillar, new Color(wallColor.r + 0.03f, wallColor.g + 0.03f, wallColor.b + 0.03f));
            }
        }

        private void BuildTorch(Vector3 pos)
        {
            var torchGO = new GameObject("Torch");
            torchGO.transform.SetParent(transform);
            torchGO.transform.position = pos;

            // Real angled-bracket-plus-flame mesh (see Models/Props/wall_torch) when the
            // resource is present, falling back to the original cube-holder/sphere-flame
            // primitives if it's ever missing.
            var model = Resources.Load<GameObject>("Models/Props/wall_torch");
            if (model != null)
            {
                var modelGO = Instantiate(model, torchGO.transform);
                modelGO.name = "TorchModel";
                modelGO.transform.localPosition = Vector3.zero;
                modelGO.transform.localRotation = Quaternion.identity;
            }
            else
            {
                var holder = GameObject.CreatePrimitive(PrimitiveType.Cube);
                holder.name = "TorchHolder";
                var holderCol = holder.GetComponent<Collider>();
                if (holderCol != null) Destroy(holderCol);
                holder.transform.SetParent(torchGO.transform);
                holder.transform.localPosition = Vector3.zero;
                holder.transform.localScale = new Vector3(0.15f, 0.55f, 0.15f);
                SetColor(holder, new Color(0.12f, 0.08f, 0.05f));

                var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                flame.name = "Flame";
                var flameCol = flame.GetComponent<Collider>();
                if (flameCol != null) Destroy(flameCol);
                flame.transform.SetParent(torchGO.transform);
                flame.transform.localPosition = new Vector3(0, 0.4f, 0);
                flame.transform.localScale = Vector3.one * 0.22f;
                SetColor(flame, new Color(1f, 0.6f, 0.15f));
            }

            var light = torchGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.65f, 0.3f);
            // Corner torches sit ~16-17 units from a 28-unit room's own center (see
            // BuildRoomDecor's inset) -- an 8-unit range never reached it, leaving the
            // middle of every room (exactly where the player spawns/walks) lit by ambient
            // alone. Bumped to actually cover that distance.
            light.range = 18f;
            light.intensity = 2f;
            torchGO.AddComponent<TorchFlicker>();
        }

        // A "door" is just two shorter wall segments with a corridorWidth-wide gap
        // between them, rather than one full-length wall.
        private void BuildWallOrDoor(Vector3 roomCenter, float zOffset, bool open)
        {
            Vector3 wallCenter = roomCenter + new Vector3(0, wallHeight / 2f, zOffset);
            if (!open)
            {
                BuildWall(wallCenter, new Vector3(roomWidth, wallHeight, wallThickness));
                return;
            }

            float sideLength = (roomWidth - corridorWidth) / 2f;
            if (sideLength <= 0f) return; // corridor as wide as the room -- no side segments needed
            float sideOffset = (corridorWidth + sideLength) / 2f;
            BuildWall(wallCenter + new Vector3(sideOffset, 0, 0), new Vector3(sideLength, wallHeight, wallThickness));
            BuildWall(wallCenter + new Vector3(-sideOffset, 0, 0), new Vector3(sideLength, wallHeight, wallThickness));
        }

        // Same two-segment-with-a-gap trick as BuildWallOrDoor, just applied to a room's
        // west face instead of its north/south ends -- this is what the vertical tunnel's
        // ramp actually walks out through.
        private void BuildWestWallWithGap(Vector3 roomCenter)
        {
            Vector3 wallCenter = roomCenter + new Vector3(-roomWidth / 2f, wallHeight / 2f, 0);
            float sideLength = (roomDepth - corridorWidth) / 2f;
            if (sideLength <= 0f) return;
            float sideOffset = (corridorWidth + sideLength) / 2f;
            BuildWall(wallCenter + new Vector3(0, 0, sideOffset), new Vector3(wallThickness, wallHeight, sideLength));
            BuildWall(wallCenter + new Vector3(0, 0, -sideOffset), new Vector3(wallThickness, wallHeight, sideLength));
        }

        // East-side mirror of BuildWestWallWithGap -- what BuildRoom's new eastBranch flag
        // uses to open a gap for BuildBranchPocket's connecting corridor, exactly the same
        // two-segment-with-a-gap shape just on the opposite wall.
        private void BuildEastWallWithGap(Vector3 roomCenter)
        {
            Vector3 wallCenter = roomCenter + new Vector3(roomWidth / 2f, wallHeight / 2f, 0);
            float sideLength = (roomDepth - corridorWidth) / 2f;
            if (sideLength <= 0f) return;
            float sideOffset = (corridorWidth + sideLength) / 2f;
            BuildWall(wallCenter + new Vector3(0, 0, sideOffset), new Vector3(wallThickness, wallHeight, sideLength));
            BuildWall(wallCenter + new Vector3(0, 0, -sideOffset), new Vector3(wallThickness, wallHeight, sideLength));
        }

        // First slice of dungeon verticality: a ramp off a room's west wall dips down to a
        // below-grade dead-end chamber with its own little ambush and a reward chest (see
        // GameBootstrap.EnterDungeon) -- a real second Y-level connected by something you
        // actually walk down, not just a teleport or a second flat room.
        private void BuildVerticalTunnel(Vector3 roomCenter)
        {
            Vector3 gapOuter = roomCenter + new Vector3(-roomWidth / 2f, 0, 0);
            Vector3 rampBottom = gapOuter + new Vector3(-6f, -4f, 0);
            BuildRamp(gapOuter, rampBottom, corridorWidth);

            Vector3 chamberCenter = rampBottom + new Vector3(-6f, 0, 0);
            TunnelPoint = chamberCenter;
            BuildTunnelChamber(chamberCenter, corridorWidth / 2f);

            if (buildTorches) BuildTorch(gapOuter + new Vector3(-1.2f, 1.1f, 0));
        }

        // Oriented via LookRotation(dir, world-up) rather than hand-derived Euler angles or
        // FromToRotation -- local Z tracks the slope direction (bottomPos to topPos) and
        // local X (width) is cross(up, forward), which is always horizontal, so the box
        // pitches to match the slope but never rolls, regardless of which horizontal axis
        // the ramp runs along.
        //
        // This used to be Quaternion.FromToRotation(Vector3.right, dir.normalized) with
        // local X as the length axis. FromToRotation picks the single minimal rotation
        // from local +X onto dir -- fine when dir's horizontal component lies along world
        // X, but for a ramp that rises while running along world Z instead (the platform
        // ramp below climbs in Y over a horizontal run entirely along Z), that minimal
        // rotation wasn't pure pitch: it also rolled the box, tilting its width axis out
        // of level. The roll compounded with the pitch and steepened the walkable
        // surface's real slope well past the rise/run angle -- confirmed ~47.8 degrees of
        // actual surface tilt for that ramp versus the ~35 degrees its rise
        // (platformHeight=3.5) over run (5) implies, which clears CharacterController's
        // default 45-degree slopeLimit and made the "ramp" functionally an unclimbable
        // wall (the exact "invisible barrier" shape the room already had a decor-pillar
        // version of). LookRotation can't roll, so this can't recur regardless of which
        // horizontal axis a future ramp runs along.
        private void BuildRamp(Vector3 topPos, Vector3 bottomPos, float width, float thickness = 0.4f)
        {
            Vector3 dir = topPos - bottomPos;
            float length = dir.magnitude;
            if (length < 0.01f) return;

            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Ramp";
            ramp.transform.SetParent(transform);
            ramp.transform.position = (topPos + bottomPos) / 2f;
            ramp.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            // Axis roles follow the rotation above: local Z is the length axis and local X
            // the width axis (the old FromToRotation version had local X as length, local
            // Z as width).
            ramp.transform.localScale = new Vector3(width, thickness, length);
            SetColor(ramp, wallColor);
        }

        // A flat slab plus four support legs -- an elevated stand for a ranged enemy (see
        // GameBootstrap), reached by a ramp built separately by the caller.
        private void BuildPlatform(Vector3 topCenter)
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Platform";
            slab.transform.SetParent(transform);
            slab.transform.position = topCenter;
            slab.transform.localScale = new Vector3(platformHalfSize * 2f, 0.4f, platformHalfSize * 2f);
            SetColor(slab, new Color(wallColor.r + 0.04f, wallColor.g + 0.04f, wallColor.b + 0.04f));

            Vector3 legBase = topCenter - new Vector3(0, platformHeight / 2f + 0.2f, 0);
            float legInset = platformHalfSize - 0.4f;
            Vector3[] legOffsets =
            {
                new Vector3(legInset, 0, legInset), new Vector3(-legInset, 0, legInset),
                new Vector3(legInset, 0, -legInset), new Vector3(-legInset, 0, -legInset),
            };
            foreach (var offset in legOffsets)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = "PlatformLeg";
                var col = leg.GetComponent<Collider>();
                if (col != null) Destroy(col); // decorative -- the slab above is the only thing that needs to be solid
                leg.transform.SetParent(transform);
                leg.transform.position = legBase + offset;
                leg.transform.localScale = new Vector3(0.35f, platformHeight, 0.35f);
                SetColor(leg, wallColor);
            }
        }

        // A dead-end pocket, not a loop back up -- the player climbs back out the same
        // ramp they came down. East wall carries the gap the ramp connects through; the
        // other three sides are solid.
        private void BuildTunnelChamber(Vector3 center, float gapHalf)
        {
            const float half = 6f;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "TunnelFloor";
            floor.transform.SetParent(transform);
            floor.transform.position = center;
            floor.transform.localScale = new Vector3(half * 2f / 10f, 1f, half * 2f / 10f);
            SetColor(floor, new Color(0.1f, 0.09f, 0.1f)); // darker, damp cave tone -- distinct from the rooms above

            BuildWall(center + new Vector3(0, wallHeight / 2f, half), new Vector3(half * 2f, wallHeight, wallThickness));
            BuildWall(center + new Vector3(0, wallHeight / 2f, -half), new Vector3(half * 2f, wallHeight, wallThickness));
            BuildWall(center + new Vector3(-half, wallHeight / 2f, 0), new Vector3(wallThickness, wallHeight, half * 2f));

            float sideLength = half - gapHalf;
            if (sideLength > 0f)
            {
                float sideOffset = (gapHalf + half) / 2f;
                BuildWall(center + new Vector3(half, wallHeight / 2f, sideOffset), new Vector3(wallThickness, wallHeight, sideLength));
                BuildWall(center + new Vector3(half, wallHeight / 2f, -sideOffset), new Vector3(wallThickness, wallHeight, sideLength));
            }

            if (buildCeiling)
            {
                var ceiling = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ceiling.name = "TunnelCeiling";
                var col = ceiling.GetComponent<Collider>();
                if (col != null) Destroy(col);
                ceiling.transform.SetParent(transform);
                ceiling.transform.position = center + new Vector3(0, wallHeight, 0);
                ceiling.transform.rotation = Quaternion.Euler(180, 0, 0);
                ceiling.transform.localScale = new Vector3(half * 2f / 10f, 1f, half * 2f / 10f);
                SetColor(ceiling, ceilingColor);
            }

            if (buildTorches)
            {
                BuildTorch(center + new Vector3(half - 1.5f, 1.1f, half - 1.5f));
                BuildTorch(center + new Vector3(-(half - 1.5f), 1.1f, -(half - 1.5f)));
            }
            BuildBonePile(center + new Vector3(-1.5f, 0, 1f));
        }

        // Connects any two adjacent spine rooms regardless of which cardinal direction
        // separates them -- the old version only ever built a Z-aligned (north-south)
        // corridor, which was fine when the spine was a fixed straight line but breaks
        // the moment a hop can also run east-west (see Build()'s random-walk path).
        // Picks its own orientation from whichever axis actually differs between the
        // two centers; the two are always axis-aligned single grid hops (never
        // diagonal), so this is never ambiguous.
        private void BuildCorridor(Vector3 fromCenter, Vector3 toCenter)
        {
            Vector3 mid = (fromCenter + toCenter) / 2f;
            bool alongZ = Mathf.Abs(toCenter.z - fromCenter.z) > Mathf.Abs(toCenter.x - fromCenter.x);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "CorridorFloor";
            floor.transform.SetParent(transform);
            floor.transform.position = mid;
            floor.transform.localScale = alongZ
                ? new Vector3(corridorWidth / 10f, 1f, corridorLength / 10f)
                : new Vector3(corridorLength / 10f, 1f, corridorWidth / 10f);
            SetColor(floor, corridorFloorColor);

            if (alongZ)
            {
                BuildWall(mid + new Vector3(corridorWidth / 2f, wallHeight / 2f, 0), new Vector3(wallThickness, wallHeight, corridorLength));
                BuildWall(mid + new Vector3(-corridorWidth / 2f, wallHeight / 2f, 0), new Vector3(wallThickness, wallHeight, corridorLength));
            }
            else
            {
                BuildWall(mid + new Vector3(0, wallHeight / 2f, corridorWidth / 2f), new Vector3(corridorLength, wallHeight, wallThickness));
                BuildWall(mid + new Vector3(0, wallHeight / 2f, -corridorWidth / 2f), new Vector3(corridorLength, wallHeight, wallThickness));
            }

            if (buildCeiling)
            {
                var ceiling = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ceiling.name = "CorridorCeiling";
                var col = ceiling.GetComponent<Collider>();
                if (col != null) Destroy(col);
                ceiling.transform.SetParent(transform);
                ceiling.transform.position = mid + new Vector3(0, wallHeight, 0);
                ceiling.transform.rotation = Quaternion.Euler(180, 0, 0);
                ceiling.transform.localScale = alongZ
                    ? new Vector3(corridorWidth / 10f, 1f, corridorLength / 10f)
                    : new Vector3(corridorLength / 10f, 1f, corridorWidth / 10f);
                SetColor(ceiling, ceilingColor);
            }

            if (buildTorches) BuildTorch(mid + new Vector3(0, 1.1f, 0));
        }

        private void BuildWall(Vector3 worldCenter, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.SetParent(transform);
            wall.transform.position = worldCenter;
            wall.transform.localScale = scale;
            SetColor(wall, wallColor);

            AddWallTrim(worldCenter, scale);
        }

        // A darker baseboard and a lighter cap band, flush against the wall's own footprint
        // -- breaks up what used to be one flat-colored box into a body+accent read, the
        // same body/accent-color language the enemies now use (see ProceduralMonster).
        // Both bands are decorative only (colliders destroyed) so they never change a
        // wall's actual collision footprint or any corridor-width math elsewhere.
        private void AddWallTrim(Vector3 worldCenter, Vector3 scale)
        {
            float bandHeight = Mathf.Min(0.3f, scale.y * 0.15f);
            Color baseboard = wallColor * 0.55f; baseboard.a = 1f;
            Color cap = Color.Lerp(wallColor, Color.white, 0.25f);
            // Padded a hair past the wall's own width/depth on both plan-view axes so the
            // band never z-fights with the wall body, regardless of which axis is this
            // particular wall's "length" vs. "thickness."
            Vector3 bandScale = new Vector3(scale.x + 0.04f, bandHeight, scale.z + 0.04f);

            BuildTrimBand(worldCenter + new Vector3(0, -scale.y / 2f + bandHeight / 2f, 0), bandScale, baseboard);
            BuildTrimBand(worldCenter + new Vector3(0, scale.y / 2f - bandHeight / 2f, 0), bandScale, cap);
        }

        // Returns the created GameObject (rather than void) so BuildCircularWallArc can
        // reparent it onto a rotated wall segment afterward -- AddWallTrim's straight-wall
        // callers just discard the return value.
        private GameObject BuildTrimBand(Vector3 pos, Vector3 scale, Color color)
        {
            var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
            band.name = "WallTrim";
            var col = band.GetComponent<Collider>();
            if (col != null) Destroy(col);
            band.transform.SetParent(transform);
            band.transform.position = pos;
            band.transform.localScale = scale;
            SetColor(band, color);
            return band;
        }

        private void SetColor(GameObject go, Color c)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Standard")) { color = c };
            }
        }

        // One-shot "you've arrived" trigger for RoomBanner (see BuildRoomEntryTrigger) --
        // fires the banner the first time the player's own collider passes through it, then
        // destroys itself so backtracking through an already-visited room doesn't re-show
        // the name. Sphere is trigger-only (see BuildRoomEntryTrigger), so it never blocks
        // movement or interferes with the room's real geometry.
        //
        // Identifies the player the same way Projectile/AbyssMage already do off a trigger
        // collision: GetComponentInParent<PlayerCharacter>() off the collider that entered,
        // rather than a tag or layer check.
        private class RoomEntryTrigger : MonoBehaviour
        {
            public string roomName;
            public string flavor;

            private void OnTriggerEnter(Collider other)
            {
                if (other.GetComponentInParent<PlayerCharacter>() == null) return;
                RoomBanner.Show(roomName, flavor);
                Destroy(gameObject);
            }
        }
    }
}
