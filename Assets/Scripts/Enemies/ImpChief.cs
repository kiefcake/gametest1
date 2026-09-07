using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.Enemies
{
    // The Wastes camp's guardian -- same ImpDemon everything below (spiked variant,
    // visual, AttachVisual, on-hit Bleed) plus one signature move layered on top: Molten
    // Slam, a telegraphed leap-and-slam AoE. Same channel/telegraph/resolve shape the real
    // dungeon bosses (FrostLich/SwampWarden) already use, just a single un-phased special
    // sized for an open-world camp guardian rather than a full boss-room encounter.
    public class ImpChief : ImpDemon
    {
        [Header("Signature attack: Molten Slam")]
        public float slamInterval = 8f;
        public float slamChannelTime = 1f;
        public float slamRadius = 4f;
        public float slamDamage = 22f;
        private float slamTimer;
        private bool channeling;
        private float channelElapsed;
        private GameObject telegraphGO;

        private static readonly Color TelegraphStart = new Color(0.6f, 0.15f, 0.02f);
        private static readonly Color TelegraphEnd = new Color(1f, 0.65f, 0.1f);

        protected override void Awake()
        {
            base.Awake();
            slamTimer = slamInterval * 0.6f;
        }

        protected override void Update()
        {
            if (channeling)
            {
                TickSlam();
                return; // frozen mid-channel, same as every other telegraphed boss special
            }

            base.Update();
            if (health.IsDowned || target == null) return;

            slamTimer -= Time.deltaTime;
            if (slamTimer <= 0f) BeginSlam();
        }

        private void BeginSlam()
        {
            channeling = true;
            channelElapsed = 0f;
            slamTimer = slamInterval * Random.Range(0.85f, 1.15f);
            SetInvulnerable(true);

            telegraphGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            telegraphGO.name = "MoltenSlamTelegraph";
            var col = telegraphGO.GetComponent<Collider>();
            if (col != null) Destroy(col);
            telegraphGO.transform.position = transform.position + Vector3.up * 0.05f;
            telegraphGO.transform.localScale = new Vector3(slamRadius * 2f, 0.01f, slamRadius * 2f);
            var renderer = telegraphGO.GetComponent<Renderer>();
            if (renderer != null) renderer.material = new Material(Shader.Find("Standard")) { color = TelegraphStart };
        }

        private void TickSlam()
        {
            channelElapsed += Time.deltaTime;
            if (telegraphGO != null)
            {
                float t = Mathf.Clamp01(channelElapsed / slamChannelTime);
                var renderer = telegraphGO.GetComponent<Renderer>();
                if (renderer != null) renderer.material.color = Color.Lerp(TelegraphStart, TelegraphEnd, t);
            }

            if (channelElapsed >= slamChannelTime) ResolveSlam();
        }

        private void ResolveSlam()
        {
            if (telegraphGO != null) Destroy(telegraphGO);
            channeling = false;
            SetInvulnerable(false);

            var hits = Physics.OverlapSphere(transform.position, slamRadius);
            foreach (var hit in hits)
            {
                if (hit.GetComponentInParent<ImpChief>() != null) continue;
                hit.GetComponentInParent<IHealth>()?.TakeDamage(slamDamage, ignoreDef: true);
            }

            Visuals.ImpactBurst.Spawn(transform.position + Vector3.up, new Color(1f, 0.5f, 0.1f));
        }

        protected override void HandleDeath()
        {
            if (telegraphGO != null) Destroy(telegraphGO);
            base.HandleDeath();
        }
    }
}
