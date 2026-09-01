using UnityEngine;

namespace DungeonCrawler.World
{
    // Replaces the old dungeon-select portal with a single RotMG-style overworld: three
    // biome zones sitting side by side along X, each with its own hazard flavor, roaming
    // trash, and a bandit camp guarded by a miniboss. Killing a camp's miniboss is what
    // unlocks that biome's actual dungeon (see GameBootstrap.EnterOpenWorld) -- this class
    // only builds geometry and hands back spawn points, exactly like DungeonLayout never
    // spawns enemies itself.
    //
    // Occupies the same world-space slot dungeons already use (built from world origin
    // northward) since an open world and a dungeon are never alive at the same time -- both
    // live under GameBootstrap's single reusable `dungeonRoot` GameObject, so entering any
    // of the three camp portals tears this down via the same Destroy(dungeonRoot) call
    // PrepareDungeonRoot already does, no new teardown logic needed.
    //
    // Builds in Awake(), not a separate Build() -- unlike DungeonLayout this needs no
    // external theme parameter to construct, so GameBootstrap can read EntryPoint/Wastes/
    // Frostlands/Marshlands the same frame it calls AddComponent<OpenWorldLayout>().
    public class OpenWorldLayout : MonoBehaviour
    {
        // One biome's worth of data GameBootstrap needs to populate it -- geometry only
        // exposes points, spawning happens entirely in GameBootstrap (see class comment).
        public struct BiomeZone
        {
            public string dungeonLabel;      // e.g. "The Wastes" -- for portal/UI text
            public Vector3 campPortalPoint;  // where the dungeon-entry portal appears after the miniboss dies
            public Vector3 minibossPoint;
            public Vector3[] guardPoints;    // camp guard enemies
            public Vector3[] roamPoints;     // enemies scattered across the open biome
        }

        private enum HazardKind { Lava, Ice, Bog }

        // Each zone is 60 (X) x 70 (Z) -- three side by side spans the whole overworld
        // roughly X: -90..90, Z: 0..70, well clear of the hub's own footprint
        // (x: -30..30, z: -60..-5).
        private const float ZoneHalfWidth = 30f;
        private const float ZoneHalfDepth = 35f;
        private const float ZoneCenterZ = 35f;
        private const float WallHeight = 4f;
        private const float WallThickness = 1f;

        // Relative to each zone's own center -- kept away from the camp region (z offset
        // +18 from center, guards radiating a further ~8) so a hazard patch never overlaps
        // the camp itself.
        private static readonly Vector3[] HazardOffsets =
        {
            new Vector3(-16f, 0, -18f), new Vector3(14f, 0, -20f), new Vector3(-4f, 0, -8f),
        };

        // Relative to each zone's own center -- spread across the zone, clear of the camp
        // region above.
        private static readonly Vector3[] RoamOffsets =
        {
            new Vector3(-23f, 0, -20f), new Vector3(22f, 0, -15f), new Vector3(-18f, 0, 3f),
            new Vector3(18f, 0, 8f), new Vector3(-25f, 0, 28f),
        };

        // Relative to the camp's own center.
        private static readonly Vector3[] GuardOffsets =
        {
            new Vector3(-7f, 0, -6f), new Vector3(7f, 0, -6f), new Vector3(0f, 0, 8f), new Vector3(-6f, 0, 5f),
        };

        public Vector3 EntryPoint { get; private set; }
        public BiomeZone Wastes { get; private set; }
        public BiomeZone Frostlands { get; private set; }
        public BiomeZone Marshlands { get; private set; }

        private void Awake()
        {
            // South-center edge -- closest point to where the player arrives from the hub
            // gate (mirrors DungeonLayout.EntryPoint sitting at the world-origin end nearest
            // the hub).
            EntryPoint = new Vector3(0, 0, 5f);

            Wastes = BuildBiome("The Wastes", -60f, new Color(0.32f, 0.16f, 0.08f), HazardKind.Lava);
            Frostlands = BuildBiome("The Frostlands", 0f, new Color(0.72f, 0.82f, 0.9f), HazardKind.Ice);
            Marshlands = BuildBiome("The Marshlands", 60f, new Color(0.22f, 0.26f, 0.16f), HazardKind.Bog);

            BuildPerimeterWalls();
        }

