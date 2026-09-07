using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Inventory
{
    // Runtime-built items (not hand-authored .asset files, unlike potion_hp/potion_mp) for
    // the new potion types added alongside the HP/MP Potion rework -- see ItemData's
    // MaxStatPotion/RegenPotion categories. Same "build new content in code" pattern as
    // BeltMaterialFactory/Essence.cs; injected into the shared loot table and vendor stock
    // by GameBootstrap the same way Ember Core already is.
    public static class AdvancedPotionFactory
    {
        private static ItemData _maxLife, _renewalVial, _manaBead;

        // Takes over the OLD HP/MP Potion behavior (permanent +1/5 max stat, 5-potion cap)
        // now that plain Potion is an instant flat heal instead -- scoped to HP+MP rather
        // than all 8 stats, so it sits between a single stat potion and a full All-Stat
        // Potion in what it's worth.
        public static ItemData MaxLifePotion
        {
            get
            {
                if (_maxLife == null)
                {
                    _maxLife = ScriptableObject.CreateInstance<ItemData>();
                    _maxLife.itemName = "Potion of Maximum Life";
                    _maxLife.category = ItemCategory.MaxStatPotion;
                    _maxLife.rarity = ItemRarity.Epic;
                    _maxLife.icon = IconFactory.CreateRingIcon(new Color(0.95f, 0.85f, 0.4f));
                    _maxLife.description = "Permanently raises your maximum HP and MP by 1/5 each. 5 potions fully max both.";
                }
                return _maxLife;
            }
        }

        public static ItemData VialOfRenewal
        {
            get
            {
                if (_renewalVial == null)
                {
                    _renewalVial = ScriptableObject.CreateInstance<ItemData>();
                    _renewalVial.itemName = "Vial of Renewal";
                    _renewalVial.category = ItemCategory.RegenPotion;
                    _renewalVial.potionStat = StatType.HP;
                    _renewalVial.regenPerTick = 8f;
                    _renewalVial.regenDuration = 6f;
                    _renewalVial.rarity = ItemRarity.Uncommon;
                    _renewalVial.icon = IconFactory.CreateRingIcon(new Color(0.4f, 0.85f, 0.4f));
                    _renewalVial.description = "Heals 8 HP per second for 6 seconds.";
                }
                return _renewalVial;
            }
        }

        public static ItemData ManaBead
        {
            get
            {
                if (_manaBead == null)
                {
                    _manaBead = ScriptableObject.CreateInstance<ItemData>();
                    _manaBead.itemName = "Mana Bead";
                    _manaBead.category = ItemCategory.RegenPotion;
                    _manaBead.potionStat = StatType.MP;
                    _manaBead.regenPerTick = 5f;
                    _manaBead.regenDuration = 6f;
                    _manaBead.rarity = ItemRarity.Uncommon;
                    _manaBead.icon = IconFactory.CreateRingIcon(new Color(0.4f, 0.6f, 0.95f));
                    _manaBead.description = "Restores 5 MP per second for 6 seconds.";
                }
                return _manaBead;
            }
        }
    }
}
