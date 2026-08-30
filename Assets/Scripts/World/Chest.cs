using System.Collections.Generic;
using UnityEngine;
using DungeonCrawler.Inventory;
using DungeonCrawler.Loot;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.World
{
    // A closed container instead of loot just sitting on the floor. Press E to open, which
    // pops its items out as normal WorldPickups around the chest -- reuses the exact same
    // pickup path items already use, so nothing downstream needs to know loot came from a
    // chest instead of a kill.
    public class Chest : MonoBehaviour
    {
        private List<ItemData> contents;
        private bool opened;
        private GameObject lidVisual;
        private GameObject triggerGO;

        public static Chest Spawn(Vector3 pos, List<ItemData> contents)
        {
            var root = new GameObject("Chest");
            root.transform.position = pos;

            var chest = root.AddComponent<Chest>();
            chest.contents = contents;
            chest.BuildVisual();

            chest.triggerGO = new GameObject("ChestTrigger");
            chest.triggerGO.transform.SetParent(root.transform);
            chest.triggerGO.transform.localPosition = new Vector3(0, 0.6f, 0.9f);
            var col = chest.triggerGO.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(1.8f, 1.4f, 2f);

            var interactable = chest.triggerGO.AddComponent<Interactable>();
            interactable.prompt = "Open Chest (E)";
            interactable.onInteract = chest.Open;

            return chest;
        }

        private void BuildVisual()
        {
            var baseGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseGO.name = "ChestBase";
            baseGO.transform.SetParent(transform);
            baseGO.transform.localPosition = new Vector3(0, 0.3f, 0);
            baseGO.transform.localScale = new Vector3(1.1f, 0.6f, 0.7f);
            SetColor(baseGO, new Color(0.35f, 0.22f, 0.1f));

            lidVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lidVisual.name = "ChestLid";
            var lidCol = lidVisual.GetComponent<Collider>();
            if (lidCol != null) Destroy(lidCol);
            lidVisual.transform.SetParent(transform);
            lidVisual.transform.localPosition = new Vector3(0, 0.68f, -0.02f);
            lidVisual.transform.localScale = new Vector3(1.15f, 0.15f, 0.75f);
            SetColor(lidVisual, new Color(0.55f, 0.4f, 0.15f));
        }

        private void Open()
        {
            if (opened) return;
            opened = true;

            if (lidVisual != null)
                lidVisual.transform.localRotation = Quaternion.Euler(-100f, 0, 0); // swung open -- crude but reads instantly

            // The trigger + Interactable that opened this chest would otherwise sit there
            // forever, right in front of the loot it just spilled out -- close enough to
            // keep winning the interact raycast over the smaller pickups behind it, and
            // showing a now-meaningless "Open Chest (E)" prompt.
            if (triggerGO != null) Destroy(triggerGO);

            for (int i = 0; i < contents.Count; i++)
            {
                var item = contents[i];
                if (item == null) continue;

                Vector3 offset = new Vector3((i - (contents.Count - 1) / 2f) * 0.7f, 0, 1.4f);
                var pickupGO = new GameObject("ChestLoot_" + item.itemName);
                pickupGO.transform.position = transform.position + offset;
                var col = pickupGO.AddComponent<SphereCollider>();
                col.isTrigger = true;
                col.radius = 0.4f;
                if (item.icon != null)
                    SpriteVisual.Attach(pickupGO.transform, item.icon, new Vector3(0, 0.5f, 0), scale: 0.5f);
                var wp = pickupGO.AddComponent<WorldPickup>();
                wp.item = item;
            }
        }

        private void SetColor(GameObject go, Color c)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.material = new Material(Shader.Find("Standard")) { color = c };
        }
    }
}
