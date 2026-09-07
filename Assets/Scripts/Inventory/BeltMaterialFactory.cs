using UnityEngine;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Inventory
{
    // Canonical singleton for the potion belt's upgrade material -- created once at
    // runtime (this project builds all new content in code rather than hand-authored
    // .asset files, see CLAUDE.md) so the exact same ItemData reference can be injected
    // into the shared loot tables (see GameBootstrap.EnsureBeltMaterialDrop) and later
    // recognized/counted/removed by InventorySystem.CountItem/RemoveItem, which compare
    // by reference rather than by name.
    public static class BeltMaterialFactory
    {
        private static ItemData _emberCore;

        public static ItemData EmberCore
        {
            get
            {
                if (_emberCore == null)
                {
                    _emberCore = ScriptableObject.CreateInstance<ItemData>();
                    _emberCore.itemName = "Ember Core";
                    _emberCore.category = ItemCategory.Material;
                    _emberCore.rarity = ItemRarity.Rare;
                    _emberCore.icon = IconFactory.CreateRingIcon(new Color(0.9f, 0.5f, 0.2f));
                    _emberCore.description = "A crystallized fragment of a boss's essence. Spent at your potion belt to expand it.";
                }
                return _emberCore;
            }
        }
    }
}
