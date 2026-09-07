using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.Inventory
{
    // Ring MUST stay appended at the end, not inserted earlier -- Unity serializes enums as
    // raw integers on every existing .asset file on disk, so inserting a value in the
    // middle silently reassigns every value after it (this was caught mid-session: every
    // potion asset has category:2 baked in from when Potion was the 3rd entry, and briefly
    // deserialized as Ring instead once Ring was inserted before it).
    // MaxStatPotion/RegenPotion appended per the same "always append" rule -- see the
    // comment above. Potion itself was redefined in place (still value 2, no reassignment
    // risk) from "permanently raises 1/5 of a stat" to "instantly restores a flat amount"
    // -- HP/MP Potion are the only two Potion-category items that exist, and both assets
    // were updated to carry the new potionAmount field alongside the redefinition.
    public enum ItemCategory { Weapon, Armor, Potion, AllStatPotion, Cosmetic, Ring, Material, MaxStatPotion, RegenPotion, CleansePotion, BuffPotion, RevivePotion }

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

        [Header("Potion (Potion/MaxStatPotion/RegenPotion all use potionStat -- HP or MP)")]
        public StatType potionStat;
        // Potion: instant flat restore. MaxStatPotion ("Potion of Maximum Life") ignores
        // this and always grows both HP and MP permanently, one potionsApplied step each.
        public float potionAmount = 30f;
        // RegenPotion only: total restored is regenPerTick * (regenDuration / 1s tick),
        // ticked by StatusEffectController the same cadence as Poison/Bleed.
        public float regenPerTick = 8f;
        public float regenDuration = 6f;

        [Header("BuffPotion only -- applies one temporary status effect on use")]
        public StatusEffectType buffEffect = StatusEffectType.None;
        public float buffDuration = 5f;
        public float buffMagnitude = 0.3f;

        // RevivePotion only -- fraction of max HP to revive at. Usable ONLY while downed
        // (see InventorySystem.ApplyPotionEffect); a no-op otherwise, same as any other
        // potion whose precondition isn't met (e.g. MaxStatPotion at the 5-potion cap).
        public float revivePercent = 0.3f;

        [TextArea] public string description;
    }
}
