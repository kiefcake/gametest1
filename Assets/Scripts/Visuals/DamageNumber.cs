using UnityEngine;
using UnityEngine.UI;

namespace DungeonCrawler.Visuals
{
    // Billboarded floating number that rises and fades, then destroys itself. Spawned by
    // HealthVFX wherever Health.OnDamaged/OnHealed fires, so every damage/heal source
    // (abilities, enemy melee, status DoT ticks) gets the same feedback for free without
    // each call site needing to know about it.
    public class DamageNumber : MonoBehaviour
    {
        private const float Lifetime = 1f;
        private const float RiseSpeed = 1f;

        private float age;
        private CanvasGroup canvasGroup;

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
            var go = new GameObject("DamageNumber", typeof(Canvas), typeof(CanvasGroup));
            go.transform.position = worldPos;
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
            text.color = color;
            text.text = content;

            var dn = go.AddComponent<DamageNumber>();
            dn.canvasGroup = go.GetComponent<CanvasGroup>();
            go.AddComponent<BillboardSprite>();
        }

        private void Update()
        {
            age += Time.deltaTime;
            transform.position += Vector3.up * RiseSpeed * Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(1f - age / Lifetime);
            if (age >= Lifetime) Destroy(gameObject);
        }
    }
}
