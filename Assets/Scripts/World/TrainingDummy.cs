using UnityEngine;
using DungeonCrawler.Enemies;

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

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "DummyVisual";
            body.transform.SetParent(transform);
            body.transform.localPosition = new Vector3(0, 1f, 0);
            Destroy(body.GetComponent<Collider>()); // the root's own collider (added by EnemyBase.Awake) is the real one
            var renderer = body.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = new Material(Shader.Find("Standard")) { color = new Color(0.55f, 0.45f, 0.3f) };
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
