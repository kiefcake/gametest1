using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Enemies
{
    // Snake Pit's basic melee trash mob -- a numerous, low-individual-threat swarmer per
    // the source dungeon's design, so it undercuts ImpDemon's baseline HP rather than
    // matching it. Reuses EnemyBase's own move-or-attack loop for normal hits, layering
    // one signature move on top: Coil Strike -- every few attacks it holds still for a
    // brief telegraphed beat (a real snake coiling before it lunges) then bites for bonus
    // damage and a guaranteed, stronger Bleed instead of the normal on-hit chance.
    public class PitSnake : EnemyBase
    {
        public float bleedChance = 0.3f;
        public float bleedDamage = 3f;
        public float bleedDuration = 4f;

        [Header("Signature attack: Coil Strike")]
        public float coilInterval = 6f;
        public float coilTelegraphTime = 0.5f;
        public float coilDamageMultiplier = 1.8f;
        private float coilTimer;
        private bool coiling;
        private float coilElapsed;

        protected override void Awake()
        {
            enemyName = "Pit Snake";
            healthBarHeight = 1.2f; // lies close to the ground, unlike an upright humanoid

            moveSpeed = 2.2f;
            attackDamage = 7f;
            attackRange = 1.3f;
            attackCooldown = 1.3f;

            base.Awake();

            health.maxHP *= 0.7f; // weaker than ImpDemon -- meant to swarm in numbers, not tank
            health.SetCurrentHP(health.maxHP);
            coilTimer = coilInterval * 0.5f;
        }

        protected override void AttachVisual()
        {
            var model = Resources.Load<GameObject>("Models/Enemies/pit_snake");
            if (model == null)
            {
                var built = ProceduralMonster.Serpent(transform, new ProceduralMonster.SerpentSpec
                {
                    bodyColor = new Color(0.42f, 0.32f, 0.16f), // dusty brown, matches this dungeon's earthy palette
                    accentColor = new Color(0.85f, 0.75f, 0.2f), // pale yellow-gold eyes/tongue
                    scale = 1f, length = 6f
                });
                visualRenderers = built.renderers;
                spriteAnimator = built.root.gameObject.AddComponent<SpriteAnimator>();
                spriteAnimator.bobHeight = 0.04f;
                spriteAnimator.bobSpeed = 4f;
                return;
            }

            var modelGO = Instantiate(model, transform);
            modelGO.name = "PitSnakeModel";
            modelGO.transform.localPosition = Vector3.zero;
            modelGO.transform.localRotation = Quaternion.identity;

            visualRenderers = modelGO.GetComponentsInChildren<Renderer>();
            spriteAnimator = modelGO.AddComponent<SpriteAnimator>();
            spriteAnimator.bobHeight = 0.04f;
            spriteAnimator.bobSpeed = 4f;
        }

        protected override void Update()
        {
            if (coiling)
            {
                coilElapsed += Time.deltaTime;
                if (coilElapsed >= coilTelegraphTime)
                {
                    coiling = false;
                    if (target != null && Vector3.Distance(transform.position, target.position) <= attackRange * 1.3f)
                    {
                        target.GetComponent<IHealth>()?.TakeDamage(attackDamage * coilDamageMultiplier, ignoreDef: false);
                        target.GetComponent<StatusEffectController>()?.ApplyEffect(
                            StatusEffectType.Bleed, bleedDuration * 1.5f, bleedDamage * 1.5f);
                        spriteAnimator?.PulseAttack();
                    }
                }
                return; // held coil, frozen for the whole telegraph
            }

            base.Update();
            if (health.IsDowned || target == null) return;

            coilTimer -= Time.deltaTime;
            float dist = Vector3.Distance(transform.position, target.position);
            if (coilTimer <= 0f && dist <= attackRange * 1.3f)
            {
                coiling = true;
                coilElapsed = 0f;
                coilTimer = coilInterval * Random.Range(0.85f, 1.15f);
            }
        }

        protected override void Attack()
        {
            base.Attack();
            if (Random.value < bleedChance)
            {
                target.GetComponent<StatusEffectController>()?.ApplyEffect(
                    StatusEffectType.Bleed, bleedDuration, bleedDamage);
            }
        }
    }
}