        private BiomeZone BuildBiome(string label, float centerX, Color groundColor, HazardKind hazard)
        {
            Vector3 zoneCenter = new Vector3(centerX, 0, ZoneCenterZ);
            BuildGroundPlane(zoneCenter, groundColor);

            foreach (var offset in HazardOffsets)
                BuildHazardPatch(zoneCenter + offset, hazard);

            Vector3 campCenter = zoneCenter + new Vector3(0, 0, 18f);
            BuildCamp(campCenter);

            var guardPoints = new Vector3[GuardOffsets.Length];
            for (int i = 0; i < GuardOffsets.Length; i++)
                guardPoints[i] = campCenter + GuardOffsets[i];

            var roamPoints = new Vector3[RoamOffsets.Length];
            for (int i = 0; i < RoamOffsets.Length; i++)
                roamPoints[i] = zoneCenter + RoamOffsets[i];

            return new BiomeZone
            {
                dungeonLabel = label,
                // CampGround's half-extent is exactly 9 (localScale 1.8 on a 10-unit Plane)
                // -- offset past that so the portal doesn't visually straddle the patch edge.
                campPortalPoint = campCenter + new Vector3(12f, 0, 0),
                minibossPoint = campCenter,
                guardPoints = guardPoints,
                roamPoints = roamPoints,
            };
        }

        private void BuildGroundPlane(Vector3 center, Color color)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "BiomeGround";
            floor.transform.SetParent(transform);
            floor.transform.position = center;
            floor.transform.localScale = new Vector3(ZoneHalfWidth * 2f / 10f, 1f, ZoneHalfDepth * 2f / 10f);
            SetColor(floor, color);
        }

        // Same periodic-damage hazard as DungeonLayout's BuildLavaPool/BuildIcePatch/
        // BuildPoisonBog (LavaHazard is generic despite the name), just reskinned per
        // biome and sized up a little for open-terrain scale.
        private void BuildHazardPatch(Vector3 pos, HazardKind kind)
        {
            const float radius = 3.5f;
            Color flat, glowA, glowB;
            switch (kind)
            {
                case HazardKind.Ice:
                    flat = new Color(0.55f, 0.85f, 1f);
                    glowA = new Color(0.35f, 0.65f, 0.9f);
                    glowB = new Color(0.75f, 0.95f, 1f);
                    break;
                case HazardKind.Bog:
                    flat = new Color(0.35f, 0.42f, 0.12f);
                    glowA = new Color(0.25f, 0.32f, 0.08f);
                    glowB = new Color(0.55f, 0.58f, 0.2f);
                    break;
                default:
                    flat = new Color(0.9f, 0.35f, 0.05f);
                    glowA = new Color(0.7f, 0.15f, 0.02f);
                    glowB = new Color(1f, 0.6f, 0.1f);
                    break;
            }

            var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pool.name = "HazardPatch";
            pool.transform.SetParent(transform);
            pool.transform.position = pos + new Vector3(0, 0.03f, 0);
            pool.transform.localScale = new Vector3(radius * 2f, 0.03f, radius * 2f);
            SetColor(pool, flat);

            var glow = pool.AddComponent<PortalGlow>();
            glow.colorA = glowA;
            glow.colorB = glowB;
            glow.speed = 0.8f;

            // The Cylinder primitive ships with its own CapsuleCollider -- reused as the
            // hazard's trigger volume rather than destroying and rebuilding one.
            var col = pool.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            pool.AddComponent<LavaHazard>();
        }

        // A cleared dirt patch with a couple of lean-to tents, a campfire, and a crate or
        // two -- reads as a bandit camp using only primitives, matching the level of visual
        // complexity DungeonLayout.BuildTorch/BuildPillar already use for placeholder decor.
        private void BuildCamp(Vector3 center)
        {
            var patch = GameObject.CreatePrimitive(PrimitiveType.Plane);
            patch.name = "CampGround";
            var patchCol = patch.GetComponent<Collider>();
            if (patchCol != null) Destroy(patchCol); // decorative overlay -- the zone floor beneath already provides collision
            patch.transform.SetParent(transform);
            patch.transform.position = center + new Vector3(0, 0.02f, 0);
            patch.transform.localScale = new Vector3(1.8f, 1f, 1.8f);
            SetColor(patch, new Color(0.36f, 0.26f, 0.16f));

            BuildTent(center + new Vector3(-3f, 0, -2f), 20f);
            BuildTent(center + new Vector3(3f, 0, -1f), -160f);
            BuildTent(center + new Vector3(0f, 0, 3f), 100f);

            BuildCampfire(center + new Vector3(0f, 0, -0.5f));

            BuildCrate(center + new Vector3(4f, 0, 1.5f));
            BuildCrate(center + new Vector3(-4f, 0, 2.5f));
        }

