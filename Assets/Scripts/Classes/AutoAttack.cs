using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Classes
{
    // Free, unlimited basic attack -- distinct from the 3 mana-costing ability slots.
    // Hold LMB to fire repeatedly; DEX ("attack rate / cast rate" per the locked stat
    // design) shortens the interval between hits, ATT scales damage the same way the
    // ability system already does, for consistency.
    [RequireComponent(typeof(PlayerCharacter))]
    public class AutoAttack : MonoBehaviour
    {
        public float baseDamage = 8f;
        public float baseInterval = 1f;
        public float minInterval = 0.15f;
        public float castRange = 12f;

        // Set by GameBootstrap -- same viewmodel instance PlayerAbilityInput swings, so
        // auto-attacks and abilities both animate the same weapon.
        public WeaponViewmodel viewmodel;

        private PlayerCharacter player;
        private float cooldownTimer;

        private void Awake()
        {
            player = GetComponent<PlayerCharacter>();
        }

        private void Update()
        {
            if (player.health != null && player.health.IsDowned) return;
            if (player.statusController != null && player.statusController.IsParalyzed) return;

            if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
            if (cooldownTimer > 0f || !Input.GetMouseButton(0)) return;

            if (TryAttack())
            {
                float dex = player.Stats != null ? player.Stats.GetValue(StatType.DEX) : 0f;
                cooldownTimer = Mathf.Max(minInterval, baseInterval / (1f + dex * 0.08f));
            }
        }

        private bool TryAttack()
        {
            var target = EnemyTargeting.FindTarget(transform, castRange);
            if (target == null) return false;

            var health = target.GetComponent<IHealth>();
            if (health == null) return false;

            float att = player.Stats != null ? player.Stats.GetValue(StatType.ATT) : 0f;
            health.TakeDamage(baseDamage * (1f + att * 0.01f), ignoreDef: false);

            ImpactBurst.Spawn(target.transform.position + Vector3.up, new Color(0.9f, 0.85f, 0.55f));
            viewmodel?.Swing();
            return true;
        }
    }
}
