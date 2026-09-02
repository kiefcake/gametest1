using UnityEngine;
using UnityEngine.UI;

namespace DungeonCrawler.UI
{
    // Dark and Darker-style room-name announcement -- "The Cinder Ring," not "Combat Room
    // 2." Deliberately not the "build once, reuse via a static instance" shape TooltipUI/
    // DebugTools use: a banner only ever needs to appear once per room per dungeon run,
    // so each Show() spins up its own throwaway Canvas+Text pair (same primitive building
    // blocks as DebugTools' ShowMessage) and destroys itself once its fade-out finishes,
    // rather than keeping a hidden instance parked around for the rest of the session.
    public class RoomBanner : MonoBehaviour
    {
        private const float FadeInDuration = 0.3f;
        private const float HoldDuration = 2.5f;
        private const float FadeOutDuration = 0.6f;

        private CanvasGroup canvasGroup;
        private float age;

        public static void Show(string roomName, string flavor)
        {
            var go = new GameObject("RoomBanner");
            go.AddComponent<RoomBanner>().BuildUI(roomName, flavor);
        }

        private void Update()
        {
            age += Time.deltaTime;

            if (age < FadeInDuration)
            {
                canvasGroup.alpha = age / FadeInDuration;
            }
            else if (age < FadeInDuration + HoldDuration)
            {
                canvasGroup.alpha = 1f;
            }
            else if (age < FadeInDuration + HoldDuration + FadeOutDuration)
            {
                canvasGroup.alpha = 1f - (age - FadeInDuration - HoldDuration) / FadeOutDuration;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void BuildUI(string roomName, string flavor)
        {
            var canvasGO = new GameObject("RoomBannerCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the base HUD (default order) and StatScreenUI (10), below the pause
            // menu (100) -- flavor text shouldn't out-rank a menu the player opened on top
            // of it, but should still read clearly over ordinary gameplay HUD.
            canvas.sortingOrder = 50;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasGroup = canvasGO.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGO.transform.SetParent(canvasGO.transform, false);
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0, -150);
            titleRect.sizeDelta = new Vector2(1000, 60);
            var titleText = titleGO.GetComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 42;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.93f, 0.86f, 0.66f);
            titleText.text = roomName;

            var flavorGO = new GameObject("Flavor", typeof(RectTransform), typeof(Text));
            flavorGO.transform.SetParent(canvasGO.transform, false);
            var flavorRect = flavorGO.GetComponent<RectTransform>();
            flavorRect.anchorMin = new Vector2(0.5f, 1f);
            flavorRect.anchorMax = new Vector2(0.5f, 1f);
            flavorRect.pivot = new Vector2(0.5f, 1f);
            flavorRect.anchoredPosition = new Vector2(0, -200);
            flavorRect.sizeDelta = new Vector2(1000, 40);
            var flavorText = flavorGO.GetComponent<Text>();
            flavorText.font = font;
            flavorText.fontSize = 20;
            flavorText.alignment = TextAnchor.MiddleCenter;
            flavorText.color = new Color(0.78f, 0.78f, 0.8f, 0.9f);
            flavorText.text = flavor;
        }
    }
}
