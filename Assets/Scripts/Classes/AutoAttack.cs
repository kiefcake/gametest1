using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Enemies;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Classes
{
    // Free, unlimited basic attack -- distinct from the 3 mana-costing ability slots.
    // Hold LMB to fire repeatedly; DEX ("attack rate / cast rate" per the locked stat
    // design) shortens the interval between hits, ATT scales damage the same way the
    // ability system already does, for consistency.
    //
    // Melee classes (isMelee, set by GameBootstrap from ClassDefinition) get a short
    // range and higher damage and hit instantly; ranged classes fire an actual Projectile
    // that travels, can miss, and despawns at the end of its lifetime even without a hit --
    // and either kind now swings/fires with no target at all (a melee swing that connects
    // with nothing, or a bolt straight down the crosshair) instead of silently doing
    // nothing, since previously TryAttack() just bailed out whenever EnemyTargeting found
    // no target -- no animation, no feedback, nothing.
    [RequireComponent(typeof(PlayerCharacter))]
    public class AutoAttack : MonoBehaviour
    {
        public float baseDamage = 8f;
        public float baseInterval = 1f;
        public float minInterval = 0.15f;
        public float castRange = 12f;
        public bool isMelee = false;

        [Header("Ranged (ignored if isMelee)")]
        public int projectileCount = 1; // Wizard fires more than one bolt per shot
        public float projectileSpeed = 16f;
        public float projectileLifetime = 3f;
        public float projectileSpreadDegrees = 5f;

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

            Attack();
            float dex = player.Stats != null ? player.Stats.GetValue(StatType.DEX) : 0f;
            cooldownTimer = Mathf.Max(minInterval, baseInterval / (1f + dex * 0.08f));
        }

        private void Attack()
        {
            float att = player.Stats != null ? player.Stats.GetValue(StatType.ATT) : 0f;
            float damage = baseDamage * (1f + att * 0.01f);

            if (isMelee) MeleeAttack(damage);
            else RangedAttack(damage);

            viewmodel?.Swing();
        }

        private void MeleeAttack(float damage)
        {
            var target = EnemyTargeting.FindTarget(transform, castRange);
            var health = target != null ? target.GetComponent<IHealth>() : null;
            if (health != null)
            {
                health.TakeDamage(damage, ignoreDef: false);
                ImpactBurst.Spawn(target.transform.position + Vector3.up, new Color(0.9f, 0.85f, 0.55f));
            }
            // No target: still swings (see Attack()) -- a practice/whiff swing rather than
            // silently doing nothing when nothing's in range.
        }

        private void RangedAttack(float damage)
        {
            if (Camera.main == null) return;
            Vector3 origin = Camera.main.transform.position + Camera.main.transform.forward * 0.6f;

            var target = EnemyTargeting.FindTarget(transform, castRange);
            Vector3 aimDir = target != null
                ? ((target.transform.position + Vector3.up) - origin).normalized
                : Camera.main.transform.forward; // no target -- fire straight down the crosshair instead of not firing at all

            int count = Mathf.Max(1, projectileCount);
            float startAngle = -projectileSpreadDegrees * (count - 1) / 2f;
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(startAngle + projectileSpreadDegrees * i, Vector3.up) * aimDir;
                Projectile.Spawn(origin, dir, projectileSpeed, damage, new Color(0.5f, 0.75f, 1f), projectileLifetime, targetsPlayer: false);
            }
        }
    }
}
