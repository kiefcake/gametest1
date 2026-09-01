using UnityEngine;
using UnityEngine.UI;
using DungeonCrawler.Core;
using DungeonCrawler.Classes;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.UI
{
    // Toggleable (C) stat sheet -- current effective value and potion progress (X/5) for
    // each of the 8 stats, plus a RealmEye-style "Stats Maxed: X/8" summary (RealmEye shows
    // this exact "N/8" badge on every character's row -- see StatBlock.IsMaxed, which this
    // reads from). Built at runtime like the rest of the HUD.
    public class StatScreenUI : MonoBehaviour
    {
        private PlayerCharacter player;
        private GameObject panel;
        private Text[] statTexts;
        private RectTransform[] barFills;
        private Image[] barImages;
        private Text maxedText;

        private static readonly Color BarColor = new Color(0.35f, 0.55f, 0.85f);
        private static readonly Color MaxedBarColor = new Color(0.9f, 0.75f, 0.25f); // gold -- matches the maxed-badge color below
        private static readonly Color MaxedTextColor = new Color(0.9f, 0.75f, 0.25f);
        private static readonly Color PanelFill = new Color(0.08f, 0.08f, 0.11f, 0.97f);
        private static readonly Color PanelBorder = new Color(0.35f, 0.55f, 0.85f, 1f); // blue accent -- matches the stat bars

        private static readonly StatType[] AllStats =
        {
            StatType.HP, StatType.MP, StatType.ATT, StatType.DEF,
            StatType.SPD, StatType.DEX, StatType.VIT, StatType.WIS
        };

        public static StatScreenUI Build(PlayerCharacter player)
        {
            var go = new GameObject("StatScreenUI");
            var s = go.AddComponent<StatScreenUI>();
            s.player = player;
            s.BuildUI();
            return s;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.C)) panel.SetActive(!panel.activeSelf);
            if (panel.activeSelf) Refresh();
        }

        private void Refresh()
        {
            if (player == null || player.Stats == null) return;

            int maxedCount = 0;
            for (int i = 0; i < AllStats.Length; i++)
            {
                var stat = AllStats[i];
                float val = player.Stats.GetValue(stat);
                int potions = player.Stats.PotionsApplied(stat);
                bool maxed = player.Stats.IsMaxed(stat);
                if (maxed) maxedCount++;

                statTexts[i].text = $"{stat,-4} {val,6:0.#}   [{potions}/{StatBlock.MAX_POTIONS_PER_STAT}]";
                statTexts[i].color = maxed ? MaxedTextColor : new Color(0.9f, 0.9f, 0.9f);

                var max = barFills[i].anchorMax;
                max.x = Mathf.Clamp01((float)potions / StatBlock.MAX_POTIONS_PER_STAT);
                barFills[i].anchorMax = max;
                barImages[i].color = maxed ? MaxedBarColor : BarColor;
            }

            maxedText.text = $"Stats Maxed: {maxedCount}/8";
            maxedText.color = maxedCount >= 8 ? MaxedTextColor : new Color(0.75f, 0.75f, 0.78f);
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("StatCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10; // above the base HUD, below the pause menu
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGO.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = new Vector2(-24, -24);
            panelRect.sizeDelta = new Vector2(380, 480);
            var panelImage = panel.GetComponent<Image>();
            panelImage.sprite = PanelSpriteFactory.CreateRoundedSprite(PanelFill, PanelBorder);
            panelImage.type = Image.Type.Sliced;
            panelImage.color = Color.white;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGO.transform.SetParent(panel.transform, false);
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0, -16);
            titleRect.sizeDelta = new Vector2(340, 34);
            var titleText = titleGO.GetComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 20;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
            titleText.text = "STATS (C to close)";

            // Class + role line, RealmEye-style (every character row there leads with its
            // class) -- colored with the same role tint the placeholder capsule body uses.
            var classGO = new GameObject("ClassLine", typeof(RectTransform), typeof(Text));
            classGO.transform.SetParent(panel.transform, false);
            var classRect = classGO.GetComponent<RectTransform>();
            classRect.anchorMin = new Vector2(0.5f, 1f);
            classRect.anchorMax = new Vector2(0.5f, 1f);
            classRect.pivot = new Vector2(0.5f, 1f);
            classRect.anchoredPosition = new Vector2(0, -54);
            classRect.sizeDelta = new Vector2(340, 24);
            var classText = classGO.GetComponent<Text>();
            classText.font = font;
            classText.fontSize = 15;
            classText.fontStyle = FontStyle.Bold;
            classText.alignment = TextAnchor.MiddleCenter;
            if (player != null && player.classDefinition != null)
            {
                classText.color = ClassDefinition.RoleColor(player.classDefinition.role);
                classText.text = $"{player.classDefinition.className} -- {player.classDefinition.role}";
            }

            var maxedGO = new GameObject("MaxedSummary", typeof(RectTransform), typeof(Text));
            maxedGO.transform.SetParent(panel.transform, false);
            var maxedRect = maxedGO.GetComponent<RectTransform>();
            maxedRect.anchorMin = new Vector2(0.5f, 1f);
            maxedRect.anchorMax = new Vector2(0.5f, 1f);
            maxedRect.pivot = new Vector2(0.5f, 1f);
            maxedRect.anchoredPosition = new Vector2(0, -82);
            maxedRect.sizeDelta = new Vector2(340, 24);
            maxedText = maxedGO.GetComponent<Text>();
            maxedText.font = font;
            maxedText.fontSize = 15;
            maxedText.fontStyle = FontStyle.Bold;
            maxedText.alignment = TextAnchor.MiddleCenter;
            maxedText.text = "Stats Maxed: 0/8";

            statTexts = new Text[AllStats.Length];
            barFills = new RectTransform[AllStats.Length];
            barImages = new Image[AllStats.Length];

            for (int i = 0; i < AllStats.Length; i++)
            {
                float y = -118 - i * 46;

                var rowGO = new GameObject("Stat_" + AllStats[i], typeof(RectTransform), typeof(Text));
                rowGO.transform.SetParent(panel.transform, false);
                var rowRect = rowGO.GetComponent<RectTransform>();
                rowRect.anchorMin = new Vector2(0f, 1f);
                rowRect.anchorMax = new Vector2(1f, 1f);
                rowRect.pivot = new Vector2(0.5f, 1f);
                rowRect.anchoredPosition = new Vector2(0, y);
                rowRect.sizeDelta = new Vector2(-32, 24);

                var text = rowGO.GetComponent<Text>();
                text.font = font;
                text.fontSize = 16;
                text.alignment = TextAnchor.MiddleLeft;
                text.color = new Color(0.9f, 0.9f, 0.9f);
                statTexts[i] = text;

                // Potion-progress bar (X/5), same anchorMax.x fill technique as the HP/MP
                // bars -- Image.fillAmount silently doesn't repaint here on a runtime-built
                // Type.Filled Image with no sprite (see PlayerHUD's header comment).
                var barBgGO = new GameObject("BarBg", typeof(RectTransform), typeof(Image));
                barBgGO.transform.SetParent(panel.transform, false);
                var barBgRect = barBgGO.GetComponent<RectTransform>();
                barBgRect.anchorMin = new Vector2(0f, 1f);
                barBgRect.anchorMax = new Vector2(1f, 1f);
                barBgRect.pivot = new Vector2(0.5f, 1f);
                barBgRect.anchoredPosition = new Vector2(0, y - 20);
                barBgRect.sizeDelta = new Vector2(-32, 8);
                barBgGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);

                var barFillGO = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
                barFillGO.transform.SetParent(barBgGO.transform, false);
                var barFillRect = barFillGO.GetComponent<RectTransform>();
                // Starts full-size (anchorMin (0,0) - anchorMax (1,1)); Refresh() shrinks it
                // by pulling anchorMax.x down toward the potion fraction, same as PlayerHUD's
                // HP/MP bars -- anchorMin.y/anchorMax.y stay put so the bar keeps full height.
                barFillRect.anchorMin = Vector2.zero;
                barFillRect.anchorMax = Vector2.one;
                barFillRect.offsetMin = Vector2.zero;
                barFillRect.offsetMax = Vector2.zero;
                var barFillImg = barFillGO.GetComponent<Image>();
                barFillImg.color = BarColor;

                barFills[i] = barFillRect;
                barImages[i] = barFillImg;
            }

            panel.SetActive(false);
        }
    }
}
