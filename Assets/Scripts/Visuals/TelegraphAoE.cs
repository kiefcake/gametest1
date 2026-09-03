using System;
using UnityEngine;

namespace DungeonCrawler.Visuals
{
    // Shared "grow a flat warning disc, lerp its color over a channel time, then run a
    // damage callback and clean up" shape. AbyssFinalDemon's slam, FrostLich's frost nova,
    // SwampWarden's toxic burst, AbyssMage's cast telegraph, and Stheno's bombs each
    // independently hand-rolled this same ~15-20 line pattern -- 5 copies, past this
    // project's own "rule of three" (see HazardVisuals.cs, extracted from the same
    // situation for hazard pools). New telegraphed-AoE attacks should use this instead of
    // adding a 6th copy.
    //
    // The 5 existing callers were deliberately left as their own hand-rolled copies rather
    // than retrofitted onto this: they're already-tested, working boss logic, and this
    // project has no automated test suite to confirm a blind retrofit didn't shift any of
    // their timing/damage behavior. This exists so the NEXT telegraphed attack doesn't add
    // a 6th copy, not to migrate the existing 5.
    public class TelegraphAoE : MonoBehaviour
    {
        private float channelTime;
        private float elapsed;
        private Color startColor;
        private Color endColor;
        private Action onResolve;
        private Renderer cachedRenderer;

        public static TelegraphAoE Spawn(Vector3 pos, float radius, float channelTime,
            Color startColor, Color endColor, Action onResolve)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "TelegraphAoE";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col); // warning marker only -- the caller's onResolve does the actual hit detection
            go.transform.position = pos + Vector3.up * 0.05f;
            go.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);

            var telegraph = go.AddComponent<TelegraphAoE>();
            telegraph.cachedRenderer = go.GetComponent<Renderer>();
            if (telegraph.cachedRenderer != null)
                telegraph.cachedRenderer.material = new Material(Shader.Find("Standard")) { color = startColor };
            telegraph.channelTime = Mathf.Max(0.01f, channelTime);
            telegraph.startColor = startColor;
            telegraph.endColor = endColor;
            telegraph.onResolve = onResolve;
            return telegraph;
        }

        // Lets a caller tear the telegraph down early without running onResolve -- e.g. the
        // channeling boss died mid-channel, so the attack was interrupted, not completed.
        public void Cancel()
        {
            if (this != null) Destroy(gameObject);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / channelTime);
            if (cachedRenderer != null) cachedRenderer.material.color = Color.Lerp(startColor, endColor, t);

            if (elapsed >= channelTime)
            {
                onResolve?.Invoke();
                Destroy(gameObject);
            }
        }
    }
}
