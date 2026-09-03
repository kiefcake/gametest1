using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Enemies
{
    // Snake Pit's basic melee trash mob -- a numerous, low-individual-threat swarmer per
    // the source dungeon's design, so it undercuts ImpDemon's baseline HP rather than
    // matching it. Reuses EnemyBase's own move-or-attack loop unchanged, same shape as
    // ImpDemon.
    public class PitSnake : EnemyBase
    {
        public float bleedChance = 0.3f;
        public float bleedDamage = 3f;
        public float bleedDuration = 4f;

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
        }

        protected override void AttachVisual()
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
