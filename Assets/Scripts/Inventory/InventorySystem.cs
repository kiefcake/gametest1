using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.Inventory
{
    // Bare-bones inventory: fixed slot count, no stacking logic yet (fine for testing).
    // Fires OnChanged so InventoryUI can redraw without polling every frame.
    public class InventorySystem : MonoBehaviour
    {
        public int slotCount = 20;
        private ItemData[] slots;

        public event Action OnChanged;
        // Separate from OnChanged so UI can redraw just the two equip slots without
        // rebuilding the whole grid on every potion pickup too.
        public event Action OnEquipmentChanged;

        public ItemData EquippedWeapon { get; private set; }
        public ItemData EquippedArmor { get; private set; }
        // Third equip slot -- matches RotMG's real Weapon/Ability/Armor/Ring convention
        // (see RealmEye character pages) and the design doc's own "Accessory/trinket" slot.
        public ItemData EquippedRing { get; private set; }

        public ItemData GetEquipped(ItemCategory slot) => slot switch
        {
            ItemCategory.Weapon => EquippedWeapon,
            ItemCategory.Armor => EquippedArmor,
            ItemCategory.Ring => EquippedRing,
            _ => null,
        };

        private void SetEquipped(ItemCategory slot, ItemData item)
        {
            switch (slot)
            {
                case ItemCategory.Weapon: EquippedWeapon = item; break;
                case ItemCategory.Armor: EquippedArmor = item; break;
                case ItemCategory.Ring: EquippedRing = item; break;
            }
        }

        private static bool IsEquippable(ItemCategory category) =>
            category == ItemCategory.Weapon || category == ItemCategory.Armor || category == ItemCategory.Ring;

        private void Awake()
        {
            slots = new ItemData[slotCount];
        }

        public bool AddItem(ItemData item)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    slots[i] = item;
                    OnChanged?.Invoke();
                    return true;
                }
            }
            return false; // inventory full
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= slots.Length) return;
            slots[index] = null;
            OnChanged?.Invoke();
        }

        public ItemData GetAt(int index) => (index >= 0 && index < slots.Length) ? slots[index] : null;

        public int SlotCount => slots.Length;

        // Consumes a potion at a slot and applies it to the given StatBlock.
        // Returns false if the slot is empty, not a potion, or the stat is already maxed.
        public bool UsePotionAt(int index, StatBlock stats)
        {
            var item = GetAt(index);
            if (item == null || stats == null) return false;

            if (item.category == ItemCategory.Potion)
            {
                if (!stats.ApplyPotion(item.potionStat)) return false;
            }
            else if (item.category == ItemCategory.AllStatPotion)
            {
                stats.ApplyAllStatPotion();
            }
            else
            {
                return false;
            }

            RemoveAt(index);
            return true;
        }

        // Equips a Weapon/Armor item from the given inventory slot, swapping whatever was
        // previously equipped in that category back into the same slot (so nothing is ever
        // lost -- just swapped). Returns false for potions/cosmetics or an empty slot.
        public bool Equip(int index, StatBlock stats)
        {
            var item = GetAt(index);
            if (item == null || stats == null) return false;
            if (!IsEquippable(item.category)) return false;

            ItemData previous = GetEquipped(item.category);
            slots[index] = previous; // null is fine here -- it just empties the slot
            SetEquipped(item.category, item);

            ApplyEquipmentBonuses(stats);
            OnChanged?.Invoke();
            OnEquipmentChanged?.Invoke();
            return true;
        }

        // Unequips back into the first open inventory slot. Returns false if there's no
        // room -- deliberately doesn't destroy the item to make space.
        public bool Unequip(ItemCategory slot, StatBlock stats)
        {
            ItemData item = GetEquipped(slot);
            if (item == null || stats == null) return false;
            if (!AddItem(item)) return false; // AddItem already fires OnChanged on success

            SetEquipped(slot, null);
            ApplyEquipmentBonuses(stats);
            OnEquipmentChanged?.Invoke();
            return true;
        }

        private void ApplyEquipmentBonuses(StatBlock stats)
        {
            stats.ClearEquipmentBonuses();
            if (EquippedWeapon != null) stats.AddEquipmentBonus(EquippedWeapon.primaryStat, EquippedWeapon.primaryStatBonus);
            if (EquippedArmor != null) stats.AddEquipmentBonus(EquippedArmor.primaryStat, EquippedArmor.primaryStatBonus);
            if (EquippedRing != null) stats.AddEquipmentBonus(EquippedRing.primaryStat, EquippedRing.primaryStatBonus);
        }
    }
}
