using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.Enemies
{
    // The Reliquary camp's guardian -- WraithKnight's on-hit Curse stays inherited as-is,
    // plus one signature move: Grave Bind, a telegraphed radius pulse that lands the
    // dungeon's own Curse+Weaken combo at once (mirroring SunderedLord's own signature,
    // so the camp guardian previews the dungeon boss's real gimmick before the player
    // ever reaches it) instead of a big single-target hit.
    public class WraithKnightChief : WraithKnight
    {
        [Header("Signature attack: Grave Bind")]
        public float bindInterval = 9f;
        public float bindChannelTime = 1.2f;
        public float bindRadius = 4.5f;
        public float bindDamage = 11f;
        public float bindDebuffDuration = 4f;
        public float bindCurseMagnitude = 0.3f;
        public float bindWeakenMagnitude = 0.3f;
        private float bindTimer;
        private bool channeling;
        private float channelElapsed;
        private GameObject telegraphGO;

        private static readonly Color TelegraphStart = new Color(0.3f, 0.1f, 0.35f);
        private static readonly Color TelegraphEnd = new Color(0.65f, 0.9f, 0.55f);

        protected override void Awake()
        {
            base.Awake();
            bindTimer = bindInterval * 0.6f;
        }

        protected override void Update()
        {
            if (channeling)
            {
                TickBind();
                return;
            }

            base.Update();
            if (health.IsDowned || target == null) return;

            bindTimer -= Time.deltaTime;
            if (bindTimer <= 0f) BeginBind();
        }

        private void BeginBind()
        {
            channeling = true;
            channelElapsed = 0f;
            bindTimer = bindInterval * Random.Range(0.85f, 1.15f);
            SetInvulnerable(true);

            telegraphGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            telegraphGO.name = "GraveBindTelegraph";
            var col = telegraphGO.GetComponent<Collider>();
            if (col != null) Destroy(col);
            telegraphGO.transform.position = transform.position + Vector3.up * 0.05f;
            telegraphGO.transform.localScale = new Vector3(bindRadius * 2f, 0.01f, bindRadius * 2f);
            var renderer = telegraphGO.GetComponent<Renderer>();
            if (renderer != null) renderer.material = new Material(Shader.Find("Standard")) { color = TelegraphStart };
        }

        private void TickBind()
        {
            channelElapsed += Time.deltaTime;
            if (telegraphGO != null)
            {
                float t = Mathf.Clamp01(channelElapsed / bindChannelTime);
                var renderer = telegraphGO.GetComponent<Renderer>();
                if (renderer != null) renderer.material.color = Color.Lerp(TelegraphStart, TelegraphEnd, t);
            }

            if (channelElapsed >= bindChannelTime) ResolveBind();
        }

        private void ResolveBind()
        {
            if (telegraphGO != null) Destroy(telegraphGO);
            channeling = false;
            SetInvulnerable(false);

            var hits = Physics.OverlapSphere(transform.position, bindRadius);
            foreach (var hit in hits)
            {
                if (hit.GetComponentInParent<WraithKnightChief>() != null) continue;
                hit.GetComponentInParent<IHealth>()?.TakeDamage(bindDamage, ignoreDef: true);
                var sc = hit.GetComponentInParent<StatusEffectController>();
                sc?.ApplyEffect(StatusEffectType.Curse, bindDebuffDuration, bindCurseMagnitude);
                sc?.ApplyEffect(StatusEffectType.Weaken, bindDebuffDuration, bindWeakenMagnitude);
            }

            Visuals.ImpactBurst.Spawn(transform.position + Vector3.up, new Color(0.6f, 0.85f, 0.5f));
        }

        protected override void HandleDeath()
        {
            if (telegraphGO != null) Destroy(telegraphGO);
            base.HandleDeath();
        }
    }
}
