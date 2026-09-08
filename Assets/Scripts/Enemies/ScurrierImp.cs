using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Enemies
{
    // Weak-but-fast swarmer -- dies in a couple of hits but closes distance more than
    // twice as fast as a regular imp and attacks nearly three times as often, so it reads
    // as a real threat to ignore rather than free experience. Reuses EnemyBase's own
    // move-or-attack loop for normal melee, layering one signature move on top: Pounce,
    // a short telegraphed lunge that closes the last stretch of distance in a burst
    // instead of a walk, fitting its "long spindly limbs" build.
    public class ScurrierImp : EnemyBase
    {
        [Header("Signature attack: Pounce")]
        public float pounceRange = 3.5f;
        public float pounceCooldown = 5f;
        public float pounceSpeed = 14f;
        public float pounceDamageMultiplier = 2.2f;
        private const float PounceDuration = 0.22f;
        private float pounceTimer;
        private bool pouncing;
        private float pounceElapsed;
        private Vector3 pounceDir;

        protected override void Awake()
        {
            enemyName = "Imp Scurrier";
            healthBarHeight = 1.5f;
            healthBarWidth = 0.8f;

            moveSpeed = 4.2f; // more than double ImpDemon's default 2
            attackDamage = 3f;
            attackCooldown = 0.7f;
            attackRange = 1f;
            weaveAmount = 0.9f; // extra jittery -- harder to land a clean hit on than its lumbering approach would suggest
            weaveSpeed = 5f;

            base.Awake();

            health.maxHP *= 0.35f;
            health.SetCurrentHP(health.maxHP);
            pounceTimer = pounceCooldown * 0.5f; // first pounce comes sooner than a full cooldown
        }

        protected override void AttachVisual()
        {
            var model = Resources.Load<GameObject>("Models/Enemies/imp_scurrier_body");
            if (model == null)
            {
                var built = ProceduralMonster.Humanoid(transform, new ProceduralMonster.HumanoidSpec
                {
                    bodyColor = new Color(0.55f, 1f, 0.55f), // sickly green -- distinct from the regular/spiked imps' warm tones at a glance
                    accentColor = new Color(0.3f, 0.6f, 0.3f),
                    scale = 0.7f, horns = false, weapon = false, hunched = true
                });
                visualRenderers = built.renderers;
                spriteAnimator = built.root.gameObject.AddComponent<SpriteAnimator>();
                spriteAnimator.bobHeight = 0.05f;
                spriteAnimator.bobSpeed = 5f;
                AttachLimbAnimator(built);
                return;
            }

            var modelGO = Instantiate(model, transform);
            modelGO.name = "ScurrierModel";
            modelGO.transform.localPosition = Vector3.zero;
            modelGO.transform.localRotation = Quaternion.identity;

            visualRenderers = modelGO.GetComponentsInChildren<Renderer>();
            spriteAnimator = modelGO.AddComponent<SpriteAnimator>();
            spriteAnimator.bobHeight = 0.05f;
            spriteAnimator.bobSpeed = 5f;
            AttachImportedMeshRig(modelGO.transform, "Models/Enemies/imp_scurrier");
        }

        protected override void Update()
        {
            if (pouncing)
            {
                pounceElapsed += Time.deltaTime;
                Move(pounceDir * pounceSpeed * Time.deltaTime);
                if (pounceElapsed >= PounceDuration)
                {
                    pouncing = false;
                    if (target != null && Vector3.Distance(transform.position, target.position) <= attackRange * 1.6f)
                    {
                        target.GetComponent<IHealth>()?.TakeDamage(attackDamage * pounceDamageMultiplier, ignoreDef: false);
                        spriteAnimator?.PulseAttack();
                    }
                }
                return; // committed to the lunge for its whole short duration
            }

            base.Update();
            if (health.IsDowned || target == null) return;

            pounceTimer -= Time.deltaTime;
            float dist = Vector3.Distance(transform.position, target.position);
            if (pounceTimer <= 0f && dist > attackRange && dist <= pounceRange)
            {
                pouncing = true;
                pounceElapsed = 0f;
                pounceDir = (target.position - transform.position).normalized;
                pounceTimer = pounceCooldown * Random.Range(0.85f, 1.15f);
                spriteAnimator?.PulseAttack(); // crouch-flash reads as the pounce's own brief tell
            }
        }
    }
}
