using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DungeonCrawler.Core;
using DungeonCrawler.Visuals;
using DungeonCrawler.Audio;

namespace DungeonCrawler.UI
{
    // Tavern gambling table -- wager gold on a weighted roll (5% jackpot at 5x, 35% a
    // simple double, 60% nothing back: a 5% house edge, enough to make it a real gold sink
    // rather than a free-money exploit, while still paying out often enough to feel worth
    // trying). Same single-shared-instance/Show() pattern as ShopUI.
    public class GambleUI : MonoBehaviour
    {
        private static GambleUI instance;

        private GameObject panelRoot;
        private Text goldText;
        private Text betText;
        private Text resultText;
        private Button betDownButton;
        private Button betUpButton;
        private Button wagerButton;
        private Font font;

        private PlayerWallet wallet;
        private int bet = 25;
        private bool rolling;

        private const int BetStep = 5;
        private const int MinBet = 5;
        private const int MaxBet = 250;

        private static readonly Color PanelFill = new Color(0.1f, 0.06f, 0.07f, 0.97f);
        private static readonly Color PanelBorder = new Color(0.75f, 0.2f, 0.2f, 1f); // deep red -- tavern felt, not a shop

        public static void Show(PlayerWallet wallet)
        {
            if (instance == null)
            {
                var go = new GameObject("GambleUI");
                instance = go.AddComponent<GambleUI>();
                instance.BuildUI();
            }
            instance.Open(wallet);
        }

        private void Open(PlayerWallet w)
        {
            if (wallet != null) wallet.OnChanged -= RefreshGold;
            wallet = w;
            if (wallet != null) wallet.OnChanged += RefreshGold;

            rolling = false;
            resultText.text = "Place your wager and roll the bones.";
            resultText.color = new Color(0.8f, 0.8f, 0.8f);
            ClampBet();

            panelRoot.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RefreshGold();
        }

        private void Close()
        {
            panelRoot.SetActive(false);
            if (wallet != null) wallet.OnChanged -= RefreshGold;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void ClampBet()
        {
            int cap = wallet != null ? Mathf.Min(MaxBet, Mathf.Max(MinBet, wallet.Gold)) : MaxBet;
            bet = Mathf.Clamp(bet, MinBet, cap);
        }

        private void RefreshGold()
        {
            goldText.text = wallet != null ? $"{wallet.Gold}g" : "0g";
            ClampBet();
            betText.text = $"Bet: {bet}g";
            bool canWager = !rolling && wallet != null && wallet.Gold >= bet;
            wagerButton.interactable = canWager;
            betDownButton.interactable = !rolling && bet > MinBet;
            betUpButton.interactable = !rolling && wallet != null && bet < Mathf.Min(MaxBet, wallet.Gold);
        }

        private void ChangeBet(int delta)
        {
            bet = Mathf.Clamp(bet + delta, MinBet, MaxBet);
            RefreshGold();
        }

        private void OnWagerClicked()
        {
            if (rolling || wallet == null || wallet.Gold < bet) return;
            if (!wallet.Spend(bet)) return;
            StartCoroutine(RollRoutine(bet));
        }

        private IEnumerator RollRoutine(int wager)
        {
            rolling = true;
            wagerButton.interactable = false;
            betDownButton.interactable = false;
            betUpButton.interactable = false;
            resultText.color = new Color(0.85f, 0.85f, 0.85f);
            resultText.text = "Rolling...";

            float elapsed = 0f;
            while (elapsed < 0.6f)
            {
                resultText.text = "Rolling" + new string('.', 1 + Mathf.FloorToInt(elapsed * 6f) % 3);
                elapsed += Time.deltaTime;
                yield return null;
            }

            float roll = Random.value;
            int payout;
            string message;
            Color color;
            AudioClip clip;

            if (roll < 0.05f)
            {
                payout = wager * 5;
                message = $"JACKPOT! You win {payout}g!";
                color = new Color(1f, 0.84f, 0.2f);
                clip = SfxLibrary.Win;
            }
            else if (roll < 0.40f)
            {
                payout = wager * 2;
                message = $"A winner! You take {payout}g.";
                color = new Color(0.4f, 0.85f, 0.45f);
                clip = SfxLibrary.Win;
            }
            else
            {
                payout = 0;
                message = "The house wins this one.";
                color = new Color(0.8f, 0.4f, 0.4f);
                clip = SfxLibrary.Lose;
            }

            if (payout > 0) wallet.Add(payout);
            SfxLibrary.PlayAt(clip, transform.position, 0.5f);
            resultText.text = message;
            resultText.color = color;

            rolling = false;
            RefreshGold();
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("GambleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            panelRoot = new GameObject("GamblePanel", typeof(RectTransform), typeof(Image));
            panelRoot.transform.SetParent(canvasGO.transform, false);
            var panelRect = panelRoot.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(460, 400);
            var panelImage = panelRoot.GetComponent<Image>();
            panelImage.sprite = PanelSpriteFactory.CreateRoundedSprite(PanelFill, PanelBorder);
            panelImage.type = Image.Type.Sliced;
            panelImage.color = Color.white;

            var buttonSprite = PanelSpriteFactory.CreateRoundedSprite(new Color(0.2f, 0.2f, 0.24f), new Color(0.4f, 0.4f, 0.48f), size: 64, radius: 10, borderThickness: 3);

            MakeLabel(panelRoot.transform, 26, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0, -34), new Vector2(400, 40), Color.white).text = "THE GAMBLER'S TABLE";
            MakeLabel(panelRoot.transform, 14, FontStyle.Italic, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0, -66), new Vector2(400, 26), new Color(0.7f, 0.68f, 0.65f)).text
                = "5x jackpot, 2x on a winner, nothing on a bad roll.";

            goldText = MakeLabel(panelRoot.transform, 20, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(1f, 1f), new Vector2(-28, -34), new Vector2(160, 34), new Color(1f, 0.84f, 0.2f));

            resultText = MakeLabel(panelRoot.transform, 17, FontStyle.Italic, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(400, 60), new Color(0.85f, 0.85f, 0.85f));
            resultText.horizontalOverflow = HorizontalWrapMode.Wrap;

            betText = MakeLabel(panelRoot.transform, 20, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0, 108), new Vector2(200, 34), Color.white);

            betDownButton = MakeSmallButton(panelRoot.transform, "-", new Vector2(-90, 108), buttonSprite, () => ChangeBet(-BetStep));
            betUpButton = MakeSmallButton(panelRoot.transform, "+", new Vector2(90, 108), buttonSprite, () => ChangeBet(BetStep));

            wagerButton = BuildButton(panelRoot.transform, "Roll the Dice", new Vector2(0, 56), buttonSprite, OnWagerClicked);
            wagerButton.GetComponent<Image>().color = new Color(0.75f, 0.2f, 0.2f);

            BuildButton(panelRoot.transform, "Leave", new Vector2(0, 14), buttonSprite, Close);

            panelRoot.SetActive(false);
        }

        private Button MakeSmallButton(Transform parent, string label, Vector2 anchoredPos, Sprite sprite, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(48, 40);
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
            text.fontSize = 22;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            return button;
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
