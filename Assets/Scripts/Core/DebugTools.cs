using UnityEngine;
using UnityEngine.UI;
using DungeonCrawler.Classes;
using DungeonCrawler.Enemies;

namespace DungeonCrawler.Core
{
    // Hotkey testing aids -- added once per run so repeated hub/dungeon/combat testing
    // doesn't mean re-earning gold or re-fighting the same trash pack every single time.
    // Not gameplay: nothing here is reachable without knowing the hotkeys, and none of it
    // is wired into any normal progression path. A small always-on hint line in the
    // bottom-left names the keys; it swaps to a confirmation line for 2s after each use.
    public class DebugTools : MonoBehaviour
    {
        public PlayerCharacter player;
        public PlayerWallet wallet;

        public KeyCode addGoldKey = KeyCode.F1;
        public KeyCode fullHealKey = KeyCode.F2;
        public KeyCode killAllKey = KeyCode.F3;
        public KeyCode godModeKey = KeyCode.F4;
        public KeyCode resetCooldownsKey = KeyCode.F5;

        private bool godMode;
        private Text statusText;
        private float messageTimer;

        public static DebugTools Build(PlayerCharacter player, PlayerWallet wallet)
        {
            var go = new GameObject("DebugTools");
            var tools = go.AddComponent<DebugTools>();
            tools.player = player;
            tools.wallet = wallet;
            tools.BuildUI();
            return tools;
        }

        private void Update()
        {
            if (Input.GetKeyDown(addGoldKey)) { wallet?.Add(200); ShowMessage("+200 gold"); }
            if (Input.GetKeyDown(fullHealKey)) FullHeal();
            if (Input.GetKeyDown(killAllKey)) KillAll();
            if (Input.GetKeyDown(godModeKey)) ToggleGodMode();
            if (Input.GetKeyDown(resetCooldownsKey)) ResetCooldowns();

            if (messageTimer > 0f)
            {
                messageTimer -= Time.deltaTime;
                if (messageTimer <= 0f) statusText.text = HintLine();
            }
        }

        private void FullHeal()
        {
            if (player == null) return;
            if (player.health != null)
            {
                if (player.health.IsDowned) player.health.Revive(1f);
                else player.health.Heal(999999f);
            }
            if (player.mana != null) player.mana.SetMax(player.mana.maxMP, refill: true);
            ShowMessage("Full HP/MP");
        }

        private void KillAll()
        {
            int count = 0;
            foreach (var enemy in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
            {
                var h = enemy.GetComponent<Health>();
                if (h == null || h.IsDowned) continue;
                h.TakeDamage(999999f, ignoreDef: true);
                count++;
            }
            ShowMessage($"Killed {count} enemies");
        }

        private void ToggleGodMode()
        {
            godMode = !godMode;
            if (player != null && player.health != null) player.health.invulnerable = godMode;
            ShowMessage(godMode ? "God Mode ON" : "God Mode OFF");
        }

        private void ResetCooldowns()
        {
            if (player == null || player.abilityCaster == null) return;
            foreach (var a in player.abilityCaster.abilities)
                player.abilityCaster.SetCooldown(a, 0f);
            ShowMessage("Cooldowns reset");
        }

        private void ShowMessage(string msg)
        {
            statusText.text = $"[Debug] {msg}";
            messageTimer = 2f;
        }

        private string HintLine() => "F1 +Gold   F2 Full Heal   F3 Kill All   F4 God Mode   F5 Reset Cooldowns";

        private void BuildUI()
        {
            var canvasGO = new GameObject("DebugToolsCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5; // above the base HUD's default order, well below any popup menu
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var rectGO = new GameObject("DebugHint", typeof(RectTransform), typeof(Text));
            rectGO.transform.SetParent(canvasGO.transform, false);
            var rect = rectGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 0);
            rect.pivot = new Vector2(0, 0);
            rect.anchoredPosition = new Vector2(20, 20);
            rect.sizeDelta = new Vector2(760, 24);
            statusText = rectGO.GetComponent<Text>();
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusText.fontSize = 14;
            statusText.alignment = TextAnchor.LowerLeft;
            statusText.color = new Color(1f, 1f, 1f, 0.55f);
            statusText.text = HintLine();
        }
    }
}
