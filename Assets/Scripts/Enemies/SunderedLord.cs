using UnityEngine;
using DungeonCrawler.Audio;
using DungeonCrawler.Core;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Enemies
{
    // The Wraithbound Sanctum's boss -- same "role-check boss template" shape as the other
    // four dungeon bosses: a Phase-1 tank-check add-spawn (Wraith Knights), a Phase-2
    // enrage trigger (Buffer check), and a channeled telegraphed AoE special. Its signature
    // is the dungeon's own Curse+Weaken combo gimmick landing at once -- WraithKnight/
    // SpecterCaster each only apply one of the two, the Lord applies both together, on
    // both his basic attack and his special.
    public class SunderedLord : EnemyBase
    {
        [Header("Phase 2 trigger")]
        public float phase2HpFraction = 0.5f;
        private bool inPhase2;

        [Header("Phase 1: add spawns (Tank check)")]
        public float addSpawnInterval = 12f;
        private float addSpawnTimer;

        [Header("Phase 2: enrage (Buffer check)")]
        public float enrageDamageMultiplier = 1.8f;
        public float enrageAttackSpeedMultiplier = 1.5f;

        [Header("Signature: Curse + Weaken combo")]
        public float debuffChance = 0.5f;
        public float debuffDuration = 5f;
        public float curseMagnitude = 0.3f;
        public float weakenMagnitude = 0.3f;

        [Header("Special: channeled Reliquary Ruin")]
        [Tooltip("The Lord plants itself and telegraphs a burst of grave-light -- stand in the glowing circle when it resolves and it hurts regardless of DEF, and lands both Curse and Weaken at once. Walking out during the channel avoids all of it.")]
        public float specialInterval = 11f;
        public float specialChannelTime = 1.8f;
        public float specialRadius = 4.5f;
        public float specialDamage = 42f;
        private float specialTimer;
        private bool channeling;
        private float channelElapsed;
        private GameObject telegraphGO;

        private static readonly Color TelegraphStart = new Color(0.25f, 0.08f, 0.3f);
        private static readonly Color TelegraphEnd = new Color(0.6f, 0.85f, 0.5f);

        protected override void Awake()
        {
            enemyName = "The Sundered Lord";
            healthBarHeight = 3.8f;
            healthBarWidth = 2.4f;

            base.Awake();

            health.maxHP = 1180f; // matches the other three bosses' 1100-1200 range
            health.SetCurrentHP(health.maxHP);
            attackCooldown = 2.1f;
            attackDamage = 17f;
            specialTimer = specialInterval;
            addSpawnTimer = addSpawnInterval;
        }

        // Boss-scale Humanoid -- deep purple-black armor with pale bone/gold trim, a
        // fallen knight rather than a hulking brute (SwampWarden) or a hovering caster
        // (FrostLich) -- a third silhouette family for the boss roster.
        protected override void AttachVisual()
        {
            var built = ProceduralMonster.Humanoid(transform, new ProceduralMonster.HumanoidSpec
            {
                bodyColor = new Color(0.18f, 0.12f, 0.22f),
                accentColor = new Color(0.75f, 0.7f, 0.55f),
                scale = 2.2f, horns = true, weapon = true, hunched = false
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
            if (health.IsDowned) return;

            if (!inPhase2 && health.CurrentHP <= health.maxHP * phase2HpFraction)
                EnterPhase2();

            if (!inPhase2 && target != null)
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
                BeginSpecialAttack();
        }

        private void EnterPhase2()
        {
            inPhase2 = true;
            attackDamage *= enrageDamageMultiplier;
            attackCooldown /= enrageAttackSpeedMultiplier;

            Vector3 origin = transform.position;
            HazardVisuals.SpawnPatch(transform.parent, origin + new Vector3(4f, 0, 3f), 2.5f,
                new Color(0.25f, 0.08f, 0.3f), new Color(0.15f, 0.04f, 0.2f), new Color(0.55f, 0.8f, 0.45f));
            HazardVisuals.SpawnPatch(transform.parent, origin + new Vector3(-4f, 0, -3f), 2.5f,
                new Color(0.25f, 0.08f, 0.3f), new Color(0.15f, 0.04f, 0.2f), new Color(0.55f, 0.8f, 0.45f));
        }

        private void SpawnAdds()
        {
            // TANK CHECK: adds must be picked up off the healer/support, or the party
            // takes chip damage from multiple directions at once.
            for (int i = 0; i < 2; i++)
            {
                Vector3 offset = new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f));
                var go = new GameObject("WraithKnight");
                go.transform.position = transform.position + offset;
                go.transform.SetParent(transform.parent);
                go.AddComponent<Health>();
                go.AddComponent<StatusEffectController>();
                go.AddComponent<WraithKnight>();
                go.AddComponent<AggroController>();
            }
        }

        private void BeginSpecialAttack()
        {
            channeling = true;
            channelElapsed = 0f;
            specialTimer = specialInterval;
            SetInvulnerable(true);

            telegraphGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            telegraphGO.name = "ReliquaryRuinTelegraph";
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
                if (hit.GetComponentInParent<SunderedLord>() != null) continue;
                hit.GetComponentInParent<IHealth>()?.TakeDamage(specialDamage, ignoreDef: true);
                var sc = hit.GetComponentInParent<StatusEffectController>();
                sc?.ApplyEffect(StatusEffectType.Curse, debuffDuration, curseMagnitude);
                sc?.ApplyEffect(StatusEffectType.Weaken, debuffDuration, weakenMagnitude);
            }

            ImpactBurst.Spawn(transform.position + Vector3.up, new Color(0.55f, 0.8f, 0.45f));
        }

        protected override void Attack()
        {
            float mitigation = statusController.HasEffect(StatusEffectType.Weaken) ? 0.6f : 1f;
            target.GetComponent<IHealth>()?.TakeDamage(attackDamage * mitigation, ignoreDef: false);

            // Its signature: a chance to land BOTH debuffs on a plain hit, not just the
            // channeled special -- the combo gimmick should show up on ordinary attacks
            // too, not only once every ~11 seconds.
            if (Random.value < debuffChance)
            {
                var sc = target.GetComponent<StatusEffectController>();
                sc?.ApplyEffect(StatusEffectType.Curse, debuffDuration, curseMagnitude);
                sc?.ApplyEffect(StatusEffectType.Weaken, debuffDuration, weakenMagnitude);
            }
        }

        protected override void HandleDeath()
        {
            if (telegraphGO != null) Destroy(telegraphGO);
            base.HandleDeath();
        }
    }
}
