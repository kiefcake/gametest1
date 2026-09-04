using UnityEngine;

namespace DungeonCrawler.World
{
    // Purely decorative hub-village clutter -- hills, rocks, statues, carnival tents,
    // banners. Self-contained (no dependency on Visuals/ProceduralMonster) so the
    // in-progress HubLayout rewrite can call into this without pulling in anything else.
    public static class VillageDecor
    {
        // Real elevated terrain, not clutter -- keeps its collider so players can climb
        // and stand on it. Domed Sphere cap on a squashed Cylinder base reads as a mound
        // from ground level better than a single squashed sphere does.
        public static void BuildHill(Transform parent, Vector3 pos, float radius, float height, Color grassColor)
        {
            var mound = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mound.name = "Hill";
            mound.transform.SetParent(parent);
            mound.transform.position = pos + new Vector3(0, height * 0.5f, 0);
            mound.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            SetColor(mound, grassColor);

            var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.name = "HillCap";
            var capCol = cap.GetComponent<Collider>();
            if (capCol != null) Destroy(capCol); // the cylinder base below already blocks/supports movement
            cap.transform.SetParent(parent);
            cap.transform.position = pos + new Vector3(0, height, 0);
            cap.transform.localScale = new Vector3(radius * 1.9f, height * 0.8f, radius * 1.9f);
            SetColor(cap, grassColor);

            int rockCount = Random.Range(2, 5);
            var rockColor = new Color(0.4f, 0.4f, 0.42f);
            for (int i = 0; i < rockCount; i++)
            {
                bool cube = Random.value > 0.5f;
                var rock = GameObject.CreatePrimitive(cube ? PrimitiveType.Cube : PrimitiveType.Sphere);
                rock.name = "HillBaseRock";
                var col = rock.GetComponent<Collider>();
                if (col != null) Destroy(col); // decorative clutter -- shouldn't snag movement
                rock.transform.SetParent(parent);
                Vector2 ring = Random.insideUnitCircle.normalized * (radius + Random.Range(0f, 0.8f));
                rock.transform.position = pos + new Vector3(ring.x, 0.15f, ring.y);
                rock.transform.rotation = Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
                float s = Random.Range(0.25f, 0.5f);
                rock.transform.localScale = new Vector3(s, s * Random.Range(0.6f, 1f), s);
                SetColor(rock, rockColor);
            }
        }

        // Same jittered-cluster technique as DungeonLayout.BuildBonePile/BuildIceSpikes --
        // a handful of irregular primitives scattered around a center point.
        public static void BuildRockCluster(Transform parent, Vector3 pos, float scale = 1f)
        {
            int count = Random.Range(3, 6);
            var rocks = new GameObject[count];
            float[] sizes = new float[count];

            for (int i = 0; i < count; i++)
            {
                bool cube = Random.value > 0.5f;
                var rock = GameObject.CreatePrimitive(cube ? PrimitiveType.Cube : PrimitiveType.Sphere);
                rock.name = "Rock";
                rock.transform.SetParent(parent);
                Vector3 offset = new Vector3(Random.Range(-0.7f, 0.7f), 0, Random.Range(-0.7f, 0.7f)) * scale;
                float size = Random.Range(0.3f, 1.2f) * scale;
                rock.transform.position = pos + offset + new Vector3(0, size * 0.4f, 0);
                rock.transform.rotation = Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
                rock.transform.localScale = new Vector3(size, size * Random.Range(0.7f, 1.1f), size);
                SetColor(rock, new Color(0.38f + Random.Range(-0.05f, 0.05f), 0.38f + Random.Range(-0.05f, 0.05f), 0.4f));
                rocks[i] = rock;
                sizes[i] = size;
            }

            // The largest/most central rock alone keeps its collider -- the cluster should
            // block movement like a real obstacle, but the accent rocks around it shouldn't.
            int centralIndex = 0;
            for (int i = 1; i < count; i++)
            {
                if (sizes[i] > sizes[centralIndex]) centralIndex = i;
            }
            for (int i = 0; i < count; i++)
            {
                if (i != centralIndex) DestroyCollider(rocks[i]);
            }
        }

        // A stone guardian on a low plinth -- entirely static geometry, no animation/AI.
        // Built from bare primitives rather than ProceduralMonster so this file has no
        // dependency on DungeonCrawler.Visuals.
        public static void BuildStatue(Transform parent, Vector3 pos, float rotationYDegrees, Color stoneColor)
        {
            var root = new GameObject("Statue");
            root.transform.SetParent(parent);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0, rotationYDegrees, 0);

            var plinth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plinth.name = "StatuePlinth";
            plinth.transform.SetParent(root.transform);
            plinth.transform.localPosition = new Vector3(0, 0.25f, 0);
            plinth.transform.localScale = new Vector3(1.2f, 0.5f, 1.2f);
            SetColor(plinth, stoneColor);

            var torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            torso.name = "StatueTorso";
            DestroyCollider(torso);
            torso.transform.SetParent(root.transform);
            torso.transform.localPosition = new Vector3(0, 1.25f, 0);
            torso.transform.localScale = new Vector3(0.55f, 0.6f, 0.4f);
            SetColor(torso, stoneColor);

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "StatueHead";
            DestroyCollider(head);
            head.transform.SetParent(root.transform);
            head.transform.localPosition = new Vector3(0, 1.85f, 0);
            head.transform.localScale = new Vector3(0.4f, 0.42f, 0.4f);
            SetColor(head, stoneColor);

