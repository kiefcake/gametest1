using System.Collections.Generic;
using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.World
{
    // Floor hazard -- deals periodic damage to anything standing in it, so the bigger
    // combat rooms actually require picking a path through them instead of a straight
    // walk. Per-object throttling (rather than one shared timer on the hazard) so multiple
    // things standing in the same pool don't all speed up each other's tick rate.
    public class LavaHazard : MonoBehaviour
    {
        public float damagePerTick = 4f;
        public float tickInterval = 0.5f;
        private readonly Dictionary<GameObject, float> nextTickAt = new Dictionary<GameObject, float>();

        private void OnTriggerStay(Collider other)
        {
            var root = other.transform.root.gameObject;
            if (nextTickAt.TryGetValue(root, out float t) && Time.time < t) return;
            nextTickAt[root] = Time.time + tickInterval;

            other.GetComponentInParent<IHealth>()?.TakeDamage(damagePerTick, ignoreDef: true);
        }
    }
}
