using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.Abilities
{
    public enum AbilitySlot { Basic1, Basic2, Ultimate } // 3 abilities/class, ultimate unlocks later per design

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
    }
}
