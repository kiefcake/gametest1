using UnityEngine;
using UnityEngine.UI;
using DungeonCrawler.Core;

namespace DungeonCrawler.Visuals
{
    // World-space floating HP bar, built entirely in code (no prefab) to match this
    // project's spawn-via-AddComponent pattern -- see EnemyBase.Awake, which attaches
    // one to every enemy alongside its sprite. Billboards toward the camera so it stays
    // readable regardless of view angle, and self-destroys once its target is downed
    // (EnemyBase.HandleDeath removes the enemy itself a moment later).
    public class EnemyHealthBar : MonoBehaviour
    {
        private Health target;
        // Driven via RectTransform.anchorMax.x, not Image.fillAmount -- see PlayerHUD's
        // SetFillFraction for why: fillAmount on a runtime-built Type.Filled Image with no
        // sprite silently stopped repainting here (value changed, bar didn't move).
        private RectTransform fillRect;

        public static EnemyHealthBar Attach(Transform parent, Health health, Vector3 localOffset, float width, float height = 0.16f)
        {
            var go = new GameObject("HealthBar");
            go.transform.SetParent(parent);
            go.transform.localPosition = localOffset;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var bar = go.AddComponent<EnemyHealthBar>();
            bar.target = health;
            bar.Build(go, width, height);
            go.AddComponent<BillboardSprite>();
            return bar;
        }

        private void Build(GameObject root, float width, float height)
        {
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);

            var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGO.transform.SetParent(root.transform, false);
            var bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(root.transform, false);
            fillRect = fillGO.GetComponent<RectTransform>();
            // anchorMin stays (0,0)-(0,1); LateUpdate shrinks the bar by moving anchorMax.x
            // alone, so the left edge (0 = empty) never moves and it drains right-to-left.
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            float inset = height * 0.15f;
            fillRect.offsetMin = new Vector2(inset, inset);
            fillRect.offsetMax = new Vector2(-inset, -inset);
            // fillGO already has an Image from the constructor above -- Graphic components
            // disallow a second one, so AddComponent<Image>() here would return null and
            // throwing NullReferenceException on the next line, which (since this runs
            // inside EnemyBase.Awake) aborts that enemy's init before AggroController/
            // LootDropper ever get added. GetComponent, not AddComponent.
            fillGO.GetComponent<Image>().color = new Color(0.82f, 0.15f, 0.15f);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                Destroy(gameObject);
                return;
            }

            var max = fillRect.anchorMax;
            max.x = Mathf.Clamp01(target.CurrentHP / Mathf.Max(1f, target.maxHP));
            fillRect.anchorMax = max;
            if (target.IsDowned) Destroy(gameObject);
        }
    }
}
