using UnityEngine;

namespace DungeonCrawler.Visuals
{
    // No sprite art exists for the new Ring equipment slot (see ItemCategory.Ring) --
    // everything under Sprites/Equipment is a hand-drawn PNG this session has no way to
    // produce more of. A simple band-plus-gem icon is drawn directly into a Texture2D
    // instead, same "build it in code" philosophy already used for PlayerHUD's white pixel
    // and the procedural SFX in Audio/SfxLibrary.
    public static class IconFactory
    {
        public static Sprite CreateRingIcon(Color gemColor)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float bandOuter = size * 0.40f;
            float bandInner = size * 0.27f;
            Vector2 gemCenter = center + new Vector2(0, bandOuter * 0.65f);
            float gemRadius = size * 0.16f;
            Color bandColor = new Color(0.8f, 0.7f, 0.35f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float bandDist = Vector2.Distance(p, center);
                    float gemDist = Vector2.Distance(p, gemCenter);

                    Color c;
                    if (gemDist <= gemRadius) c = gemColor;
                    else if (bandDist <= bandOuter && bandDist >= bandInner) c = bandColor;
                    else c = new Color(0, 0, 0, 0);

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
