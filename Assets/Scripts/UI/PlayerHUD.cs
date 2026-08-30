using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DungeonCrawler.Abilities;
using DungeonCrawler.Classes;
using DungeonCrawler.Core;
using DungeonCrawler.Loot;
using DungeonCrawler.World;

namespace DungeonCrawler.UI
{
    // The entire HUD is built at runtime in code -- consistent with how everything else
    // here is spawned (see DefaultContentFactory/GameBootstrap), and it sidesteps the
    // edit-time-asset-creation timing hazard that broke the loot tables (see
    // Loot/LootTable.cs). GameBootstrap calls Build() once the player exists.
    public class PlayerHUD : MonoBehaviour
    {
        private PlayerCharacter player;
        private PlayerWallet wallet;
        private DownedRecovery downedRecovery;
        private Font uiFont;

        // Driven via RectTransform.anchorMax.x rather than Image.fillAmount -- fillAmount
        // on a runtime-built Type.Filled Image with no sprite assigned was silently not
        // repainting here (value changed, visual didn't), so this sidesteps the fill-mesh
        // path entirely and just resizes the rect, which can't have that failure mode.
        private RectTransform hpFillRect;
        private Text hpLabel;
        private RectTransform mpFillRect;
        private Text mpLabel;
        private Text downedBanner;
        private Text downedRecoveryLabel;
        private Text lookAtLabel;
        private Text goldLabel;
        private const float LookAtRange = 4f; // short -- "what am I about to pick up," not ability targeting range

        private readonly List<AbilitySlotUI> abilitySlots = new List<AbilitySlotUI>();

        private static readonly Color ReadyColor = new Color(0.16f, 0.18f, 0.24f, 0.92f);
        private static readonly Color NotReadyColor = new Color(0.07f, 0.07f, 0.08f, 0.92f);
        private static readonly Color HpColor = new Color(0.78f, 0.18f, 0.18f);
        private static readonly Color MpColor = new Color(0.2f, 0.45f, 0.85f);

        private class AbilitySlotUI
        {
            public AbilityData ability;
            public Image background;
            public Image cooldownOverlay;
        }

        public static PlayerHUD Build(PlayerCharacter player, PlayerWallet wallet = null, DownedRecovery downedRecovery = null)
        {
            var go = new GameObject("PlayerHUD");
            var hud = go.AddComponent<PlayerHUD>();
            hud.player = player;
            hud.wallet = wallet;
            hud.downedRecovery = downedRecovery;
            hud.uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            hud.BuildUI();
            if (player.health != null) player.health.OnDamaged += hud.OnPlayerDamaged;
            return hud;
        }

        private void OnDestroy()
        {
            if (player != null && player.health != null) player.health.OnDamaged -= OnPlayerDamaged;
        }

        // The player has no visible sprite body to hit-flash (first-person, no player-body
        // art -- see PlayerCharacter.BuildVisual), so a full-screen red pulse stands in as
        // "you got hit" feedback instead.
        private Image screenFlash;
        private float screenFlashTimer;
        private const float ScreenFlashDuration = 0.25f;

        private void OnPlayerDamaged(float amount)
        {
            screenFlashTimer = ScreenFlashDuration;
        }

        private void Update()
        {
            if (player == null || player.health == null) return;

            if (screenFlashTimer > 0f)
            {
                screenFlashTimer -= Time.deltaTime;
                var c = screenFlash.color;
                c.a = Mathf.Clamp01(screenFlashTimer / ScreenFlashDuration) * 0.35f;
                screenFlash.color = c;
            }

            SetFillFraction(hpFillRect, Mathf.Clamp01(SafeDiv(player.health.CurrentHP, player.health.maxHP)));
            hpLabel.text = $"HP {player.health.CurrentHP:0}/{player.health.maxHP:0}";

            if (player.mana != null)
            {
                SetFillFraction(mpFillRect, Mathf.Clamp01(SafeDiv(player.mana.CurrentMP, player.mana.maxMP)));
                mpLabel.text = $"MP {player.mana.CurrentMP:0}/{player.mana.maxMP:0}";
            }

            foreach (var slotUI in abilitySlots)
            {
                if (slotUI.ability == null) continue;

                float remaining = player.abilityCaster.GetCooldownRemaining(slotUI.ability);
                float frac = slotUI.ability.cooldown > 0f ? remaining / slotUI.ability.cooldown : 0f;
                slotUI.cooldownOverlay.fillAmount = Mathf.Clamp01(frac);

                bool canAfford = player.mana == null || player.mana.CurrentMP >= slotUI.ability.manaCost;
                bool locked = slotUI.ability.slot == AbilitySlot.Ultimate && !player.abilityCaster.ultimateUnlocked;
                slotUI.background.color = (remaining <= 0f && canAfford && !locked) ? ReadyColor : NotReadyColor;
            }

            downedBanner.gameObject.SetActive(player.health.IsDowned);
            if (downedRecoveryLabel != null)
            {
                downedRecoveryLabel.gameObject.SetActive(player.health.IsDowned && downedRecovery != null);
                if (player.health.IsDowned && downedRecovery != null)
                    downedRecoveryLabel.text = $"Returning to Hub in {Mathf.CeilToInt(downedRecovery.SecondsRemaining)}s";
            }
            if (goldLabel != null && wallet != null) goldLabel.text = $"Gold: {wallet.Gold}";
            UpdateLookAtLabel();
        }

