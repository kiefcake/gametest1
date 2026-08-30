using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonCrawler.Inventory;

namespace DungeonCrawler.Loot
{
    [Serializable]
    public class LootEntry
    {
        public ItemData item;
        [Range(0f, 1f)] public float dropChance = 0.1f;
    }

    [CreateAssetMenu(menuName = "DungeonCrawler/Loot Table")]
    public class LootTable : ScriptableObject
    {
        public List<LootEntry> entries = new List<LootEntry>();

        // Rolls each entry independently (RotMG-style independent drop chances,
        // not a single-roll table), so multiple items can drop from one kill.
        public List<ItemData> RollDrops()
        {
            var results = new List<ItemData>();
            foreach (var entry in entries)
            {
                if (entry.item == null) continue;
                if (UnityEngine.Random.value <= entry.dropChance)
                    results.Add(entry.item);
            }
            return results;
        }
    }
}
