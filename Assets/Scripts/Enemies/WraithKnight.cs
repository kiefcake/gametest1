using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Enemies
{
    // The Wraithbound Sanctum's basic melee enemy -- ImpDemon/FrostSkeleton/BogLurker/
    // PitSnake's structural twin (same EnemyBase move-or-attack loop, same on-hit-chance-
    // to-debuff shape) but applies Curse: this dungeon's half of its Curse+Weaken combo
    // gimmick (see SpecterCaster for the other half, and SunderedLord for both at once).
    public class WraithKnight : EnemyBase
    {
        public float curseChance = 0.35f;
        public float curseDuration = 5f;
        public float curseMagnitude = 0.3f; // +30% damage taken from all sources

        protected override void Awake()
        {
            enemyName = "Wraith Knight";
            healthBarHeight = 1.9f;

            base.Awake();
        }

        // Hunched, armored humanoid -- no real mesh exists for this dungeon yet (unlike
        // the first four), so this stays on the ProceduralMonster placeholder tier.
        protected override void AttachVisual()
        {
            var built = ProceduralMonster.Humanoid(transform, new ProceduralMonster.HumanoidSpec
            {
                bodyColor = new Color(0.25f, 0.18f, 0.32f),
                accentColor = new Color(0.55f, 0.9f, 0.45f), // sickly cursed-green glow against the dark purple armor
                scale = 1f, horns = false, weapon = true, hunched = true
            });
            visualRenderers = built.renderers;
            spriteAnimator = built.root.gameObject.AddComponent<SpriteAnimator>();
            spriteAnimator.bobHeight = 0.05f;
            spriteAnimator.bobSpeed = 2.8f;
            AttachLimbAnimator(built);
        }

        protected override void Attack()
        {
            base.Attack();
            if (Random.value < curseChance)
            {
                target.GetComponent<StatusEffectController>()?.ApplyEffect(
                    StatusEffectType.Curse, curseDuration, curseMagnitude);
            }
        }
    }
}