        // "What am I looking at" -- before you walk into a dropped item and it's just
        // gone, show its name near the crosshair. Trigger colliders are included
        // explicitly since WorldPickup's SphereCollider is one (Physics.Raycast ignores
        // triggers under some global settings otherwise). Interactable is checked first so
        // vendor/gate prompts (which also sit on trigger colliders) take priority over an
        // item name if they somehow overlap.
        private void UpdateLookAtLabel()
        {
            string label = null;
            if (Camera.main != null &&
                Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out RaycastHit hit, LookAtRange, ~0, QueryTriggerInteraction.Collide))
            {
                var interactable = hit.collider.GetComponentInParent<Interactable>();
                if (interactable != null)
                {
                    label = interactable.prompt;
                }
                else
                {
                    var pickup = hit.collider.GetComponentInParent<WorldPickup>();
                    if (pickup != null && pickup.item != null) label = pickup.item.itemName;
                }
            }

            lookAtLabel.text = label ?? "";
        }

        private static float SafeDiv(float a, float b) => b > 0f ? a / b : 0f;

        // Shrinks the rect from the right by moving anchorMax.x (anchorMin.x stays 0) --
        // anchorMin/anchorMax must have been left at their build-time values (0,_)/(1,_)
        // for this to read as a left-anchored fill.
        private static void SetFillFraction(RectTransform fillRect, float frac)
        {
            var max = fillRect.anchorMax;
            max.x = frac;
            fillRect.anchorMax = max;
        }

