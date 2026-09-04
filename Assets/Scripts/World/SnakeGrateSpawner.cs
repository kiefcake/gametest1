using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Enemies;
using DungeonCrawler.Loot;

namespace DungeonCrawler.World
{
    // Snake Pit's signature room fixture (RotMG's "indestructible Snake Grate tiles which
    // will continually spawn Pit Snakes") -- periodically spawns a Pit Snake nearby, up to
    // a small concurrent cap, so a room doesn't go quiet forever once its initial ambush is
    // cleared. Attached to the decorative grate marker built by
    // DungeonLayout.BuildSnakeGrate.
    public class SnakeGrateSpawner : MonoBehaviour
    {
        public float spawnInterval = 8f;
        public int maxAlive = 2;
        private float timer;
        private int aliveCount;

        private void Start()
        {
            timer = Random.Range(0f, spawnInterval); // desyncs multiple grates in the same room
        }

        private void Update()
        {
            // Checked before decrementing -- if this returned only via the OR below, timer
            // would keep draining negative for the whole time the grate sits at its cap,
            // and the moment a slot freed up it'd spawn immediately (a banked burst)
            // instead of waiting out a fresh spawnInterval like it's supposed to.
            if (aliveCount >= maxAlive) return;

            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = spawnInterval;

            Vector3 pos = transform.position + new Vector3(Random.Range(-1.5f, 1.5f), 0, Random.Range(-1.5f, 1.5f));
            var go = new GameObject("PitSnake");
            go.transform.position = pos;
            go.AddComponent<Health>();
            go.AddComponent<StatusEffectController>();
            go.AddComponent<PitSnake>();
            go.AddComponent<AggroController>();
            var loot = go.AddComponent<LootDropper>();
            loot.lootTable = Resources.Load<LootTable>("Data/Loot/AbyssLootTable");
            loot.minGold = 2;
            loot.maxGold = 5;
            loot.minEssence = 1;
            loot.maxEssence = 2;

            aliveCount++;
            go.GetComponent<Health>().OnDeath += () => aliveCount--;
        }
    }
}
