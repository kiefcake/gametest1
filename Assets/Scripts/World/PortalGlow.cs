using UnityEngine;

namespace DungeonCrawler.World
{
    // Slow color pulse on the dungeon gate's portal slab -- cheap "this is magic, not just
    // a colored wall" cue with no shader/particle assets required, same philosophy as
    // TorchFlicker for torches.
    public class PortalGlow : MonoBehaviour
    {
        public Color colorA = new Color(0.5f, 0.1f, 0.7f);
        public Color colorB = new Color(0.85f, 0.25f, 0.95f);
        public float speed = 1.2f;

        private Material mat;

        private void Awake()
        {
            var rend = GetComponent<Renderer>();
            if (rend != null) mat = rend.material;
        }

        private void Update()
        {
            if (mat == null) return;
            float t = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;
            mat.color = Color.Lerp(colorA, colorB, t);
        }
    }
}
