using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Visuals;

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
            healthBarHeight = 2.1f;

            base.Awake();
        }

        protected override void AttachVisual()
        {
            var built = ProceduralMonster.Humanoid(transform, new ProceduralMonster.HumanoidSpec
            {
                bodyColor = new Color(0.75f, 0.25f, 0.15f),
                accentColor = new Color(0.15f, 0.05f, 0.05f),
                scale = 1f, horns = true, weapon = false, hunched = false
            });
            visualRenderers = built.renderers;
            spriteAnimator = built.root.gameObject.AddComponent<SpriteAnimator>();
            spriteAnimator.bobHeight = 0.06f;
            spriteAnimator.bobSpeed = 3.5f;
            AttachLimbAnimator(built);
        }

        // Call this right after AddComponent<ImpDemon>() to actually get the spiked variant
        // -- setting isSpikedVariant alone no longer does anything, since Awake() has
        // already run and already attached the non-spiked model by that point.
        public void ApplyVariant(bool spiked)
        {
            isSpikedVariant = spiked;
            if (!spiked) return;

            enemyName = "Spiked Imp";
            attackDamage *= 1.4f;
            health.maxHP *= 1.3f;
            health.SetCurrentHP(health.maxHP);

            // No separate spiked model -- darken/redden the existing one in place instead of rebuilding it.
            if (visualRenderers != null)
            {
                foreach (var r in visualRenderers)
                    if (r != null) r.material.color = Color.Lerp(r.material.color, new Color(0.35f, 0f, 0f), 0.35f);
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
