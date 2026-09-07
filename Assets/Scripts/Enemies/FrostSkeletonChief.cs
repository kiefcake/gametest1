using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.Enemies
{
    // The Frostlands camp's guardian -- FrostSkeleton's Brittle Frenzy (attack speed
    // scaling with missing HP) stays inherited as-is, plus one signature move: Bone Chill
    // Nova, a telegraphed radius pulse that Slows everyone caught in it instead of
    // dealing a big hit -- a camp guardian's job is to punish standing still while its
    // trash escort closes in, not to out-damage the dungeon boss it's guarding.
    public class FrostSkeletonChief : FrostSkeleton
    {
        [Header("Signature attack: Bone Chill Nova")]
        public float novaInterval = 9f;
        public float novaChannelTime = 1.2f;
        public float novaRadius = 4.5f;
        public float novaDamage = 10f;
        public float novaSlowMagnitude = 0.5f;
        public float novaSlowDuration = 2.5f;
        private float novaTimer;
        private bool channeling;
        private float channelElapsed;
        private GameObject telegraphGO;

        private static readonly Color TelegraphStart = new Color(0.3f, 0.55f, 0.8f);
        private static readonly Color TelegraphEnd = new Color(0.85f, 0.95f, 1f);

        protected override void Awake()
        {
            base.Awake();
            novaTimer = novaInterval * 0.6f;
        }

        protected override void Update()
        {
            if (channeling)
            {
                TickNova();
                return;
            }

            base.Update();
            if (health.IsDowned || target == null) return;

            novaTimer -= Time.deltaTime;
            if (novaTimer <= 0f) BeginNova();
        }

        private void BeginNova()
        {
            channeling = true;
            channelElapsed = 0f;
            novaTimer = novaInterval * Random.Range(0.85f, 1.15f);
            SetInvulnerable(true);

            telegraphGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            telegraphGO.name = "BoneChillTelegraph";
            var col = telegraphGO.GetComponent<Collider>();
            if (col != null) Destroy(col);
            telegraphGO.transform.position = transform.position + Vector3.up * 0.05f;
            telegraphGO.transform.localScale = new Vector3(novaRadius * 2f, 0.01f, novaRadius * 2f);
            var renderer = telegraphGO.GetComponent<Renderer>();
            if (renderer != null) renderer.material = new Material(Shader.Find("Standard")) { color = TelegraphStart };
        }

        private void TickNova()
        {
            channelElapsed += Time.deltaTime;
            if (telegraphGO != null)
            {
                float t = Mathf.Clamp01(channelElapsed / novaChannelTime);
                var renderer = telegraphGO.GetComponent<Renderer>();
                if (renderer != null) renderer.material.color = Color.Lerp(TelegraphStart, TelegraphEnd, t);
            }

            if (channelElapsed >= novaChannelTime) ResolveNova();
        }

        private void ResolveNova()
        {
            if (telegraphGO != null) Destroy(telegraphGO);
            channeling = false;
            SetInvulnerable(false);

            var hits = Physics.OverlapSphere(transform.position, novaRadius);
            foreach (var hit in hits)
            {
                if (hit.GetComponentInParent<FrostSkeletonChief>() != null) continue;
                hit.GetComponentInParent<IHealth>()?.TakeDamage(novaDamage, ignoreDef: true);
                hit.GetComponentInParent<StatusEffectController>()?.ApplyEffect(
                    StatusEffectType.Slow, novaSlowDuration, novaSlowMagnitude);
            }

            Visuals.ImpactBurst.Spawn(transform.position + Vector3.up, new Color(0.6f, 0.85f, 1f));
        }

        protected override void HandleDeath()
        {
            if (telegraphGO != null) Destroy(telegraphGO);
            base.HandleDeath();
        }
    }
}
