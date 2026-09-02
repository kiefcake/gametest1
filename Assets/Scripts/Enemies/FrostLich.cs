using UnityEngine;
using DungeonCrawler.Audio;
using DungeonCrawler.Core;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Enemies
{
    // The Frozen Crypt's boss -- same channeled-telegraph-AoE shape as AbyssFinalDemon's
    // slam (stand in the glowing circle when it resolves and it hurts regardless of DEF,
    // walking out during the channel avoids it entirely) plus the same enrage-at-half-HP
    // beat, kept as its own independent class rather than sharing a base with the Abyss
    // boss -- each boss stays separately tunable, matching how ImpDemon/RangedImp/AbyssMage
    // are already independent siblings under EnemyBase rather than sharing extra layers.
    public class FrostLich : EnemyBase
    {
        [Header("Phase 2 trigger")]
        public float phase2HpFraction = 0.5f;
        private bool inPhase2;

        [Header("Phase 2: enrage")]
        public float enrageDamageMultiplier = 1.7f;
        public float enrageAttackSpeedMultiplier = 1.4f;

        [Header("Special: channeled frost nova")]
        public float specialInterval = 10f;
        public float specialChannelTime = 1.6f;
        public float specialRadius = 5f;
        public float specialDamage = 40f;
        private float specialTimer;
        private bool channeling;
        private float channelElapsed;
        private GameObject telegraphGO;

        private static readonly Color TelegraphStart = new Color(0.3f, 0.55f, 0.8f);
        private static readonly Color TelegraphEnd = new Color(0.85f, 0.95f, 1f);

        protected override void Awake()
        {
            enemyName = "Frost Lich";
            // The model's own bounding box is taller than the old sprite -- bumped so the
            // health bar clears the model's head, same reasoning as AbyssFinalDemon's own
            // sprite-to-model conversion (3.3 -> 3.6).
            healthBarHeight = 3.6f;
            healthBarWidth = 2.2f;

            base.Awake();

            attackCooldown = 2.2f;
            attackDamage = 16f;
            specialTimer = specialInterval;
        }

        // Floating caster rather than Humanoid -- a lich reads better as a hovering
        // channeler than a planted brute, and it visually distinguishes this boss from
        // SwampWarden's hulking Humanoid silhouette.
        protected override void AttachVisual()
        {
            var built = ProceduralMonster.FloatingCaster(transform, new ProceduralMonster.FloatingSpec {
                robeColor = new Color(0.55f, 0.75f, 0.95f),
                accentColor = new Color(0.85f, 0.95f, 1f),
                scale = 1.9f, orb = true
            });
            visualRenderers = built.renderers;
            spriteAnimator = built.root.gameObject.AddComponent<SpriteAnimator>();
            spriteAnimator.bobHeight = 0.07f;
            spriteAnimator.bobSpeed = 1.6f;
        }

        protected override void Update()
        {
            if (health.IsDowned) return;

            if (channeling)
            {
                TickSpecialAttack();
                return; // frozen in place for the whole channel, same as the Abyss boss's slam
            }

            base.Update();
            if (health.IsDowned) return;

            if (!inPhase2 && health.CurrentHP <= health.maxHP * phase2HpFraction)
                EnterPhase2();

            specialTimer -= Time.deltaTime;
            if (specialTimer <= 0f && target != null)
                BeginSpecialAttack();
        }

        private void EnterPhase2()
        {
            inPhase2 = true;
            attackDamage *= enrageDamageMultiplier;
            attackCooldown /= enrageAttackSpeedMultiplier;

            // RotMG Shatters-style escalation: phase 2 doesn't just buff the boss, it makes
            // the arena itself worse -- fixed at the boss's position now, not following it.
            Vector3 origin = transform.position;
            HazardVisuals.SpawnPatch(transform.parent, origin + new Vector3(4f, 0, 3f), 2.5f,
                new Color(0.55f, 0.85f, 1f), new Color(0.35f, 0.65f, 0.9f), new Color(0.75f, 0.95f, 1f));
            HazardVisuals.SpawnPatch(transform.parent, origin + new Vector3(-4f, 0, -3f), 2.5f,
                new Color(0.55f, 0.85f, 1f), new Color(0.35f, 0.65f, 0.9f), new Color(0.75f, 0.95f, 1f));
        }

        private void BeginSpecialAttack()
        {
            channeling = true;
            channelElapsed = 0f;
            specialTimer = specialInterval;
            SetInvulnerable(true); // frozen-in-place channel is the telegraph -- now backed by actual damage immunity

            telegraphGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            telegraphGO.name = "FrostNovaTelegraph";
            var col = telegraphGO.GetComponent<Collider>();
            if (col != null) Destroy(col);
            telegraphGO.transform.position = transform.position + Vector3.up * 0.05f;
            telegraphGO.transform.localScale = new Vector3(specialRadius * 2f, 0.01f, specialRadius * 2f);
            var renderer = telegraphGO.GetComponent<Renderer>();
            if (renderer != null) renderer.material = new Material(Shader.Find("Standard")) { color = TelegraphStart };

            SfxLibrary.PlayAt(SfxLibrary.Warning, transform.position, 0.5f);
        }

        private void TickSpecialAttack()
        {
            channelElapsed += Time.deltaTime;
            if (telegraphGO != null)
            {
                float t = Mathf.Clamp01(channelElapsed / specialChannelTime);
                var renderer = telegraphGO.GetComponent<Renderer>();
                if (renderer != null) renderer.material.color = Color.Lerp(TelegraphStart, TelegraphEnd, t);
            }

            if (channelElapsed >= specialChannelTime) ResolveSpecialAttack();
        }

        private void ResolveSpecialAttack()
        {
            if (telegraphGO != null) Destroy(telegraphGO);
            channeling = false;
            SetInvulnerable(false);

            var hits = Physics.OverlapSphere(transform.position, specialRadius);
            foreach (var hit in hits)
            {
                if (hit.GetComponentInParent<FrostLich>() != null) continue; // don't hit itself
                hit.GetComponentInParent<IHealth>()?.TakeDamage(specialDamage, ignoreDef: true);
            }

            ImpactBurst.Spawn(transform.position + Vector3.up, new Color(0.6f, 0.85f, 1f));
        }

        protected override void Attack()
        {
            float mitigation = statusController.HasEffect(StatusEffectType.Weaken) ? 0.6f : 1f;
            target.GetComponent<IHealth>()?.TakeDamage(attackDamage * mitigation, ignoreDef: false);
        }

        protected override void HandleDeath()
        {
            if (telegraphGO != null) Destroy(telegraphGO);
            base.HandleDeath();
        }
    }
}
