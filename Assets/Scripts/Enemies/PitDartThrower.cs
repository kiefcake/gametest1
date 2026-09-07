using UnityEngine;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Enemies
{
    // Snake Pit's ranged trash mob -- kites at range and lobs darts. Signature attack:
    // Dart Flurry -- every so often it fires a quick 3-dart burst instead of its normal
    // single shot, a lighter-weight escalation than RangedImp's channeled fan volley (no
    // invulnerable freeze, just a faster cadence for a few beats).
    //
    // Fully overrides Update() for the same reason RangedImp does: EnemyBase's own
    // move-or-attack branch can't express "retreat if too close, hold and fire in the
    // sweet spot, approach if too far" on its own.
    public class PitDartThrower : EnemyBase
    {
        public float preferredRange = 6.5f;
        public float retreatRange = 3.5f;
        public float projectileSpeed = 8f;
        public float projectileDamage = 8f;

        [Header("Signature attack: Dart Flurry")]
        public float flurryInterval = 7f;
        public int flurryCount = 3;
        public float flurryShotDelay = 0.15f;
        private float flurryTimer;
        private bool flurrying;
        private int flurryShotsLeft;
        private float flurryShotTimer;

        private float rangedAttackTimer;

        protected override void Awake()
        {
            enemyName = "Snakepit Dart Thrower";
            healthBarHeight = 1.2f;
            moveSpeed = 1.5f;

            base.Awake();

            attackCooldown = 1.8f;
            attackDamage = 0f; // never melees -- Attack() is fully overridden below, this just keeps the field honest
            flurryTimer = flurryInterval;
        }

        protected override void AttachVisual()
        {
            var model = Resources.Load<GameObject>("Models/Enemies/dart_thrower");
            if (model == null)
            {
                var built = ProceduralMonster.Serpent(transform, new ProceduralMonster.SerpentSpec
                {
                    bodyColor = new Color(0.25f, 0.45f, 0.2f), // olive green
                    accentColor = new Color(0.9f, 0.3f, 0.15f), // orange-red eyes/tongue, matches its dart's own tint below
                    scale = 0.9f, length = 5f
                });
                visualRenderers = built.renderers;
                spriteAnimator = built.root.gameObject.AddComponent<SpriteAnimator>();
                spriteAnimator.bobHeight = 0.04f;
                spriteAnimator.bobSpeed = 3.5f;
                return;
            }

            var modelGO = Instantiate(model, transform);
            modelGO.name = "DartThrowerModel";
            modelGO.transform.localPosition = Vector3.zero;
            modelGO.transform.localRotation = Quaternion.identity;

            visualRenderers = modelGO.GetComponentsInChildren<Renderer>();
            spriteAnimator = modelGO.AddComponent<SpriteAnimator>();
            spriteAnimator.bobHeight = 0.04f;
            spriteAnimator.bobSpeed = 3.5f;
        }

        protected override void Update()
        {
            if (health.IsDowned || target == null) return;
            if (statusController.IsParalyzed) return;

            if (flurrying)
            {
                flurryShotTimer -= Time.deltaTime;
                if (flurryShotTimer <= 0f)
                {
                    FireDart(projectileDamage * 0.7f);
                    flurryShotsLeft--;
                    flurryShotTimer = flurryShotDelay;
                    if (flurryShotsLeft <= 0) flurrying = false;
                }
                return; // holds position for the whole burst, same as the retreat/approach gate below skipping while flurrying
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

            flurryTimer -= Time.deltaTime;
            if (flurryTimer <= 0f)
            {
                flurrying = true;
                flurryShotsLeft = flurryCount;
                flurryShotTimer = 0f;
                flurryTimer = flurryInterval * Random.Range(0.85f, 1.15f);
            }
        }

        protected override void Attack() => FireDart(projectileDamage);

        private void FireDart(float damage)
        {
            if (target == null) return;
            Vector3 origin = transform.position + Vector3.up;
            Vector3 dir = (target.position + Vector3.up) - origin;
            Projectile.Spawn(origin, dir, projectileSpeed, damage, new Color(0.9f, 0.3f, 0.15f));
            spriteAnimator?.PulseAttack();
        }
    }
}
