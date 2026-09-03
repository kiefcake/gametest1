using UnityEngine;
using DungeonCrawler.Enemies;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.World
{
    // A safe, un-killable target in the hub for trying out abilities/auto-attack without
    // consequence. Subclasses EnemyBase (rather than reinventing Health/StatusEffectController
    // wiring, the health bar, or hit VFX) so it's a valid target for EnemyTargeting/AbilityCaster
    // for free -- both explicitly look for an EnemyBase component. Never getting an
    // AggroController means `target` stays null forever, so EnemyBase.Update()'s move/attack
    // path never fires; it just stands there, which is exactly what a dummy should do.
    public class TrainingDummy : EnemyBase
    {
        public float reviveDelay = 1.2f;
        private float reviveTimer;
        private bool waitingToRevive;

        protected override void Awake()
        {
            enemyName = "Training Dummy";
            healthBarHeight = 2.2f;
            healthBarWidth = 1.4f;
            attackDamage = 0f;

            base.Awake();

            health.maxHP = 500f;
            health.SetCurrentHP(health.maxHP);
        }

        // Used to build its own capsule directly in Awake() instead of overriding this --
        // harmless for movement (a dummy never moves), but it meant visualRenderers/
        // spriteAnimator were never set, so HealthVFX's hit-flash silently never worked on
        // it either. Same Humanoid archetype every other humanoid uses now.
        protected override void AttachVisual()
        {
            var built = ProceduralMonster.Humanoid(transform, new ProceduralMonster.HumanoidSpec
            {
                bodyColor = new Color(0.55f, 0.45f, 0.3f),
                accentColor = new Color(0.75f, 0.65f, 0.4f),
                scale = 1f, horns = false, weapon = false, hunched = false
            });
            visualRenderers = built.renderers;
            spriteAnimator = built.root.gameObject.AddComponent<SpriteAnimator>();
            spriteAnimator.bobHeight = 0.02f; // barely any bob -- an inanimate practice dummy, not a living creature
            spriteAnimator.bobSpeed = 1f;
        }

        protected override void HandleDeath()
        {
            // EnemyBase.HandleDeath destroys the GameObject -- a dummy should reset instead
            // of being a one-shot kill, so it's always available to test on.
            waitingToRevive = true;
            reviveTimer = reviveDelay;
        }

        protected override void Update()
        {
            if (waitingToRevive)
            {
                reviveTimer -= Time.deltaTime;
                if (reviveTimer <= 0f)
                {
                    waitingToRevive = false;
                    health.Revive(1f);
                }
                return;
            }

            base.Update(); // no-ops on move/attack since target is always null (no AggroController)
        }

        protected override void Attack() { } // never reached (target is always null), guarded anyway
    }
}
