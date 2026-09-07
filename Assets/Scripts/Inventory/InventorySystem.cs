using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonCrawler.Classes;
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

        // Consumes a potion at a slot and applies it to the given player.
        // Returns false if the slot is empty, not a potion, or (MaxStatPotion only) both
        // stats are already maxed.
        public bool UsePotionAt(int index, PlayerCharacter player)
        {
            var item = GetAt(index);
            if (!ApplyPotionEffect(item, player)) return false;
            RemoveAt(index);
            return true;
        }

        // Shared with PotionBelt.TryQuaff so a belt charge and a grid potion apply the
        // exact same effect without duplicating the category/potionStat branch.
        //
        // Potion: instant flat HP/MP restore (item.potionAmount). AllStatPotion: permanent
        // +1/5 to every one of the 8 stats. MaxStatPotion ("Potion of Maximum Life"):
        // permanent +1/5 to HP and MP only, same underlying StatBlock.ApplyPotion 5-potion
        // cap as AllStatPotion, just scoped to two stats instead of eight. RegenPotion:
        // heal-over-time (HP) or restore-over-time (MP) depending on potionStat, ticked by
        // StatusEffectController the same cadence as Poison/Bleed -- covers both "potions
        // that heal over time" and "mana beads" as one category.
        public static bool ApplyPotionEffect(ItemData item, PlayerCharacter player)
        {
            if (item == null || player == null) return false;
            switch (item.category)
            {
                case ItemCategory.Potion:
                    if (item.potionStat == StatType.HP) { player.health?.Heal(item.potionAmount); return true; }
                    if (item.potionStat == StatType.MP) { player.mana?.Regen(item.potionAmount); return true; }
                    return false;

                case ItemCategory.AllStatPotion:
                    player.Stats?.ApplyAllStatPotion();
                    return true;

                case ItemCategory.MaxStatPotion:
                    // Caller refreshes derived stats after a successful use, same as it
                    // already does for AllStatPotion -- see InventoryUI.OnSlotClicked.
                    bool hpGrew = player.Stats != null && player.Stats.ApplyPotion(StatType.HP);
                    bool mpGrew = player.Stats != null && player.Stats.ApplyPotion(StatType.MP);
                    return hpGrew || mpGrew;

                case ItemCategory.RegenPotion:
                    var effect = item.potionStat == StatType.MP ? StatusEffectType.ManaRegenerating : StatusEffectType.Regenerating;
                    player.statusController?.ApplyEffect(effect, item.regenDuration, item.regenPerTick);
                    return true;

                // "Antidote" -- a cleanse in a bottle, same full-debuff strip Priest's
                // Mending Light already does (see StatusEffectController.CleanseAll).
                case ItemCategory.CleansePotion:
                    player.statusController?.CleanseAll();
                    return true;

                // "Draught of Haste"/"Stone Skin Tonic" -- one temporary status effect,
                // whichever the item is configured for (item.buffEffect).
                case ItemCategory.BuffPotion:
                    if (item.buffEffect == StatusEffectType.None) return false;
                    player.statusController?.ApplyEffect(item.buffEffect, item.buffDuration, item.buffMagnitude);
                    return true;

                // "Bottled Second Wind" -- only usable while actually downed; a no-op
                // otherwise (matches every other precondition-gated potion here). Reviving
                // just flips Health.IsDowned back to false, which DownedRecovery's own
                // Update() already notices and cleanly stops its countdown from -- no
                // separate "cancel the recovery timer" call needed.
                case ItemCategory.RevivePotion:
                    if (player.health == null || !player.health.IsDowned) return false;
                    player.health.Revive(item.revivePercent);
                    return true;

                default:
                    return false;
            }
        }

        // Reference-equality count/removal -- upgrade materials are either a real asset
        // ScriptableObject or a canonical runtime singleton (see BeltMaterialFactory),
        // never independently-cloned duplicates of "the same" item, so this is safe.
        public int CountItem(ItemData item)
        {
            if (item == null) return 0;
            int count = 0;
            for (int i = 0; i < slots.Length; i++) if (slots[i] == item) count++;
            return count;
        }

        public void RemoveItem(ItemData item, int count)
        {
            if (item == null || count <= 0) return;
            for (int i = 0; i < slots.Length && count > 0; i++)
            {
                if (slots[i] != item) continue;
                slots[i] = null;
                count--;
            }
            OnChanged?.Invoke();
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