        private static Sprite _whiteSprite;
        // Type.Filled + Radial360 (the ability cooldown sweep) can't be done via anchor
        // resizing -- an angular wipe needs the fill-mesh path. Giving it an explicit
        // sprite instead of leaving `sprite` null removes the one variable that looked
        // suspect on the bars above, so the sweep doesn't inherit the same failure mode.
        private static Sprite WhiteSprite()
        {
            if (_whiteSprite == null)
            {
                var tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            }
            return _whiteSprite;
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("HUDCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            BuildScreenFlash(canvasGO.transform);
            BuildResourceBars(canvasGO.transform);
            BuildAbilityBar(canvasGO.transform);
            BuildCrosshair(canvasGO.transform);
            BuildDownedBanner(canvasGO.transform);
        }

        private void BuildScreenFlash(Transform parent)
        {
            var rect = MakeRect("ScreenFlash", parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            screenFlash = rect.gameObject.AddComponent<Image>();
            screenFlash.color = new Color(0.8f, 0f, 0f, 0f);
            screenFlash.raycastTarget = false;
        }

        private RectTransform MakeRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        private void BuildResourceBars(Transform parent)
        {
            var hpRoot = MakeRect("HPBar", parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -20), new Vector2(260, 28));
            hpRoot.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            hpFillRect = BuildFillBar(hpRoot, HpColor);
            hpLabel = BuildLabel(hpRoot, "", 16, TextAnchor.MiddleCenter);

            var mpRoot = MakeRect("MPBar", parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -54), new Vector2(260, 22));
            mpRoot.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            mpFillRect = BuildFillBar(mpRoot, MpColor);
            mpLabel = BuildLabel(mpRoot, "", 14, TextAnchor.MiddleCenter);

            var goldRoot = MakeRect("GoldBar", parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -82), new Vector2(260, 22));
            goldRoot.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            goldLabel = BuildLabel(goldRoot, "Gold: 0", 14, TextAnchor.MiddleCenter);
            goldLabel.color = new Color(1f, 0.84f, 0.2f);
        }

        // anchorMin stays (0, 0)/(0, 1) so SetFillFraction can shrink from the right by
        // moving anchorMax.x alone -- see SetFillFraction.
        private RectTransform BuildFillBar(RectTransform parent, Color color)
        {
            var rect = MakeRect("Fill", parent, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            rect.offsetMin = new Vector2(2, 2);
            rect.offsetMax = new Vector2(-2, -2);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        private Text BuildLabel(RectTransform parent, string initial, int fontSize, TextAnchor anchor)
        {
            var rect = MakeRect("Label", parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = rect.gameObject.AddComponent<Text>();
            text.font = uiFont;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.text = initial;
            return text;
        }

        private void BuildAbilityBar(Transform parent)
        {
            // LMB used to alias Basic1 -- now it's dedicated to AutoAttack (see that class),
            // so the hint here dropped it to avoid implying LMB still casts Basic1.
            var slots = new[] { AbilitySlot.Basic1, AbilitySlot.Basic2, AbilitySlot.Ultimate };
            var keyHints = new[] { "1", "2 / RMB", "3" };
            const float slotSize = 84f;
            const float spacing = 12f;
            float totalWidth = slotSize * slots.Length + spacing * (slots.Length - 1);
            float startX = -totalWidth / 2f + slotSize / 2f;

            for (int i = 0; i < slots.Length; i++)
            {
                float x = startX + i * (slotSize + spacing);
                var slotRect = MakeRect($"AbilitySlot_{slots[i]}", parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f), new Vector2(x, 24), new Vector2(slotSize, slotSize));

                var bg = slotRect.gameObject.AddComponent<Image>();
                bg.color = ReadyColor;

                var overlayRect = MakeRect("CooldownOverlay", slotRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                overlayRect.offsetMin = Vector2.zero;
                overlayRect.offsetMax = Vector2.zero;
                var overlay = overlayRect.gameObject.AddComponent<Image>();
                overlay.sprite = WhiteSprite();
                overlay.color = new Color(0f, 0f, 0f, 0.75f);
                overlay.type = Image.Type.Filled;
                overlay.fillMethod = Image.FillMethod.Radial360;
                overlay.fillOrigin = (int)Image.Origin360.Top;
                overlay.fillClockwise = true;
                overlay.fillAmount = 0f;

                var keyLabel = MakeRect("KeyHint", slotRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                keyLabel.offsetMin = new Vector2(4, 0);
                keyLabel.offsetMax = new Vector2(0, -4);
                var keyText = keyLabel.gameObject.AddComponent<Text>();
                keyText.font = uiFont;
                keyText.fontSize = 12;
                keyText.alignment = TextAnchor.UpperLeft;
                keyText.color = new Color(1f, 1f, 1f, 0.85f);
                keyText.text = keyHints[i];

                var nameRect = MakeRect("AbilityName", slotRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                nameRect.offsetMin = new Vector2(2, 2);
                nameRect.offsetMax = new Vector2(-2, 28);
                var nameText = nameRect.gameObject.AddComponent<Text>();
                nameText.font = uiFont;
                nameText.fontSize = 12;
                nameText.alignment = TextAnchor.LowerCenter;
                nameText.color = Color.white;

                var ability = player.abilityCaster.abilities.Find(a => a.slot == slots[i]);
                nameText.text = ability != null ? ability.abilityName : "--";

                abilitySlots.Add(new AbilitySlotUI { ability = ability, background = bg, cooldownOverlay = overlay });
            }
        }

        private void BuildCrosshair(Transform parent)
        {
            var h = MakeRect("CrosshairH", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14, 2));
            h.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.85f);
            var v = MakeRect("CrosshairV", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2, 14));
            v.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.85f);

            var hintRect = MakeRect("AutoAttackHint", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -26), new Vector2(220, 22));
            var hint = hintRect.gameObject.AddComponent<Text>();
            hint.font = uiFont;
            hint.fontSize = 13;
            hint.alignment = TextAnchor.MiddleCenter;
            hint.color = new Color(1f, 1f, 1f, 0.6f);
            hint.text = "Hold LMB: Auto Attack -- Shift: Dash";

            var lookAtRect = MakeRect("LookAtLabel", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 26), new Vector2(400, 26));
            lookAtLabel = lookAtRect.gameObject.AddComponent<Text>();
            lookAtLabel.font = uiFont;
            lookAtLabel.fontSize = 17;
            lookAtLabel.fontStyle = FontStyle.Bold;
            lookAtLabel.alignment = TextAnchor.MiddleCenter;
            lookAtLabel.color = Color.white;
            lookAtLabel.text = "";
        }

        private void BuildDownedBanner(Transform parent)
        {
            var rect = MakeRect("DownedBanner", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(700, 100));
            downedBanner = rect.gameObject.AddComponent<Text>();
            downedBanner.font = uiFont;
            downedBanner.fontSize = 56;
            downedBanner.fontStyle = FontStyle.Bold;
            downedBanner.alignment = TextAnchor.MiddleCenter;
            downedBanner.color = new Color(0.85f, 0.15f, 0.15f);
            downedBanner.text = "DOWNED";
            downedBanner.gameObject.SetActive(false);

            var recoveryRect = MakeRect("DownedRecoveryLabel", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(500, 30));
            downedRecoveryLabel = recoveryRect.gameObject.AddComponent<Text>();
            downedRecoveryLabel.font = uiFont;
            downedRecoveryLabel.fontSize = 22;
            downedRecoveryLabel.alignment = TextAnchor.MiddleCenter;
            downedRecoveryLabel.color = new Color(0.9f, 0.7f, 0.7f);
            downedRecoveryLabel.gameObject.SetActive(false);
        }
    }
}
