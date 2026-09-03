using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.World
{
    // Safe starting zone, built the same way DungeonLayout builds the abyss (runtime
    // primitives, flat colors -- see that file's header for why). Sits well south of the
    // dungeon's own world-space footprint (DungeonLayout positions everything from world
    // origin northward, starting at its own EntryPoint = Vector3.zero) so the two never
    // overlap -- no scene switching needed, just two disjoint patches of the same world.
    //
    // Laid out as a walled castle courtyard rather than one open circle: a central market
    // square (fountain, vendor row, training yard, the dungeon gate) framed by a
    // crenellated stone wall with corner towers, plus two side rooms reached through
    // archway gaps in that wall -- an enclosed tavern housing a gambling table to the west,
    // and an open-air fairground with a claw machine to the east. GameBootstrap spawns the
    // player here first; walking up to the gate and pressing E is what actually builds the
    // dungeon and teleports the player into it (see GameBootstrap.EnterDungeon). Builds in
    // Awake() so GameBootstrap can read EntryPoint/GateInteractable/etc the same frame it
    // calls AddComponent<HubLayout>().
    public class HubLayout : MonoBehaviour
    {
        private static readonly Vector3 Center = new Vector3(0, 0, -40f);

        private const float SquareHalf = 20f;      // market square half-extent (x and z)
        private const float WallHeight = 3.4f;
        private const float WallThickness = 0.6f;
        private const float GateGapHalf = 3.5f;     // north-wall opening for the dungeon gate
        private const float WingGapHalf = 5f;       // east/west-wall openings into the side rooms
        private const float WingHalfWidth = 10f;    // tavern/fairground room half-width (x)
        private const float WingHalfDepth = 8f;     // tavern/fairground room half-depth (z)

        private static readonly Color WallStoneColor = new Color(0.44f, 0.41f, 0.37f);
        private static readonly Color MerlonColor = new Color(0.38f, 0.35f, 0.32f);

        public Vector3 EntryPoint { get; private set; }
        public Interactable GateInteractable { get; private set; }
        public Interactable GambleInteractable { get; private set; }
        public ClawMachineNPC ClawMachine { get; private set; }

        private void Awake()
        {
            EntryPoint = Center + new Vector3(0, 0, -14f);

            BuildSquareFloor();
            BuildCastleWalls();
            BuildWell(Center);
            BuildVendorStall(Center + new Vector3(-11f, 0, 9f), "Alchemist",
                "Potions for every stat -- a small boost per bottle, five to a cap.", new Color(0.4f, 0.7f, 0.5f));
            BuildVendorStall(Center + new Vector3(11f, 0, 9f), "Blacksmith",
                "Weapons and armor pulled from the vault. Gear up before you head down.", new Color(0.6f, 0.5f, 0.4f));
            BuildVendorStall(Center + new Vector3(0f, 0, 9f), "Curiosities",
                "Rare finds, priced accordingly. Not for the faint of coinpurse.", new Color(0.55f, 0.35f, 0.7f));
            BuildTrainingArea(Center + new Vector3(-13f, 0, -11f));
            BuildDungeonGate(Center + new Vector3(0, 0, 18f));
            BuildTavernWing();
            BuildFairgroundWing();
        }

        private void BuildSquareFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "HubFloor";
            floor.transform.SetParent(transform);
            floor.transform.position = Center;
            floor.transform.localScale = new Vector3(SquareHalf * 2f / 10f, 1f, SquareHalf * 2f / 10f);
            SetColor(floor, new Color(0.32f, 0.28f, 0.22f));
        }

        // A proper castle wall -- crenellated stone runs with a corner tower at each
        // corner, instead of the plain ring of posts this used to be. Archway gaps lead
        // into the tavern (west) and fairground (east); a narrower gap up north frames the
        // dungeon gate.
        private void BuildCastleWalls()
        {
            float h = SquareHalf;

            BuildWallRunX(-h, -h, h); // south -- solid; the player already spawns inside it

            BuildWallRunX(h, -h, -GateGapHalf);
            BuildWallRunX(h, GateGapHalf, h);

            BuildWallRunZ(-h, -h, -WingGapHalf);
            BuildWallRunZ(-h, WingGapHalf, h);

            BuildWallRunZ(h, -h, -WingGapHalf);
            BuildWallRunZ(h, WingGapHalf, h);

            BuildCornerTower(new Vector3(-h, 0, -h));
            BuildCornerTower(new Vector3(h, 0, -h));
            BuildCornerTower(new Vector3(h, 0, h));
            BuildCornerTower(new Vector3(-h, 0, h));

            BuildArchPillar(new Vector3(-h, 0, -WingGapHalf));
            BuildArchPillar(new Vector3(-h, 0, WingGapHalf));
            BuildArchPillar(new Vector3(h, 0, -WingGapHalf));
            BuildArchPillar(new Vector3(h, 0, WingGapHalf));
        }

        // A wall run parallel to X at world-relative z, from xFrom to xTo (both relative to
        // Center). Skipped entirely if the range collapses to nothing -- lets callers pass
        // a gap's two flanking ranges without special-casing a zero-length side.
        private void BuildWallRunX(float z, float xFrom, float xTo)
        {
            float length = xTo - xFrom;
            if (length <= 0.01f) return;
            Vector3 basePos = Center + new Vector3((xFrom + xTo) / 2f, 0, z);
            BuildWallCube(basePos + new Vector3(0, WallHeight / 2f, 0), new Vector3(length, WallHeight, WallThickness));
            BuildCrenellations(basePos, length, alongX: true);
        }

        private void BuildWallRunZ(float x, float zFrom, float zTo)
        {
            float length = zTo - zFrom;
            if (length <= 0.01f) return;
            Vector3 basePos = Center + new Vector3(x, 0, (zFrom + zTo) / 2f);
            BuildWallCube(basePos + new Vector3(0, WallHeight / 2f, 0), new Vector3(WallThickness, WallHeight, length));
            BuildCrenellations(basePos, length, alongX: false);
        }

        private void BuildWallCube(Vector3 worldCenter, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "CastleWall";
            wall.transform.SetParent(transform);
            wall.transform.position = worldCenter;
            wall.transform.localScale = scale;
            SetColor(wall, WallStoneColor);

            AddWallTrim(worldCenter, scale);
        }

        private void AddWallTrim(Vector3 worldCenter, Vector3 scale)
        {
            float bandHeight = Mathf.Min(0.3f, scale.y * 0.15f);
            Color baseboard = WallStoneColor * 0.55f; baseboard.a = 1f;
            Color cap = Color.Lerp(WallStoneColor, Color.white, 0.25f);
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

        // Merlons narrower than their spacing slot -- the gaps between them fall out
        // naturally rather than needing a separate "skip every other" pass.
        private void BuildCrenellations(Vector3 basePos, float length, bool alongX)
        {
            const float spacing = 2.2f;
            const float merlonWidth = 1.1f;
            const float merlonHeight = 0.7f;
            int count = Mathf.Max(1, Mathf.FloorToInt(length / spacing));
            float start = -length / 2f + spacing / 2f;

            for (int i = 0; i < count; i++)
            {
                float offset = start + i * spacing;
                Vector3 pos = basePos + (alongX
                    ? new Vector3(offset, WallHeight + merlonHeight / 2f, 0)
                    : new Vector3(0, WallHeight + merlonHeight / 2f, offset));

                var merlon = GameObject.CreatePrimitive(PrimitiveType.Cube);
                merlon.name = "Merlon";
                var col = merlon.GetComponent<Collider>();
                if (col != null) Destroy(col); // decorative -- the wall body below already blocks movement
                merlon.transform.SetParent(transform);
                merlon.transform.position = pos;
                merlon.transform.localScale = alongX
                    ? new Vector3(merlonWidth, merlonHeight, WallThickness + 0.1f)
                    : new Vector3(WallThickness + 0.1f, merlonHeight, merlonWidth);
                SetColor(merlon, MerlonColor);
            }
        }

        private void BuildCornerTower(Vector3 relPos)
        {
            Vector3 basePos = Center + relPos;
            float towerHeight = WallHeight + 3f;
            float radius = 1.7f;

            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "CornerTower";
            body.transform.SetParent(transform);
            body.transform.position = basePos + new Vector3(0, towerHeight / 2f, 0);
            body.transform.localScale = new Vector3(radius * 2f, towerHeight / 2f, radius * 2f);
            SetColor(body, WallStoneColor);

            // A squashed sphere instead of a true cone (Unity ships no cone primitive) --
            // reads as a tower roof at a glance, which is all this needs to do.
            var roof = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            roof.name = "TowerRoof";
            var roofCol = roof.GetComponent<Collider>();
            if (roofCol != null) Destroy(roofCol);
            roof.transform.SetParent(transform);
            roof.transform.position = basePos + new Vector3(0, towerHeight + 0.3f, 0);
            roof.transform.localScale = new Vector3(radius * 2.3f, 1.6f, radius * 2.3f);
            SetColor(roof, new Color(0.35f, 0.12f, 0.12f));

            BuildLantern(basePos + new Vector3(0, towerHeight + 1.2f, 0));
        }

        private void BuildArchPillar(Vector3 relPos)
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "ArchPillar";
            pillar.transform.SetParent(transform);
            pillar.transform.position = Center + relPos + new Vector3(0, WallHeight / 2f, 0);
            pillar.transform.localScale = new Vector3(0.9f, WallHeight, 0.9f);
            SetColor(pillar, new Color(WallStoneColor.r * 0.85f, WallStoneColor.g * 0.85f, WallStoneColor.b * 0.85f));
        }

        private void BuildLantern(Vector3 pos, Color? lightColor = null)
        {
            var holder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            holder.name = "LanternHolder";
            var holderCol = holder.GetComponent<Collider>();
            if (holderCol != null) Destroy(holderCol);
            holder.transform.SetParent(transform);
            holder.transform.position = pos;
            holder.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            SetColor(holder, new Color(0.15f, 0.12f, 0.08f));

            var light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = lightColor ?? new Color(1f, 0.78f, 0.45f);
            light.range = 10f;
            light.intensity = 1.3f;
            holder.AddComponent<TorchFlicker>().flickerAmount = 0.2f; // gentler than dungeon torches -- hub should read as calm
        }

        private void BuildWell(Vector3 center)
        {
            var basin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            basin.name = "WellBasin";
            basin.transform.SetParent(transform);
            basin.transform.position = center + new Vector3(0, 0.35f, 0);
            basin.transform.localScale = new Vector3(3.2f, 0.35f, 3.2f);
            SetColor(basin, new Color(0.4f, 0.4f, 0.42f));

            var water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            water.name = "WellWater";
            var waterCol = water.GetComponent<Collider>();
            if (waterCol != null) Destroy(waterCol);
            water.transform.SetParent(transform);
            water.transform.position = center + new Vector3(0, 0.55f, 0);
            water.transform.localScale = new Vector3(2.8f, 0.05f, 2.8f);
            SetColor(water, new Color(0.25f, 0.55f, 0.75f));

            // A solid short cylinder standing on the basin's edge -- reads as a knee-high
            // stone rim without needing an actual hollow-ring mesh. Kept low (top just
            // above the water's own surface at y=0.6) rather than the taller block a
            // "rim" first suggests: since a Cylinder primitive has a solid top cap, not an
            // open ring, anything taller here would sit as an opaque lid directly over the
            // water and hide it completely from a standing player's eye height, defeating
            // the still-water read this well is built around. Keeps its collider (the
            // basin/water below don't have one) so the player can bump against the well
            // itself, same as BuildCornerTower's body vs. its collider-less roof.
            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "WellRim";
            rim.transform.SetParent(transform);
            rim.transform.position = center + new Vector3(0, 0.75f, 0);
            rim.transform.localScale = new Vector3(3.4f, 0.25f, 3.4f);
            SetColor(rim, WallStoneColor);

            // Two posts angled inward at the top (same paired-tilt trick BuildCanopy uses
            // for its peaked awning) so the beam they carry reads as resting on an A-frame
            // rather than floating between two straight sticks.
            BuildWellPost(center + new Vector3(-1.6f, 0, 0), 10f);
            BuildWellPost(center + new Vector3(1.6f, 0, 0), -10f);

            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "WellBeam";
            var beamCol = beam.GetComponent<Collider>();
            if (beamCol != null) Destroy(beamCol);
            beam.transform.SetParent(transform);
            beam.transform.position = center + new Vector3(0, 2.5f, 0);
            beam.transform.localScale = new Vector3(3.4f, 0.2f, 0.2f);
            SetColor(beam, new Color(0.25f, 0.18f, 0.1f));

            // Rope + bucket hanging toward the water -- the one detail that makes this
            // unmistakably a well rather than a fountain.
            var rope = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rope.name = "WellRope";
            var ropeCol = rope.GetComponent<Collider>();
            if (ropeCol != null) Destroy(ropeCol);
            rope.transform.SetParent(transform);
            rope.transform.position = center + new Vector3(0, 1.6f, 0);
            rope.transform.localScale = new Vector3(0.06f, 1.8f, 0.06f);
            SetColor(rope, new Color(0.5f, 0.4f, 0.25f));

            var bucket = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bucket.name = "WellBucket";
            var bucketCol = bucket.GetComponent<Collider>();
            if (bucketCol != null) Destroy(bucketCol);
            bucket.transform.SetParent(transform);
            bucket.transform.position = center + new Vector3(0, 0.75f, 0);
            bucket.transform.localScale = new Vector3(0.4f, 0.25f, 0.4f);
            SetColor(bucket, new Color(0.3f, 0.24f, 0.16f));
        }

        private void BuildWellPost(Vector3 basePos, float tiltDegrees)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "WellPost";
            var col = post.GetComponent<Collider>();
            if (col != null) Destroy(col);
            post.transform.SetParent(transform);
            post.transform.position = basePos + new Vector3(0, 1.8f, 0);
            post.transform.localRotation = Quaternion.Euler(0, 0, tiltDegrees);
            post.transform.localScale = new Vector3(0.15f, 1.3f, 0.15f);
            SetColor(post, new Color(0.25f, 0.18f, 0.1f));
        }

        // Builds the stall's geometry and an empty-stock VendorNPC + Interactable.
        // GameBootstrap fills in real stock once it can resolve ItemData references (see
        // GameBootstrap.WireVendors) -- HubLayout only owns what it can build with zero
        // outside data, matching how DungeonLayout owns geometry while GameBootstrap owns
        // what actually spawns inside it.
        private void BuildVendorStall(Vector3 pos, string vendorName, string flavor, Color npcColor)
        {
            var stallRoot = new GameObject(vendorName + "Stall");
            stallRoot.transform.SetParent(transform);
            stallRoot.transform.position = pos;

            // Poles + a peaked cloth canopy -- turns a bare counter-and-NPC into something
            // that actually reads as a market stall.
            Color canopyColor = new Color(Mathf.Min(1f, npcColor.r + 0.15f), Mathf.Min(1f, npcColor.g + 0.15f), Mathf.Min(1f, npcColor.b + 0.15f));
            BuildStallPole(stallRoot.transform, new Vector3(-1.5f, 0, 1.6f));
            BuildStallPole(stallRoot.transform, new Vector3(1.5f, 0, 1.6f));
            BuildStallPole(stallRoot.transform, new Vector3(-1.5f, 0, -0.6f));
            BuildStallPole(stallRoot.transform, new Vector3(1.5f, 0, -0.6f));
            BuildCanopy(stallRoot.transform, new Vector3(0, 2.6f, 0.5f), canopyColor);

            var counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
            counter.name = "Counter";
            counter.transform.SetParent(stallRoot.transform);
            counter.transform.localPosition = new Vector3(0, 0.5f, 0.7f);
            counter.transform.localScale = new Vector3(2.4f, 1f, 0.7f);
            SetColor(counter, new Color(0.32f, 0.22f, 0.14f));

            // Same Humanoid archetype every humanoid enemy (and the player, see
            // PlayerCharacter.BuildVisual) already uses -- was the last bare capsule left
            // in the hub. A CapsuleCollider is added back manually since ProceduralMonster
            // parts always destroy their own (decorative-only there, since an enemy's
            // CharacterController is what actually collides) -- this NPC has no such
            // stand-in, and losing its solid body would let the player walk straight
            // through it.
            var npcBuilt = ProceduralMonster.Humanoid(stallRoot.transform, new ProceduralMonster.HumanoidSpec
            {
                bodyColor = npcColor,
                accentColor = Color.Lerp(npcColor, Color.white, 0.5f),
                scale = 1f, horns = false, weapon = false, hunched = false
            });
            npcBuilt.root.name = "NPC";
            npcBuilt.root.localPosition = new Vector3(0, 0, -0.2f);
            var npcCol = npcBuilt.root.gameObject.AddComponent<CapsuleCollider>();
            npcCol.height = 1.9f;
            npcCol.radius = 0.35f;
            npcCol.center = new Vector3(0, 0.95f, 0);

            BuildLantern(pos + new Vector3(-1.6f, WallHeight - 0.4f, -0.6f));

            var triggerGO = new GameObject("VendorTrigger");
            triggerGO.transform.SetParent(stallRoot.transform);
            triggerGO.transform.localPosition = new Vector3(0, 1f, 1.6f);
            var col = triggerGO.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(2.6f, 2.2f, 1.8f);

            var vendor = triggerGO.AddComponent<VendorNPC>();
            vendor.vendorName = vendorName;
            vendor.flavorText = flavor;

            var interactable = triggerGO.AddComponent<Interactable>();
            interactable.prompt = $"{vendorName} -- Shop (E)";
        }

        private void BuildStallPole(Transform parent, Vector3 localPos)
        {
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "StallPole";
            pole.transform.SetParent(parent);
            pole.transform.localPosition = localPos + new Vector3(0, 1.3f, 0);
            pole.transform.localScale = new Vector3(0.12f, 1.3f, 0.12f);
            SetColor(pole, new Color(0.25f, 0.18f, 0.1f));
        }

        // Two cubes tilted toward each other -- a crude but instantly-readable peaked
        // awning over the stall.
        private void BuildCanopy(Transform parent, Vector3 localPos, Color color)
        {
            var left = GameObject.CreatePrimitive(PrimitiveType.Cube);
            left.name = "CanopyLeft";
            var leftCol = left.GetComponent<Collider>();
            if (leftCol != null) Destroy(leftCol);
            left.transform.SetParent(parent);
            left.transform.localPosition = localPos + new Vector3(-0.9f, 0, 0);
            left.transform.localRotation = Quaternion.Euler(0, 0, 22f);
            left.transform.localScale = new Vector3(2.2f, 0.08f, 2.4f);
            SetColor(left, color);

            var right = GameObject.CreatePrimitive(PrimitiveType.Cube);
            right.name = "CanopyRight";
            var rightCol = right.GetComponent<Collider>();
            if (rightCol != null) Destroy(rightCol);
            right.transform.SetParent(parent);
            right.transform.localPosition = localPos + new Vector3(0.9f, 0, 0);
            right.transform.localRotation = Quaternion.Euler(0, 0, -22f);
            right.transform.localScale = new Vector3(2.2f, 0.08f, 2.4f);
            SetColor(right, color);
        }

        private void BuildTrainingArea(Vector3 pos)
        {
            var signPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            signPost.name = "TrainingSign";
            signPost.transform.SetParent(transform);
            signPost.transform.position = pos + new Vector3(0, 1.4f, -2.2f);
            signPost.transform.localScale = new Vector3(1.6f, 0.8f, 0.15f);
            SetColor(signPost, new Color(0.3f, 0.24f, 0.15f));

            SpawnDummy(pos + new Vector3(-1.6f, 0, 1f));
            SpawnDummy(pos + new Vector3(1.6f, 0, 1f));
        }

        private void SpawnDummy(Vector3 pos)
        {
            var go = new GameObject("TrainingDummy");
            go.transform.SetParent(transform);
            go.transform.position = pos;
            go.AddComponent<Health>();
            go.AddComponent<StatusEffectController>();
            go.AddComponent<TrainingDummy>();
        }

        private void BuildDungeonGate(Vector3 pos)
        {
            BuildGatePillar(pos + new Vector3(-2.2f, 0, 0));
            BuildGatePillar(pos + new Vector3(2.2f, 0, 0));

            var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lintel.name = "GateLintel";
            lintel.transform.SetParent(transform);
            lintel.transform.position = pos + new Vector3(0, WallHeight + 0.6f, 0);
            lintel.transform.localScale = new Vector3(5.2f, 0.7f, 0.7f);
            SetColor(lintel, new Color(0.2f, 0.05f, 0.08f));

            // A thin glowing slab standing in the gate opening -- simpler and less
            // rotation-error-prone than a rotated Plane for a vertical "portal surface."
            var portal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            portal.name = "GatePortal";
            var portalCol = portal.GetComponent<Collider>();
            if (portalCol != null) Destroy(portalCol);
            portal.transform.SetParent(transform);
            portal.transform.position = pos + new Vector3(0, WallHeight / 2f + 0.15f, 0);
            portal.transform.localScale = new Vector3(3.6f, WallHeight - 0.3f, 0.15f);
            SetColor(portal, new Color(0.6f, 0.15f, 0.75f));
            portal.AddComponent<PortalGlow>();

            var triggerGO = new GameObject("GateTrigger");
            triggerGO.transform.SetParent(transform);
            triggerGO.transform.position = pos + new Vector3(0, 1f, -1.2f);
            var col = triggerGO.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(4.5f, 2.4f, 1.6f);

            GateInteractable = triggerGO.AddComponent<Interactable>();
            GateInteractable.prompt = "Enter the Dungeon (E)";
        }

        private void BuildGatePillar(Vector3 basePos)
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "GatePillar";
            pillar.transform.SetParent(transform);
            pillar.transform.position = basePos + new Vector3(0, WallHeight / 2f, 0);
            pillar.transform.localScale = new Vector3(0.8f, WallHeight, 0.8f);
            SetColor(pillar, new Color(0.18f, 0.15f, 0.15f));
        }

        // Enclosed room reached through the west archway -- real walls plus a flat ceiling
        // (unlike the fairground, this is meant to read as an interior). Houses the
        // gambling table; GameBootstrap wires GambleInteractable to open GambleUI once the
        // player's wallet exists.
        private void BuildTavernWing()
        {
            float centerX = -(SquareHalf + WingHalfWidth);
            Vector3 wingCenter = Center + new Vector3(centerX, 0, 0);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "TavernFloor";
            floor.transform.SetParent(transform);
            floor.transform.position = wingCenter;
            floor.transform.localScale = new Vector3(WingHalfWidth * 2f / 10f, 1f, WingHalfDepth * 2f / 10f);
            SetColor(floor, new Color(0.32f, 0.2f, 0.12f));

            // North, south, and the outer (west) wall; the east side is the square's own
            // west wall, already gapped for this doorway (see BuildCastleWalls).
            BuildWallRunX(WingHalfDepth, centerX - WingHalfWidth, centerX + WingHalfWidth);
            BuildWallRunX(-WingHalfDepth, centerX - WingHalfWidth, centerX + WingHalfWidth);
            BuildWallRunZ(centerX - WingHalfWidth, -WingHalfDepth, WingHalfDepth);

            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ceiling.name = "TavernRoof";
            var ceilCol = ceiling.GetComponent<Collider>();
            if (ceilCol != null) Destroy(ceilCol);
            ceiling.transform.SetParent(transform);
            ceiling.transform.position = wingCenter + new Vector3(0, WallHeight, 0);
            ceiling.transform.rotation = Quaternion.Euler(180, 0, 0);
            ceiling.transform.localScale = new Vector3(WingHalfWidth * 2f / 10f, 1f, WingHalfDepth * 2f / 10f);
            SetColor(ceiling, new Color(0.16f, 0.1f, 0.07f));

            BuildLantern(wingCenter + new Vector3(0, WallHeight - 0.6f, 0));
            BuildLantern(wingCenter + new Vector3(WingHalfWidth - 0.5f, WallHeight - 0.6f, WingHalfDepth - 1.5f));
            BuildLantern(wingCenter + new Vector3(WingHalfWidth - 0.5f, WallHeight - 0.6f, -(WingHalfDepth - 1.5f)));

            Color gamblerColor = new Color(0.5f, 0.15f, 0.15f);
            var gamblerBuilt = ProceduralMonster.Humanoid(transform, new ProceduralMonster.HumanoidSpec
            {
                bodyColor = gamblerColor,
                accentColor = Color.Lerp(gamblerColor, Color.white, 0.5f),
                scale = 1f, horns = false, weapon = false, hunched = false
            });
            gamblerBuilt.root.name = "Gambler";
            gamblerBuilt.root.position = wingCenter + new Vector3(-6f, 0, 0);
            var gamblerCol = gamblerBuilt.root.gameObject.AddComponent<CapsuleCollider>();
            gamblerCol.height = 1.9f;
            gamblerCol.radius = 0.35f;
            gamblerCol.center = new Vector3(0, 0.95f, 0);

            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "GambleTable";
            table.transform.SetParent(transform);
            table.transform.position = wingCenter + new Vector3(-3f, 0.45f, 0);
            table.transform.localScale = new Vector3(1.8f, 0.9f, 1.8f);
            SetColor(table, new Color(0.2f, 0.35f, 0.22f));

            BuildDie(wingCenter + new Vector3(-3.3f, 0.95f, 0.2f));
            BuildDie(wingCenter + new Vector3(-2.7f, 0.95f, -0.15f));

            var triggerGO = new GameObject("GambleTrigger");
            triggerGO.transform.SetParent(transform);
            triggerGO.transform.position = wingCenter + new Vector3(-1f, 1f, 0);
            var col = triggerGO.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(3f, 2.2f, 3f);

            GambleInteractable = triggerGO.AddComponent<Interactable>();
            GambleInteractable.prompt = "Try your luck (E)";
        }

        private void BuildDie(Vector3 pos)
        {
            var die = GameObject.CreatePrimitive(PrimitiveType.Cube);
            die.name = "Die";
            var col = die.GetComponent<Collider>();
            if (col != null) Destroy(col);
            die.transform.SetParent(transform);
            die.transform.position = pos;
            die.transform.rotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
            die.transform.localScale = Vector3.one * 0.22f;
            SetColor(die, Color.white);
        }

        // Open-air, undecorated by walls (unlike the tavern) -- a lightly fenced-off patch
        // of ground marked by its own floor color and festive bunting instead of a
        // fortified room, matching a fairground's outdoor-carnival feel. Houses the claw
        // machine; GameBootstrap fills in ClawMachine.prizePool and wires its Interactable.
        private void BuildFairgroundWing()
        {
            float centerX = SquareHalf + WingHalfWidth;
            Vector3 wingCenter = Center + new Vector3(centerX, 0, 0);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "FairgroundFloor";
            floor.transform.SetParent(transform);
            floor.transform.position = wingCenter;
            floor.transform.localScale = new Vector3(WingHalfWidth * 2f / 10f, 1f, WingHalfDepth * 2f / 10f);
            SetColor(floor, new Color(0.55f, 0.42f, 0.22f));

            Color[] flagColors =
            {
                new Color(0.9f, 0.2f, 0.3f), new Color(0.2f, 0.6f, 0.9f),
                new Color(0.95f, 0.8f, 0.2f), new Color(0.6f, 0.3f, 0.85f)
            };
            Vector3[] poleOffsets =
            {
                new Vector3(-7f, 0, 6f), new Vector3(7f, 0, 6f),
                new Vector3(-7f, 0, -6f), new Vector3(7f, 0, -6f)
            };
            for (int i = 0; i < poleOffsets.Length; i++)
                BuildBuntingPole(wingCenter + poleOffsets[i], flagColors[i]);

            BuildClawMachine(wingCenter + new Vector3(2f, 0, 0));
        }

        private void BuildBuntingPole(Vector3 pos, Color flagColor)
        {
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "BuntingPole";
            pole.transform.SetParent(transform);
            pole.transform.position = pos + new Vector3(0, 1.6f, 0);
            pole.transform.localScale = new Vector3(0.1f, 1.6f, 0.1f);
            SetColor(pole, new Color(0.3f, 0.24f, 0.16f));

            var flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flag.name = "Flag";
            var flagCol = flag.GetComponent<Collider>();
            if (flagCol != null) Destroy(flagCol);
            flag.transform.SetParent(transform);
            flag.transform.position = pos + new Vector3(0.35f, 3f, 0);
            flag.transform.localRotation = Quaternion.Euler(0, 0, 20f);
            flag.transform.localScale = new Vector3(0.7f, 0.5f, 0.05f);
            SetColor(flag, flagColor);

            BuildLantern(pos + new Vector3(0, 3.3f, 0), flagColor);
        }

        private void BuildClawMachine(Vector3 pos)
        {
            var basePad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            basePad.name = "ClawMachineBase";
            basePad.transform.SetParent(transform);
            basePad.transform.position = pos + new Vector3(0, 0.4f, 0);
            basePad.transform.localScale = new Vector3(1.6f, 0.8f, 1.6f);
            SetColor(basePad, new Color(0.85f, 0.2f, 0.35f));

            // A translucent "glass" cabinet -- Standard shader set to alpha-blend at
            // runtime (its default Inspector-driven Transparent mode has no effect unless
            // the blend state/keywords/queue are also set directly, so all four are).
            var cabinet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabinet.name = "ClawMachineCabinet";
            cabinet.transform.SetParent(transform);
            cabinet.transform.position = pos + new Vector3(0, 1.6f, 0);
            cabinet.transform.localScale = new Vector3(1.5f, 1.6f, 1.5f);
            var cabMat = new Material(Shader.Find("Standard")) { color = new Color(0.6f, 0.85f, 0.95f, 0.35f) };
            cabMat.SetFloat("_Mode", 3);
            cabMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            cabMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            cabMat.SetInt("_ZWrite", 0);
            cabMat.DisableKeyword("_ALPHATEST_ON");
            cabMat.EnableKeyword("_ALPHABLEND_ON");
            cabMat.renderQueue = 3000;
            var cabRenderer = cabinet.GetComponent<Renderer>();
            if (cabRenderer != null) cabRenderer.material = cabMat;

            Color[] prizeColors =
            {
                new Color(0.9f, 0.3f, 0.3f), new Color(0.3f, 0.7f, 0.9f),
                new Color(0.95f, 0.85f, 0.3f), new Color(0.5f, 0.85f, 0.4f)
            };
            for (int i = 0; i < prizeColors.Length; i++)
            {
                var prize = GameObject.CreatePrimitive(i % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere);
                prize.name = "PrizeDecor";
                var pCol = prize.GetComponent<Collider>();
                if (pCol != null) Destroy(pCol);
                prize.transform.SetParent(transform);
                float angle = i / (float)prizeColors.Length * Mathf.PI * 2f;
                prize.transform.position = pos + new Vector3(Mathf.Sin(angle) * 0.4f, 1.1f, Mathf.Cos(angle) * 0.4f);
                prize.transform.localScale = Vector3.one * 0.28f;
                SetColor(prize, prizeColors[i]);
            }

            var claw = GameObject.CreatePrimitive(PrimitiveType.Cube);
            claw.name = "ClawMachineClaw";
            var clawCol = claw.GetComponent<Collider>();
            if (clawCol != null) Destroy(clawCol);
            claw.transform.SetParent(transform);
            claw.transform.position = pos + new Vector3(0, 2.5f, 0);
            claw.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            SetColor(claw, new Color(0.25f, 0.25f, 0.28f));

            var triggerGO = new GameObject("ClawMachineTrigger");
            triggerGO.transform.SetParent(transform);
            triggerGO.transform.position = pos + new Vector3(0, 1f, 1.4f);
            var col = triggerGO.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(2.2f, 2.2f, 2f);

            ClawMachine = triggerGO.AddComponent<ClawMachineNPC>();

            var interactable = triggerGO.AddComponent<Interactable>();
            interactable.prompt = "Claw Machine (E)";
        }

        private void SetColor(GameObject go, Color c)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.material = new Material(Shader.Find("Standard")) { color = c };
        }
    }
}
