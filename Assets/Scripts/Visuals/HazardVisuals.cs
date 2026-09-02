using UnityEngine;
using DungeonCrawler.World;

namespace DungeonCrawler.Visuals
{
    // Shared factory for the "glowing hazard pool" shape -- a flat Cylinder + PortalGlow +
    // a reused trigger collider + LavaHazard -- that DungeonLayout.BuildLavaPool/
    // BuildIcePatch/BuildPoisonBog and OpenWorldLayout.BuildHazardPatch each already build
    // independently. This is a THIRD+ site that needs it (boss EnterPhase2 arena
    // escalation, RotMG Shatters-style), which is the "rule of three" this codebase's own
    // review process extracts on -- so new callers go through here instead of pasting a
    // fourth/fifth/sixth copy. The two existing layout generators are left exactly as they
    // are (already-tested, not worth touching for this).
    public static class HazardVisuals
    {
        public static void SpawnPatch(Transform parent, Vector3 pos, float radius, Color flat, Color glowA, Color glowB, float glowSpeed = 0.8f)
        {
            var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pool.name = "HazardPatch";
            pool.transform.SetParent(parent);
            pool.transform.position = pos + new Vector3(0, 0.03f, 0);
            pool.transform.localScale = new Vector3(radius * 2f, 0.03f, radius * 2f);
            SetColor(pool, flat);

            var glow = pool.AddComponent<PortalGlow>();
            glow.colorA = glowA;
            glow.colorB = glowB;
            glow.speed = glowSpeed;

            // The Cylinder primitive ships with its own CapsuleCollider -- reused as the
            // hazard's trigger volume rather than destroying and rebuilding one.
            var col = pool.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            pool.AddComponent<LavaHazard>();
        }

        private static void SetColor(GameObject go, Color c)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.material = new Material(Shader.Find("Standard")) { color = c };
        }
    }
}
