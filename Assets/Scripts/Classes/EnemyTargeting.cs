using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Enemies;

namespace DungeonCrawler.Classes
{
    // Shared crosshair-first / nearest-fallback targeting -- used by both ability casting
    // (PlayerAbilityInput) and auto-attack (AutoAttack) so "point roughly at something and
    // it hits" behaves identically for both. Was duplicated per-caller before; pulled out
    // once AutoAttack needed the exact same logic.
    public static class EnemyTargeting
    {
        public static GameObject FindTarget(Transform origin, float range)
        {
            bool blinded = origin.GetComponent<StatusEffectController>()?.HasEffect(StatusEffectType.Blind) ?? false;

            if (!blinded && Camera.main != null &&
                Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out RaycastHit hit, range))
            {
                var enemy = hit.collider.GetComponentInParent<EnemyBase>();
                if (enemy != null)
                {
                    var eh = enemy.GetComponent<Health>();
                    if (eh == null || !eh.IsDowned) return enemy.gameObject;
                }
            }

            float effectiveRange = blinded ? range * 0.35f : range;
            return FindNearestEnemy(origin, effectiveRange);
        }

        public static GameObject FindNearestEnemy(Transform origin, float range)
        {
            var enemies = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
            GameObject nearest = null;
            float nearestDist = range;
            foreach (var e in enemies)
            {
                var h = e.GetComponent<Health>();
                if (h != null && h.IsDowned) continue;

                float d = Vector3.Distance(origin.position, e.transform.position);
                if (d < nearestDist)
                {
                    nearestDist = d;
                    nearest = e.gameObject;
                }
            }
            return nearest;
        }
    }
}
