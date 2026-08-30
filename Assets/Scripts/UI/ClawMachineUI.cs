using UnityEngine;
using UnityEngine.UI;
using DungeonCrawler.Core;
using DungeonCrawler.Inventory;
using DungeonCrawler.Visuals;
using DungeonCrawler.Audio;
using DungeonCrawler.World;

namespace DungeonCrawler.UI
{
    // Fairground claw machine -- a timing skill check stands in for real claw physics: a
    // marker sweeps back and forth along a track and the player has to stop it (by clicking
    // the same button that started the run) inside a randomized target zone to win a prize.
    // Same single-shared-instance/Show() pattern as ShopUI/GambleUI.
    public class ClawMachineUI : MonoBehaviour
    {
        private static ClawMachineUI instance;

        private GameObject panelRoot;
        private Text goldText;
        private Text resultText;
        private Button playButton;
        private Text playButtonLabel;
        private RectTransform trackRect;
        private RectTransform targetZoneRect;
        private RectTransform markerRect;
        private Font font;

        private ClawMachineNPC machine;
        private InventorySystem inventory;
        private PlayerWallet wallet;

        private bool playing;
        private float markerSpeed = 1.4f;
        private float targetMin;
        private float targetMax;

        private const float TrackWidth = 380f;

        private static readonly Color PanelFill = new Color(0.08f, 0.05f, 0.1f, 0.97f);
        private static readonly Color PanelBorder = new Color(0.85f, 0.25f, 0.65f, 1f); // arcade magenta

        public static void Show(ClawMachineNPC machine, InventorySystem inventory, PlayerWallet wallet)
        {
            if (instance == null)
            {
                var go = new GameObject("ClawMachineUI");
                instance = go.AddComponent<ClawMachineUI>();
                instance.BuildUI();
            }
            instance.Open(machine, inventory, wallet);
        }

        private void Open(ClawMachineNPC m, InventorySystem inv, PlayerWallet w)
        {
            if (wallet != null) wallet.OnChanged -= RefreshGold;
            machine = m;
            inventory = inv;
            wallet = w;
            if (wallet != null) wallet.OnChanged += RefreshGold;

            playing = false;
            resultText.text = machine != null && machine.prizePool.Length > 0
                ? "Stop the claw over the glowing zone to win a prize!"
                : "The machine is empty right now.";
            resultText.color = new Color(0.85f, 0.85f, 0.85f);
            targetZoneRect.gameObject.SetActive(false);
            markerRect.anchoredPosition = new Vector2(0, markerRect.anchoredPosition.y);

            panelRoot.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RefreshGold();
        }

        private void Close()
        {
            playing = false;
            panelRoot.SetActive(false);
            if (wallet != null) wallet.OnChanged -= RefreshGold;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (!playing || panelRoot == null || !panelRoot.activeSelf) return;

            float t = Mathf.PingPong(Time.time * markerSpeed, 1f);
            markerRect.anchoredPosition = new Vector2((t - 0.5f) * TrackWidth, markerRect.anchoredPosition.y);
        }

        private void RefreshGold()
        {
            goldText.text = wallet != null ? $"{wallet.Gold}g" : "0g";
            int cost = machine != null ? machine.cost : 0;
            bool hasPrizes = machine != null && machine.prizePool.Length > 0;
            playButton.interactable = hasPrizes && (playing || (wallet != null && wallet.Gold >= cost));
            playButtonLabel.text = playing ? "STOP!" : $"Play ({cost}g)";
        }

