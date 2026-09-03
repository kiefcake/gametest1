using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonCrawler.Visuals
{
    // Billboarded floating number that rises and fades, then returns to a pool. Spawned by
    // HealthVFX wherever Health.OnDamaged/OnHealed fires, so every damage/heal source
    // (abilities, enemy melee, status DoT ticks) gets the same feedback for free without
    // each call site needing to know about it.
    //
    // Pooled rather than Instantiate/Destroy per number: each one is a Canvas+CanvasGroup+
    // Text hierarchy, and a Canvas is expensive to construct and tear down (its own mesh
    // rebuild, on top of the GameObject/component allocation) -- a busy fight can trigger
    // several OnDamaged calls a second (multiple attackers, a DoT tick, a boss AoE), and
    // rebuilding a whole Canvas for each one was the actual lag source the "damage number
    // spam" complaint was pointing at, not the damage events themselves being wrong.
    public class DamageNumber : MonoBehaviour
    {
        private const float Lifetime = 1f;
        private const float RiseSpeed = 1f;

        private static readonly Queue<DamageNumber> pool = new Queue<DamageNumber>();

        private float age;
        private CanvasGroup canvasGroup;
        private Text text;

        public static void Spawn(Vector3 worldPos, float amount, bool isHeal)
        {
            Build(worldPos, (isHeal ? "+" : "") + Mathf.RoundToInt(amount),
                isHeal ? new Color(0.35f, 0.9f, 0.35f) : new Color(1f, 0.85f, 0.2f));
        }

        // Distinct from Spawn's damage/heal coloring so a gold pickup doesn't read as
        // "you just healed" -- used by LootDropper on enemy kills.
        public static void SpawnGold(Vector3 worldPos, int amount)
        {
            Build(worldPos, $"+{amount}g", new Color(1f, 0.84f, 0.1f));
        }

        private static void Build(Vector3 worldPos, string content, Color color)
        {
            DamageNumber dn = pool.Count > 0 ? pool.Dequeue() : CreateInstance();

            dn.gameObject.SetActive(true);
            dn.transform.position = worldPos;
            dn.text.text = content;
            dn.text.color = color;
            dn.age = 0f;
            dn.canvasGroup.alpha = 1f;
        }

        // The one-time Canvas/Text construction, split out of Build so a pool hit skips it
        // entirely -- only ever runs again once every currently-pooled instance is already
        // in use at the same time.
        private static DamageNumber CreateInstance()
        {
            var go = new GameObject("DamageNumber", typeof(Canvas), typeof(CanvasGroup));
            go.transform.localScale = Vector3.one * 0.5f;

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(2f, 0.6f);

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(go.transform, false);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 40;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;

            var dn = go.AddComponent<DamageNumber>();
            dn.canvasGroup = go.GetComponent<CanvasGroup>();
            dn.text = text;
            go.AddComponent<BillboardSprite>();
            return dn;
        }

        private void Update()
        {
            age += Time.deltaTime;
            transform.position += Vector3.up * RiseSpeed * Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(1f - age / Lifetime);
            if (age >= Lifetime) Release();
        }

        // Deactivate and hand back to the pool instead of Destroy -- next Spawn reuses this
        // exact Canvas hierarchy rather than building a new one from scratch.
        private void Release()
        {
            gameObject.SetActive(false);
            pool.Enqueue(this);
        }
    }
}
