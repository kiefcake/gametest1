using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Enemies
{
    // The Frozen Crypt's basic melee enemy -- ImpDemon's structural twin (same EnemyBase
    // move-or-attack loop, same on-hit-chance-to-debuff shape) but applies ArmorBreak
    // instead of Bleed, so the two dungeons' trash mobs punish differently: an Abyss fight
    // bleeds you out over time, a Crypt fight makes every subsequent hit land harder.
    public class FrostSkeleton : EnemyBase
    {
        public float armorBreakChance = 0.35f;
        public float armorBreakDuration = 5f;
        public float armorBreakMagnitude = 0.5f; // +50% damage taken, matching Knight's Shield Slam's own value

        protected override void Awake()
        {
            enemyName = "Frost Skeleton";
            healthBarHeight = 1.9f;

            base.Awake();
        }

        // Hunched, no horns -- bone color alone carries the icy/skeletal read, distinct
        // from the Abyss imps' warm tones.
        protected override void AttachVisual()
        {
            var built = ProceduralMonster.Humanoid(transform, new ProceduralMonster.HumanoidSpec {
                bodyColor = new Color(0.85f, 0.92f, 0.98f),
                accentColor = new Color(0.6f, 0.8f, 1f),
                scale = 0.95f, horns = false, weapon = false, hunched = true
            });
            visualRenderers = built.renderers;
            spriteAnimator = built.root.gameObject.AddComponent<SpriteAnimator>();
            spriteAnimator.bobHeight = 0.05f;
            spriteAnimator.bobSpeed = 3f;
        }

        protected override void Attack()
        {
            base.Attack();
            if (Random.value < armorBreakChance)
            {
                target.GetComponent<StatusEffectController>()?.ApplyEffect(
                    StatusEffectType.ArmorBreak, armorBreakDuration, armorBreakMagnitude);
            }
        }
    }
}
