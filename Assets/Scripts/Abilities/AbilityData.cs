using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.Abilities
{
    public enum AbilitySlot { Basic1, Basic2, Ultimate } // 3 abilities/class, ultimate unlocks later per design

    // Stable identity for rank/rune logic in AbilityCaster -- abilityName is player-facing
    // text (could be renamed for flavor without changing behavior), so bespoke rune
    // behavior switches on this instead. None is the default for any AbilityData that
    // predates this system; AbilityCaster treats None as "no rune content, ranks are
    // numeric-only for this ability" rather than throwing.
    //
    // Append-only, same rule as every other enum in this codebase (Unity serializes as a
    // raw int) -- see StatusEffectType's comment for why.
    public enum AbilityId
    {
        None,
        ShieldSlam, BulwarkStance, Unbreakable,
        MendingLight, HolySmite, Rebirth,
        Empower, Hex, Chronoshift,
        VenomBolt, Icicle, DeathMark,
    }

    // Which of an ability's two Rank-3 rune choices is active. None until the ability
    // actually reaches rank 3 and the player picks one (see AbilityCaster.ChooseRune) --
    // ranks 1-2 never read this.
    public enum AbilityRune { None, A, B }

    // Base class for all abilities. Each class's 3 abilities are AbilityData assets
    // referenced in a List<AbilityData> on the class definition -- NOT hardcoded fields --
    // so adding a 4th ability slot later is a content change, not a refactor.
    [CreateAssetMenu(menuName = "DungeonCrawler/Ability")]
    public class AbilityData : ScriptableObject
    {
        public string abilityName;
        public AbilitySlot slot;
        public float cooldown = 3f;
        public float manaCost = 10f;
        [TextArea] public string description;

        // Override behavior per-ability via subclassing, or keep simple abilities
        // data-driven through these fields (kept generic for the bare-bones pass).
        public float damage;
        public float healAmount;
        public StatusEffectType appliesStatus;
        public float statusDuration;
        public float statusMagnitude;
        public bool isCleanse;
        public bool isAoE;
        public float aoeRadius;

        // Explicit rather than inferred from healAmount/isCleanse (PlayerAbilityInput's
        // targeting fallback still checks those too) -- a pure self-buff like Fortified/
        // Empowered has no heal and isn't a cleanse, so it needs its own signal to avoid
        // getting aimed at the nearest enemy instead of the caster.
        public bool isSelfTargeted;

        // Rank/rune progression (see AbilityCaster). id ties this instance to the bespoke
        // rune behavior switch; the rank-1 fields above are the base values ranks 2/3 scale
        // up from (AbilityCaster.RankScale), not overridden per rank.
        public AbilityId id;
        [Header("Rank 3 Rune Choice (leave blank if this ability's id is None)")]
        public string runeAName;
        [TextArea] public string runeADescription;
        public string runeBName;
        [TextArea] public string runeBDescription;
    }
}
