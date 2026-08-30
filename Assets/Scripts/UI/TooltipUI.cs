using UnityEngine;
using UnityEngine.UI;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.UI
{
    // Single shared tooltip box, built once and reused by every HoverTooltip trigger
    // (inventory slots, equipment slots) rather than each one building its own panel.
    public class TooltipUI : MonoBehaviour
    {
        private static TooltipUI instance;

        private GameObject panel;
        private RectTransform panelRect;
        private Text text;

        public static void Show(string content, Vector2 screenPos)
        {
            if (string.IsNullOrEmpty(content)) return;
            EnsureInstance();
            instance.panel.SetActive(true);
            instance.text.text = content;

            // The panel used to sit at a fixed height regardless of content -- fine for a
            // one-line name, but rarity+UT tooltips are routinely 2-3 lines and were
            // spilling out past the background. Grow to fit instead.
            float height = Mathf.Max(70f, instance.text.preferredHeight + 24f);
            instance.panelRect.sizeDelta = new Vector2(instance.panelRect.sizeDelta.x, height);
            instance.panelRect.position = screenPos + new Vector2(18, -8);
        }

        public static void Hide()
        {
            if (instance != null) instance.panel.SetActive(false);
        }

        private static void EnsureInstance()
        {
            if (instance != null) return;
            var go = new GameObject("TooltipUI");
            instance = go.AddComponent<TooltipUI>();
            instance.BuildUI();
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("TooltipCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200; // above the pause menu (100) and everything else
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGO.transform, false);
            panelRect = panel.GetComponent<RectTransform>();
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.sizeDelta = new Vector2(300, 90);
            var panelImage = panel.GetComponent<Image>();
            panelImage.sprite = PanelSpriteFactory.CreateRoundedSprite(
                new Color(0.06f, 0.06f, 0.08f, 0.98f), new Color(0.4f, 0.4f, 0.5f), size: 48, radius: 8, borderThickness: 2);
            panelImage.type = Image.Type.Sliced;
            panelImage.color = Color.white;

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(panel.transform, false);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 10);
            textRect.offsetMax = new Vector2(-10, -10);

            text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 15;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            panel.SetActive(false);
        }
    }
}
