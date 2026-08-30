using UnityEngine;
using DungeonCrawler.Classes;

namespace DungeonCrawler.Enemies
{
    // Finds the nearest PlayerCharacter within aggroRange and calls SetTarget().
    // Attach alongside EnemyBase (or add directly to EnemyBase.Awake -- kept separate
    // here so the aggro rule itself is easy to swap out, e.g. for threat-based tanking).
    public class AggroController : MonoBehaviour
    {
        public float aggroRange = 8f;
        public float rescanInterval = 0.5f;

        private EnemyBase enemy;
        private float rescanTimer;

        private void Awake()
        {
            enemy = GetComponent<EnemyBase>();
        }

        private void Update()
        {
            rescanTimer -= Time.deltaTime;
            if (rescanTimer > 0f) return;
            rescanTimer = rescanInterval;

            // FindObjectsOfType (not FindObjectsByType) deliberately -- the latter only
            // exists on Unity 2023.1+. FindObjectsOfType works on any version the project
            // is likely using and only costs anything on the 0.5s rescan tick, not per-frame.
            // If you're confirmed on 2023.1+, swap to FindObjectsByType(FindObjectsSortMode.None)
            // for a minor allocation/perf improvement.
            var players = FindObjectsOfType<PlayerCharacter>();
            Transform nearest = null;
            float nearestDist = aggroRange;

            foreach (var p in players)
            {
                if (p.health != null && p.health.IsDowned) continue; // tank check: ignore downed players, don't reward tunneling a corpse
                float d = Vector3.Distance(transform.position, p.transform.position);
                if (d < nearestDist)
                {
                    nearestDist = d;
                    nearest = p.transform;
                }
            }

            if (nearest != null) enemy.SetTarget(nearest);
        }
    }
}
