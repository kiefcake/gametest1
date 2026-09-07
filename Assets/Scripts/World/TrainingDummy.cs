using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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
    //
    // Used to just sit at 0 HP for reviveDelay (1.2s) then snap straight back to full --
    // no visible healing at all, and the flat 1.2s "death" window didn't read as much of
    // an AI either way. Now it passively regenerates in small ticks (visible heal numbers
    // via the same Health.OnHealed -> HealthVFX path everything else uses) and only ever
    // has a near-instant stumble if it's actually driven to 0, plus a floating damage/DPS
    // readout so testing an ability's real output doesn't require external tools.
    public class TrainingDummy : EnemyBase
    {
        [Header("Regen")]
        public float regenPerTick = 18f;
        public float regenTickInterval = 0.4f;
        public float reviveDelay = 0.3f; // a stumble, not a death -- was 1.2s of standing empty
        private float regenTimer;
        private float reviveTimer;
        private bool waitingToRevive;

        [Header("Damage meter")]
        public float idleResetSeconds = 3f; // no hits for this long -- the next hit starts a fresh count
        public float dpsWindowSeconds = 5f; // rolling window DPS is averaged over
        private float totalDamage;
        private float lastHitTime = -999f;
        private readonly List<(float time, float amount)> recentHits = new List<(float, float)>();
        private Text meterText;

        protected override void Awake()
        {
            enemyName = "Training Dummy";
            healthBarHeight = 2.2f;
            healthBarWidth = 1.4f;
            attackDamage = 0f;

            base.Awake();

            health.maxHP = 500f;
            health.SetCurrentHP(health.maxHP);
            health.OnDamaged += OnHit;

            BuildDamageMeter();
        }

        protected override void OnDestroy()
        {
            if (health != null) health.OnDamaged -= OnHit;
            base.OnDestroy();
        }

        // Used to build its own capsule directly in Awake() instead of overriding this --
        // harmless for movement (a dummy never moves), but it meant visualRenderers/
        // spriteAnimator were never set, so HealthVFX's hit-flash silently never worked on
        // it either. Same Humanoid archetype every other humanoid uses now.
        protected override void AttachVisual()
        {
            var model = Resources.Load<GameObject>("Models/Enemies/training_dummy");
            if (model == null)
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
                return;
            }

            var modelGO = Instantiate(model, transform);
            modelGO.name = "TrainingDummyModel";
            modelGO.transform.localPosition = Vector3.zero;
            modelGO.transform.localRotation = Quaternion.identity;

            visualRenderers = modelGO.GetComponentsInChildren<Renderer>();
            spriteAnimator = modelGO.AddComponent<SpriteAnimator>();
            spriteAnimator.bobHeight = 0.02f;
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
                    health.Revive(0f); // comes back at 0 HP -- the regen tick below does the
                                       // actual, visible healing back up rather than an instant snap to full
                    // EnemyHealthBar hard-destroys its own GameObject the moment IsDowned
                    // was true (see its LateUpdate) and nothing else ever rebuilds it --
                    // fine for a real enemy (it's gone a moment later anyway), not fine for
                    // a dummy that's meant to keep taking hits indefinitely.
                    EnemyHealthBar.Attach(transform, health, new Vector3(0, healthBarHeight, 0), healthBarWidth);
                }
                return;
            }

            if (!health.IsDowned)
            {
                regenTimer -= Time.deltaTime;
                if (regenTimer <= 0f)
                {
                    regenTimer = regenTickInterval;
                    health.Heal(regenPerTick);
                }
            }

            base.Update(); // no-ops on move/attack since target is always null (no AggroController)
            RefreshMeter();
        }

        protected override void Attack() { } // never reached (target is always null), guarded anyway

        private void OnHit(float amount)
        {
            if (Time.time - lastHitTime > idleResetSeconds)
            {
                totalDamage = 0f;
                recentHits.Clear();
            }
            lastHitTime = Time.time;
            totalDamage += amount;
            recentHits.Add((Time.time, amount));
        }

        private void BuildDamageMeter()
        {
            var go = new GameObject("DamageMeter", typeof(Canvas));
            go.transform.SetParent(transform);
            go.transform.localPosition = new Vector3(0, healthBarHeight + 0.3f, 0);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(2.4f, 0.3f);

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(go.transform, false);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            meterText = textGO.GetComponent<Text>();
            meterText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            meterText.fontSize = 24;
            meterText.alignment = TextAnchor.MiddleCenter;
            meterText.color = new Color(1f, 0.85f, 0.3f);
            meterText.text = "";

            go.AddComponent<BillboardSprite>();
        }

        private void RefreshMeter()
        {
            if (meterText == null) return;

            // Trim hits older than the idle-reset window OR the DPS window, whichever is
            // shorter to keep around -- idle reset already clears everything past
            // idleResetSeconds of silence, so the list never grows unbounded between combos.
            float cutoff = Time.time - dpsWindowSeconds;
            recentHits.RemoveAll(h => h.time < cutoff);

            if (totalDamage <= 0f)
            {
                meterText.text = "";
                return;
            }

            float windowDamage = 0f;
            foreach (var hit in recentHits) windowDamage += hit.amount;
            float dps = windowDamage / dpsWindowSeconds;

            meterText.text = $"Dealt: {totalDamage:0} -- DPS: {dps:0}";
        }
    }
}
