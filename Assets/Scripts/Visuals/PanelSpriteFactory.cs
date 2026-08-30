using UnityEngine;

namespace DungeonCrawler.Visuals
{
    // The existing UI sprites (Sprites/UI/inventory_panel.png etc) are flat placeholder
    // squares with a hard 1px border -- no rounding, no depth. Rounded-rect-with-border
    // panels are drawn directly into a Texture2D instead (signed-distance-field style,
    // same "build it in code" approach as IconFactory's ring icons and the procedural SFX),
    // and returned 9-sliced so one small texture stretches cleanly to any panel size.
    public static class PanelSpriteFactory
    {
        public static Sprite CreateRoundedSprite(Color fill, Color border, int size = 96, int radius = 18, int borderThickness = 4)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Vector2 half = new Vector2(size / 2f, size / 2f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - half;
                    float d = RoundedRectSDF(p, half, radius);

                    // Smooth ~1px falloff on the outer silhouette only -- the border/fill
                    // boundary further inside stays a hard edge, which is fine since it's
                    // not read against the game world behind it.
                    float outerAlpha = Mathf.Clamp01(0.5f - d);
                    Color c;
                    if (d > 0.5f - borderThickness)
                    {
                        c = border;
                        c.a *= outerAlpha;
                    }
                    else
                    {
                        c = fill;
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();

            float b = radius + borderThickness + 2;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        // Inigo Quilez's rounded-box SDF: negative inside the shape, positive outside,
        // magnitude is the distance to the nearest edge.
        private static float RoundedRectSDF(Vector2 p, Vector2 halfSize, float r)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - halfSize + new Vector2(r, r);
            float outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(q.x, q.y), 0f);
            return outside + inside - r;
        }
    }
}
