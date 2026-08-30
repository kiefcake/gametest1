namespace DungeonCrawler.Core
{
    // The 8 core stats, RotMG-style. Add new stats here only -- everything else
    // (potions, UI, stat blocks) reads from this enum, so extending later
    // (e.g. adding CooldownReduction) is a one-line change.
    public enum StatType
    {
        HP,   // max health pool
        MP,   // max mana / resource pool
        ATT,  // damage dealt per hit
        DEF,  // damage reduction
        SPD,  // movement speed
        DEX,  // attack rate / cast rate
        VIT,  // HP/MP regen rate
        WIS   // heal/buff potency + regen assist
    }
}
