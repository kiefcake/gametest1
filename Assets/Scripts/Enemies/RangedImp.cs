using UnityEngine;
using DungeonCrawler.Visuals;

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

        [Header("Special: channeled fan volley")]
        [Tooltip("Every volleyInterval the imp freezes in place (invulnerable, same shape as the boss channels) and fires a multi-shot fan at the end of the channel -- predictable movement (frozen) plus a real shot pattern to dodge, instead of just the single dodgeable bolt.")]
        public float volleyInterval = 9f;
        public int volleyCount = 5;
        public float volleySpreadAngle = 70f; // total fan width in degrees, centered on the target
        public float volleyChannelTime = 0.7f;
        private float volleyTimer;
        private bool channelingVolley;
        private float volleyChannelElapsed;

        private float rangedAttackTimer;

        protected override void Awake()
        {
            enemyName = "Imp Shaman";
            healthBarHeight = 2.05f;
            moveSpeed = 1.6f; // slower than melee imps -- it's meant to kite, not brawl

            base.Awake();

            attackCooldown = 2.2f; // slower fire rate than a melee imp's swing -- projectiles need to feel avoidable, not a stream
            attackDamage = 0f; // never melees -- Attack() is fully overridden below, this just keeps the field honest
            volleyTimer = volleyInterval; // was defaulting to 0 -- fired a volley on the very first Update() frame instead of waiting out the interval
        }

        protected override void AttachVisual()
        {
            var built = ProceduralMonster.Humanoid(transform, new ProceduralMonster.HumanoidSpec
            {
                bodyColor = new Color(0.5f, 0.2f, 0.55f),
                accentColor = new Color(0.85f, 0.25f, 0.95f),
                scale = 0.95f, horns = true, weapon = true, hunched = false
            });
            visualRenderers = built.renderers;
            spriteAnimator = built.root.gameObject.AddComponent<SpriteAnimator>();
            spriteAnimator.bobHeight = 0.05f;
            spriteAnimator.bobSpeed = 3f;
        }

        protected override void Update()
        {
            if (health.IsDowned || target == null) return;
            if (statusController.IsParalyzed) return;

            if (channelingVolley)
            {
                volleyChannelElapsed += Time.deltaTime;
                if (volleyChannelElapsed >= volleyChannelTime)
                {
                    FireVolley();
                    channelingVolley = false;
                }
                return; // frozen in place for the whole channel -- no retreat/approach/normal-attack, same as the bosses' own channels
            }

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

            volleyTimer -= Time.deltaTime;
            if (volleyTimer <= 0f && target != null)
            {
                channelingVolley = true;
                volleyChannelElapsed = 0f;
                SetInvulnerable(true);
                volleyTimer = volleyInterval;
            }
        }

        protected override void Attack()
        {
            Vector3 origin = transform.position + Vector3.up;
            Vector3 dir = (target.position + Vector3.up) - origin;
            Projectile.Spawn(origin, dir, projectileSpeed, projectileDamage, new Color(0.85f, 0.25f, 0.95f));
        }

        // Fired once, when the channel completes -- volleyCount bolts fanned evenly across
        // volleySpreadAngle centered on the target, so the whole channel reads as one
        // dodgeable pattern rather than several independent single shots.
        private void FireVolley()
        {
            // target could in principle have gone null mid-channel (aggro reset etc.) --
            // guard defensively even though Update()'s own top-of-frame target==null return
            // means this method is only ever reached with a live target in practice.
            if (target != null)
            {
                Vector3 origin = transform.position + Vector3.up;
                Vector3 baseDir = (target.position + Vector3.up) - origin;
                float halfSpread = volleySpreadAngle / 2f;

                for (int i = 0; i < volleyCount; i++)
                {
                    float t = volleyCount > 1 ? (float)i / (volleyCount - 1) : 0.5f;
                    float angleOffset = Mathf.Lerp(-halfSpread, halfSpread, t);
                    Vector3 dir = Quaternion.Euler(0, angleOffset, 0) * baseDir;
                    Projectile.Spawn(origin, dir, projectileSpeed, projectileDamage * 0.5f, new Color(0.85f, 0.25f, 0.95f));
                }

                spriteAnimator?.PulseAttack();
            }
            SetInvulnerable(false); // unconditional -- never leave the imp permanently invulnerable even if target vanished mid-channel
        }
    }
}
