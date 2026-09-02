using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Abilities;

namespace DungeonCrawler.Classes
{
    // Builds all 4 classes and their 3 abilities each purely in code (ScriptableObject.CreateInstance),
    // so you can hit Play and test immediately without creating assets by hand in the Editor first.
    // Once you're happy with numbers, right-click these into real .asset files via the
    // DungeonCrawler/Ability and DungeonCrawler/Class Definition menu items instead.
    public static class DefaultContentFactory
    {
        private static AbilityData MakeAbility(string name, AbilitySlot slot, float cd, float mana,
            float damage = 0, float heal = 0, StatusEffectType status = default,
            float statusDur = 0, float statusMag = 0, bool cleanse = false, bool aoe = false, float aoeR = 0,
            bool selfTargeted = false)
        {
            var a = ScriptableObject.CreateInstance<AbilityData>();
            a.abilityName = name;
            a.slot = slot;
            a.cooldown = cd;
            a.manaCost = mana;
            a.damage = damage;
            a.healAmount = heal;
            a.appliesStatus = status;
            a.statusDuration = statusDur;
            a.statusMagnitude = statusMag;
            a.isCleanse = cleanse;
            a.isAoE = aoe;
            a.aoeRadius = aoeR;
            a.isSelfTargeted = selfTargeted;
            return a;
        }

        public static ClassDefinition CreateKnight()
        {
            var c = ScriptableObject.CreateInstance<ClassDefinition>();
            c.className = "Knight";
            c.role = ClassRole.Tank;
            c.isMelee = true;
            c.weaponSprite = Resources.Load<Sprite>("Sprites/Equipment/sword_knight"); // Sword & Shield per locked design -- sword icon stands in for the set
            c.baseHP = 250; c.baseDEF = 15; c.baseATT = 12; c.baseSPD = 5; c.baseDEX = 5; c.baseVIT = 8; c.baseWIS = 3; c.baseMP = 40;
            c.abilities.Add(MakeAbility("Shield Slam", AbilitySlot.Basic1, 2.5f, 8, damage: 15,
                status: StatusEffectType.ArmorBreak, statusDur: 4f, statusMag: 0.5f));
            c.abilities.Add(MakeAbility("Bulwark Stance", AbilitySlot.Basic2, 6f, 15,
                status: StatusEffectType.Fortified, statusDur: 4f, statusMag: 0.5f, selfTargeted: true));
            c.abilities.Add(MakeAbility("Unbreakable", AbilitySlot.Ultimate, 45f, 40,
                status: StatusEffectType.Fortified, statusDur: 5f, statusMag: 0.75f, aoe: true, aoeR: 4f, selfTargeted: true));
            return c;
        }

        public static ClassDefinition CreatePriest()
        {
            var c = ScriptableObject.CreateInstance<ClassDefinition>();
            c.className = "Priest";
            c.role = ClassRole.Heal;
            c.weaponSprite = Resources.Load<Sprite>("Sprites/Equipment/wand_priest");
            // ATT bumped from 4 -- a pure-support Priest had no offensive skills at all
            // (see Holy Smite below) and no auto-attack scaling worth mentioning either.
            // Still well below Wizard(16)/Knight(12): Priest isn't becoming a damage class,
            // just no longer helpless without an ally to heal.
            c.baseHP = 130; c.baseDEF = 4; c.baseATT = 8; c.baseSPD = 5; c.baseDEX = 6; c.baseVIT = 6; c.baseWIS = 14; c.baseMP = 90;
            c.abilities.Add(MakeAbility("Mending Light", AbilitySlot.Basic1, 2f, 10, heal: 25, cleanse: true));
            // Replaces Sacred Ground -- Priest was 3/3 pure-healing abilities with zero way
            // to deal damage itself. Sick (reduced healing received) is the offensive hook
            // that still reads as "holy," not just a reskinned Wizard bolt; see Health.Heal
            // for where Sick actually gets consumed (previously a completely inert status).
            c.abilities.Add(MakeAbility("Holy Smite", AbilitySlot.Basic2, 4f, 14, damage: 12,
                status: StatusEffectType.Sick, statusDur: 4f, statusMag: 0.6f));
            c.abilities.Add(MakeAbility("Rebirth", AbilitySlot.Ultimate, 60f, 50, heal: 999));
            return c;
        }

        public static ClassDefinition CreatePaladin()
        {
            var c = ScriptableObject.CreateInstance<ClassDefinition>();
            c.className = "Paladin";
            c.role = ClassRole.Buff;
            c.isMelee = true;
            c.weaponSprite = Resources.Load<Sprite>("Sprites/Equipment/warhammer_paladin");
            c.baseHP = 190; c.baseDEF = 10; c.baseATT = 10; c.baseSPD = 5; c.baseDEX = 5; c.baseVIT = 7; c.baseWIS = 9; c.baseMP = 60;
            // Active cooldown buff, per locked decision. Real design is a party-wide damage
            // buff timed with group burst windows; solo-testable version buffs the Paladin's
            // own damage until there's a second player to actually target.
            c.abilities.Add(MakeAbility("Empower", AbilitySlot.Basic1, 8f, 15,
                status: StatusEffectType.Empowered, statusDur: 6f, statusMag: 0.35f, selfTargeted: true));
            c.abilities.Add(MakeAbility("Hex", AbilitySlot.Basic2, 5f, 12,
                status: StatusEffectType.Weaken, statusDur: 5f, statusMag: 0.3f));
            c.abilities.Add(MakeAbility("Chronoshift", AbilitySlot.Ultimate, 50f, 45,
                status: StatusEffectType.Paralyze, statusDur: 2.5f, statusMag: 1f));
            return c;
        }

        public static ClassDefinition CreateWizard()
        {
            var c = ScriptableObject.CreateInstance<ClassDefinition>();
            c.className = "Wizard";
            c.role = ClassRole.Damage;
            c.rangedShotCount = 3; // fires a 3-bolt spread instead of a single shot
            c.weaponSprite = Resources.Load<Sprite>("Sprites/Equipment/staff_wizard");
            c.baseHP = 110; c.baseDEF = 3; c.baseATT = 16; c.baseSPD = 5; c.baseDEX = 8; c.baseVIT = 5; c.baseWIS = 7; c.baseMP = 80;
            c.abilities.Add(MakeAbility("Venom Bolt", AbilitySlot.Basic1, 1.5f, 8, damage: 10,
                status: StatusEffectType.Poison, statusDur: 4f, statusMag: 4f));
            c.abilities.Add(MakeAbility("Icicle", AbilitySlot.Basic2, 4f, 14, damage: 8,
                status: StatusEffectType.Paralyze, statusDur: 1.75f, statusMag: 1f)); // "freeze" = Paralyze, locked: 1.5-2s, no stacking duration
            c.abilities.Add(MakeAbility("Death Mark", AbilitySlot.Ultimate, 40f, 35, damage: 40,
                status: StatusEffectType.Curse, statusDur: 5f, statusMag: 0.4f));
            return c;
        }
    }
}
