using UnityEngine;

namespace DungeonCrawler.World
{
    // Which palette/hazard set BuildRoom-family methods use -- lets the exact same room/
    // corridor/circular-room/platform/tunnel generator below serve more than one dungeon
    // without duplicating any of that structural code. Add a case here plus an Apply*
    // Palette method and a hazard branch (see BuildRoom/BuildCircularRoom) for each new
    // dungeon theme.
    public enum DungeonTheme { Abyss, FrozenCrypt, SunkenRuins }

    // A real (if crude) dungeon: five rooms in a line -- Entry, two Combat rooms, a Vault,
    // and the Boss -- joined by corridors, instead of BlockoutRoom's single flat box. Same
    // placeholder-geometry philosophy (primitives, flat colors), just enough structure
    // that a "dungeon" reads as more than one room. Positions are exposed so GameBootstrap
    // can place the player and enemies without hardcoding coordinates that would drift out
    // of sync with the geometry.
    //
    // Builds in Awake(), not Start(): GameBootstrap does
    // `roomGO.AddComponent<DungeonLayout>()` and immediately reads the room points the
    // same frame. AddComponent() calls Awake() synchronously; Start() would not run until
    // after GameBootstrap.Start() has already returned, leaving those properties at their
    // default (0,0,0).
    public class DungeonLayout : MonoBehaviour
    {
        public float roomWidth = 28f;
        public float roomDepth = 28f;
        public float circularRoomRadius = 15f;
        public float platformHeight = 3.5f;
        public float platformHalfSize = 3f;
        public float corridorWidth = 4f;
        public float corridorLength = 6f;
        public float wallHeight = 3f;
        public float wallThickness = 0.5f;

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

        // Explicit call instead of building in Awake() -- GameBootstrap needs to hand this
        // a theme (which room/enemy content to build) before generation runs, the same
        // reason PlayerCharacter.Initialize() exists instead of doing everything in Awake.
        public void Build(DungeonTheme dungeonTheme = DungeonTheme.Abyss)
        {
            theme = dungeonTheme;
            if (theme == DungeonTheme.FrozenCrypt) ApplyFrozenCryptPalette();
            else if (theme == DungeonTheme.SunkenRuins) ApplySunkenRuinsPalette();

            EntryPoint = Vector3.zero;
            CombatPoint = new Vector3(0, 0, RoomSpacing);
            Combat2Point = new Vector3(0, 0, RoomSpacing * 2f);
            VaultPoint = new Vector3(0, 0, RoomSpacing * 3f);
            BossPoint = new Vector3(0, 0, RoomSpacing * 4f);

            BuildRoom(EntryPoint, entryFloorColor, openNorth: true, openSouth: false, hazardous: false);
            BuildCircularRoom(CombatPoint, combatFloorColor, circularRoomRadius);
            BuildRoom(Combat2Point, combatFloorColor, openNorth: true, openSouth: true, hazardous: true, westTunnel: true, platform: true);
            BuildRoom(VaultPoint, vaultFloorColor, openNorth: true, openSouth: true, hazardous: true);
            BuildRoom(BossPoint, bossFloorColor, openNorth: false, openSouth: true, hazardous: true);

            BuildCorridor((EntryPoint + CombatPoint) / 2f);
            BuildCorridor((CombatPoint + Combat2Point) / 2f);
            BuildCorridor((Combat2Point + VaultPoint) / 2f);
            BuildCorridor((VaultPoint + BossPoint) / 2f);

            BuildVerticalTunnel(Combat2Point);
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

        private void BuildRoom(Vector3 center, Color floorColor, bool openNorth, bool openSouth, bool hazardous, bool westTunnel = false, bool platform = false)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "RoomFloor";
            floor.transform.SetParent(transform);
            floor.transform.position = center;
            floor.transform.localScale = new Vector3(roomWidth / 10f, 1f, roomDepth / 10f);
            SetColor(floor, floorColor);

            // East/west walls are always solid, except a room flagged westTunnel -- doors
            // otherwise only ever open north/south, toward the next room in the line.
            BuildWall(center + new Vector3(roomWidth / 2f, wallHeight / 2f, 0), new Vector3(wallThickness, wallHeight, roomDepth));
            if (westTunnel)
                BuildWestWallWithGap(center);
            else
                BuildWall(center + new Vector3(-roomWidth / 2f, wallHeight / 2f, 0), new Vector3(wallThickness, wallHeight, roomDepth));

            BuildWallOrDoor(center, roomDepth / 2f, openNorth);
            BuildWallOrDoor(center, -roomDepth / 2f, openSouth);

            if (buildCeiling) BuildCeiling(center);
            if (buildPillars || buildTorches) BuildRoomDecor(center);

            // Entry stays a clean, calm transition room -- lava/bones are reserved for the
            // rooms that are actually fights, so the abyss theming reads as "danger zone,"
            // not just uniform decoration everywhere.
            if (hazardous)
            {
                if (theme == DungeonTheme.FrozenCrypt)
                {
                    BuildIcePatch(center + new Vector3(6f, 0, 4f), 2.5f);
                    BuildIceSpikes(center + new Vector3(-8f, 0, -3f));
                    BuildIceSpikes(center + new Vector3(5f, 0, -8f));
                }
                else if (theme == DungeonTheme.SunkenRuins)
                {
                    BuildPoisonBog(center + new Vector3(6f, 0, 4f), 2.5f);
                    BuildReedCluster(center + new Vector3(-8f, 0, -3f));
                    BuildReedCluster(center + new Vector3(5f, 0, -8f));
                }
                else
                {
                    BuildLavaPool(center + new Vector3(6f, 0, 4f), 2.5f);
                    BuildBonePile(center + new Vector3(-8f, 0, -3f));
                    BuildBonePile(center + new Vector3(5f, 0, -8f));
                }
            }

            // A raised platform in the room's south-east corner, up a ramp -- a ranged
            // enemy posted here (see GameBootstrap) has to actually be climbed up to and
            // engaged, not just shot at from below with no way to close the gap.
            if (platform)
            {
                Vector3 platformTop = center + new Vector3(roomWidth / 2f - 5f, platformHeight, -(roomDepth / 2f - 5f));
                Combat2PlatformPoint = platformTop; // only Combat2Point passes platform:true today
                BuildPlatform(platformTop);
                Vector3 rampBottom = platformTop + new Vector3(0, -platformHeight, platformHalfSize + 5f);
                Vector3 rampTop = platformTop + new Vector3(0, 0, platformHalfSize);
                BuildRamp(rampTop, rampBottom, 3f);
            }
        }

