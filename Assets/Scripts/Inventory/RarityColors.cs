using UnityEngine;

namespace DungeonCrawler.Inventory
{
    // Ascending rarity color ramp -- common ARPG convention (white/grey -> green -> blue ->
    // purple -> orange). RealmEye's actual rarity colors are baked into RotMG's own sprite
    // pixels rather than exposed as CSS, so exact hex values aren't recoverable from the
    // page; this ramp is close in spirit and reads correctly to anyone who's played an ARPG.
    public static class RarityColors
    {
        public static Color Get(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Uncommon: return new Color(0.35f, 0.75f, 0.35f);
                case ItemRarity.Rare: return new Color(0.3f, 0.55f, 0.95f);
                case ItemRarity.Epic: return new Color(0.65f, 0.35f, 0.9f);
                case ItemRarity.Legendary: return new Color(0.95f, 0.65f, 0.15f);
                default: return new Color(0.6f, 0.6f, 0.63f); // Common
            }
        }

        public static string Label(ItemRarity rarity) => rarity.ToString();
    }
}
