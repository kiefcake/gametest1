using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Inventory
{
    // Rings have no sprite art (nothing under Sprites/Equipment covers the new Ring slot)
    // -- built purely in code with a procedurally-drawn icon (see IconFactory), the same
    // "no external art tool, build it in code" pattern DefaultContentFactory already uses
    // for classes/abilities.
    public static class RingFactory
    {
        public static ItemData CreateVitalityBand()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = "Band of Vitality";
            item.description = "A simple copper band. Equip it to add flat VIT.";
            item.category = ItemCategory.Ring;
            item.rarity = ItemRarity.Rare;
            item.primaryStat = StatType.VIT;
            item.primaryStatBonus = 4f;
            item.icon = IconFactory.CreateRingIcon(RarityColors.Get(ItemRarity.Rare));
            return item;
        }

        public static ItemData CreatePowerSignet()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = "Signet of Power";
            item.description = "Radiates old, hungry magic. Equip it to add flat ATT.";
            item.category = ItemCategory.Ring;
            item.rarity = ItemRarity.Epic;
            item.isUnique = true;
            item.primaryStat = StatType.ATT;
            item.primaryStatBonus = 5f;
            item.icon = IconFactory.CreateRingIcon(RarityColors.Get(ItemRarity.Epic));
            return item;
        }
    }
}
