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

        // Panel style for the "Grimy Little Descent" UI kit (see the Unity-handoff design
        // brief) -- flat carved-stone edges with two corners cut on the diagonal instead of
        // rounded, "a crest that sits slightly crooked on purpose" rather than a symmetric
        // rounded-rect. Same SetPixel-into-a-Texture2D / 9-slice approach as
        // CreateRoundedSprite, just a different SDF.
        public static Sprite CreateChamferedSprite(Color fill, Color border, int size = 96, int chamfer = 14, int borderThickness = 4)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Vector2 half = new Vector2(size / 2f, size / 2f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - half;
                    float d = ChamferRectSDF(p, half, chamfer);

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

            float b = chamfer + borderThickness + 2;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        // Max-norm (Chebyshev) box SDF -- exact and flat-edged, unlike the Euclidean
        // rounded-box SDF above, which is what we want for a hard carved-stone edge. Only
        // two opposite corners (top-left/bottom-right in local p-space) get a diagonal cut;
        // the other two stay square, matching the kit's asymmetric bevel.
        private static float ChamferRectSDF(Vector2 p, Vector2 half, float chamfer)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half;
            float rectDist = Mathf.Max(q.x, q.y);

            bool cutCorner = (p.x < 0f && p.y > 0f) || (p.x > 0f && p.y < 0f);
            if (!cutCorner) return rectDist;

            float diag = (Mathf.Abs(p.x) + Mathf.Abs(p.y) - (half.x + half.y - chamfer)) * 0.70710678f;
            return Mathf.Max(rectDist, diag);
        }
    }
}