        // A ring-walled circular arena instead of a rectangular box -- CombatPoint
        // specifically, for room-shape variety. North/south openings line up with the
        // corridors the same way a rectangular room's doors do (see BuildCircularWallRing);
        // everything else around the ring is solid wall built from short tangent segments.
        private void BuildCircularRoom(Vector3 center, Color floorColor, float radius)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "RoomFloor";
            floor.transform.SetParent(transform);
            floor.transform.position = center;
            floor.transform.localScale = new Vector3(radius * 2f, 0.05f, radius * 2f);
            SetColor(floor, floorColor);

            float gapHalfAngle = Mathf.Asin(Mathf.Clamp01((corridorWidth / 2f) / radius)) * Mathf.Rad2Deg;
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

            if (theme == DungeonTheme.FrozenCrypt)
            {
                BuildIcePatch(center + new Vector3(-6f, 0, -6f), 2.5f);
                BuildIceSpikes(center + new Vector3(-8f, 0, 5f));
                BuildIceSpikes(center + new Vector3(6f, 0, -8f));
            }
            else if (theme == DungeonTheme.SunkenRuins)
            {
                BuildPoisonBog(center + new Vector3(-6f, 0, -6f), 2.5f);
                BuildReedCluster(center + new Vector3(-8f, 0, 5f));
                BuildReedCluster(center + new Vector3(6f, 0, -8f));
            }
            else
            {
                BuildLavaPool(center + new Vector3(-6f, 0, -6f), 2.5f);
                BuildBonePile(center + new Vector3(-8f, 0, 5f));
                BuildBonePile(center + new Vector3(6f, 0, -8f));
            }

            // Sniper platform to the east, well clear of both corridor openings (0 deg and
            // 180 deg) -- the ramp climbs toward the room's own center so it can't run past
            // the wall on the far side.
            Vector3 platformTop = center + new Vector3(radius - 6f, platformHeight, 0);
            CombatPlatformPoint = platformTop;
            BuildPlatform(platformTop);
            Vector3 rampTop = platformTop + new Vector3(-platformHalfSize, 0, 0);
            Vector3 rampBottom = rampTop + new Vector3(-4f, -platformHeight, 0);
            BuildRamp(rampTop, rampBottom, 3f);
        }

