using System.Collections.Generic;
using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Abilities;

namespace DungeonCrawler.Classes
{
    public enum ClassRole { Tank, Heal, Buff, Damage }

    [CreateAssetMenu(menuName = "DungeonCrawler/Class Definition")]
    public class ClassDefinition : ScriptableObject
    {
        public string className;
        public ClassRole role;
        public Sprite weaponSprite;
        public Sprite portraitSprite;

        [Header("Base stats (before gear/potions)")]
        public float baseHP = 100;
        public float baseMP = 60;
        public float baseATT = 10;
        public float baseDEF = 5;
        public float baseSPD = 5;
        public float baseDEX = 5;
        public float baseVIT = 5;
        public float baseWIS = 5;

        [Header("Abilities (2 basics + 1 ultimate)")]
        public List<AbilityData> abilities = new List<AbilityData>();

        public StatBlock BuildStatBlock()
        {
            var sb = new StatBlock();
            sb.SetBase(StatType.HP, baseHP);
            sb.SetBase(StatType.MP, baseMP);
            sb.SetBase(StatType.ATT, baseATT);
            sb.SetBase(StatType.DEF, baseDEF);
            sb.SetBase(StatType.SPD, baseSPD);
            sb.SetBase(StatType.DEX, baseDEX);
            sb.SetBase(StatType.VIT, baseVIT);
            sb.SetBase(StatType.WIS, baseWIS);

            // Potion caps are flat for the bare-bones pass -- tune per-class later.
            foreach (StatType stat in System.Enum.GetValues(typeof(StatType)))
                sb.SetPotionCap(stat, 20f);

            return sb;
        }

        // Crude role-color coding for the placeholder capsule visual (PlayerCharacter has
        // no per-class body art yet). Tank=steel blue, Heal=warm gold, Buff=violet,
        // Damage=crimson -- picked to stay readable against the abyss dungeon's dark reds.
        public static Color RoleColor(ClassRole role)
        {
            switch (role)
            {
                case ClassRole.Tank: return new Color(0.35f, 0.55f, 0.85f);
                case ClassRole.Heal: return new Color(0.95f, 0.85f, 0.4f);
                case ClassRole.Buff: return new Color(0.65f, 0.4f, 0.85f);
                default: return new Color(0.85f, 0.25f, 0.25f);
            }
        }
    }
}
