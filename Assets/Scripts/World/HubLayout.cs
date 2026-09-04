using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.World
{
    // Medieval carnival castle village -- rebuilt from the original plain walled-square
    // hub into a real centerpiece layout: a walled village green with a proper keep (not
    // just a wall ring) dominating the north end, a market row and well in the plaza south
    // of it, a carnival wing (games, tents, bunting) through the west archway, and a
    // terraced garden wing (hills, rocks, statues) through the east archway for
    // verticality. The keep's own north face doubles as the "grand portal" -- what used to
    // be a plain two-pillar dungeon gate is now the castle's rear gate, framed by its own
    // towers and flanking statues, so leaving on a run reads as walking out through the
    // keep instead of through an unrelated door in a wall.
    //
    // Every existing GameBootstrap contract is preserved exactly: EntryPoint,
    // GateInteractable (now on the grand portal), GambleInteractable (now inside the
    // keep's own courtyard), ClawMachine (now in the carnival wing), and the three
    // VendorNPC-tagged stalls GameBootstrap.WireVendors finds by name -- none of those
    // call sites needed to change.
    //
    // Placeholder geometry throughout, same as it always was (see DungeonLayout's header
    // for why): primitives, flat colors, built purely in code. Builds in Awake() so
    // GameBootstrap can read EntryPoint/GateInteractable/etc the same frame it calls
    // AddComponent<HubLayout>().
    public class HubLayout : MonoBehaviour
    {
        private static readonly Vector3 Center = new Vector3(0, 0, -40f);

        private const float SquareHalf = 22f;       // village green half-extent (x and z)
        private const float WallHeight = 3.4f;
        private const float WallThickness = 0.6f;
        private const float WingGapHalf = 5f;        // east/west archway openings into the wings
        private const float WingHalfWidth = 11f;     // carnival/terrace wing half-width (x)
        private const float WingHalfDepth = 9f;      // carnival/terrace wing half-depth (z)

        private const float CastleHalfWidth = 7f;
        private const float CastleHalfDepth = 6f;
        private const float CastleCenterZ = 11f;     // relative to Center -- keep sits toward the north wall

        private static readonly Color WallStoneColor = new Color(0.44f, 0.41f, 0.37f);
        private static readonly Color KeepStoneColor = new Color(0.5f, 0.47f, 0.43f); // a shade lighter -- the keep should read as the "important" stone, not just more wall
        private static readonly Color MerlonColor = new Color(0.38f, 0.35f, 0.32f);
        private static readonly Color StatueStoneColor = new Color(0.56f, 0.55f, 0.53f);

        public Vector3 EntryPoint { get; private set; }
        public Interactable GateInteractable { get; private set; }
        public Interactable GambleInteractable { get; private set; }
        public ClawMachineNPC ClawMachine { get; private set; }

        private void Awake()
        {
            EntryPoint = Center + new Vector3(0, 0, -14f);

            BuildSquareFloor();
            BuildVillageWalls();
            BuildWell(Center);
            BuildVendorStall(Center + new Vector3(-12f, 0, -7f), "Alchemist",
                "Potions for every stat -- a small boost per bottle, five to a cap.", new Color(0.4f, 0.7f, 0.5f));
            BuildVendorStall(Center + new Vector3(12f, 0, -7f), "Blacksmith",
                "Weapons and armor pulled from the vault. Gear up before you head down.", new Color(0.6f, 0.5f, 0.4f));
            BuildVendorStall(Center + new Vector3(0f, 0, -9f), "Curiosities",
                "Rare finds, priced accordingly. Not for the faint of coinpurse.", new Color(0.55f, 0.35f, 0.7f));
            BuildTrainingArea(Center + new Vector3(-17f, 0, -15f));
            BuildCastle(Center + new Vector3(0, 0, CastleCenterZ));
            BuildCarnivalWing();
            BuildTerraceWing();
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

        // North and south stay solid -- south because the player spawns just inside it,
        // north because the keep built inside the green (see BuildCastle) carries its own
        // rear gate instead of the wall needing a second one. Archway gaps only lead east
        // (terrace wing) and west (carnival wing).
        private void BuildVillageWalls()
        {
            float h = SquareHalf;

            BuildWallRunX(-h, -h, h);
            BuildWallRunX(h, -h, h);

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

            AddWallTrim(worldCenter, scale, WallStoneColor);
        }

        private void AddWallTrim(Vector3 worldCenter, Vector3 scale, Color baseColor)
        {
            float bandHeight = Mathf.Min(0.3f, scale.y * 0.15f);
            Color baseboard = baseColor * 0.55f; baseboard.a = 1f;
            Color cap = Color.Lerp(baseColor, Color.white, 0.25f);
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
                if (col != null) Destroy(col);
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
            holder.AddComponent<TorchFlicker>().flickerAmount = 0.2f;
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

            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "WellRim";
            rim.transform.SetParent(transform);
            rim.transform.position = center + new Vector3(0, 0.75f, 0);
            rim.transform.localScale = new Vector3(3.4f, 0.25f, 3.4f);
            SetColor(rim, WallStoneColor);

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

        // --- The castle: a real keep in the middle of the green, not just more wall. Four
        // corner towers + a taller central keep tower, a gatehouse arch facing south (the
        // way the player actually walks in from the market), solid east/west/north walls
        // except where BuildGrandPortal below cuts the north face open -- the keep's own
        // rear gate doubles as GateInteractable, so leaving on a run reads as walking out
        // through the castle instead of some unrelated door in the outer wall.
        private void BuildCastle(Vector3 keepCenter)
        {
            float w = CastleHalfWidth;
            float d = CastleHalfDepth;
            const float gateHalf = 2.6f;

            // South (entrance) face -- gapped for the gatehouse.
            BuildKeepWallX(keepCenter, -d, -w, -gateHalf);
            BuildKeepWallX(keepCenter, -d, gateHalf, w);
            // East/west faces -- solid.
            BuildKeepWallZ(keepCenter, -w, -d, d);
            BuildKeepWallZ(keepCenter, w, -d, d);
            // North face is intentionally NOT built here -- BuildGrandPortal below is the
            // keep's rear wall.

            BuildKeepTower(keepCenter + new Vector3(-w, 0, -d), 2f, false);
            BuildKeepTower(keepCenter + new Vector3(w, 0, -d), 2f, false);
            BuildKeepTower(keepCenter + new Vector3(-w, 0, d), 2.1f, true);
            BuildKeepTower(keepCenter + new Vector3(w, 0, d), 2.1f, true);
            BuildKeepTower(keepCenter, 3.2f, true); // the central keep -- tallest thing in the village

            BuildGatehouseArch(keepCenter + new Vector3(0, 0, -d), gateHalf);
            VillageDecor.BuildStatue(transform, keepCenter + new Vector3(-gateHalf - 1.4f, 0, -d - 0.3f), 20f, StatueStoneColor);
            VillageDecor.BuildStatue(transform, keepCenter + new Vector3(gateHalf + 1.4f, 0, -d - 0.3f), -20f, StatueStoneColor);

            BuildCourtyardInterior(keepCenter);
            BuildGrandPortal(keepCenter + new Vector3(0, 0, d));
        }

        private void BuildKeepWallX(Vector3 keepCenter, float z, float xFrom, float xTo)
        {
            float length = xTo - xFrom;
            if (length <= 0.01f) return;
            Vector3 basePos = keepCenter + new Vector3((xFrom + xTo) / 2f, 0, z);
            BuildKeepWallCube(basePos + new Vector3(0, WallHeight / 2f, 0), new Vector3(length, WallHeight, WallThickness));
            BuildCrenellations(basePos, length, alongX: true);
        }

        private void BuildKeepWallZ(Vector3 keepCenter, float x, float zFrom, float zTo)
        {
            float length = zTo - zFrom;
            if (length <= 0.01f) return;
            Vector3 basePos = keepCenter + new Vector3(x, 0, (zFrom + zTo) / 2f);
            BuildKeepWallCube(basePos + new Vector3(0, WallHeight / 2f, 0), new Vector3(WallThickness, WallHeight, length));
            BuildCrenellations(basePos, length, alongX: false);
        }

        private void BuildKeepWallCube(Vector3 worldCenter, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "KeepWall";
            wall.transform.SetParent(transform);
            wall.transform.position = worldCenter;
            wall.transform.localScale = scale;
            SetColor(wall, KeepStoneColor);
            AddWallTrim(worldCenter, scale, KeepStoneColor);
        }

        // tall=true gives the central keep and rear towers extra height plus a banner --
        // used for the towers that should read as "the important ones" versus the
        // shorter, plainer front-facing towers flanking the gatehouse.
        private void BuildKeepTower(Vector3 pos, float radius, bool tall)
        {
            float towerHeight = WallHeight + (tall ? 6f : 3.5f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "KeepTower";
            body.transform.SetParent(transform);
            body.transform.position = pos + new Vector3(0, towerHeight / 2f, 0);
            body.transform.localScale = new Vector3(radius * 2f, towerHeight / 2f, radius * 2f);
            SetColor(body, KeepStoneColor);

            var roof = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            roof.name = "KeepTowerRoof";
            var roofCol = roof.GetComponent<Collider>();
            if (roofCol != null) Destroy(roofCol);
            roof.transform.SetParent(transform);
            roof.transform.position = pos + new Vector3(0, towerHeight + radius * 0.4f, 0);
            roof.transform.localScale = new Vector3(radius * 2.3f, radius * 1.4f, radius * 2.3f);
            SetColor(roof, new Color(0.32f, 0.14f, 0.16f));

            BuildLantern(pos + new Vector3(0, towerHeight + 1.2f, 0));

            if (tall)
            {
                VillageDecor.BuildBanner(transform, pos + new Vector3(radius + 0.05f, towerHeight - 1.5f, 0),
                    new Color(0.7f, 0.1f, 0.15f), 90f);
            }
        }

        private void BuildGatehouseArch(Vector3 pos, float gapHalf)
        {
            var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lintel.name = "GatehouseLintel";
            lintel.transform.SetParent(transform);
            lintel.transform.position = pos + new Vector3(0, WallHeight + 0.5f, 0);
            lintel.transform.localScale = new Vector3(gapHalf * 2f + 1.4f, 0.6f, 0.7f);
            SetColor(lintel, KeepStoneColor);
        }

        // The gambling table used to live in its own walled tavern wing -- now it's what
        // the castle itself "houses," per the brief, inside the keep's own courtyard
        // between the south gatehouse and the north portal. Same GambleInteractable wiring
        // as before; only the setting around it changed.
        private void BuildCourtyardInterior(Vector3 keepCenter)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "CourtyardFloor";
            floor.transform.SetParent(transform);
            floor.transform.position = keepCenter;
            floor.transform.localScale = new Vector3(CastleHalfWidth * 2f / 10f, 1f, CastleHalfDepth * 2f / 10f);
            SetColor(floor, new Color(0.34f, 0.31f, 0.26f));

            BuildLantern(keepCenter + new Vector3(-CastleHalfWidth + 1.2f, WallHeight - 0.4f, 0));
            BuildLantern(keepCenter + new Vector3(CastleHalfWidth - 1.2f, WallHeight - 0.4f, 0));

            Color gamblerColor = new Color(0.5f, 0.15f, 0.15f);
            var gamblerBuilt = ProceduralMonster.Humanoid(transform, new ProceduralMonster.HumanoidSpec
            {
                bodyColor = gamblerColor,
                accentColor = Color.Lerp(gamblerColor, Color.white, 0.5f),
                scale = 1f, horns = false, weapon = false, hunched = false
            });
            gamblerBuilt.root.name = "Gambler";
            gamblerBuilt.root.position = keepCenter + new Vector3(-2.5f, 0, 1.5f);
            var gamblerCol = gamblerBuilt.root.gameObject.AddComponent<CapsuleCollider>();
            gamblerCol.height = 1.9f;
            gamblerCol.radius = 0.35f;
            gamblerCol.center = new Vector3(0, 0.95f, 0);

            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "GambleTable";
            table.transform.SetParent(transform);
            table.transform.position = keepCenter + new Vector3(0.5f, 0.45f, 1.5f);
            table.transform.localScale = new Vector3(1.8f, 0.9f, 1.8f);
            SetColor(table, new Color(0.2f, 0.35f, 0.22f));

            BuildDie(keepCenter + new Vector3(0.2f, 0.95f, 1.7f));
            BuildDie(keepCenter + new Vector3(0.8f, 0.95f, 1.35f));

            var triggerGO = new GameObject("GambleTrigger");
            triggerGO.transform.SetParent(transform);
            triggerGO.transform.position = keepCenter + new Vector3(1.5f, 1f, 1.5f);
            var col = triggerGO.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(3f, 2.2f, 3f);

            GambleInteractable = triggerGO.AddComponent<Interactable>();
            GambleInteractable.prompt = "Try your luck (E)";
        }

        // The "grand portal" -- what used to be BuildDungeonGate's plain two-pillar arch is
        // now the castle's own rear gate: taller flanking towers, a wider glowing archway,
        // a short flight of steps leading up to it, and statues at its base. Same
        // GateInteractable wiring as the old gate (GameBootstrap.EnterOpenWorld), just a
        // much bigger frame around it.
        private void BuildGrandPortal(Vector3 pos)
        {
            const float gateHalf = 3.2f;
            const float towerRadius = 2.3f;
            float towerHeight = WallHeight + 7f;

            // Portal towers sit at gateHalf+towerRadius from center, so their own outer
            // edge (+towerRadius again) already reaches past CastleHalfWidth (7) with
            // these numbers -- they close the keep's north face on their own, no separate
            // flanking wall stub needed between them and the keep's corner towers.
            BuildPortalTower(pos + new Vector3(-gateHalf - towerRadius, 0, 0), towerRadius, towerHeight);
            BuildPortalTower(pos + new Vector3(gateHalf + towerRadius, 0, 0), towerRadius, towerHeight);

            var arch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arch.name = "PortalArch";
            arch.transform.SetParent(transform);
            arch.transform.position = pos + new Vector3(0, towerHeight * 0.55f, 0);
            arch.transform.localScale = new Vector3(gateHalf * 2f + 1.6f, 1f, 1f);
            SetColor(arch, KeepStoneColor);

            var portalSlab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            portalSlab.name = "GrandPortalGlow";
            var slabCol = portalSlab.GetComponent<Collider>();
            if (slabCol != null) Destroy(slabCol);
            portalSlab.transform.SetParent(transform);
            portalSlab.transform.position = pos + new Vector3(0, towerHeight * 0.55f / 2f, 0);
            portalSlab.transform.localScale = new Vector3(gateHalf * 2f - 0.6f, towerHeight * 0.55f - 0.4f, 0.2f);
            SetColor(portalSlab, new Color(0.65f, 0.2f, 0.8f));
            var glow = portalSlab.AddComponent<PortalGlow>();
            glow.colorA = new Color(0.45f, 0.1f, 0.6f);
            glow.colorB = new Color(0.85f, 0.55f, 1f);

            // A few steps up to the threshold -- reads as a real approach instead of the
            // portal just standing flush on the ground.
            for (int i = 0; i < 3; i++)
            {
                var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.name = "PortalStep";
                step.transform.SetParent(transform);
                float stepZ = (2 - i) * 0.5f;
                step.transform.position = pos + new Vector3(0, 0.1f + i * 0.001f, stepZ);
                step.transform.localScale = new Vector3(gateHalf * 2f + 2f - i * 0.6f, 0.2f, 0.6f);
                SetColor(step, WallStoneColor);
            }

            VillageDecor.BuildStatue(transform, pos + new Vector3(-gateHalf - towerRadius * 1.7f, 0, 0.6f), -70f, StatueStoneColor);
            VillageDecor.BuildStatue(transform, pos + new Vector3(gateHalf + towerRadius * 1.7f, 0, 0.6f), 70f, StatueStoneColor);
            VillageDecor.BuildBanner(transform, pos + new Vector3(-gateHalf - 0.3f, towerHeight * 0.55f + 1f, 0), new Color(0.65f, 0.2f, 0.8f), 0f);
            VillageDecor.BuildBanner(transform, pos + new Vector3(gateHalf + 0.3f, towerHeight * 0.55f + 1f, 0), new Color(0.65f, 0.2f, 0.8f), 180f);

            var triggerGO = new GameObject("GrandPortalTrigger");
            triggerGO.transform.SetParent(transform);
            triggerGO.transform.position = pos + new Vector3(0, 1f, -1.4f);
            var col = triggerGO.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(gateHalf * 2f, 2.4f, 1.8f);

            GateInteractable = triggerGO.AddComponent<Interactable>();
            GateInteractable.prompt = "Enter the Dungeon (E)";
        }

        private void BuildPortalTower(Vector3 pos, float radius, float towerHeight)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "PortalTower";
            body.transform.SetParent(transform);
            body.transform.position = pos + new Vector3(0, towerHeight / 2f, 0);
            body.transform.localScale = new Vector3(radius * 2f, towerHeight / 2f, radius * 2f);
            SetColor(body, KeepStoneColor);

            var roof = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            roof.name = "PortalTowerRoof";
            var roofCol = roof.GetComponent<Collider>();
            if (roofCol != null) Destroy(roofCol);
            roof.transform.SetParent(transform);
            roof.transform.position = pos + new Vector3(0, towerHeight + radius * 0.4f, 0);
            roof.transform.localScale = new Vector3(radius * 2.3f, radius * 1.4f, radius * 2.3f);
            SetColor(roof, new Color(0.32f, 0.14f, 0.16f));

            BuildLantern(pos + new Vector3(0, towerHeight + 1.2f, 0), new Color(0.75f, 0.5f, 1f));
        }

        private void BuildVendorStall(Vector3 pos, string vendorName, string flavor, Color npcColor)
        {
            var stallRoot = new GameObject(vendorName + "Stall");
            stallRoot.transform.SetParent(transform);
            stallRoot.transform.position = pos;

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

        // West archway -- games of chance and fairground fun, both the "carnival" half of
        // "medieval carnival castle village": festival tents and bunting framing the claw
        // machine. Houses the claw machine; GameBootstrap fills in ClawMachine.prizePool
        // and wires its Interactable.
        private void BuildCarnivalWing()
        {
            float centerX = -(SquareHalf + WingHalfWidth);
            Vector3 wingCenter = Center + new Vector3(centerX, 0, 0);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "CarnivalFloor";
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
                new Vector3(-8f, 0, 7f), new Vector3(8f, 0, 7f),
                new Vector3(-8f, 0, -7f), new Vector3(8f, 0, -7f)
            };
            for (int i = 0; i < poleOffsets.Length; i++)
                BuildBuntingPole(wingCenter + poleOffsets[i], flagColors[i]);

            VillageDecor.BuildCarnivalTent(transform, wingCenter + new Vector3(-6f, 0, 0), new Color(0.85f, 0.2f, 0.25f), Color.white, 15f);
            VillageDecor.BuildCarnivalTent(transform, wingCenter + new Vector3(6.5f, 0, -3f), new Color(0.2f, 0.4f, 0.85f), Color.white, -25f);

            BuildClawMachine(wingCenter + new Vector3(2f, 0, 3f));

            // The gambling table used to sit in its own enclosed room in this wing --
            // it's since moved into the castle courtyard (see BuildCourtyardInterior), so
            // this third tent just keeps the wing from reading empty in its place.
            VillageDecor.BuildCarnivalTent(transform, wingCenter + new Vector3(-2f, 0, -6f), new Color(0.95f, 0.75f, 0.2f), new Color(0.3f, 0.2f, 0.1f), 5f);
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

        // East archway -- the verticality/terrain wing the brief asked for: hills to climb,
        // rock outcrops, and statues along a raised scenic path. Purely decorative/
        // atmospheric -- no interactables live here, it's the village's "garden" half.
        private void BuildTerraceWing()
        {
            float centerX = SquareHalf + WingHalfWidth;
            Vector3 wingCenter = Center + new Vector3(centerX, 0, 0);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "TerraceFloor";
            floor.transform.SetParent(transform);
            floor.transform.position = wingCenter;
            floor.transform.localScale = new Vector3(WingHalfWidth * 2f / 10f, 1f, WingHalfDepth * 2f / 10f);
            SetColor(floor, new Color(0.28f, 0.34f, 0.2f));

            Color grass = new Color(0.32f, 0.42f, 0.22f);
            VillageDecor.BuildHill(transform, wingCenter + new Vector3(-6f, 0, 4f), 4.5f, 2.2f, grass);
            VillageDecor.BuildHill(transform, wingCenter + new Vector3(6f, 0, -5f), 3.5f, 1.6f, grass);
            VillageDecor.BuildHill(transform, wingCenter + new Vector3(3f, 0, 6f), 2.6f, 1.2f, grass);

            VillageDecor.BuildRockCluster(transform, wingCenter + new Vector3(-2f, 0, -6f), 1.3f);
            VillageDecor.BuildRockCluster(transform, wingCenter + new Vector3(8f, 0, 3f), 0.9f);
            VillageDecor.BuildRockCluster(transform, wingCenter + new Vector3(-8f, 0, -2f), 1.1f);

            VillageDecor.BuildStatue(transform, wingCenter + new Vector3(0f, 0, 0f), 200f, StatueStoneColor);
            VillageDecor.BuildStatue(transform, wingCenter + new Vector3(-6f, 2.2f, 4f), 160f, StatueStoneColor); // atop the tallest hill, a lookout guardian

            BuildLantern(wingCenter + new Vector3(0f, 1.4f, -1.5f));
        }

        private void SetColor(GameObject go, Color c)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.material = new Material(Shader.Find("Standard")) { color = c };
        }
    }
}
