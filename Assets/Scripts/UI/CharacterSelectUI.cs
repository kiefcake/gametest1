using System;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonCrawler.UI
{
    // Full-screen class picker shown before a run starts. Built at runtime like the rest
    // of the HUD (see PlayerHUD's header comment for why -- avoids the edit-time-asset
    // timing hazard that broke the loot tables). GameBootstrap.Start() calls Show() and
    // only spawns the run once a card is clicked.
    public class CharacterSelectUI : MonoBehaviour
    {
        private struct ClassOption
        {
            public GameBootstrap.TestClass testClass;
            public string name;
            public string role;
            public string blurb;
            public Color color;
        }

        private static readonly ClassOption[] Options =
        {
            new ClassOption { testClass = GameBootstrap.TestClass.Knight, name = "Knight", role = "TANK",
                blurb = "High HP. Shield Slam applies ArmorBreak. Bulwark Stance/Unbreakable cut incoming damage.",
                color = new Color(0.35f, 0.55f, 0.85f) },
            new ClassOption { testClass = GameBootstrap.TestClass.Priest, name = "Priest", role = "HEAL",
                blurb = "Mending Light heals + cleanses, Rebirth is a big emergency heal -- but Holy Smite deals real damage and cripples enemy healing.",
                color = new Color(0.95f, 0.85f, 0.4f) },
            new ClassOption { testClass = GameBootstrap.TestClass.Paladin, name = "Paladin", role = "BUFF",
                blurb = "Empower boosts damage output. Hex weakens an enemy. Chronoshift paralyzes.",
                color = new Color(0.65f, 0.4f, 0.85f) },
            new ClassOption { testClass = GameBootstrap.TestClass.Wizard, name = "Wizard", role = "DAMAGE",
                blurb = "Venom Bolt poisons. Icicle freezes. Death Mark curses -- pure damage kit.",
                color = new Color(0.85f, 0.25f, 0.25f) },
        };

        public static void Show(Action<GameBootstrap.TestClass> onSelected)
        {
            var go = new GameObject("CharacterSelectUI");
            var ui = go.AddComponent<CharacterSelectUI>();
            ui.BuildUI(onSelected);
        }

        private void BuildUI(Action<GameBootstrap.TestClass> onSelected)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var canvasGO = new GameObject("SelectCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgGO.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.07f, 0.97f);

            var font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var titleRect = MakeAnchoredRect(canvasGO.transform, new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(800, 100));
            var titleText = titleRect.gameObject.AddComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 52;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
            titleText.text = "Choose Your Class";

            const float cardWidth = 340f;
            const float spacing = 30f;
            float totalWidth = cardWidth * Options.Length + spacing * (Options.Length - 1);
            float startX = -totalWidth / 2f + cardWidth / 2f;

            for (int i = 0; i < Options.Length; i++)
            {
                var opt = Options[i];
                float x = startX + i * (cardWidth + spacing);
                BuildCard(canvasGO.transform, font, opt, x, cardWidth, () =>
                {
                    Destroy(gameObject);
                    onSelected?.Invoke(opt.testClass);
                });
            }
        }

        private RectTransform MakeAnchoredRect(Transform parent, Vector2 anchor, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject("Rect", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        private void BuildCard(Transform parent, Font font, ClassOption opt, float x, float width, UnityEngine.Events.UnityAction onClick)
        {
            var cardGO = new GameObject(opt.name + "Card", typeof(RectTransform), typeof(Image), typeof(Button));
            cardGO.transform.SetParent(parent, false);
            var rect = cardGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, -20);
            rect.sizeDelta = new Vector2(width, 460);
            cardGO.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.15f, 0.95f);
            cardGO.GetComponent<Button>().onClick.AddListener(onClick);

            var stripeGO = new GameObject("Stripe", typeof(RectTransform), typeof(Image));
            stripeGO.transform.SetParent(cardGO.transform, false);
            var stripeRect = stripeGO.GetComponent<RectTransform>();
            stripeRect.anchorMin = new Vector2(0, 1);
            stripeRect.anchorMax = new Vector2(1, 1);
            stripeRect.pivot = new Vector2(0.5f, 1);
            stripeRect.sizeDelta = new Vector2(0, 14);
            stripeGO.GetComponent<Image>().color = opt.color;

            MakeCardText(cardGO.transform, font, opt.name, 30, FontStyle.Bold, Color.white, new Vector2(0, -50), new Vector2(width - 40, 50));
            MakeCardText(cardGO.transform, font, opt.role, 16, FontStyle.Bold, opt.color, new Vector2(0, -85), new Vector2(width - 40, 30));
            MakeCardText(cardGO.transform, font, opt.blurb, 15, FontStyle.Normal, new Color(0.82f, 0.82f, 0.82f), new Vector2(0, -160), new Vector2(width - 50, 260));

            var hintRect = MakeAnchoredRect(cardGO.transform, new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(width - 40, 30));
            var hint = hintRect.gameObject.AddComponent<Text>();
            hint.font = font;
            hint.fontSize = 14;
            hint.alignment = TextAnchor.MiddleCenter;
            hint.color = new Color(0.6f, 0.6f, 0.65f);
            hint.text = "Click to select";
        }

        private void MakeCardText(Transform parent, Font font, string content, int size, FontStyle style, Color color,
            Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var rect = MakeAnchoredRect(parent, new Vector2(0.5f, 1f), anchoredPos, sizeDelta);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAnchor.UpperCenter;
            text.color = color;
            text.text = content;
        }
    }
}
