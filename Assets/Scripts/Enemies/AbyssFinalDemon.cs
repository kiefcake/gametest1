using UnityEngine;
using DungeonCrawler.Audio;
using DungeonCrawler.Core;
using DungeonCrawler.Loot;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Enemies
{
    // The abyss dungeon's final boss. Sprite: Sprites/Enemies/Abyss/abyss_final_demon.png
    // Two phases, each with a mechanic that specifically checks one party role,
    // per the "role-check boss template" pattern from the full scope doc.
    public class AbyssFinalDemon : EnemyBase
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

        [Header("Special: channeled AoE slam")]
        [Tooltip("The boss plants itself and telegraphs a ground-slam -- stand in the glowing circle when it resolves and it hurts regardless of DEF. Walking out during the channel avoids it entirely.")]
        public float specialInterval = 11f;
        public float specialChannelTime = 1.8f;
        public float specialRadius = 4.5f;
        public float specialDamage = 45f;
        private float specialTimer;
        private bool channeling;
        private float channelElapsed;
        private GameObject telegraphGO;

        protected override void Awake()
        {
            enemyName = "Abyss Demon";
            spriteResourcePath = "Sprites/Enemies/Abyss/abyss_final_demon";
            spriteHeight = 1.6f;
            healthBarHeight = 3.3f;
            healthBarWidth = 2.2f;

            base.Awake();

            attackCooldown = 2f;
            attackDamage = 18f;
            specialTimer = specialInterval;
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

            telegraphGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            telegraphGO.name = "SlamTelegraph";
            var col = telegraphGO.GetComponent<Collider>();
            if (col != null) Destroy(col); // warning marker only -- ResolveSpecialAttack does the actual hit detection
            telegraphGO.transform.position = transform.position + Vector3.up * 0.05f;
            telegraphGO.transform.localScale = new Vector3(specialRadius * 2f, 0.01f, specialRadius * 2f);
            var renderer = telegraphGO.GetComponent<Renderer>();
            if (renderer != null) renderer.material = new Material(Shader.Find("Standard")) { color = new Color(0.6f, 0.1f, 0.05f) };

            SfxLibrary.PlayAt(SfxLibrary.Warning, transform.position, 0.5f);
        }

        private void TickSpecialAttack()
        {
            channelElapsed += Time.deltaTime;
            if (telegraphGO != null)
            {
                float t = Mathf.Clamp01(channelElapsed / specialChannelTime);
                var renderer = telegraphGO.GetComponent<Renderer>();
                if (renderer != null) renderer.material.color = Color.Lerp(new Color(0.6f, 0.1f, 0.05f), new Color(1f, 0.9f, 0.2f), t);
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

            var hits = Physics.OverlapSphere(transform.position, specialRadius);
            foreach (var hit in hits)
            {
                if (hit.GetComponentInParent<AbyssFinalDemon>() != null) continue; // don't hit itself
                // ignoreDef -- a boss slam should always punish standing in it, that's the
                // entire point of making the player dodge instead of just tanking it.
                hit.GetComponentInParent<IHealth>()?.TakeDamage(specialDamage, ignoreDef: true);
            }

            ImpactBurst.Spawn(transform.position + Vector3.up, new Color(1f, 0.5f, 0.1f));
        }

        // Built the same way GameBootstrap.SpawnImp builds every other imp -- this codebase
        // has no prefabs (see CLAUDE.md), so the field this replaced (a serialized
        // GameObject prefab reference, always null since nothing ever assigned it) could
        // never have worked; the whole point of this method (the boss's tank-check
        // mechanic) was permanently dead until this was inlined directly.
        private void SpawnAdds()
        {
            // TANK CHECK: adds must be picked up off the healer/support, or the party
            // takes chip damage from multiple directions at once.
            for (int i = 0; i < 2; i++)
            {
                Vector3 offset = new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f));
                var go = new GameObject("Imp");
                go.transform.position = transform.position + offset;
                go.AddComponent<Health>();
                go.AddComponent<StatusEffectController>();
                go.AddComponent<ImpDemon>();
                go.AddComponent<AggroController>();
                var loot = go.AddComponent<LootDropper>();
                loot.lootTable = Resources.Load<LootTable>("Data/Loot/AbyssLootTable");
                loot.minGold = 4;
                loot.maxGold = 8;
            }
        }

        private void EnterPhase2()
        {
            inPhase2 = true;
            // BUFFER CHECK: enrage is the default state -- only a Weaken or Curse
            // application brings the boss back down to a manageable damage/speed level.
            attackDamage *= enrageDamageMultiplier;
            attackCooldown /= enrageAttackSpeedMultiplier;
        }

        protected override void Attack()
        {
            // If the party has applied Weaken/Curse to the boss, treat that as
            // suppressing the enrage multiplier for this hit (buffer's check).
            float mitigation = 1f;
            if (statusController.HasEffect(StatusEffectType.Weaken)) mitigation *= 0.6f;

            float dmg = attackDamage * mitigation;
            target.GetComponent<IHealth>()?.TakeDamage(dmg, ignoreDef: false);

            // HEALER CHECK: phase 2 sustained damage is tuned so a healer must be
            // actively topping the party off, not just reacting to emergencies.
        }
    }
}
