using UnityEngine;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Enemies
{
    // The Wraithbound Sanctum's ranged trash -- kites at range and lobs a Weaken bolt,
    // same basic kite-and-shoot loop as PitDartThrower. This dungeon's other half of its
    // Curse+Weaken combo gimmick (see WraithKnight for Curse).
    //
    // Fully overrides Update() for the same reason every other kiter here does: EnemyBase's
    // own move-or-attack branch can't express "retreat if too close, hold and fire in the
    // sweet spot, approach if too far" on its own.
    public class SpecterCaster : EnemyBase
    {
        public float preferredRange = 7f;
        public float retreatRange = 4f;
        public float projectileSpeed = 8f;
        public float projectileDamage = 8f;
        public float weakenDuration = 5f;
        public float weakenMagnitude = 0.3f; // matches Paladin's Hex value

        private float rangedAttackTimer;

        protected override void Awake()
        {
            enemyName = "Specter Caster";
            healthBarHeight = 1.85f;
            moveSpeed = 1.4f;

            base.Awake();

            attackCooldown = 2.1f;
            attackDamage = 0f; // never melees -- Attack() is fully overridden below, this just keeps the field honest
        }

        // Floating caster, not a humanoid -- no legs, reads as hovering/haunting rather
        // than planted, matching AbyssMage/FrostLich's own archetype choice for casters.
        protected override void AttachVisual()
        {
            var built = ProceduralMonster.FloatingCaster(transform, new ProceduralMonster.FloatingSpec
            {
                robeColor = new Color(0.3f, 0.15f, 0.4f),
                accentColor = new Color(0.75f, 0.95f, 0.8f), // pale ghostly green-white
                scale = 0.95f, orb = true
            });
            visualRenderers = built.renderers;
            spriteAnimator = built.root.gameObject.AddComponent<SpriteAnimator>();
            spriteAnimator.bobHeight = 0.07f;
            spriteAnimator.bobSpeed = 2f;
        }

        protected override void Update()
        {
            if (health.IsDowned || target == null) return;
            if (statusController.IsParalyzed) return;

            float dist = Vector3.Distance(transform.position, target.position);

            if (dist < retreatRange)
            {
                Vector3 away = (transform.position - target.position).normalized;
                Move(away * moveSpeed * Time.deltaTime);
            }
            else if (dist > preferredRange)
            {
                MoveTowardTarget();
            }
            else
            {
                rangedAttackTimer -= Time.deltaTime;
                if (rangedAttackTimer <= 0f)
                {
                    Attack();
                    rangedAttackTimer = attackCooldown * Random.Range(0.85f, 1.15f);
                }
            }
        }

        protected override void Attack()
        {
            Vector3 origin = transform.position + Vector3.up;
            Vector3 dir = (target.position + Vector3.up) - origin;
            Projectile.Spawn(origin, dir, projectileSpeed, projectileDamage, new Color(0.75f, 0.95f, 0.8f),
                appliedEffect: Core.StatusEffectType.Weaken, effectDuration: weakenDuration, effectMagnitude: weakenMagnitude);
        }
    }
}