            // Shield arm planted at the side -- a heroic stance is easier to read as two
            // asymmetric arms than two mirrored ones.
            var shieldArm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            shieldArm.name = "StatueArmShield";
            DestroyCollider(shieldArm);
            shieldArm.transform.SetParent(root.transform);
            shieldArm.transform.localPosition = new Vector3(-0.42f, 1.15f, 0);
            shieldArm.transform.localRotation = Quaternion.Euler(0, 0, 20f);
            shieldArm.transform.localScale = new Vector3(0.18f, 0.45f, 0.18f);
            SetColor(shieldArm, stoneColor);

            // Sword arm raised across the chest, sword planted point-down in front --
            // classic guardian-statue pose.
            var swordArm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            swordArm.name = "StatueArmSword";
            DestroyCollider(swordArm);
            swordArm.transform.SetParent(root.transform);
            swordArm.transform.localPosition = new Vector3(0.42f, 1.3f, 0.1f);
            swordArm.transform.localRotation = Quaternion.Euler(0, 0, -35f);
            swordArm.transform.localScale = new Vector3(0.18f, 0.4f, 0.18f);
            SetColor(swordArm, stoneColor);

            var sword = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sword.name = "StatueSword";
            DestroyCollider(sword);
            sword.transform.SetParent(root.transform);
            sword.transform.localPosition = new Vector3(0.3f, 0.85f, 0.32f);
            sword.transform.localRotation = Quaternion.Euler(4f, 0, 0);
            sword.transform.localScale = new Vector3(0.1f, 0.7f, 0.03f);
            SetColor(sword, stoneColor);

            var legLeft = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            legLeft.name = "StatueLegLeft";
            DestroyCollider(legLeft);
            legLeft.transform.SetParent(root.transform);
            legLeft.transform.localPosition = new Vector3(-0.2f, 0.65f, 0);
            legLeft.transform.localScale = new Vector3(0.2f, 0.35f, 0.2f);
            SetColor(legLeft, stoneColor);

            var legRight = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            legRight.name = "StatueLegRight";
            DestroyCollider(legRight);
            legRight.transform.SetParent(root.transform);
            legRight.transform.localPosition = new Vector3(0.2f, 0.65f, 0);
            legRight.transform.localScale = new Vector3(0.2f, 0.35f, 0.2f);
            SetColor(legRight, stoneColor);
        }

        // A pointed circus roof (four tilted triangular-ish panels alternating stripe
        // colors) over a short canvas-toned drum -- distinct from HubLayout.BuildCanopy's
        // flat two-panel market awning.
        public static void BuildCarnivalTent(Transform parent, Vector3 pos, Color stripeColorA, Color stripeColorB, float rotationYDegrees = 0f)
        {
            var root = new GameObject("CarnivalTent");
            root.transform.SetParent(parent);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0, rotationYDegrees, 0);

            var canvasColor = Color.Lerp(stripeColorA, Color.white, 0.3f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wall.name = "TentWall";
            DestroyCollider(wall);
            wall.transform.SetParent(root.transform);
            wall.transform.localPosition = new Vector3(0, 1f, 0);
            wall.transform.localScale = new Vector3(3.2f, 1f, 3.2f);
            SetColor(wall, canvasColor);

            // Four wedge-shaped panels tilted inward -- each is a flattened cube pitched
            // toward the roof's apex, alternating colors for the classic circus-stripe read.
            float roofBase = 2f;
            float roofHeight = 2.2f;
            float apexY = roofBase + roofHeight;
            for (int i = 0; i < 4; i++)
            {
                var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = "TentRoofPanel";
                DestroyCollider(panel);
                panel.transform.SetParent(root.transform);
                float yaw = i * 90f;
                panel.transform.localPosition = new Vector3(0, (roofBase + apexY) * 0.5f, 0);
                panel.transform.localRotation = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(58f, 0, 0);
                panel.transform.localScale = new Vector3(3.4f, 0.08f, 2.6f);
                panel.transform.localPosition += Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, 1.1f);
                SetColor(panel, i % 2 == 0 ? stripeColorA : stripeColorB);
            }

            var finial = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            finial.name = "TentFinial";
            DestroyCollider(finial);
            finial.transform.SetParent(root.transform);
            finial.transform.localPosition = new Vector3(0, apexY, 0);
            finial.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            SetColor(finial, stripeColorB);
        }

        // Flat rectangular flag hanging from a small dark crossbar nub -- crude primitives
        // read instantly, per this project's established look (see DungeonLayout.cs).
        public static void BuildBanner(Transform parent, Vector3 pos, Color color, float rotationYDegrees = 0f)
        {
            var root = new GameObject("Banner");
            root.transform.SetParent(parent);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0, rotationYDegrees, 0);

            var crossbar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crossbar.name = "BannerCrossbar";
            DestroyCollider(crossbar);
            crossbar.transform.SetParent(root.transform);
            crossbar.transform.localPosition = new Vector3(0, 1.9f, 0);
            crossbar.transform.localScale = new Vector3(0.9f, 0.1f, 0.1f);
            SetColor(crossbar, new Color(0.22f, 0.15f, 0.08f));

            var cloth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cloth.name = "BannerCloth";
            DestroyCollider(cloth);
            cloth.transform.SetParent(root.transform);
            cloth.transform.localPosition = new Vector3(0, 0.9f, 0);
            cloth.transform.localScale = new Vector3(0.7f, 1.9f, 0.06f);
            SetColor(cloth, color);
        }

        private static void DestroyCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        private static void SetColor(GameObject go, Color c)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.material = new Material(Shader.Find("Standard")) { color = c };
        }
    }
}