        // A single cube scaled thin and tall, then tilted forward like a leaning tent
        // flap -- crude but instantly reads as a tent among the camp's other primitives.
        // Kept solid (not decorative): a visible tent blocking a path is a fine obstacle,
        // unlike the invisible-collider bug BuildBonePile/BuildIceSpikes/BuildReedCluster
        // already fixed elsewhere by destroying their colliders.
        private void BuildTent(Vector3 basePos, float yRotation)
        {
            var tent = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tent.name = "BanditTent";
            tent.transform.SetParent(transform);
            tent.transform.position = basePos + new Vector3(0, 1f, 0);
            tent.transform.rotation = Quaternion.Euler(35f, yRotation, 0f);
            tent.transform.localScale = new Vector3(1.6f, 2f, 0.9f);
            SetColor(tent, new Color(0.72f, 0.6f, 0.4f)); // weathered canvas-tan
        }

        // Same flame-sphere + point-light technique as DungeonLayout.BuildTorch, just
        // placed low on the ground with no holder -- reads as a campfire, not a wall torch.
        private void BuildCampfire(Vector3 pos)
        {
            var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flame.name = "Campfire";
            var flameCol = flame.GetComponent<Collider>();
            if (flameCol != null) Destroy(flameCol); // decorative -- shouldn't snag movement
            flame.transform.SetParent(transform);
            flame.transform.position = pos + new Vector3(0, 0.15f, 0);
            flame.transform.localScale = Vector3.one * 0.35f;
            SetColor(flame, new Color(1f, 0.55f, 0.15f));

            var light = flame.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.25f);
            light.range = 7f;
            light.intensity = 1.6f;
            flame.AddComponent<TorchFlicker>();
        }

        private void BuildCrate(Vector3 pos)
        {
            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "SupplyCrate";
            crate.transform.SetParent(transform);
            crate.transform.position = pos + new Vector3(0, 0.4f, 0);
            crate.transform.localScale = Vector3.one * 0.8f;
            SetColor(crate, new Color(0.4f, 0.28f, 0.15f));
        }

        // Simple boundary cubes along all 4 sides of the whole overworld footprint (all 3
        // zones together) -- a FallRecovery safety net already exists project-wide as
        // backup, so this only needs to keep the player from casually walking into the
        // void, not be airtight.
        private void BuildPerimeterWalls()
        {
            const float minX = -90f, maxX = 90f, minZ = 0f, maxZ = 70f;
            float width = maxX - minX;
            float depth = maxZ - minZ;
            float centerX = (minX + maxX) / 2f;
            float centerZ = (minZ + maxZ) / 2f;

            BuildWall(new Vector3(centerX, WallHeight / 2f, minZ), new Vector3(width, WallHeight, WallThickness));
            BuildWall(new Vector3(centerX, WallHeight / 2f, maxZ), new Vector3(width, WallHeight, WallThickness));
            BuildWall(new Vector3(minX, WallHeight / 2f, centerZ), new Vector3(WallThickness, WallHeight, depth));
            BuildWall(new Vector3(maxX, WallHeight / 2f, centerZ), new Vector3(WallThickness, WallHeight, depth));
        }

        private void BuildWall(Vector3 worldCenter, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "WorldBoundary";
            wall.transform.SetParent(transform);
            wall.transform.position = worldCenter;
            wall.transform.localScale = scale;
            SetColor(wall, new Color(0.2f, 0.2f, 0.2f));
        }

        private void SetColor(GameObject go, Color c)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.material = new Material(Shader.Find("Standard")) { color = c };
        }
    }
}
