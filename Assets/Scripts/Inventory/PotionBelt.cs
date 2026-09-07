using System;
using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.Inventory
{
    // Quick-access potion reserve shown inside the inventory panel (see InventoryUI) --
    // 3 fixed-role slots (HP / MP / All-Stat, matching the game's only 3 potion-like
    // items today), each holding a small pre-loaded quantity independent of the general
    // inventory grid, so Z/X can instantly quaff without opening the inventory at all.
    // Filled by dragging a matching potion out of the grid (see PotionBeltSlotUI); what
    // upgrades is each slot's capacity, not the slot count -- there's no 4th potion type
    // to fill a 4th slot with yet, but "carry more before needing to restock" is useful
    // on its own.
    public class PotionBelt : MonoBehaviour
    {
        public enum Role { HP, MP, AllStat }
        public static readonly Role[] Roles = { Role.HP, Role.MP, Role.AllStat };

        private static readonly int[] CapacityByTier = { 5, 10, 15 };
        // Cost to advance FROM tier index -> index+1 (2 upgrades total, tier 2 is the cap).
        private static readonly int[] MaterialCostByTier = { 3, 6 };
        private static readonly int[] CurrencyCostByTier = { 120, 320 };

        private readonly int[] counts = new int[3];
        public int Tier { get; private set; }
        public event Action OnChanged;

        public int Capacity => CapacityByTier[Tier];
        public int GetCount(Role role) => counts[(int)role];
        public bool CanUpgrade => Tier < CapacityByTier.Length - 1;
        public int NextMaterialCost => CanUpgrade ? MaterialCostByTier[Tier] : 0;
        public int NextCurrencyCost => CanUpgrade ? CurrencyCostByTier[Tier] : 0;

        // A slot's role is fixed by the item's own category/stat -- AllStatPotion outright,
        // or Potion tagged HP/MP for the other two. Anything else (weapon, cosmetic, a
        // Material) has no role and can't be dragged in.
        public static bool TryGetRole(ItemData item, out Role role)
        {
            role = default;
            if (item == null) return false;
            if (item.category == ItemCategory.AllStatPotion) { role = Role.AllStat; return true; }
            if (item.category == ItemCategory.Potion && item.potionStat == StatType.HP) { role = Role.HP; return true; }
            if (item.category == ItemCategory.Potion && item.potionStat == StatType.MP) { role = Role.MP; return true; }
            return false;
        }

        // Called when a matching inventory potion is dropped onto its belt slot. Returns
        // false (caller leaves the source item alone) if the role doesn't match or the
        // slot is already at capacity.
        public bool TryFill(Role role, ItemData item)
        {
            if (!TryGetRole(item, out var actual) || actual != role) return false;
            if (counts[(int)role] >= Capacity) return false;
            counts[(int)role]++;
            OnChanged?.Invoke();
            return true;
        }

        // Z/X (or clicking the slot directly) -- drains one charge and applies the exact
        // same effect InventorySystem.UsePotionAt would via the shared static helper, so a
        // belt potion and a grid potion behave identically, including failing silently
        // once the stat's already maxed at 5/5 (see StatBlock.ApplyPotion) -- the charge
        // is NOT spent in that case, so a maxed stat doesn't quietly drain the belt.
        public bool TryQuaff(Role role, StatBlock stats)
        {
            if (counts[(int)role] <= 0) return false;
            if (!InventorySystem.ApplyPotionEffect(RoleProxyItem(role), stats)) return false;
            counts[(int)role]--;
            OnChanged?.Invoke();
            return true;
        }

        public bool TryUpgrade(InventorySystem inventory, PlayerWallet wallet, Abilities.Essence essence)
        {
            if (!CanUpgrade || inventory == null || wallet == null || essence == null) return false;
            int matCost = NextMaterialCost;
            int curCost = NextCurrencyCost;
            if (inventory.CountItem(BeltMaterialFactory.EmberCore) < matCost) return false;
            if (!wallet.Spend(curCost) && !essence.Spend(curCost)) return false;

            inventory.RemoveItem(BeltMaterialFactory.EmberCore, matCost);
            Tier++;
            OnChanged?.Invoke();
            return true;
        }

        // A lightweight runtime ItemData standing in for "an HP/MP/All-Stat potion" so
        // TryQuaff can reuse InventorySystem.ApplyPotionEffect's category/potionStat
        // branch -- these are never added to anyone's inventory, just held here as a
        // reusable argument, same singleton-factory pattern as BeltMaterialFactory.
        private static ItemData _hpProxy, _mpProxy, _allStatProxy;
        private static ItemData RoleProxyItem(Role role)
        {
            switch (role)
            {
                case Role.HP:
                    if (_hpProxy == null)
                    {
                        _hpProxy = ScriptableObject.CreateInstance<ItemData>();
                        _hpProxy.category = ItemCategory.Potion;
                        _hpProxy.potionStat = StatType.HP;
                    }
                    return _hpProxy;
                case Role.MP:
                    if (_mpProxy == null)
                    {
                        _mpProxy = ScriptableObject.CreateInstance<ItemData>();
                        _mpProxy.category = ItemCategory.Potion;
                        _mpProxy.potionStat = StatType.MP;
                    }
                    return _mpProxy;
                default:
                    if (_allStatProxy == null)
                    {
                        _allStatProxy = ScriptableObject.CreateInstance<ItemData>();
                        _allStatProxy.category = ItemCategory.AllStatPotion;
                    }
                    return _allStatProxy;
            }
        }
    }
}
