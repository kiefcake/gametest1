using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Enemies
{
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(StatusEffectController))]
    public class EnemyBase : MonoBehaviour
    {
        public string enemyName;
        public float moveSpeed = 2f;
        public float attackDamage = 8f;
        public float attackRange = 1.2f;
        public float attackCooldown = 1.5f;

        protected Health health;
        protected StatusEffectController statusController;
        protected Transform target; // nearest player, assigned by spawner/aggro system
        private float attackTimer;

        [Header("Death")]
        [Tooltip("Delay before the GameObject is destroyed after dying, so LootDropper (and later, a death animation) has a moment to run first.")]
        public float destroyDelayAfterDeath = 1.5f;

        // Loaded via Resources rather than a serialized Sprite field: everything here is
        // spawned purely in code (no prefabs -- see DefaultContentFactory), so subclasses
        // set the path before calling base.Awake() instead of wiring a reference in the
        // Inspector. Set spriteScale/spriteHeight the same way if the default doesn't fit.
        protected string spriteResourcePath;
        // Sprites are imported at 100px/unit (Unity's default), so a 1x scale already
        // matches each source PNG's natural size -- no extra multiplier needed by default.
        protected float spriteScale = 1f;
        protected float spriteHeight = 1f;
        protected float healthBarHeight = 2f;
        protected float healthBarWidth = 1.2f;
        private SpriteAnimator spriteAnimator;

        [Header("Separation")]
        [Tooltip("Nothing here has a Rigidbody -- movement is direct transform manipulation, so Unity's physics never pushes overlapping enemies apart on its own. This radius/strength substitute for that.")]
        public float separationRadius = 1.0f;
        public float separationStrength = 1.5f;

        [Header("Weave")]
        [Tooltip("Sideways wobble while closing distance, so approaches read as alive movement instead of a dead-straight beeline. Fades out near attack range so enemies still commit to landing hits.")]
        public float weaveAmount = 0.5f;
        public float weaveSpeed = 2.5f;
        private float weavePhase;

        protected virtual void Awake()
        {
            weavePhase = Random.Range(0f, 100f); // desyncs multiple enemies weaving in lockstep
            health = GetComponent<Health>();
            statusController = GetComponent<StatusEffectController>();
            statusController.health = health;
            health.statusController = statusController;
            health.OnDeath += HandleDeath;
            gameObject.AddComponent<HealthVFX>(); // floating damage numbers + hit flash

            // AoE abilities use Physics.OverlapSphere, which needs a collider to detect this object.
            if (GetComponent<Collider>() == null)
            {
                var col = gameObject.AddComponent<CapsuleCollider>();
                col.height = 2f;
                col.radius = 0.4f;
            }

            if (!string.IsNullOrEmpty(spriteResourcePath))
            {
                var sprite = Resources.Load<Sprite>(spriteResourcePath);
                if (sprite != null)
                {
                    var sr = SpriteVisual.Attach(transform, sprite, new Vector3(0, spriteHeight, 0), spriteScale);
                    if (sr != null) spriteAnimator = sr.GetComponent<SpriteAnimator>();
                }
            }

            EnemyHealthBar.Attach(transform, health, new Vector3(0, healthBarHeight, 0), healthBarWidth);
        }

        protected virtual void OnDestroy()
        {
            if (health != null) health.OnDeath -= HandleDeath;
        }

        // "Downed" (health.IsDowned == true) already halts Update's move/attack logic below,
        // so this just handles cleanup: enemies don't get revived like players do, they're
        // done for good, so remove them from the scene once other OnDeath subscribers
        // (LootDropper etc.) have had a chance to run.
        protected virtual void HandleDeath()
        {
            Destroy(gameObject, destroyDelayAfterDeath);
        }

        protected virtual void Update()
        {
            if (health.IsDowned || target == null) return;
            if (statusController.IsParalyzed) return; // enemies can be paralyzed too

            float dist = Vector3.Distance(transform.position, target.position);
            if (dist > attackRange)
            {
                MoveTowardTarget();
            }
            else
            {
                attackTimer -= Time.deltaTime;
                if (attackTimer <= 0f)
                {
                    Attack();
                    // Jittered rather than a fixed interval -- multiple enemies on the same
                    // cooldown otherwise swing in lockstep, which reads as predictable/gameable.
                    attackTimer = attackCooldown * Random.Range(0.85f, 1.15f);
                }
            }
        }

        protected virtual void MoveTowardTarget()
        {
            float speedMod = statusController.HasEffect(StatusEffectType.Weaken) ? 0.6f : 1f;
            Vector3 toTarget = target.position - transform.position;
            float dist = toTarget.magnitude;
            Vector3 dirToTarget = dist > 0.001f ? toTarget / dist : transform.forward;

            // Sideways weave that fades out near attack range -- less predictable than a
            // dead-straight approach, but still commits to closing the distance instead of
            // orbiting forever.
            Vector3 perpendicular = new Vector3(-dirToTarget.z, 0, dirToTarget.x);
            float weaveFade = Mathf.Clamp01((dist - attackRange) / 4f);
            float weave = Mathf.Sin((Time.time + weavePhase) * weaveSpeed) * weaveAmount * weaveFade;

            Vector3 dir = (dirToTarget + perpendicular * weave + ComputeSeparation() * separationStrength).normalized;
            transform.position += dir * moveSpeed * speedMod * Time.deltaTime;
        }

        // Pushes away from any other EnemyBase within separationRadius, weighted stronger
        // the closer they are. Cheap enough at this enemy count (a handful per encounter)
        // via OverlapSphere every frame per enemy; revisit if that count ever grows a lot.
        private Vector3 ComputeSeparation()
        {
            Vector3 push = Vector3.zero;
            var nearby = Physics.OverlapSphere(transform.position, separationRadius);
            foreach (var col in nearby)
            {
                if (col.gameObject == gameObject) continue;
                var other = col.GetComponentInParent<EnemyBase>();
                if (other == null) continue;

                Vector3 away = transform.position - other.transform.position;
                float dist = away.magnitude;
                if (dist > 0.001f) push += away.normalized / dist;
            }
            return push;
        }

        protected virtual void Attack()
        {
            float dmgMod = statusController.HasEffect(StatusEffectType.Weaken) ? 0.7f : 1f;
            target.GetComponent<IHealth>()?.TakeDamage(attackDamage * dmgMod, ignoreDef: false);
            if (spriteAnimator != null) spriteAnimator.PulseAttack();
        }

        public void SetTarget(Transform t) => target = t;
    }
}
