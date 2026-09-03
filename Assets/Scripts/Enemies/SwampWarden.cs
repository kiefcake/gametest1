using UnityEngine;
using DungeonCrawler.Audio;
using DungeonCrawler.Core;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Enemies
{
    // Sunken Ruins' boss -- same "role-check boss template" shape as AbyssFinalDemon: a
    // Phase-1 tank-check add-spawn (Bog Lurkers instead of Imps), a Phase-2 enrage trigger
    // (Buffer check), and a channeled telegraphed AoE special attack (a toxic cloud burst
    // instead of a ground slam/frost nova). Its own Attack() also rolls a chance to apply
    // Poison -- unlike the other two bosses' plain-damage Attack(), this is the Warden's
    // signature mechanic: the boss-scale escalation of BogLurker's own Poison gimmick.
    public class SwampWarden : EnemyBase
    {
        [Header("Phase 2 trigger")]
        public float phase2HpFraction = 0.5f;
        private bool inPhase2 = false;

        [Header("Phase 1: add spawns (Tank check)")]
        public float addSpawnInterval = 12f;
        private float addSpawnTimer;

        [Header("Phase 2: enrage (Buffer check)")]
        public float enrageDamageMultiplier = 1.8f;
        public float enrageAttackSpeedMultiplier = 1.5f;

        [Header("Signature attack: Poison")]
        public float poisonChance = 0.4f;
        public float poisonDuration = 5f;
        public float poisonDamage = 4f; // a bit harder-hitting than BogLurker's own 3/tick -- boss-scale escalation of the same gimmick

        [Header("Special: channeled toxic cloud burst")]
        [Tooltip("The warden plants itself and telegraphs a toxic burst -- stand in the glowing circle when it resolves and it hurts regardless of DEF. Walking out during the channel avoids it entirely.")]
        public float specialInterval = 11f;
        public float specialChannelTime = 1.8f;
        public float specialRadius = 4.5f;
        public float specialDamage = 45f;
        private float specialTimer;
        private bool channeling;
        private float channelElapsed;
        private GameObject telegraphGO;

        private static readonly Color TelegraphStart = new Color(0.2f, 0.35f, 0.1f);
        private static readonly Color TelegraphEnd = new Color(0.65f, 0.85f, 0.25f);

        protected override void Awake()
        {
            enemyName = "Swamp Warden";
            healthBarHeight = 4.4f;
            healthBarWidth = 2.6f;

            base.Awake();

            attackCooldown = 2f;
            attackDamage = 18f;
            specialTimer = specialInterval;
            addSpawnTimer = addSpawnInterval; // was defaulting to 0 -- fired an add wave on the very first Update() frame instead of waiting out the interval
        }

        // Hulking melee brute at boss scale -- Humanoid archetype, distinct from FrostLich's
        // floating caster. SetInvulnerable's channel tell (BeginSpecialAttack/
        // ResolveSpecialAttack below) depends on visualRenderers being set here.
        protected override void AttachVisual()
        {
            var built = ProceduralMonster.Humanoid(transform, new ProceduralMonster.HumanoidSpec
            {
                bodyColor = new Color(0.3f, 0.45f, 0.25f),
                accentColor = new Color(0.55f, 0.4f, 0.15f),
                scale = 2.1f, horns = true, weapon = false, hunched = false
            });
            visualRenderers = built.renderers;
            spriteAnimator = built.root.gameObject.AddComponent<SpriteAnimator>();
            spriteAnimator.bobHeight = 0.06f;
            spriteAnimator.bobSpeed = 1.3f;
            AttachLimbAnimator(built);
        }

        protected override void Update()
        {
            if (health.IsDowned) return;

            if (channeling)
            {
                TickSpecialAttack();
                return; // frozen in place for the whole channel -- no movement, no normal attacks
            }

            base.Update();
            // base.Update() early-returns on death but doesn't stop OUR override from
            // continuing past it -- without this guard, phase transition and add-spawning
            // would keep running for the ~1.5s destroy delay after the boss is already dead.
            if (health.IsDowned) return;

            if (!inPhase2 && health.CurrentHP <= health.maxHP * phase2HpFraction)
            {
                EnterPhase2();
            }

            if (!inPhase2)
            {
                addSpawnTimer -= Time.deltaTime;
                if (addSpawnTimer <= 0f)
                {
                    SpawnAdds();
                    addSpawnTimer = addSpawnInterval;
                }
            }

            specialTimer -= Time.deltaTime;
            if (specialTimer <= 0f && target != null)
            {
                BeginSpecialAttack();
            }
        }

        private void BeginSpecialAttack()
        {
            channeling = true;
            channelElapsed = 0f;
            specialTimer = specialInterval;
            SetInvulnerable(true); // frozen-in-place channel is the telegraph -- now backed by actual damage immunity

            telegraphGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            telegraphGO.name = "ToxicBurstTelegraph";
            var col = telegraphGO.GetComponent<Collider>();
            if (col != null) Destroy(col); // warning marker only -- ResolveSpecialAttack does the actual hit detection
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

            if (channelElapsed >= specialChannelTime)
            {
                ResolveSpecialAttack();
            }
        }

        private void ResolveSpecialAttack()
        {
            if (telegraphGO != null) Destroy(telegraphGO);
            channeling = false;
            SetInvulnerable(false);

            var hits = Physics.OverlapSphere(transform.position, specialRadius);
            foreach (var hit in hits)
            {
                if (hit.GetComponentInParent<SwampWarden>() != null) continue; // don't hit itself
                // ignoreDef -- a boss burst should always punish standing in it, that's the
                // entire point of making the player dodge instead of just tanking it.
                hit.GetComponentInParent<IHealth>()?.TakeDamage(specialDamage, ignoreDef: true);
            }

            ImpactBurst.Spawn(transform.position + Vector3.up, new Color(0.5f, 0.75f, 0.2f));
        }

        private void SpawnAdds()
        {
            // TANK CHECK: adds must be picked up off the healer/support, or the party
            // takes chip damage from multiple directions at once.
            for (int i = 0; i < 2; i++)
            {
                Vector3 offset = new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f));
                BogLurker.Spawn(transform.position + offset);
            }
        }

        private void EnterPhase2()
        {
            inPhase2 = true;
            // BUFFER CHECK: enrage is the default state -- only a Weaken or Curse
            // application brings the boss back down to a manageable damage/speed level.
            attackDamage *= enrageDamageMultiplier;
            attackCooldown /= enrageAttackSpeedMultiplier;

            // RotMG Shatters-style escalation: phase 2 doesn't just buff the boss, it makes
            // the arena itself worse -- fixed at the boss's position now, not following it.
            Vector3 origin = transform.position;
            HazardVisuals.SpawnPatch(transform.parent, origin + new Vector3(4f, 0, 3f), 2.5f,
                new Color(0.35f, 0.42f, 0.12f), new Color(0.25f, 0.32f, 0.08f), new Color(0.55f, 0.58f, 0.2f));
            HazardVisuals.SpawnPatch(transform.parent, origin + new Vector3(-4f, 0, -3f), 2.5f,
                new Color(0.35f, 0.42f, 0.12f), new Color(0.25f, 0.32f, 0.08f), new Color(0.55f, 0.58f, 0.2f));
        }

        protected override void Attack()
        {
            // If the party has applied Weaken/Curse to the boss, treat that as
            // suppressing the enrage multiplier for this hit (buffer's check).
            float mitigation = 1f;
            if (statusController.HasEffect(StatusEffectType.Weaken)) mitigation *= 0.6f;

            float dmg = attackDamage * mitigation;
            target.GetComponent<IHealth>()?.TakeDamage(dmg, ignoreDef: false);

            // Its signature attack, distinct from the other two bosses' plain-damage
            // Attack() -- a chance to stack Poison on top of the hit.
            if (Random.value < poisonChance)
            {
                target.GetComponent<StatusEffectController>()?.ApplyEffect(
                    StatusEffectType.Poison, poisonDuration, poisonDamage);
            }

            // HEALER CHECK: sustained poison damage is what makes this boss specifically
            // test the Healer/cleanse role -- ticking DoT the party can't just tank through
            // without active healing or a cleanse.
        }

        protected override void HandleDeath()
        {
            if (telegraphGO != null) Destroy(telegraphGO);
            base.HandleDeath();
        }
    }
}
