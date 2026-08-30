using UnityEngine;

namespace DungeonCrawler.Visuals
{
    // Quick expanding "pop" at an ability's point of effect -- crude primitive-based VFX
    // (no particle assets exist), color-coded so poison/freeze/heal/curse/buff reads as
    // visually distinct at a glance. Spawned by AbilityCaster.ApplyAbilityEffects.
    public class ImpactBurst : MonoBehaviour
    {
        private const float Lifetime = 0.3f;
        private float age;
        private float maxScale;

        public static void Spawn(Vector3 worldPos, Color color, float scale = 0.7f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ImpactBurst";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            go.transform.position = worldPos;
            go.transform.localScale = Vector3.one * 0.05f;

            var renderer = go.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Standard")) { color = color };

            var burst = go.AddComponent<ImpactBurst>();
            burst.maxScale = scale;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / Lifetime);
            // Grows fast then settles -- a "pop," not a lingering glow. No alpha fade (would
            // need a transparent material setup); it just shrinks back down before it dies.
            float size = Mathf.Sin(t * Mathf.PI) * maxScale;
            transform.localScale = Vector3.one * Mathf.Max(0.02f, size);
            if (age >= Lifetime) Destroy(gameObject);
        }
    }
}
