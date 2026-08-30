using UnityEngine;

namespace DungeonCrawler.Enemies
{
    // Abyss dungeon's ranged threat -- keeps its distance and lobs a slow, dodgeable bolt
    // instead of closing to melee, pairing with the melee imps to force actual movement
    // during a fight instead of tanking hits in place behind a pillar.
    //
    // Fully overrides Update() rather than reusing EnemyBase's move-or-attack branch: that
    // branch is a strict "too far -> move, else -> attack" gate, which can't express "back
    // off if too close, hold position and fire in the sweet spot" on its own. attackTimer
    // stays private on EnemyBase (by design -- nothing else needed it), so this keeps its
    // own cooldown timer rather than reaching into the base class for it.
    public class RangedImp : EnemyBase
    {
        public float preferredRange = 7f;
        public float retreatRange = 4f;
        public float projectileSpeed = 9f;
        public float projectileDamage = 9f;

        private float rangedAttackTimer;

        protected override void Awake()
        {
            enemyName = "Imp Shaman";
            spriteResourcePath = "Sprites/Enemies/Abyss/imp_demon_spiked"; // reuses existing art -- no dedicated ranged sprite exists yet
            spriteHeight = 0.9f;
            healthBarHeight = 1.9f;
            moveSpeed = 1.6f; // slower than melee imps -- it's meant to kite, not brawl

            base.Awake();

            attackCooldown = 2.2f; // slower fire rate than a melee imp's swing -- projectiles need to feel avoidable, not a stream
            attackDamage = 0f; // never melees -- Attack() is fully overridden below, this just keeps the field honest
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
                MoveTowardTarget(); // reuses EnemyBase's weave/separation approach logic
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
            Projectile.Spawn(origin, dir, projectileSpeed, projectileDamage, new Color(0.85f, 0.25f, 0.95f));
        }
    }
}