        private void OnPlayButtonClicked()
        {
            if (machine == null) return;

            if (!playing)
            {
                if (wallet == null || !wallet.Spend(machine.cost)) return;

                float width = Random.Range(0.14f, 0.22f);
                targetMin = Random.Range(0f, 1f - width);
                targetMax = targetMin + width;
                targetZoneRect.anchorMin = new Vector2(targetMin, 0f);
                targetZoneRect.anchorMax = new Vector2(targetMax, 1f);
                targetZoneRect.gameObject.SetActive(true);

                playing = true;
                resultText.text = "Time it...";
                resultText.color = new Color(0.85f, 0.85f, 0.85f);
                RefreshGold();
                return;
            }

            playing = false;
            float landedT = (markerRect.anchoredPosition.x / TrackWidth) + 0.5f;
            bool win = landedT >= targetMin && landedT <= targetMax;

            if (win)
            {
                var prize = machine.prizePool[Random.Range(0, machine.prizePool.Length)];
                bool added = inventory != null && inventory.AddItem(prize);
                resultText.text = added ? $"Got it! You win: {prize.itemName}" : "Got it! But your inventory is full.";
                resultText.color = new Color(0.4f, 0.85f, 0.45f);
                SfxLibrary.PlayAt(SfxLibrary.Win, transform.position, 0.5f);
            }
            else
            {
                resultText.text = "So close -- the claw slipped.";
                resultText.color = new Color(0.8f, 0.4f, 0.4f);
                SfxLibrary.PlayAt(SfxLibrary.Lose, transform.position, 0.5f);
            }

            targetZoneRect.gameObject.SetActive(false);
            RefreshGold();
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("ClawMachineCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            panelRoot = new GameObject("ClawPanel", typeof(RectTransform), typeof(Image));
            panelRoot.transform.SetParent(canvasGO.transform, false);
            var panelRect = panelRoot.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(480, 360);
            var panelImage = panelRoot.GetComponent<Image>();
            panelImage.sprite = PanelSpriteFactory.CreateRoundedSprite(PanelFill, PanelBorder);
            panelImage.type = Image.Type.Sliced;
            panelImage.color = Color.white;

            var buttonSprite = PanelSpriteFactory.CreateRoundedSprite(new Color(0.2f, 0.2f, 0.24f), new Color(0.4f, 0.4f, 0.48f), size: 64, radius: 10, borderThickness: 3);

            MakeLabel(panelRoot.transform, 26, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0, -34), new Vector2(420, 40), Color.white).text = "CLAW MACHINE";

            goldText = MakeLabel(panelRoot.transform, 20, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(1f, 1f), new Vector2(-28, -34), new Vector2(160, 34), new Color(1f, 0.84f, 0.2f));

            resultText = MakeLabel(panelRoot.transform, 16, FontStyle.Italic, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0, -78), new Vector2(420, 50), new Color(0.85f, 0.85f, 0.85f));
            resultText.horizontalOverflow = HorizontalWrapMode.Wrap;

            // Track background
            var trackBgGO = new GameObject("Track", typeof(RectTransform), typeof(Image));
            trackBgGO.transform.SetParent(panelRoot.transform, false);
            trackRect = trackBgGO.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0.5f, 0f);
            trackRect.anchorMax = new Vector2(0.5f, 0f);
            trackRect.pivot = new Vector2(0.5f, 0f);
            trackRect.anchoredPosition = new Vector2(0, 130);
            trackRect.sizeDelta = new Vector2(TrackWidth + 20, 34);
            trackBgGO.GetComponent<Image>().color = new Color(0.15f, 0.12f, 0.18f, 0.9f);

            // Target zone -- sized/positioned per attempt via anchorMin/Max.x
            var targetGO = new GameObject("TargetZone", typeof(RectTransform), typeof(Image));
            targetGO.transform.SetParent(trackBgGO.transform, false);
            targetZoneRect = targetGO.GetComponent<RectTransform>();
            targetZoneRect.anchorMin = new Vector2(0.4f, 0f);
            targetZoneRect.anchorMax = new Vector2(0.6f, 1f);
            targetZoneRect.offsetMin = Vector2.zero;
            targetZoneRect.offsetMax = Vector2.zero;
            targetGO.GetComponent<Image>().color = new Color(0.4f, 0.9f, 0.5f, 0.85f);

            // Moving marker -- a thin bright pointer, positioned via anchoredPosition rather
            // than the anchorMax fill technique since this is a point, not a bar.
            var markerGO = new GameObject("Marker", typeof(RectTransform), typeof(Image));
            markerGO.transform.SetParent(trackBgGO.transform, false);
            markerRect = markerGO.GetComponent<RectTransform>();
            markerRect.anchorMin = new Vector2(0.5f, 0.5f);
            markerRect.anchorMax = new Vector2(0.5f, 0.5f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = new Vector2(6, 44);
            markerRect.anchoredPosition = Vector2.zero;
            markerGO.GetComponent<Image>().color = Color.white;

            playButton = BuildButton(panelRoot.transform, "Play", new Vector2(0, 70), buttonSprite, OnPlayButtonClicked);
            playButton.GetComponent<Image>().color = new Color(0.8f, 0.25f, 0.55f);
            playButtonLabel = playButton.GetComponentInChildren<Text>();

            BuildButton(panelRoot.transform, "Leave", new Vector2(0, 14), buttonSprite, Close);

            panelRoot.SetActive(false);
        }

        private Button BuildButton(Transform parent, string label, Vector2 anchoredPos, Sprite sprite, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(260, 48);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;

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

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            return button;
        }

        private Text MakeLabel(Transform parent, int fontSize, FontStyle style, TextAnchor anchor,
            Vector2 anchorPoint, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorPoint;
            rect.anchorMax = anchorPoint;
            rect.pivot = anchorPoint;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            return text;
        }
    }
}
