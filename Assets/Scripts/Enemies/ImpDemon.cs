using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.Enemies
{
    // The abyss dungeon's basic enemy. Sprite: Sprites/Enemies/Abyss/imp_demon.png
    // (or imp_demon_spiked.png for the tougher variant).
    public class ImpDemon : EnemyBase
    {
        // Was read directly in Awake() to pick sprite/name and apply the damage/HP bump --
        // but every caller (GameBootstrap.SpawnImp included) only ever set this AFTER
        // AddComponent<ImpDemon>(), and AddComponent<T>() runs Awake() synchronously before
        // returning, so Awake() always saw the default `false` regardless of what the
        // caller passed. Every "spiked" imp this project has ever spawned was silently
        // identical to a regular one (found reviewing the open-world feature, which added a
        // second caller with the exact same ordering). Fixed by moving the variant-dependent
        // work out of Awake() entirely, into ApplyVariant() -- called explicitly by the
        // spawner after AddComponent, the same Awake()/Initialize() split CLAUDE.md already
        // documents for exactly this "can't know caller data at Awake time" situation.
        public bool isSpikedVariant = false;
        public float bleedChance = 0.35f;
        public float bleedDamage = 3f;
        public float bleedDuration = 4f;

        protected override void Awake()
        {
            enemyName = "Imp";
            spriteResourcePath = "Sprites/Enemies/Abyss/imp_demon";
            spriteHeight = 0.9f;
            healthBarHeight = 1.9f;

            base.Awake();
        }

        // Call this right after AddComponent<ImpDemon>() to actually get the spiked variant
        // -- setting isSpikedVariant alone no longer does anything, since Awake() has
        // already run and already attached the non-spiked sprite by that point.
        public void ApplyVariant(bool spiked)
        {
            isSpikedVariant = spiked;
            if (!spiked) return;

            enemyName = "Spiked Imp";
            attackDamage *= 1.4f;
            health.maxHP *= 1.3f;
            health.SetCurrentHP(health.maxHP);

            if (spriteRenderer != null)
            {
                var sprite = Resources.Load<Sprite>("Sprites/Enemies/Abyss/imp_demon_spiked");
                if (sprite != null) spriteRenderer.sprite = sprite;
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
