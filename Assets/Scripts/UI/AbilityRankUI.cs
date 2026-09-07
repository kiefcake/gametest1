using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DungeonCrawler.Abilities;
using DungeonCrawler.Classes;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.UI
{
    // Toggleable (K) ability rank & rune panel -- the spend side of the Essence/rank/rune
    // system built in AbilityCaster.cs. Until this existed, RankUp/ChooseRune were fully
    // wired and functional but unreachable: nothing in-game ever called them, so Essence
    // just accumulated with nowhere to go. Same runtime-build/toggle pattern as
    // StatScreenUI (C) -- a poll-driven Refresh() while the panel is active rather than
    // event-driven, since it's cheap and this codebase already does it that way.
    public class AbilityRankUI : MonoBehaviour
    {
        private PlayerCharacter player;
        private GameObject panel;
        private Text essenceHeaderText;
        private readonly List<AbilityRowUI> rows = new List<AbilityRowUI>();

        private static readonly Color PanelFill = DungeonUITheme.SurfaceRaised;
        private static readonly Color PanelBorder = DungeonUITheme.Border;
        private static readonly Color PipEmpty = new Color(0.29f, 0.21f, 0.15f);
        private static readonly Color RuneCardIdle = DungeonUITheme.Surface;
        private static readonly Color RuneCardChosen = DungeonUITheme.EmberFill;

        private class AbilityRowUI
        {
            public AbilityData ability;
            public Image[] pips;
            public Text costText;
            public Button rankButton;
            public Text rankButtonLabel;
            public GameObject runeSection;
            public Image runeACard;
            public Image runeBCard;
            public Text runeAText;
            public Text runeBText;
            public Button runeAButton;
            public Button runeBButton;
        }

        public static AbilityRankUI Build(PlayerCharacter player)
        {
            var go = new GameObject("AbilityRankUI");
            var ui = go.AddComponent<AbilityRankUI>();
            ui.player = player;
            ui.BuildUI();
            return ui;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.K)) panel.SetActive(!panel.activeSelf);
            if (panel.activeSelf) Refresh();
        }

        private void Refresh()
        {
            if (player == null || player.abilityCaster == null) return;

            int essenceAmount = player.essence != null ? player.essence.Amount : 0;
            essenceHeaderText.text = $"Essence: {essenceAmount}";

            foreach (var row in rows)
            {
                var ability = row.ability;
                if (ability == null) continue;

                int rank = player.abilityCaster.GetRank(ability);
                for (int i = 0; i < row.pips.Length; i++)
                    row.pips[i].color = i < rank ? DungeonUITheme.Ember : PipEmpty;

                bool canRankUp = player.abilityCaster.CanRankUp(ability, out int cost);
                if (canRankUp)
                {
                    bool afford = essenceAmount >= cost;
                    row.costText.text = $"{cost}";
                    row.costText.color = afford ? DungeonUITheme.TextBody : new Color(0.35f, 0.35f, 0.42f);
                    row.rankButtonLabel.text = "Rank Up";
                    row.rankButton.interactable = afford;
                }
                else
                {
                    row.costText.text = "MAX";
                    row.costText.color = DungeonUITheme.Ember;
                    row.rankButtonLabel.text = "Maxed";
                    row.rankButton.interactable = false;
                }

                bool hasRunes = ability.id != AbilityId.None;
                bool runeEligible = hasRunes && rank >= 3;
                row.runeSection.SetActive(runeEligible);
                if (!runeEligible) continue;

                var chosen = player.abilityCaster.GetRune(ability);
                bool locked = chosen != AbilityRune.None;
                row.runeACard.color = chosen == AbilityRune.A ? RuneCardChosen : RuneCardIdle;
                row.runeBCard.color = chosen == AbilityRune.B ? RuneCardChosen : RuneCardIdle;
                row.runeAButton.interactable = !locked;
                row.runeBButton.interactable = !locked;
            }
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("AbilityRankCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10; // above the base HUD, below the pause menu
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGO.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(640, 660);
            var panelImage = panel.GetComponent<Image>();
            panelImage.sprite = PanelSpriteFactory.CreateChamferedSprite(PanelFill, PanelBorder, 96, 16, 4);
            panelImage.type = Image.Type.Sliced;
            panelImage.color = Color.white;

            var titleText = MakeText(panel.transform, "Title", new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(560, 32),
                DungeonUITheme.DisplayFont, 24, TextAnchor.MiddleCenter, DungeonUITheme.TextPrimary, "ABILITY RANKS  (K to close)");
            titleText.fontStyle = FontStyle.Bold;

            essenceHeaderText = MakeText(panel.transform, "Essence", new Vector2(0.5f, 1f), new Vector2(0, -52), new Vector2(560, 24),
                font, 16, TextAnchor.MiddleCenter, DungeonUITheme.RankEssence, "Essence: 0");

            float rowTop = -92f;
            const float rowHeight = 176f;
            var abilities = player.abilityCaster.abilities;
            for (int i = 0; i < abilities.Count; i++)
            {
                BuildAbilityRow(panel.transform, font, abilities[i], rowTop - i * rowHeight);
            }

            panel.SetActive(false);
        }

        private void BuildAbilityRow(Transform parent, Font font, AbilityData ability, float y)
        {
            var rowRoot = new GameObject("Row_" + ability.abilityName, typeof(RectTransform));
            rowRoot.transform.SetParent(parent, false);
            var rowRect = rowRoot.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 1f);
            rowRect.anchorMax = new Vector2(0.5f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0, y);
            rowRect.sizeDelta = new Vector2(584, 160);

            var divider = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            divider.transform.SetParent(rowRoot.transform, false);
            var divRect = divider.GetComponent<RectTransform>();
            divRect.anchorMin = new Vector2(0f, 1f);
            divRect.anchorMax = new Vector2(1f, 1f);
            divRect.pivot = new Vector2(0.5f, 1f);
            divRect.anchoredPosition = Vector2.zero;
            divRect.sizeDelta = new Vector2(0, 1);
            divider.GetComponent<Image>().color = DungeonUITheme.Border;

            MakeText(rowRoot.transform, "Name", new Vector2(0f, 1f), new Vector2(4, -14), new Vector2(220, 24),
                DungeonUITheme.DisplayFont, 18, TextAnchor.MiddleLeft, DungeonUITheme.TextPrimary, ability.abilityName).fontStyle = FontStyle.Bold;

            // 3 rank pips, small diamonds (a 45-degree-rotated square) along the row header.
            var pips = new Image[3];
            for (int p = 0; p < 3; p++)
            {
                var pipGO = new GameObject($"Pip{p}", typeof(RectTransform), typeof(Image));
                pipGO.transform.SetParent(rowRoot.transform, false);
                var pipRect = pipGO.GetComponent<RectTransform>();
                pipRect.anchorMin = new Vector2(0f, 1f);
                pipRect.anchorMax = new Vector2(0f, 1f);
                pipRect.pivot = new Vector2(0.5f, 0.5f);
                pipRect.anchoredPosition = new Vector2(230 + p * 20f, -22);
                pipRect.sizeDelta = new Vector2(12, 12);
                pipRect.localRotation = Quaternion.Euler(0, 0, 45);
                pips[p] = pipGO.GetComponent<Image>();
                pips[p].color = PipEmpty;
            }

            var costText = MakeText(rowRoot.transform, "Cost", new Vector2(1f, 1f), new Vector2(-96, -14), new Vector2(60, 24),
                font, 16, TextAnchor.MiddleRight, DungeonUITheme.TextBody, "0");
            costText.fontStyle = FontStyle.Bold;

            var rankButtonGO = new GameObject("RankUpButton", typeof(RectTransform), typeof(Image), typeof(Button));
            rankButtonGO.transform.SetParent(rowRoot.transform, false);
            var rankBtnRect = rankButtonGO.GetComponent<RectTransform>();
            rankBtnRect.anchorMin = new Vector2(1f, 1f);
            rankBtnRect.anchorMax = new Vector2(1f, 1f);
            rankBtnRect.pivot = new Vector2(1f, 1f);
            rankBtnRect.anchoredPosition = new Vector2(-4, -4);
            rankBtnRect.sizeDelta = new Vector2(88, 34);
            var rankBtnImage = rankButtonGO.GetComponent<Image>();
            rankBtnImage.sprite = PanelSpriteFactory.CreateChamferedSprite(DungeonUITheme.EmberFill, DungeonUITheme.EmberDim, 48, 6, 3);
            rankBtnImage.type = Image.Type.Sliced;
            var rankButton = rankButtonGO.GetComponent<Button>();
            var rankLabel = MakeText(rankButtonGO.transform, "Label", Vector2.zero, Vector2.zero, Vector2.zero,
                font, 12, TextAnchor.MiddleCenter, DungeonUITheme.EmberBright, "Rank Up");
            var rankLabelRect = rankLabel.GetComponent<RectTransform>();
            rankLabelRect.anchorMin = Vector2.zero;
            rankLabelRect.anchorMax = Vector2.one;
            rankLabelRect.offsetMin = Vector2.zero;
            rankLabelRect.offsetMax = Vector2.zero;
            rankButton.onClick.AddListener(() => player.abilityCaster.RankUp(ability));

            var runeSection = new GameObject("RuneSection", typeof(RectTransform));
            runeSection.transform.SetParent(rowRoot.transform, false);
            var runeSectionRect = runeSection.GetComponent<RectTransform>();
            runeSectionRect.anchorMin = new Vector2(0f, 1f);
            runeSectionRect.anchorMax = new Vector2(1f, 1f);
            runeSectionRect.pivot = new Vector2(0.5f, 1f);
            runeSectionRect.anchoredPosition = new Vector2(0, -46);
            runeSectionRect.sizeDelta = new Vector2(0, 108);

            var runeA = BuildRuneCard(runeSectionRect.transform, font, new Vector2(0f, 0.5f), new Vector2(4, 0),
                ability.runeAName, ability.runeADescription, () => player.abilityCaster.ChooseRune(ability, AbilityRune.A));
            var runeB = BuildRuneCard(runeSectionRect.transform, font, new Vector2(1f, 0.5f), new Vector2(-4, 0),
                ability.runeBName, ability.runeBDescription, () => player.abilityCaster.ChooseRune(ability, AbilityRune.B));

            rows.Add(new AbilityRowUI
            {
                ability = ability,
                pips = pips,
                costText = costText,
                rankButton = rankButton,
                rankButtonLabel = rankLabel,
                runeSection = runeSection,
                runeACard = runeA.card,
                runeBCard = runeB.card,
                runeAText = runeA.text,
                runeBText = runeB.text,
                runeAButton = runeA.button,
                runeBButton = runeB.button,
            });
        }

        private (Image card, Text text, Button button) BuildRuneCard(Transform parent, Font font, Vector2 anchor, Vector2 anchoredPos,
            string runeName, string runeDesc, UnityEngine.Events.UnityAction onPick)
        {
            var cardGO = new GameObject("RuneCard_" + runeName, typeof(RectTransform), typeof(Image), typeof(Button));
            cardGO.transform.SetParent(parent, false);
            var rect = cardGO.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(284, 104);
            // White bake, not RuneCardIdle -- Refresh() re-tints Image.color between
            // RuneCardIdle/RuneCardChosen every frame, and a non-white bake would
            // multiply against that tint instead of showing the intended color (see
            // PlayerHUD's identical fix for the ability-slot background).
            var image = cardGO.GetComponent<Image>();
            image.sprite = PanelSpriteFactory.CreateChamferedSprite(Color.white, DungeonUITheme.Border, 64, 10, 3);
            image.type = Image.Type.Sliced;
            image.color = RuneCardIdle;
            var button = cardGO.GetComponent<Button>();
            button.onClick.AddListener(onPick);

            MakeText(cardGO.transform, "Name", new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(260, 20),
                DungeonUITheme.DisplayFont, 15, TextAnchor.MiddleCenter, DungeonUITheme.TextPrimary, string.IsNullOrEmpty(runeName) ? "--" : runeName).fontStyle = FontStyle.Bold;

            var desc = MakeText(cardGO.transform, "Desc", new Vector2(0.5f, 1f), new Vector2(0, -32), new Vector2(260, 64),
                font, 11, TextAnchor.UpperCenter, DungeonUITheme.TextMuted, runeDesc ?? "");
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.verticalOverflow = VerticalWrapMode.Overflow;

            return (image, desc, button);
        }

        private static Text MakeText(Transform parent, string name, Vector2 anchor, Vector2 anchoredPos, Vector2 sizeDelta,
            Font font, int fontSize, TextAnchor alignment, Color color, string content)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.text = content;
            return text;
        }
    }
}
