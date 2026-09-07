using UnityEngine;
using DungeonCrawler.Classes;
using DungeonCrawler.Core;
using DungeonCrawler.Visuals;

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

        private enum HazardKind { Lava, Ice, Bog, Venom, Curse }

        // Each zone is 60 (X) x 70 (Z) -- four side by side spans the whole overworld
        // roughly X: -90..150, Z: 0..70, well clear of the hub's own footprint
        // (x: -30..30, z: -60..-5). The Snake Pit zone was added after the original three
        // (Wastes/Frostlands/Marshlands, at X -60/0/60) -- rather than re-center all four
        // and risk shifting camp/hazard/roam coordinates that were already tuned for the
        // original three, it just continues the same spacing one zone further east at
        // X=120.
        private const float ZoneHalfWidth = 30f;
        private const float ZoneHalfDepth = 35f;
        private const float ZoneCenterZ = 35f;
        private const float WallHeight = 4f;
        private const float WallThickness = 1f;

        // Shared by both BuildWall's main cube and AddWallTrim's bands so they derive from
        // one base color, same as DungeonLayout's wallColor field.
        private static readonly Color WallColor = new Color(0.2f, 0.2f, 0.2f);

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

        // Dais/pedestal sizing for BuildMonument -- kept as consts since both the geometry
        // and the pedestal placement radius need to agree with each other.
        private const float MonumentDaisRadius = 4f;
        private const float MonumentDaisHeight = 0.6f;
        private const float MonumentPedestalRadius = 3.2f; // just inside the dais edge
        private const float MonumentPedestalHeight = 1.1f; // waist-high

        public Vector3 EntryPoint { get; private set; }
        public Vector3 MonumentPoint { get; private set; }
        public BiomeZone Wastes { get; private set; }
        public BiomeZone Frostlands { get; private set; }
        public BiomeZone Marshlands { get; private set; }
        public BiomeZone SnakePit { get; private set; }
        public BiomeZone Reliquary { get; private set; }

        private void Awake()
        {
            // South-center edge -- closest point to where the player arrives from the hub
            // gate (mirrors DungeonLayout.EntryPoint sitting at the world-origin end nearest
            // the hub).
            EntryPoint = new Vector3(0, 0, 5f);

            Wastes = BuildBiome("The Wastes", -60f, new Color(0.32f, 0.16f, 0.08f), HazardKind.Lava,
                new Color(0.6f, 0.4f, 0.28f), new Color(0.35f, 0.14f, 0.05f));
            Frostlands = BuildBiome("The Frostlands", 0f, new Color(0.72f, 0.82f, 0.9f), HazardKind.Ice,
                new Color(0.85f, 0.9f, 0.95f), new Color(0.65f, 0.78f, 0.9f));
            Marshlands = BuildBiome("The Marshlands", 60f, new Color(0.22f, 0.26f, 0.16f), HazardKind.Bog,
                new Color(0.28f, 0.34f, 0.2f), new Color(0.14f, 0.2f, 0.12f));
            SnakePit = BuildBiome("The Snake Pit", 120f, new Color(0.36f, 0.28f, 0.14f), HazardKind.Venom,
                new Color(0.55f, 0.44f, 0.24f), new Color(0.32f, 0.22f, 0.12f));
            // Continues the established X spacing one zone further east, same reasoning
            // the Snake Pit's own addition already used -- avoids re-centering and
            // shifting every other zone's already-tuned camp/hazard/roam coordinates.
            Reliquary = BuildBiome("The Reliquary", 180f, new Color(0.14f, 0.1f, 0.18f), HazardKind.Curse,
                new Color(0.22f, 0.16f, 0.26f), new Color(0.1f, 0.06f, 0.14f));

            BuildMonument();
            BuildPerimeterWalls();
        }

        private BiomeZone BuildBiome(string label, float centerX, Color groundColor, HazardKind hazard,
            Color terrainColor, Color fogColor)
        {
            Vector3 zoneCenter = new Vector3(centerX, 0, ZoneCenterZ);
            BuildGroundPlane(zoneCenter, groundColor);
            BuildZoneTerrain(zoneCenter, terrainColor);
            BuildZoneAtmosphereTrigger(zoneCenter, fogColor);

            foreach (var offset in HazardOffsets)
            {
                Vector3 hazardPos = zoneCenter + offset;
                BuildHazardPatch(hazardPos, hazard);
                BuildHazardProps(hazardPos, hazard);
            }

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

        // Real elevation per zone -- previously every biome was a flat, featureless plane
        // (only the camp/hazard primitives broke it up), which read as "one shared field
        // recolored four times" rather than four distinct places. Positions are the
        // zone's own corners/edges, well clear of every hazard/camp/roam/guard point
        // (all of which stay within roughly |x|<=25, -20<=z<=28 relative to zoneCenter).
        private void BuildZoneTerrain(Vector3 zoneCenter, Color terrainColor)
        {
            VillageDecor.BuildHill(transform, zoneCenter + new Vector3(0f, 0, 32f), 3.5f, 2.2f, terrainColor);
            VillageDecor.BuildHill(transform, zoneCenter + new Vector3(-27f, 0, 5f), 2.8f, 1.6f, terrainColor);
            VillageDecor.BuildRockCluster(transform, zoneCenter + new Vector3(27f, 0, -25f), 1.1f);
            VillageDecor.BuildRockCluster(transform, zoneCenter + new Vector3(-27f, 0, -30f), 0.9f);
        }

        // A large trigger covering the zone's whole footprint -- tints distant fog to that
        // biome's own color the moment the player steps into it, so each of the four zones
        // actually reads as a different place to be instead of the same flat, fogless field
        // recolored underfoot. Distance is kept generous (well past the zone's own 70-unit
        // depth) so this tints the horizon rather than obscuring nearby combat -- an open-
        // world atmosphere cue, not a dungeon's close, obscuring fog.
        private void BuildZoneAtmosphereTrigger(Vector3 zoneCenter, Color fogColor)
        {
            var go = new GameObject("ZoneAtmosphere");
            go.transform.SetParent(transform);
            go.transform.position = zoneCenter;
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(ZoneHalfWidth * 2f, 20f, ZoneHalfDepth * 2f);
            var trigger = go.AddComponent<ZoneAtmosphereTrigger>();
            trigger.fogColor = fogColor;
        }

        // OnTriggerEnter-only (see BuildZoneAtmosphereTrigger's comment) -- every zone
        // tiles the walkable area edge to edge with no gap, so the player is always inside
        // exactly one zone's trigger; handling only Enter avoids any Exit/Enter ordering
        // ambiguity right at a shared boundary between two abutting triggers.
        private class ZoneAtmosphereTrigger : MonoBehaviour
        {
            public Color fogColor;

            private void OnTriggerEnter(Collider other)
            {
                if (other.GetComponentInParent<PlayerCharacter>() == null) return;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogStartDistance = 45f;
                RenderSettings.fogEndDistance = 150f;
            }
        }

        // A shared plaza between EntryPoint and the three biome zones -- see
        // GameBootstrap.SpawnMonumentReward for the payoff this decorates (RotMG's Oryx's
        // Sanctuary: three runes each light a pedestal, all three lit unlocks a bonus at
        // the shared monument). Sits at Z=15, well short of every zone's own camp (Z=53)
        // and clear of every zone's hazard patches (nearest hazard offset lands 14 units
        // away in X for Frostlands, further for Wastes/Marshlands whose hazards sit under
        // their own off-center X) -- never overlaps a biome's own combat/camp footprint.
        private void BuildMonument()
        {
            MonumentPoint = new Vector3(0, 0, 15f);

            var dais = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dais.name = "MonumentDais";
            dais.transform.SetParent(transform);
            dais.transform.position = MonumentPoint + new Vector3(0, MonumentDaisHeight / 2f, 0);
            // Cylinder primitive default diameter 1 / height 2 at scale 1 -- scale.x/z by
            // (2 * targetRadius), scale.y by (targetHeight / 2).
            dais.transform.localScale = new Vector3(MonumentDaisRadius * 2f, MonumentDaisHeight / 2f, MonumentDaisRadius * 2f);
            SetColor(dais, new Color(0.5f, 0.48f, 0.45f)); // weathered stone

            // A compass ring around the dais, one pedestal per camp -- started as a 3-point
            // triangle, became a 4-point N/E/S/W compass when Snake Pit was added, and now
            // gets a 5th pedestal on the NE diagonal for the Reliquary rather than trying
            // to force a perfect pentagon -- a slightly uneven ring reads fine at this
            // scale, and it avoids re-deriving every existing pedestal's position.
            BuildMonumentPedestal(new Vector3(-MonumentPedestalRadius, 0, 0), new Color(0.85f, 0.35f, 0.1f));  // Wastes
            BuildMonumentPedestal(new Vector3(0, 0, MonumentPedestalRadius), new Color(0.55f, 0.85f, 1f));     // Frostlands
            BuildMonumentPedestal(new Vector3(MonumentPedestalRadius, 0, 0), new Color(0.3f, 0.85f, 0.5f));    // Marshlands
            BuildMonumentPedestal(new Vector3(0, 0, -MonumentPedestalRadius), new Color(0.65f, 0.35f, 0.8f));  // Snake Pit
            BuildMonumentPedestal(new Vector3(MonumentPedestalRadius * 0.72f, 0, MonumentPedestalRadius * 0.72f), new Color(0.55f, 0.85f, 0.5f)); // Reliquary
        }

        // Small waist-high cube -- no collider concerns beyond the Cube primitive's own
        // default (small, off to the side of the open plaza, never blocks the path through).
        private void BuildMonumentPedestal(Vector3 offsetFromCenter, Color tint)
        {
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pedestal.name = "MonumentPedestal";
            pedestal.transform.SetParent(transform);
            pedestal.transform.position = MonumentPoint + offsetFromCenter +
                new Vector3(0, MonumentDaisHeight + MonumentPedestalHeight / 2f, 0);
            pedestal.transform.localScale = new Vector3(0.6f, MonumentPedestalHeight, 0.6f);
            SetColor(pedestal, tint);
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
            StatusEffectType effect = StatusEffectType.None;
            float effectMag = 0f;
            float effectDur = 0f;
            switch (kind)
            {
                case HazardKind.Ice:
                    flat = new Color(0.55f, 0.85f, 1f);
                    glowA = new Color(0.35f, 0.65f, 0.9f);
                    glowB = new Color(0.75f, 0.95f, 1f);
                    effect = StatusEffectType.Slow;
                    effectMag = 0.4f;
                    effectDur = 1.5f;
                    break;
                case HazardKind.Bog:
                    flat = new Color(0.35f, 0.42f, 0.12f);
                    glowA = new Color(0.25f, 0.32f, 0.08f);
                    glowB = new Color(0.55f, 0.58f, 0.2f);
                    effect = StatusEffectType.Blind;
                    effectMag = 1f;
                    effectDur = 1.5f;
                    break;
                case HazardKind.Venom:
                    flat = new Color(0.55f, 0.75f, 0.1f);
                    glowA = new Color(0.4f, 0.6f, 0.05f);
                    glowB = new Color(0.75f, 0.95f, 0.2f);
                    effect = StatusEffectType.Poison;
                    effectMag = 4f;
                    effectDur = 4f;
                    break;
                case HazardKind.Curse:
                    flat = new Color(0.35f, 0.15f, 0.4f);
                    glowA = new Color(0.2f, 0.06f, 0.25f);
                    glowB = new Color(0.55f, 0.85f, 0.5f);
                    effect = StatusEffectType.Curse;
                    effectMag = 0.3f;
                    effectDur = 4f;
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

            var hazard = pool.AddComponent<LavaHazard>();
            if (effect != StatusEffectType.None)
            {
                hazard.appliedEffect = effect;
                hazard.effectMagnitude = effectMag;
                hazard.effectDuration = effectDur;
            }
        }

        // Small biome-flavored clutter beside each hazard patch, same "3-6 scattered
        // decorative primitives" technique DungeonLayout's BuildBonePile/BuildIceSpikes/
        // BuildReedCluster already use. Offset clears the pool's own radius (3.5) so the
        // cluster sits beside the glow rather than on top of it.
        private void BuildHazardProps(Vector3 hazardPos, HazardKind kind)
        {
            Vector3 clusterPos = hazardPos + new Vector3(4.5f, 0, 2f);
            switch (kind)
            {
                case HazardKind.Ice:
                    BuildIceShardCluster(clusterPos);
                    break;
                case HazardKind.Bog:
                    BuildReedCluster(clusterPos);
                    break;
                case HazardKind.Venom:
                    BuildThornbrushCluster(clusterPos);
                    break;
                case HazardKind.Curse:
                    BuildGraveShardCluster(clusterPos);
                    break;
                default:
                    BuildScorchedRockCluster(clusterPos);
                    break;
            }
        }

        // The Reliquary's clutter -- broken headstones and shattered reliquary shards,
        // matching the dungeon's own cursed-crypt palette rather than reusing any of the
        // other four zones' clutter.
        private void BuildGraveShardCluster(Vector3 pos)
        {
            var stoneColor = new Color(0.28f, 0.24f, 0.32f);
            int count = Random.Range(3, 6);
            for (int i = 0; i < count; i++)
            {
                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "GraveShard";
                var col = shard.GetComponent<Collider>();
                if (col != null) Destroy(col); // decorative clutter -- shouldn't snag movement
                shard.transform.SetParent(transform);
                Vector3 offset = new Vector3(Random.Range(-0.6f, 0.6f), 0.15f, Random.Range(-0.6f, 0.6f));
                shard.transform.position = pos + offset;
                shard.transform.rotation = Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(0f, 360f), Random.Range(75f, 105f));
                shard.transform.localScale = new Vector3(Random.Range(0.25f, 0.4f), Random.Range(0.4f, 0.7f), Random.Range(0.08f, 0.15f));
                SetColor(shard, stoneColor);
            }
        }

        // Lava's clutter -- charred rock chunks scattered near the pool, crude irregular
        // cubes rather than DungeonLayout's bone pile (this is an open-world burn scar, not
        // a crypt).
        private void BuildScorchedRockCluster(Vector3 pos)
        {
            var rockColor = new Color(0.13f, 0.11f, 0.1f);
            int count = Random.Range(3, 6);
            for (int i = 0; i < count; i++)
            {
                var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rock.name = "ScorchedRock";
                var col = rock.GetComponent<Collider>();
                if (col != null) Destroy(col); // decorative clutter -- shouldn't snag movement
                rock.transform.SetParent(transform);
                Vector3 offset = new Vector3(Random.Range(-0.6f, 0.6f), 0.1f, Random.Range(-0.6f, 0.6f));
                rock.transform.position = pos + offset;
                rock.transform.rotation = Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(0f, 360f), Random.Range(-20f, 20f));
                rock.transform.localScale = new Vector3(Random.Range(0.25f, 0.45f), Random.Range(0.2f, 0.35f), Random.Range(0.25f, 0.45f));
                SetColor(rock, rockColor);
            }
        }

        // Frostlands' clutter -- same jagged ice-shard cluster as DungeonLayout.BuildIceSpikes.
        private void BuildIceShardCluster(Vector3 pos)
        {
            var iceColor = new Color(0.78f, 0.92f, 1f);
            int count = Random.Range(3, 6);
            for (int i = 0; i < count; i++)
            {
                var spike = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                spike.name = "IceShard";
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

        // Snake Pit's clutter -- dry, angular thornbrush and squat barrel-cactus cones,
        // dusty tan/olive to match the zone's own ground color. Used to just reuse
        // Marshlands' reed cluster, which read as "another wet bog plant" in a dry venom
        // pit -- this is the zone's first visually distinct hazard prop.
        private void BuildThornbrushCluster(Vector3 pos)
        {
            var brushColor = new Color(0.42f, 0.34f, 0.16f);
            var caneColor = new Color(0.34f, 0.4f, 0.18f);
            int count = Random.Range(3, 6);
            for (int i = 0; i < count; i++)
            {
                bool cactusCone = Random.value > 0.5f;
                var piece = GameObject.CreatePrimitive(cactusCone ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                piece.name = cactusCone ? "BarrelCactus" : "Thornbrush";
                var col = piece.GetComponent<Collider>();
                if (col != null) Destroy(col); // decorative clutter -- shouldn't snag movement
                piece.transform.SetParent(transform);
                Vector3 offset = new Vector3(Random.Range(-0.6f, 0.6f), 0.1f, Random.Range(-0.6f, 0.6f));
                piece.transform.position = pos + offset;
                piece.transform.rotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
                piece.transform.localScale = cactusCone
                    ? new Vector3(Random.Range(0.18f, 0.3f), Random.Range(0.25f, 0.45f), Random.Range(0.18f, 0.3f))
                    : new Vector3(Random.Range(0.1f, 0.18f), Random.Range(0.3f, 0.55f), Random.Range(0.1f, 0.18f));
                SetColor(piece, cactusCone ? caneColor : brushColor);
            }
        }

        // Marshlands' clutter -- same tall reed/rush cluster as DungeonLayout.BuildReedCluster.
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

            BuildCampPerimeter(center);
        }

        // A rough palisade ring -- decorative only (colliders destroyed) so it never blocks
        // the miniboss fight or a guard spawn. Ring radius (10.5) is picked to sit clear of
        // GuardOffsets' outermost point (~9.2 from center) with margin to spare.
        private void BuildCampPerimeter(Vector3 center)
        {
            const int stakeCount = 6;
            const float ringRadius = 10.5f;
            for (int i = 0; i < stakeCount; i++)
            {
                float angle = i * (360f / stakeCount) + Random.Range(-10f, 10f);
                Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                BuildStake(center + dir * ringRadius, angle);
            }
        }

        // A thin cylinder tilted outward like a hastily driven stake -- same crude-lean
        // technique BuildTent already uses, just standing rather than lying over.
        private void BuildStake(Vector3 basePos, float outwardAngle)
        {
            var stake = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stake.name = "CampStake";
            var col = stake.GetComponent<Collider>();
            if (col != null) Destroy(col); // decorative -- must not block guard spawns or the miniboss fight
            stake.transform.SetParent(transform);
            float height = Random.Range(1f, 1.5f);
            stake.transform.position = basePos + new Vector3(0, height / 2f, 0);
            stake.transform.rotation = Quaternion.Euler(Random.Range(10f, 18f), outwardAngle, 0f);
            stake.transform.localScale = new Vector3(0.1f, height / 2f, 0.1f);
            SetColor(stake, new Color(0.24f, 0.16f, 0.07f)); // raw split wood, darker/rougher than tents or crates
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
            const float minX = -90f, maxX = 210f, minZ = 0f, maxZ = 70f; // maxX extended for the Reliquary zone at centerX=180
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
            SetColor(wall, WallColor);

            AddWallTrim(worldCenter, scale);
        }

        // Same two-tone baseboard/cap treatment as DungeonLayout.AddWallTrim -- both bands
        // are decorative-only (no collider) so the boundary's collision footprint never
        // changes.
        private void AddWallTrim(Vector3 worldCenter, Vector3 scale)
        {
            float bandHeight = Mathf.Min(0.3f, scale.y * 0.15f);
            Color baseboard = WallColor * 0.55f; baseboard.a = 1f;
            Color cap = Color.Lerp(WallColor, Color.white, 0.25f);
            Vector3 bandScale = new Vector3(scale.x + 0.04f, bandHeight, scale.z + 0.04f);

            BuildTrimBand(worldCenter + new Vector3(0, -scale.y / 2f + bandHeight / 2f, 0), bandScale, baseboard);
            BuildTrimBand(worldCenter + new Vector3(0, scale.y / 2f - bandHeight / 2f, 0), bandScale, cap);
        }

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
            if (renderer != null) renderer.material = new Material(Shader.Find("Standard")) { color = c };
        }
    }
}
