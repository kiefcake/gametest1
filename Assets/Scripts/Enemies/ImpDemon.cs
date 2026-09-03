using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Enemies
{
    // The abyss dungeon's basic enemy. Real imported mesh (Models/Enemies/imp.obj) built
    // the same way demon_imp.obj was -- a script computing lathed/revolved geometry and
    // exporting real OBJ data, not a neural "3D generator." Falls back to the primitive
    // ProceduralMonster.Humanoid build if the resource is ever missing, same defensive
    // shape AbyssFinalDemon.AttachVisual() already uses for its own imported mesh.
    public class ImpDemon : EnemyBase
    {
        private const string ImpModelResourcePath = "Models/Enemies/imp";
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
            healthBarHeight = 1.8f; // the real mesh (~1.5-1.6 units tall) sits a bit shorter than the old primitive build this replaced

            base.Awake();
        }

        protected override void AttachVisual()
        {
            var model = Resources.Load<GameObject>(ImpModelResourcePath);
            if (model == null)
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
                return;
            }

            var modelGO = Instantiate(model, transform);
            modelGO.name = "ImpModel";
            modelGO.transform.localPosition = Vector3.zero;
            modelGO.transform.localRotation = Quaternion.identity;

            visualRenderers = modelGO.GetComponentsInChildren<Renderer>();
            spriteAnimator = modelGO.AddComponent<SpriteAnimator>();
            spriteAnimator.bobHeight = 0.05f;
            spriteAnimator.bobSpeed = 3.5f;
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
