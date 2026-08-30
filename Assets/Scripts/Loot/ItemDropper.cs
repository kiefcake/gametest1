using UnityEngine;
using DungeonCrawler.Inventory;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Loot
{
    // Spawns a dropped item as a normal WorldPickup a short distance in front of whoever
    // dropped it -- the same object type loot already uses (see LootDropper), so nothing
    // downstream needs to know a pickup came from a kill, a chest, or a player emptying
    // their own inventory. Used by InventoryUI's right-click-to-drop.
    public static class ItemDropper
    {
        public static void Drop(ItemData item, Transform from)
        {
            if (item == null || from == null) return;

            Vector3 pos = from.position + from.forward * 1.2f + Vector3.up * 0.3f;
            var pickup = new GameObject("Dropped_" + item.itemName);
            pickup.transform.position = pos;
            var col = pickup.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.4f;
            if (item.icon != null)
                SpriteVisual.Attach(pickup.transform, item.icon, new Vector3(0, 0.5f, 0), scale: 0.4f);

            var wp = pickup.AddComponent<WorldPickup>();
            wp.item = item;
        }
    }
}
