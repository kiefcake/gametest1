using UnityEngine;

namespace DungeonCrawler.Inventory
{
    // Sell prices for a player's own loot, back to whichever vendor they're standing at
    // (see ShopUI's Sell tab). Scaled off rarity alone rather than a per-item value field
    // -- every item already carries a rarity (see ItemData), so this works uniformly for
    // shop stock, chest loot, and claw-machine prizes alike without needing every existing
    // .asset touched to add one more number.
    public static class ItemEconomy
    {
        // Common..Legendary, index = (int)ItemRarity. Deliberately undercuts what buying
        // the same tier back from a vendor would cost -- selling recoups some gold, it
        // doesn't refund the purchase.
        private static readonly int[] RarityBaseValue = { 8, 18, 35, 70, 140 };

        public static int SellPrice(ItemData item)
        {
            if (item == null) return 0;
            float baseValue = RarityBaseValue[(int)item.rarity];

            // A potion is spent in one use, not carried as a stat stick -- shouldn't fetch
            // full gear money. Applies to every consumable category, not just the original
            // two -- MaxStatPotion/RegenPotion/CleansePotion/BuffPotion are exactly as
            // one-shot as Potion/AllStatPotion, this just didn't get extended when they
            // were added.
            bool isConsumable = item.category == ItemCategory.Potion || item.category == ItemCategory.AllStatPotion
                || item.category == ItemCategory.MaxStatPotion || item.category == ItemCategory.RegenPotion
                || item.category == ItemCategory.CleansePotion || item.category == ItemCategory.BuffPotion;
            if (isConsumable) baseValue *= 0.5f;

            return Mathf.Max(1, Mathf.RoundToInt(baseValue));
        }
    }
}
