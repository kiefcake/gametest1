using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Classes;
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
        // Movement used to be raw transform.position += -- no collision at all, so enemies
        // could walk straight through walls and off the edge of the floor. Routed through
        // the same CharacterController.Move() sweep the player uses instead.
        protected CharacterController controller;
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
        // protected, not private -- AbyssFinalDemon's AttachVisual() override attaches this
        // same generic bob/pulse animator to a non-sprite (3D mesh) visual and needs to
        // wire it in here so Attack() (below) can still trigger PulseAttack() on it.
        protected SpriteAnimator spriteAnimator;
        // protected -- lets a subclass whose variant isn't decided until after Awake() (see
        // ImpDemon.isSpikedVariant) swap the sprite post-hoc instead of needing to rebuild
        // the whole visual hierarchy.
        protected SpriteRenderer spriteRenderer;
        // Every renderer SetInvulnerable should tint. A billboard sprite is a length-1 array
        // (set by AttachVisual below); a procedural multi-primitive model (see
        // ProceduralMonster) has one per body part -- SetInvulnerable no longer cares which,
        // it just tints whatever this holds.
        protected Renderer[] visualRenderers;
        // Caches the real tint the first time SetInvulnerable(true) runs, so repeated
        // on/off calls (e.g. re-entering a channel) restore the exact original color
        // instead of drifting via double-lerping toward white. Indices line up with
        // visualRenderers.
        private Color[] baseVisualColors;

        [Header("Separation")]
        [Tooltip("Nothing here has a Rigidbody -- movement is direct transform manipulation, so Unity's physics never pushes overlapping enemies apart on its own. This radius/strength substitute for that.")]
        public float separationRadius = 1.0f;
        public float separationStrength = 1.5f;

        [Header("Weave")]
        [Tooltip("Sideways wobble while closing distance, so approaches read as alive movement instead of a dead-straight beeline. Fades out near attack range so enemies still commit to landing hits.")]
        public float weaveAmount = 0.5f;
        public float weaveSpeed = 2.5f;
        private float weavePhase;

        [Header("Facing")]
        [Tooltip("How fast the model turns to face its target, in degrees/second. Lives in LateUpdate rather than Update so it applies uniformly no matter how a subclass's own Update() decides to move or attack -- several (AbyssMage, RangedImp) fully override Update() with no base.Update() call at all, so putting this in Update() itself would have silently skipped them.")]
        public float turnSpeedDegrees = 360f;

        protected virtual void Awake()
        {
            weavePhase = Random.Range(0f, 100f); // desyncs multiple enemies weaving in lockstep
            health = GetComponent<Health>();
            statusController = GetComponent<StatusEffectController>();
            statusController.health = health;
            health.statusController = statusController;
            health.OnDeath += HandleDeath;
            health.OnDamaged += OnDamagedAggro;
            gameObject.AddComponent<HealthVFX>(); // floating damage numbers + hit flash

            // AoE abilities use Physics.OverlapSphere, which needs a collider to detect this
            // object -- a CharacterController satisfies that (it's itself a Collider) while
            // also giving MoveTowardTarget somewhere to route real swept collision through.
            if (GetComponent<Collider>() == null)
            {
                controller = gameObject.AddComponent<CharacterController>();
                controller.height = 2f;
                controller.radius = 0.4f;
                controller.center = new Vector3(0, 1f, 0);
            }
            else
            {
                controller = GetComponent<CharacterController>();
            }

            AttachVisual();

            EnemyHealthBar.Attach(transform, health, new Vector3(0, healthBarHeight, 0), healthBarWidth);
        }

        // The billboard-sprite path every enemy uses by default. Pulled out of Awake() so a
        // subclass with a real (if rig-less) 3D model -- see AbyssFinalDemon -- can replace
        // it entirely instead of the sprite loading silently no-op'ing alongside an unused
        // spriteResourcePath.
        protected virtual void AttachVisual()
        {
            if (string.IsNullOrEmpty(spriteResourcePath)) return;
            var sprite = Resources.Load<Sprite>(spriteResourcePath);
            if (sprite == null) return;

            var sr = SpriteVisual.Attach(transform, sprite, new Vector3(0, spriteHeight, 0), spriteScale);
            if (sr != null) spriteAnimator = sr.GetComponent<SpriteAnimator>();
            spriteRenderer = sr;
            visualRenderers = sr != null ? new Renderer[] { sr } : null;
        }

        // Shared by every AttachVisual() override that builds a ProceduralMonster.Humanoid
        // -- wires its hip/shoulder pivots into a walk-cycle animator on the model root, so
        // the swing tracks THIS enemy's own (non-bobbing) transform rather than the model
        // root SpriteAnimator is busy bobbing. A no-op for the FloatingCaster/Blob
        // archetypes, whose Built has no pivots to wire (all null, which
        // ProceduralLimbAnimator already treats as "nothing to animate there").
        protected void AttachLimbAnimator(ProceduralMonster.Built built)
        {
            var limbAnimator = built.root.gameObject.AddComponent<ProceduralLimbAnimator>();
            limbAnimator.moveTracker = transform;
            limbAnimator.leftHip = built.leftHip;
            limbAnimator.rightHip = built.rightHip;
            limbAnimator.leftShoulder = built.leftShoulder;
            limbAnimator.rightShoulder = built.rightShoulder;
        }

        protected virtual void OnDestroy()
        {
            if (health != null)
            {
                health.OnDeath -= HandleDeath;
                health.OnDamaged -= OnDamagedAggro;
            }
        }

        // "Downed" (health.IsDowned == true) already halts Update's move/attack logic below,
        // so this just handles cleanup: enemies don't get revived like players do, they're
        // done for good, so remove them from the scene once other OnDeath subscribers
        // (LootDropper etc.) have had a chance to run.
        protected virtual void HandleDeath()
        {
            // spriteAnimator's own GameObject IS the model root for every current
            // AttachVisual() override (sprite billboard, AbyssFinalDemon's imported mesh,
            // and every ProceduralMonster archetype alike all AddComponent<SpriteAnimator>
            // directly on it) -- reusing that reference here means the death topple/sink
            // animation works for every enemy in the game without any of their files
            // needing to wire in a new reference themselves.
            if (spriteAnimator != null)
            {
                var deathAnim = spriteAnimator.gameObject.AddComponent<ProceduralDeathAnimator>();
                deathAnim.Play(destroyDelayAfterDeath);
            }
            Destroy(gameObject, destroyDelayAfterDeath);
        }

        // Turns the whole enemy (not just its visual model) to face its target every
        // frame, regardless of whether this frame moved, attacked, or channeled a special
        // -- previously nothing ever rotated an enemy at all, so it could stand fighting
        // you while facing an entirely different direction, which is a big part of why a
        // model with a real face/eyes on it still read as a lifeless mannequin. Routed
        // through LateUpdate (see the Facing header's tooltip) so every subclass gets this
        // for free without needing to remember to call a base method from its own
        // Update() override. RotateTowards rather than a hard snap so a fast-turning
        // target doesn't make the enemy visibly teleport-face; horizontal-only (toTarget.y
        // zeroed) so an enemy on a platform doesn't pitch to stare up/down at a target on
        // a different Y level.
        protected virtual void LateUpdate()
        {
            if (health == null || health.IsDowned || target == null) return;

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f) return;

            Quaternion wanted = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, turnSpeedDegrees * Time.deltaTime);
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
            if (statusController.HasEffect(StatusEffectType.Slow))
                speedMod *= 1f - statusController.GetMagnitude(StatusEffectType.Slow);
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
            Move(dir * moveSpeed * speedMod * Time.deltaTime);
        }

        // Routes through the CharacterController when one exists (the normal case) so
        // movement actually collides with walls/floors; falls back to a raw transform
        // nudge only if some future subclass genuinely has no controller.
        protected void Move(Vector3 delta)
        {
            if (controller != null) controller.Move(delta);
            else transform.position += delta;
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

        // Visually marks an invulnerability window (paired with Health.invulnerable) so the
        // player understands why hits stop registering, instead of a silent damage-immune
        // flag -- tints every renderer in visualRenderers (set by AttachVisual()) since every
        // current caller of this needs both the flag and the tell together.
        protected void SetInvulnerable(bool on)
        {
            if (health != null) health.invulnerable = on;
            if (visualRenderers == null || visualRenderers.Length == 0) return;

            if (on)
            {
                if (baseVisualColors == null)
                {
                    baseVisualColors = new Color[visualRenderers.Length];
                    for (int i = 0; i < visualRenderers.Length; i++)
                        baseVisualColors[i] = visualRenderers[i] != null ? visualRenderers[i].material.color : Color.white;
                }
                for (int i = 0; i < visualRenderers.Length; i++)
                    if (visualRenderers[i] != null)
                        visualRenderers[i].material.color = Color.Lerp(baseVisualColors[i], Color.white, 0.65f); // pale "shielded" tint
            }
            else if (baseVisualColors != null)
            {
                for (int i = 0; i < visualRenderers.Length; i++)
                    if (visualRenderers[i] != null)
                        visualRenderers[i].material.color = baseVisualColors[i];
            }
        }

        public void SetTarget(Transform t) => target = t;
        // Read by AggroController's leash check -- it needs to know the CURRENT target
        // (which it may itself have set to null) without re-deriving it independently.
        public Transform CurrentTarget => target;

        // Getting shot from outside normal aggro range (a sniped Scurrier three rooms
        // over, say) used to never register at all -- only AggroController's periodic
        // proximity rescan could ever set a target. Any damage now pulls aggro straight
        // onto the player if nothing was already being fought, regardless of distance.
        private void OnDamagedAggro(float amount)
        {
            if (target != null || health.IsDowned) return;
            var player = FindFirstObjectByType<PlayerCharacter>(); // solo play -- same shortcut AggroController's own scan already takes
            if (player != null) target = player.transform;
        }
    }
}
