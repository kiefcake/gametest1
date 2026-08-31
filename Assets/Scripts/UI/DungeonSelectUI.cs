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
        private System.Action onSunkenRuins;
        private Image hardcoreButtonImage;
        private Text hardcoreButtonText;

        private static readonly Color PanelFill = new Color(0.08f, 0.08f, 0.11f, 0.97f);
        private static readonly Color PanelBorder = new Color(0.5f, 0.2f, 0.65f, 1f);
        private static readonly Color HardcoreOffColor = new Color(0.3f, 0.3f, 0.34f);
        private static readonly Color HardcoreOnColor = new Color(0.65f, 0.2f, 0.2f);

        public static void Show(System.Action enterAbyss, System.Action enterFrozenCrypt, System.Action enterSunkenRuins)
        {
            if (instance == null)
            {
                var go = new GameObject("DungeonSelectUI");
                instance = go.AddComponent<DungeonSelectUI>();
                instance.BuildUI();
            }
            instance.Open(enterAbyss, enterFrozenCrypt, enterSunkenRuins);
        }

        private void Open(System.Action enterAbyss, System.Action enterFrozenCrypt, System.Action enterSunkenRuins)
        {
            Debug.Log("[DungeonSelectUI] Open() -- gate interact reached the popup");
            onAbyss = enterAbyss;
            onFrozenCrypt = enterFrozenCrypt;
            onSunkenRuins = enterSunkenRuins;

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
            panelRect.sizeDelta = new Vector2(440, 422);
            var panelImage = panelRoot.GetComponent<Image>();
            panelImage.sprite = PanelSpriteFactory.CreateRoundedSprite(PanelFill, PanelBorder);
            panelImage.type = Image.Type.Sliced;
            panelImage.color = Color.white;

            buttonSprite = PanelSpriteFactory.CreateRoundedSprite(new Color(0.2f, 0.2f, 0.24f), new Color(0.4f, 0.4f, 0.48f), size: 64, radius: 10, borderThickness: 3);

            // Vertical layout, hand-computed so nothing overlaps (each button is 52px
            // tall; panel sizeDelta.y is set to match this exactly -- see BuildUI's
            // panelRect.sizeDelta above):
            //   panel top edge      +211
            //   title                -24  (top-anchored; label spans -24..-60 from top edge)
            //   Abyss                 95  (top=121, bottom=69)
            //   Frozen Crypt          31  (top=57,  bottom=5)
            //   Sunken Ruins         -33  (top=-7,  bottom=-59)
            //   Hardcore toggle      -97  (top=-71, bottom=-123)
            //   Cancel              -161  (top=-135,bottom=-187)
            //   panel bottom edge   -211
            // Every consecutive pair of buttons is 64px apart center-to-center (52px tall
            // + 12px clear gap), matching the ~60px rhythm the original three-button
            // layout used; title keeps its original 30px gap above the first button.
            MakeLabel("Choose a Dungeon", 24, FontStyle.Bold, new Vector2(0, -24));

            BuildButton("The Abyss", new Vector2(0, 95), new Color(0.65f, 0.2f, 0.2f), OnAbyssClicked);
            BuildButton("The Frozen Crypt", new Vector2(0, 31), new Color(0.25f, 0.45f, 0.65f), OnFrozenCryptClicked);
            BuildButton("The Sunken Ruins", new Vector2(0, -33), new Color(0.3f, 0.55f, 0.35f), OnSunkenRuinsClicked);

            var hardcoreGO = BuildButton("Hardcore: OFF", new Vector2(0, -97), HardcoreOffColor, ToggleHardcore);
            hardcoreButtonImage = hardcoreGO.GetComponent<Image>();
            hardcoreButtonText = hardcoreGO.GetComponentInChildren<Text>();

            BuildButton("Cancel", new Vector2(0, -161), new Color(0.3f, 0.3f, 0.34f), Close);

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

        private void OnSunkenRuinsClicked()
        {
            Debug.Log("[DungeonSelectUI] Sunken Ruins button clicked");
            Close();
            onSunkenRuins?.Invoke();
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
