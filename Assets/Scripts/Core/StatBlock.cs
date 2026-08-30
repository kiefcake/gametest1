using System;
using System.Collections.Generic;

namespace DungeonCrawler.Core
{
    // Per-character stat container. Base values come from the class definition,
    // potion-earned values are tracked separately so we can show "3/5 potions applied"
    // in the UI and so all-stat potions can add to every stat at once uniformly.
    //
    // Deliberately a plain C# class, NOT a MonoBehaviour field exposed in the Inspector:
    // Unity's built-in serializer does not support Dictionary at all (with or without
    // [SerializeField] -- the attribute would do nothing here), and StatBlock is only
    // ever built at runtime via ClassDefinition.BuildStatBlock() and held behind
    // PlayerCharacter.Stats (a property, so it's outside serialization anyway). If a
    // save system needs to persist this later, add an explicit ToSaveData()/FromSaveData()
    // pair that flattens these dictionaries into serializable arrays -- don't rely on
    // Unity's serializer to do it for you.
    public class StatBlock
    {
        // How much of a stat's max potion-boosted pool a single potion grants.
        // 1/5 => 5 potions to fully max a stat. Change this ONE constant to
        // retune the whole potion economy (per the locked design decision).
        public const int MAX_POTIONS_PER_STAT = 5;

        private readonly Dictionary<StatType, float> baseValues = new Dictionary<StatType, float>();
        private readonly Dictionary<StatType, float> potionCapValues = new Dictionary<StatType, float>();
        private readonly Dictionary<StatType, int> potionsApplied = new Dictionary<StatType, int>();
        // Flat bonus from equipped gear (InventorySystem.Equip) -- kept separate from
        // baseValues so re-equipping/unequipping can cleanly zero out and reapply without
        // needing to remember what the "real" base was.
        private readonly Dictionary<StatType, float> equipmentBonus = new Dictionary<StatType, float>();

        public StatBlock()
        {
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                baseValues[stat] = 0f;
                potionCapValues[stat] = 0f;
                potionsApplied[stat] = 0;
                equipmentBonus[stat] = 0f;
            }
        }

        public void SetBase(StatType stat, float value) => baseValues[stat] = value;

        // The total the stat can reach once fully potioned (base game's "max stat").
        public void SetPotionCap(StatType stat, float capBonus) => potionCapValues[stat] = capBonus;

        public int PotionsApplied(StatType stat) => potionsApplied[stat];

        public bool IsMaxed(StatType stat) => potionsApplied[stat] >= MAX_POTIONS_PER_STAT;

        // Apply one potion of a given stat. Returns false if already maxed.
        public bool ApplyPotion(StatType stat)
        {
            if (IsMaxed(stat)) return false;
            potionsApplied[stat]++;
            return true;
        }

        // All-stat potion: applies one potion's worth of every stat at once
        // (skips any stat that is already maxed).
        public void ApplyAllStatPotion()
        {
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                ApplyPotion(stat);
            }
        }

        // Final effective value = base + fraction of the potion cap earned + equipped gear.
        public float GetValue(StatType stat)
        {
            float potionFraction = (float)potionsApplied[stat] / MAX_POTIONS_PER_STAT;
            return baseValues[stat] + potionCapValues[stat] * potionFraction + equipmentBonus[stat];
        }

        public void ClearEquipmentBonuses()
        {
            foreach (StatType stat in Enum.GetValues(typeof(StatType))) equipmentBonus[stat] = 0f;
        }

        // Additive, not Set -- lets weapon and armor both contribute to the same stat
        // without one clobbering the other. Call ClearEquipmentBonuses() first if you're
        // recomputing from scratch (see InventorySystem.ApplyEquipmentBonuses).
        public void AddEquipmentBonus(StatType stat, float amount)
        {
            equipmentBonus[stat] += amount;
        }
    }
}
