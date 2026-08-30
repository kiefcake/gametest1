using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.Enemies
{
    // The abyss dungeon's basic enemy. Sprite: Sprites/Enemies/Abyss/imp_demon.png
    // (or imp_demon_spiked.png for the tougher variant).
    public class ImpDemon : EnemyBase
    {
        public bool isSpikedVariant = false;
        public float bleedChance = 0.35f;
        public float bleedDamage = 3f;
        public float bleedDuration = 4f;

        protected override void Awake()
        {
            // Sprite path/scale must be set before base.Awake() -- that's where EnemyBase
            // actually loads and attaches the visual.
            enemyName = isSpikedVariant ? "Spiked Imp" : "Imp";
            spriteResourcePath = isSpikedVariant
                ? "Sprites/Enemies/Abyss/imp_demon_spiked"
                : "Sprites/Enemies/Abyss/imp_demon";
            spriteHeight = 0.9f;
            healthBarHeight = 1.9f;

            base.Awake();

            if (isSpikedVariant)
            {
                attackDamage *= 1.4f;
                health.maxHP *= 1.3f;
                health.SetCurrentHP(health.maxHP);
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