        // Builds the ring as short wall segments, each tangent to the circle at its own
        // angle (local X becomes the tangent direction, local Z the radial thickness, once
        // rotated by that angle around Y) -- skips any segment whose center angle falls
        // within gapHalfAngle of one of gapAnglesDeg, which is where a corridor connects.
        private void BuildCircularWallRing(Vector3 center, float radius, float[] gapAnglesDeg, float gapHalfAngle)
        {
            const int segments = 24;
            float segmentAngle = 360f / segments;
            float chordLength = 2f * radius * Mathf.Sin(segmentAngle * Mathf.Deg2Rad / 2f);
            float segmentWidth = chordLength * 1.15f; // slight overlap so segments don't leave visible seams

            for (int i = 0; i < segments; i++)
            {
                float angle = i * segmentAngle;
                bool inGap = false;
                foreach (var gapAngle in gapAnglesDeg)
                {
                    if (Mathf.Abs(Mathf.DeltaAngle(angle, gapAngle)) <= gapHalfAngle) { inGap = true; break; }
                }
                if (inGap) continue;

                float rad = angle * Mathf.Deg2Rad;
                Vector3 pos = center + new Vector3(Mathf.Sin(rad) * radius, wallHeight / 2f, Mathf.Cos(rad) * radius);

                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "CircularWall";
                wall.transform.SetParent(transform);
                wall.transform.position = pos;
                wall.transform.rotation = Quaternion.Euler(0, angle, 0);
                wall.transform.localScale = new Vector3(segmentWidth, wallHeight, wallThickness);
                SetColor(wall, wallColor);
            }
        }

