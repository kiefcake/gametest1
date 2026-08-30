using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.Inventory
{
    // Ring MUST stay appended at the end, not inserted earlier -- Unity serializes enums as
    // raw integers on every existing .asset file on disk, so inserting a value in the
    // middle silently reassigns every value after it (this was caught mid-session: every
    // potion asset has category:2 baked in from when Potion was the 3rd entry, and briefly
    // deserialized as Ring instead once Ring was inserted before it).
    public enum ItemCategory { Weapon, Armor, Potion, AllStatPotion, Cosmetic, Ring }

    // RealmEye/RotMG-style rarity tiers, ascending. Purely cosmetic (tooltip label + icon
    // backdrop color, see HoverTooltip callers and IconFactory) -- doesn't affect stats.
    public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary }

    [CreateAssetMenu(menuName = "DungeonCrawler/Item")]
    public class ItemData : ScriptableObject
    {
        public string itemName;
        public ItemCategory category;
        public ItemRarity rarity = ItemRarity.Common;
        // RotMG's "UT" tag -- a uniquely-named item with hand-picked bonuses rather than a
        // generic tiered stat-stick. Purely a tooltip label here (see DescribeItem callers).
        public bool isUnique;
        public Sprite icon; // crude placeholder sprites live under Sprites/Equipment

        [Header("Weapon/Armor bonuses (flat, added to StatBlock base)")]
        public StatType primaryStat;
        public float primaryStatBonus;

        [Header("Potion (only relevant if category == Potion)")]
        public StatType potionStat;

        [TextArea] public string description;
    }
}
