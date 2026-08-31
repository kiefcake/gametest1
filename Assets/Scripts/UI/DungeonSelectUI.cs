using UnityEngine;
using UnityEngine.UI;
using DungeonCrawler.Core;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.UI
{
    // Small popup at the hub's dungeon gate -- lets a run choose which dungeon to enter
    // instead of the gate being hardcoded to one. Adding a new dungeon later just needs
    // one more BuildButton call here, not new hub geometry.
    public class DungeonSelectUI : MonoBehaviour
    {
        private static DungeonSelectUI instance;

        private GameObject panelRoot;
        private Font font;
        private Sprite buttonSprite;
        private System.Action onAbyss;
        private System.Action onFrozenCrypt;
        private Image hardcoreButtonImage;
        private Text hardcoreButtonText;

        private static readonly Color PanelFill = new Color(0.08f, 0.08f, 0.11f, 0.97f);
        private static readonly Color PanelBorder = new Color(0.5f, 0.2f, 0.65f, 1f);
        private static readonly Color HardcoreOffColor = new Color(0.3f, 0.3f, 0.34f);
        private static readonly Color HardcoreOnColor = new Color(0.65f, 0.2f, 0.2f);

        public static void Show(System.Action enterAbyss, System.Action enterFrozenCrypt)
        {
            if (instance == null)
            {
                var go = new GameObject("DungeonSelectUI");
                instance = go.AddComponent<DungeonSelectUI>();
                instance.BuildUI();
            }
            instance.Open(enterAbyss, enterFrozenCrypt);
        }

        private void Open(System.Action enterAbyss, System.Action enterFrozenCrypt)
        {
            Debug.Log("[DungeonSelectUI] Open() -- gate interact reached the popup");
            onAbyss = enterAbyss;
            onFrozenCrypt = enterFrozenCrypt;

            RefreshHardcoreButton();
            panelRoot.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Close()
        {
            panelRoot.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("DungeonSelectCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            panelRoot = new GameObject("SelectPanel", typeof(RectTransform), typeof(Image));
            panelRoot.transform.SetParent(canvasGO.transform, false);
            var panelRect = panelRoot.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(440, 400);
            var panelImage = panelRoot.GetComponent<Image>();
            panelImage.sprite = PanelSpriteFactory.CreateRoundedSprite(PanelFill, PanelBorder);
            panelImage.type = Image.Type.Sliced;
            panelImage.color = Color.white;

            buttonSprite = PanelSpriteFactory.CreateRoundedSprite(new Color(0.2f, 0.2f, 0.24f), new Color(0.4f, 0.4f, 0.48f), size: 64, radius: 10, borderThickness: 3);

            MakeLabel("Choose a Dungeon", 24, FontStyle.Bold, new Vector2(0, -32));

            BuildButton("The Abyss", new Vector2(0, 76), new Color(0.65f, 0.2f, 0.2f), OnAbyssClicked);
            BuildButton("The Frozen Crypt", new Vector2(0, 16), new Color(0.25f, 0.45f, 0.65f), OnFrozenCryptClicked);

            var hardcoreGO = BuildButton("Hardcore: OFF", new Vector2(0, -48), HardcoreOffColor, ToggleHardcore);
            hardcoreButtonImage = hardcoreGO.GetComponent<Image>();
            hardcoreButtonText = hardcoreGO.GetComponentInChildren<Text>();

            BuildButton("Cancel", new Vector2(0, -116), new Color(0.3f, 0.3f, 0.34f), Close);

            panelRoot.SetActive(false);
        }

        private void OnAbyssClicked()
        {
            Debug.Log("[DungeonSelectUI] Abyss button clicked");
            Close();
            onAbyss?.Invoke();
        }

        private void OnFrozenCryptClicked()
        {
            Debug.Log("[DungeonSelectUI] Frozen Crypt button clicked");
            Close();
            onFrozenCrypt?.Invoke();
        }

        private void MakeLabel(string text, int fontSize, FontStyle style, Vector2 anchoredPos)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(panelRoot.transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(380, 36);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.text = text;
        }

        private GameObject BuildButton(string label, Vector2 anchoredPos, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(panelRoot.transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(320, 52);
            var img = go.GetComponent<Image>();
            img.sprite = buttonSprite;
            img.type = Image.Type.Sliced;
            img.color = color;

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(go.transform, false);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.GetComponent<Text>();
            text.font = font;
            text.fontSize = 18;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;

            go.GetComponent<Button>().onClick.AddListener(onClick);
            return go;
        }

        // Prototype run modifier (see Core.RunModifiers) -- toggled here rather than in
        // the hub itself so it reads as "a choice you make right before diving in," same
        // spot a difficulty select would live if more modifiers get added later.
        private void ToggleHardcore()
        {
            RunModifiers.DoubleDamageTaken = !RunModifiers.DoubleDamageTaken;
            RefreshHardcoreButton();
        }

        private void RefreshHardcoreButton()
        {
            bool on = RunModifiers.DoubleDamageTaken;
            hardcoreButtonText.text = on ? "Hardcore: ON (2x dmg taken)" : "Hardcore: OFF";
            hardcoreButtonImage.color = on ? HardcoreOnColor : HardcoreOffColor;
        }
    }
}
