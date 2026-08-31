using UnityEngine;
using UnityEngine.UI;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.UI
{
    // Escape opens/closes this. Owns cursor lock state and Time.timeScale pausing --
    // FirstPersonLook no longer reads Escape itself, so the two don't fight over the same
    // key in the same frame. Pausing via timeScale=0 freezes every Time.deltaTime-driven
    // system (movement, cooldowns, enemy AI, status ticks) for free, without each of them
    // needing its own pause check.
    //
    // Buttons/sliders used to float directly on the full-screen dim scrim with no bounded
    // panel around them -- restyled to add an actual rounded card (see PanelSpriteFactory)
    // behind the content, matching the Shop/Stat/Inventory panels, with the scrim still
    // darkening the rest of the screen behind it.
    public class PauseMenuUI : MonoBehaviour
    {
        private GameObject panel;
        private GameObject settingsPanel;
        private Sprite buttonSprite;
        public bool IsPaused { get; private set; }

        private static readonly Color PanelFill = new Color(0.08f, 0.08f, 0.11f, 0.97f);
        private static readonly Color PauseAccent = new Color(0.75f, 0.2f, 0.2f, 1f); // red -- "paused," a stop state
        private static readonly Color SettingsAccent = new Color(0.4f, 0.42f, 0.58f, 1f);

        public static PauseMenuUI Build()
        {
            var go = new GameObject("PauseMenuUI");
            var p = go.AddComponent<PauseMenuUI>();
            p.BuildUI();
            return p;
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            // Back out of Settings to the main pause panel first, rather than resuming
            // gameplay straight from a sub-menu (which would leave it stuck on screen --
            // Toggle() only ever touches `panel`, not `settingsPanel`).
            if (settingsPanel != null && settingsPanel.activeSelf)
            {
                CloseSettings();
                return;
            }
            Toggle();
        }

        private void Toggle()
        {
            IsPaused = !IsPaused;
            panel.SetActive(IsPaused);
            Time.timeScale = IsPaused ? 0f : 1f;
            Cursor.lockState = IsPaused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = IsPaused;
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("PauseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // always above HUD/inventory/stat screen, regardless of creation order
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGO.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var card = BuildCard(panel.transform, new Vector2(440, 420), PauseAccent);

            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGO.transform.SetParent(card, false);
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0, 130);
            titleRect.sizeDelta = new Vector2(400, 80);
            var titleText = titleGO.GetComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 48;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
            titleText.text = "PAUSED";

            BuildButton(card, font, "Resume", new Vector2(0, 60), Toggle);
            BuildButton(card, font, "Settings", new Vector2(0, -10), OpenSettings);
            BuildButton(card, font, "Quit to Desktop", new Vector2(0, -80), QuitGame);

            BuildSettingsPanel(canvasGO.transform, font);

            panel.SetActive(false);
        }

        // Centered rounded card (see PanelSpriteFactory) sitting on top of a full-screen
        // dim scrim -- shared by both the main pause panel and the Settings sub-panel.
        private Transform BuildCard(Transform parent, Vector2 size, Color accent)
        {
            var cardGO = new GameObject("Card", typeof(RectTransform), typeof(Image));
            cardGO.transform.SetParent(parent, false);
            var rect = cardGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            var image = cardGO.GetComponent<Image>();
            image.sprite = PanelSpriteFactory.CreateRoundedSprite(PanelFill, accent);
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            return cardGO.transform;
        }

        private void OpenSettings()
        {
            panel.SetActive(false);
            settingsPanel.SetActive(true);
        }

        private void CloseSettings()
        {
            settingsPanel.SetActive(false);
            panel.SetActive(true);
        }

        private void BuildSettingsPanel(Transform canvasParent, Font font)
        {
            settingsPanel = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image));
            settingsPanel.transform.SetParent(canvasParent, false);
            var rect = settingsPanel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            settingsPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            var card = BuildCard(settingsPanel.transform, new Vector2(500, 400), SettingsAccent);

            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGO.transform.SetParent(card, false);
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0, 150);
            titleRect.sizeDelta = new Vector2(400, 60);
            var titleText = titleGO.GetComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 36;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
            titleText.text = "SETTINGS";

            var look = FindFirstObjectByType<FirstPersonLook>();
            BuildSlider(card, font, "Mouse Sensitivity", new Vector2(0, 60), 0.5f, 8f,
                look != null ? look.mouseSensitivity : 2.5f,
                v => { var l = FindFirstObjectByType<FirstPersonLook>(); if (l != null) l.mouseSensitivity = v; });

            BuildSlider(card, font, "Field of View", new Vector2(0, -10), 60f, 100f,
                Camera.main != null ? Camera.main.fieldOfView : 82f,
                v => { if (Camera.main != null) Camera.main.fieldOfView = v; });

            BuildButton(card, font, "Back", new Vector2(0, -130), CloseSettings);

            settingsPanel.SetActive(false);
        }

        private void BuildSlider(Transform parent, Font font, string labelPrefix, Vector2 anchoredPos,
            float min, float max, float initial, UnityEngine.Events.UnityAction<float> onChanged)
        {
            var container = new GameObject(labelPrefix + "Row", typeof(RectTransform));
            container.transform.SetParent(parent, false);
            var containerRect = container.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = anchoredPos;
            containerRect.sizeDelta = new Vector2(420, 50);

            var labelGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGO.transform.SetParent(container.transform, false);
            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 1);
            labelRect.anchorMax = new Vector2(1, 1);
            labelRect.pivot = new Vector2(0.5f, 1);
            labelRect.sizeDelta = new Vector2(0, 22);
            var labelText = labelGO.GetComponent<Text>();
            labelText.font = font;
            labelText.fontSize = 15;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.text = $"{labelPrefix}: {initial:0.0}";

            var sliderGO = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGO.transform.SetParent(container.transform, false);
            var sliderRect = sliderGO.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0, 0);
            sliderRect.anchorMax = new Vector2(1, 0);
            sliderRect.pivot = new Vector2(0.5f, 0);
            sliderRect.sizeDelta = new Vector2(0, 20);
            sliderRect.anchoredPosition = new Vector2(0, 2);

            var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGO.transform.SetParent(sliderGO.transform, false);
            var bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.25f);
            bgRect.anchorMax = new Vector2(1, 0.75f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImage = bgGO.GetComponent<Image>();
            bgImage.sprite = PanelSpriteFactory.CreateRoundedSprite(new Color(0.18f, 0.18f, 0.22f), new Color(0.35f, 0.35f, 0.42f), size: 32, radius: 8, borderThickness: 2);
            bgImage.type = Image.Type.Sliced;
            bgImage.color = Color.white;

            var handleAreaGO = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleAreaGO.transform.SetParent(sliderGO.transform, false);
            var handleAreaRect = handleAreaGO.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(8, 0);
            handleAreaRect.offsetMax = new Vector2(-8, 0);

            var handleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGO.transform.SetParent(handleAreaGO.transform, false);
            var handleRect = handleGO.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(14, 24);
            var handleImg = handleGO.GetComponent<Image>();
            handleImg.color = new Color(0.7f, 0.8f, 0.95f);

            var slider = sliderGO.GetComponent<Slider>();
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = initial;
            slider.onValueChanged.AddListener(v =>
            {
                labelText.text = $"{labelPrefix}: {v:0.0}";
                onChanged(v);
            });
        }

        private void BuildButton(Transform parent, Font font, string label, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(280, 52);
            if (buttonSprite == null)
                buttonSprite = PanelSpriteFactory.CreateRoundedSprite(new Color(0.2f, 0.2f, 0.24f), new Color(0.42f, 0.42f, 0.5f), size: 64, radius: 10, borderThickness: 3);
            var btnImage = go.GetComponent<Image>();
            btnImage.sprite = buttonSprite;
            btnImage.type = Image.Type.Sliced;
            btnImage.color = Color.white;

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(go.transform, false);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.GetComponent<Text>();
            text.font = font;
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;

            go.GetComponent<Button>().onClick.AddListener(onClick);
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