        // A glowing hazard pool -- forces the player to actually route around part of the
        // room instead of walking a straight line through every fight, and gives ranged
        // imps something worth kiting behind.
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
        }

        // Scattered bones plus a half-buried skull -- crude primitives, but they instantly
        // read as "remains" among the lava and dark stone, which is the whole point.
        private void BuildBonePile(Vector3 pos)
        {
            var boneColor = new Color(0.82f, 0.78f, 0.68f);
            int count = Random.Range(3, 6);
            for (int i = 0; i < count; i++)
            {
                var bone = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                bone.name = "Bone";
                var col = bone.GetComponent<Collider>();
                if (col != null) Destroy(col); // decorative clutter -- shouldn't snag movement
                bone.transform.SetParent(transform);
                Vector3 offset = new Vector3(Random.Range(-0.5f, 0.5f), 0.08f, Random.Range(-0.5f, 0.5f));
                bone.transform.position = pos + offset;
                bone.transform.rotation = Quaternion.Euler(Random.Range(70f, 110f), Random.Range(0f, 360f), 0f);
                bone.transform.localScale = new Vector3(0.08f, Random.Range(0.25f, 0.4f), 0.08f);
                SetColor(bone, boneColor);
            }

            var skull = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            skull.name = "Skull";
            var skullCol = skull.GetComponent<Collider>();
            if (skullCol != null) Destroy(skullCol);
            skull.transform.SetParent(transform);
            skull.transform.position = pos + new Vector3(Random.Range(-0.3f, 0.3f), 0.12f, Random.Range(-0.3f, 0.3f));
            skull.transform.localScale = new Vector3(0.32f, 0.28f, 0.36f);
            SetColor(skull, boneColor);
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

            pool.AddComponent<LavaHazard>();
        }

        // BuildBonePile's icy counterpart -- jagged ice-spike clusters instead of scattered
        // bones, same crude-primitives-read-instantly approach.
        private void BuildIceSpikes(Vector3 pos)
        {
            var iceColor = new Color(0.78f, 0.92f, 1f);
            int count = Random.Range(3, 6);
            for (int i = 0; i < count; i++)
            {
                var spike = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                spike.name = "IceSpike";
                var col = spike.GetComponent<Collider>();
                if (col != null) Destroy(col); // decorative clutter -- shouldn't snag movement
                spike.transform.SetParent(transform);
                Vector3 offset = new Vector3(Random.Range(-0.5f, 0.5f), 0.1f, Random.Range(-0.5f, 0.5f));
                spike.transform.position = pos + offset;
                spike.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
                spike.transform.localScale = new Vector3(0.1f, Random.Range(0.4f, 0.7f), 0.1f);
                SetColor(spike, iceColor);
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

            pool.AddComponent<LavaHazard>();
        }

        // BuildBonePile/BuildIceSpikes' swamp counterpart -- tall reed/rush clusters
        // instead of bones or ice spikes, same crude-primitives-read-instantly approach.
        private void BuildReedCluster(Vector3 pos)
        {
            var reedColor = new Color(0.3f, 0.42f, 0.24f);
            int count = Random.Range(3, 6);
            for (int i = 0; i < count; i++)
            {
                var reed = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                reed.name = "Reed";
                var col = reed.GetComponent<Collider>();
                if (col != null) Destroy(col); // decorative clutter -- shouldn't snag movement
                reed.transform.SetParent(transform);
                Vector3 offset = new Vector3(Random.Range(-0.5f, 0.5f), 0.1f, Random.Range(-0.5f, 0.5f));
                reed.transform.position = pos + offset;
                reed.transform.rotation = Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(0f, 360f), Random.Range(-6f, 6f));
                reed.transform.localScale = new Vector3(0.07f, Random.Range(0.5f, 0.85f), 0.07f);
                SetColor(reed, reedColor);
            }
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
        private void BuildRoomDecor(Vector3 center)
        {
            float inset = 2.2f;
            Vector3[] corners =
            {
                center + new Vector3(roomWidth / 2f - inset, 0, roomDepth / 2f - inset),
                center + new Vector3(-(roomWidth / 2f - inset), 0, roomDepth / 2f - inset),
                center + new Vector3(roomWidth / 2f - inset, 0, -(roomDepth / 2f - inset)),
                center + new Vector3(-(roomWidth / 2f - inset), 0, -(roomDepth / 2f - inset)),
            };

            for (int i = 0; i < corners.Length; i++)
            {
                if (buildPillars) BuildPillar(corners[i]);
                if (buildTorches && i % 2 == 0) BuildTorch(corners[i] + new Vector3(0, 1.1f, 0));
            }
        }

        private void BuildPillar(Vector3 basePos)
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "Pillar";
            pillar.transform.SetParent(transform);
            pillar.transform.position = basePos + new Vector3(0, wallHeight / 2f, 0);
            pillar.transform.localScale = new Vector3(0.7f, wallHeight, 0.7f);
            SetColor(pillar, new Color(wallColor.r + 0.03f, wallColor.g + 0.03f, wallColor.b + 0.03f));
        }

        private void BuildTorch(Vector3 pos)
        {
            var torchGO = new GameObject("Torch");
            torchGO.transform.SetParent(transform);
            torchGO.transform.position = pos;

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

        // Oriented via FromToRotation rather than hand-derived Euler angles -- the ramp's
        // local +X axis is rotated to point from bottomPos to topPos, so the box's ends
        // land exactly on those two world positions regardless of which quadrant the drop
        // is in, with no sign-of-angle guesswork.
        private void BuildRamp(Vector3 topPos, Vector3 bottomPos, float width, float thickness = 0.4f)
        {
            Vector3 dir = topPos - bottomPos;
            float length = dir.magnitude;
            if (length < 0.01f) return;

            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Ramp";
            ramp.transform.SetParent(transform);
            ramp.transform.position = (topPos + bottomPos) / 2f;
            ramp.transform.rotation = Quaternion.FromToRotation(Vector3.right, dir.normalized);
            ramp.transform.localScale = new Vector3(length, thickness, width);
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

        private void BuildCorridor(Vector3 center)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "CorridorFloor";
            floor.transform.SetParent(transform);
            floor.transform.position = center;
            floor.transform.localScale = new Vector3(corridorWidth / 10f, 1f, corridorLength / 10f);
            SetColor(floor, corridorFloorColor);

            BuildWall(center + new Vector3(corridorWidth / 2f, wallHeight / 2f, 0), new Vector3(wallThickness, wallHeight, corridorLength));
            BuildWall(center + new Vector3(-corridorWidth / 2f, wallHeight / 2f, 0), new Vector3(wallThickness, wallHeight, corridorLength));

            if (buildCeiling)
            {
                var ceiling = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ceiling.name = "CorridorCeiling";
                var col = ceiling.GetComponent<Collider>();
                if (col != null) Destroy(col);
                ceiling.transform.SetParent(transform);
                ceiling.transform.position = center + new Vector3(0, wallHeight, 0);
                ceiling.transform.rotation = Quaternion.Euler(180, 0, 0);
                ceiling.transform.localScale = new Vector3(corridorWidth / 10f, 1f, corridorLength / 10f);
                SetColor(ceiling, ceilingColor);
            }

            if (buildTorches) BuildTorch(center + new Vector3(0, 1.1f, 0));
        }

        private void BuildWall(Vector3 worldCenter, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.SetParent(transform);
            wall.transform.position = worldCenter;
            wall.transform.localScale = scale;
            SetColor(wall, wallColor);
        }

        private void SetColor(GameObject go, Color c)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Standard")) { color = c };
            }
        }
    }
}
