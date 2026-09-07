using System;
using UnityEngine;
using UnityEngine.UI;
using DungeonCrawler.Core;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.UI
{
    // Full-screen class picker shown before a run starts. Built at runtime like the rest
    // of the HUD (see PlayerHUD's header comment for why -- avoids the edit-time-asset
    // timing hazard that broke the loot tables). GameBootstrap.Start() calls Show() and
    // only spawns the run once a card is clicked.
    public class CharacterSelectUI : MonoBehaviour
    {
        private Image hardcoreButtonImage;
        private Text hardcoreButtonText;
        private static readonly Color HardcoreOffColor = DungeonUITheme.Surface;
        private static readonly Color HardcoreOnColor = DungeonUITheme.HpFill;
        private Sprite hardcoreOffSprite, hardcoreOnSprite;

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
            bgGO.GetComponent<Image>().color = DungeonUITheme.Ground;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var titleRect = MakeAnchoredRect(canvasGO.transform, new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(800, 100));
            var titleText = titleRect.gameObject.AddComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 52;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = DungeonUITheme.TextPrimary;
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

            BuildHardcoreToggle(canvasGO.transform, font);
        }

        // Relocated here from the old DungeonSelectUI (removed -- this was its only
        // remaining live feature once the open world replaced its dungeon-portal role).
        // Only shown once PlayerProgress.HardcoreUnlocked -- earned by beating all three
        // dungeon bosses at least once, not available from the start.
        private void BuildHardcoreToggle(Transform canvasParent, Font font)
        {
            if (!PlayerProgress.HardcoreUnlocked) return;

            var go = new GameObject("HardcoreToggle", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(canvasParent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0, 40);
            rect.sizeDelta = new Vector2(320, 48);
            hardcoreButtonImage = go.GetComponent<Image>();
            // Two fully-baked sprites (not one sprite + a runtime Image.color tint) --
            // Off gets a neutral border, On gets an ember one, so RefreshHardcoreToggle
            // swaps the sprite reference itself instead of tinting a shared bake, which
            // would multiply the baked color against the tint (see PlayerHUD/ShopUI's
            // identical fix for the same failure mode).
            hardcoreOffSprite = PanelSpriteFactory.CreateChamferedSprite(HardcoreOffColor, DungeonUITheme.Border, 64, 8, 3);
            hardcoreOnSprite = PanelSpriteFactory.CreateChamferedSprite(HardcoreOnColor, DungeonUITheme.EmberDim, 64, 8, 3);
            hardcoreButtonImage.type = Image.Type.Sliced;

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(go.transform, false);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            hardcoreButtonText = textGO.GetComponent<Text>();
            hardcoreButtonText.font = font;
            hardcoreButtonText.fontSize = 18;
            hardcoreButtonText.fontStyle = FontStyle.Bold;
            hardcoreButtonText.alignment = TextAnchor.MiddleCenter;
            hardcoreButtonText.color = DungeonUITheme.TextPrimary;

            go.GetComponent<Button>().onClick.AddListener(ToggleHardcore);
            RefreshHardcoreToggle();
        }

        private void ToggleHardcore()
        {
            RunModifiers.DoubleDamageTaken = !RunModifiers.DoubleDamageTaken;
            RefreshHardcoreToggle();
        }

        private void RefreshHardcoreToggle()
        {
            bool on = RunModifiers.DoubleDamageTaken;
            hardcoreButtonText.text = on ? "Hardcore: ON (2x dmg taken)" : "Hardcore: OFF";
            hardcoreButtonImage.sprite = on ? hardcoreOnSprite : hardcoreOffSprite;
            hardcoreButtonImage.color = Color.white;
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
            // Each class keeps its own accent as the card's border/stripe/role color --
            // that per-class distinction was already good design, just flat and
            // borderless before. Chamfered frame now, same carved-stone language as
            // everywhere else, with the class's own color as its one departure per card.
            var cardImage = cardGO.GetComponent<Image>();
            cardImage.sprite = PanelSpriteFactory.CreateChamferedSprite(DungeonUITheme.SurfaceRaised, opt.color, 96, 14, 4);
            cardImage.type = Image.Type.Sliced;
            cardImage.color = Color.white;
            cardGO.GetComponent<Button>().onClick.AddListener(onClick);

            var stripeGO = new GameObject("Stripe", typeof(RectTransform), typeof(Image));
            stripeGO.transform.SetParent(cardGO.transform, false);
            var stripeRect = stripeGO.GetComponent<RectTransform>();
            stripeRect.anchorMin = new Vector2(0, 1);
            stripeRect.anchorMax = new Vector2(1, 1);
            stripeRect.pivot = new Vector2(0.5f, 1);
            stripeRect.sizeDelta = new Vector2(0, 14);
            stripeGO.GetComponent<Image>().color = opt.color;

            MakeCardText(cardGO.transform, font, opt.name, 30, FontStyle.Bold, DungeonUITheme.TextPrimary, new Vector2(0, -50), new Vector2(width - 40, 50));
            MakeCardText(cardGO.transform, font, opt.role, 16, FontStyle.Bold, opt.color, new Vector2(0, -85), new Vector2(width - 40, 30));
            MakeCardText(cardGO.transform, font, opt.blurb, 15, FontStyle.Normal, DungeonUITheme.TextMuted, new Vector2(0, -160), new Vector2(width - 50, 260));

            var hintRect = MakeAnchoredRect(cardGO.transform, new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(width - 40, 30));
            var hint = hintRect.gameObject.AddComponent<Text>();
            hint.font = font;
            hint.fontSize = 14;
            hint.alignment = TextAnchor.MiddleCenter;
            hint.color = DungeonUITheme.TextFaint;
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
