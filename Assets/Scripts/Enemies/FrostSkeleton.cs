using UnityEngine;
using DungeonCrawler.Core;

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
            spriteResourcePath = "Sprites/Enemies/Abyss/imp_demon_spiked"; // no dedicated sprite yet -- icy tint below carries the theme
            spriteHeight = 0.9f;
            healthBarHeight = 1.9f;

            base.Awake();

            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.color = new Color(0.78f, 0.9f, 1f); // pale ice-blue -- distinct from the Abyss imps' warm tones
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
