using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.Enemies
{
    // The Marshlands camp's guardian -- BogLurker's Toxic Spit stays inherited as-is,
    // plus one signature move: Toxic Eruption, a telegraphed poison burst that also calls
    // in 2 reinforcement Lurkers -- a small-scale echo of Swamp Warden's own tank-check
    // add-spawn, appropriately scaled down for a camp guardian rather than a dungeon boss.
    public class BogLurkerChief : BogLurker
    {
        [Header("Signature attack: Toxic Eruption")]
        public float eruptionInterval = 10f;
        public float eruptionChannelTime = 1.3f;
        public float eruptionRadius = 4f;
        public float eruptionDamage = 16f;
        private float eruptionTimer;
        private bool channeling;
        private float channelElapsed;
        private GameObject telegraphGO;

        private static readonly Color TelegraphStart = new Color(0.2f, 0.35f, 0.1f);
        private static readonly Color TelegraphEnd = new Color(0.65f, 0.85f, 0.25f);

        protected override void Awake()
        {
            base.Awake();
            eruptionTimer = eruptionInterval * 0.6f;
        }

        protected override void Update()
        {
            if (channeling)
            {
                TickEruption();
                return;
            }

            base.Update();
            if (health.IsDowned || target == null) return;

            eruptionTimer -= Time.deltaTime;
            if (eruptionTimer <= 0f) BeginEruption();
        }

        private void BeginEruption()
        {
            channeling = true;
            channelElapsed = 0f;
            eruptionTimer = eruptionInterval * Random.Range(0.85f, 1.15f);
            SetInvulnerable(true);

            // Reinforcements are called the instant the channel starts, not on resolve --
            // gives the party a moment to notice the extra adds arriving alongside the
            // telegraph, rather than being ambushed by them at the same instant it hurts.
            for (int i = 0; i < 2; i++)
            {
                Vector3 offset = new Vector3(Random.Range(-2.5f, 2.5f), 0, Random.Range(-2.5f, 2.5f));
                BogLurker.Spawn(transform.position + offset).transform.SetParent(transform.parent);
            }

            telegraphGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            telegraphGO.name = "ToxicEruptionTelegraph";
            var col = telegraphGO.GetComponent<Collider>();
            if (col != null) Destroy(col);
            telegraphGO.transform.position = transform.position + Vector3.up * 0.05f;
            telegraphGO.transform.localScale = new Vector3(eruptionRadius * 2f, 0.01f, eruptionRadius * 2f);
            var renderer = telegraphGO.GetComponent<Renderer>();
            if (renderer != null) renderer.material = new Material(Shader.Find("Standard")) { color = TelegraphStart };
        }

        private void TickEruption()
        {
            channelElapsed += Time.deltaTime;
            if (telegraphGO != null)
            {
                float t = Mathf.Clamp01(channelElapsed / eruptionChannelTime);
                var renderer = telegraphGO.GetComponent<Renderer>();
                if (renderer != null) renderer.material.color = Color.Lerp(TelegraphStart, TelegraphEnd, t);
            }

            if (channelElapsed >= eruptionChannelTime) ResolveEruption();
        }

        private void ResolveEruption()
        {
            if (telegraphGO != null) Destroy(telegraphGO);
            channeling = false;
            SetInvulnerable(false);

            var hits = Physics.OverlapSphere(transform.position, eruptionRadius);
            foreach (var hit in hits)
            {
                if (hit.GetComponentInParent<BogLurkerChief>() != null) continue;
                hit.GetComponentInParent<IHealth>()?.TakeDamage(eruptionDamage, ignoreDef: true);
                hit.GetComponentInParent<StatusEffectController>()?.ApplyEffect(
                    StatusEffectType.Poison, poisonDuration, poisonDamage);
            }

            Visuals.ImpactBurst.Spawn(transform.position + Vector3.up, new Color(0.5f, 0.75f, 0.2f));
        }

        protected override void HandleDeath()
        {
            if (telegraphGO != null) Destroy(telegraphGO);
            base.HandleDeath();
        }
    }
}
