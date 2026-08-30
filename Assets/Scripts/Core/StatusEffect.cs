using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawler.Core
{
    // RotMG-style status effects. Cross-class synergy (e.g. ArmorBreak + Curse stacking)
    // is a core design goal, so effects are additive/independent rather than mutually exclusive.
    //
    // None MUST stay first (value 0). AbilityData.appliesStatus defaults to 0, and code
    // checks "does this ability apply a status?" via appliesStatus != StatusEffectType.None.
    // If ArmorBreak (or anything else) were value 0 instead, that check would be
    // indistinguishable from "no status set" and any ability using it would silently no-op.
    public enum StatusEffectType
    {
        None,
        ArmorBreak, // increased physical damage taken
        Bleed,      // damage over time (physical)
        Weaken,     // reduced outgoing damage
        Curse,      // increased damage taken from ALL sources
        Poison,     // damage over time (magical)
        Sick,       // reduced healing received
        Paralyze,   // cannot act
        Fortified,  // reduced damage taken (self-buff -- e.g. Knight's Bulwark Stance/Unbreakable)
        Empowered   // increased outgoing damage (self-buff -- e.g. Paladin's Empower)
    }

    [System.Serializable]
    public class ActiveStatusEffect
    {
        public StatusEffectType type;
        public float remainingDuration;
        public float magnitude; // meaning depends on type (e.g. % dmg increase, dot tick amount)

        public ActiveStatusEffect(StatusEffectType type, float duration, float magnitude)
        {
            this.type = type;
            this.remainingDuration = duration;
            this.magnitude = magnitude;
        }
    }

    // Attach to any damageable entity (player or enemy) to track active effects.
    public class StatusEffectController : MonoBehaviour
    {
        private readonly List<ActiveStatusEffect> active = new List<ActiveStatusEffect>();
        private float dotTickTimer;
        private const float DOT_TICK_INTERVAL = 1f;

        public IHealth health; // assign on Awake from the owning entity

        public void ApplyEffect(StatusEffectType type, float duration, float magnitude)
        {
            // Refresh instead of stack duration -- matches locked Icicle/freeze-style decision,
            // applied game-wide so no single status can be indefinitely re-chained.
            var existing = active.Find(e => e.type == type);
            if (existing != null)
            {
                existing.remainingDuration = Mathf.Max(existing.remainingDuration, duration);
                existing.magnitude = magnitude;
            }
            else
            {
                active.Add(new ActiveStatusEffect(type, duration, magnitude));
            }
        }

        public bool HasEffect(StatusEffectType type) => active.Exists(e => e.type == type);

        public float GetMagnitude(StatusEffectType type)
        {
            var e = active.Find(x => x.type == type);
            return e != null ? e.magnitude : 0f;
        }

        // Call from a healer/cleanse ability. Removes ONE effect from a priority list
        // (poison/bleed first, since those are the most commonly cleansed).
        public bool CleanseOne()
        {
            StatusEffectType[] priority = {
                StatusEffectType.Poison, StatusEffectType.Bleed, StatusEffectType.Curse,
                StatusEffectType.Weaken, StatusEffectType.ArmorBreak, StatusEffectType.Sick,
                StatusEffectType.Paralyze
            };
            foreach (var t in priority)
            {
                var e = active.Find(x => x.type == t);
                if (e != null) { active.Remove(e); return true; }
            }
            return false;
        }

        private void Update()
        {
            // tick down durations
            for (int i = active.Count - 1; i >= 0; i--)
            {
                active[i].remainingDuration -= Time.deltaTime;
                if (active[i].remainingDuration <= 0f) active.RemoveAt(i);
            }

            // apply damage-over-time ticks
            dotTickTimer += Time.deltaTime;
            if (dotTickTimer >= DOT_TICK_INTERVAL)
            {
                dotTickTimer = 0f;
                if (health == null) return;
                var poison = active.Find(e => e.type == StatusEffectType.Poison);
                if (poison != null) health.TakeDamage(poison.magnitude, ignoreDef: true);
                var bleed = active.Find(e => e.type == StatusEffectType.Bleed);
                if (bleed != null) health.TakeDamage(bleed.magnitude, ignoreDef: false);
            }
        }

        public bool IsParalyzed => HasEffect(StatusEffectType.Paralyze);
    }

    public interface IHealth
    {
        void TakeDamage(float amount, bool ignoreDef);
        void Heal(float amount);
        bool IsDowned { get; }
    }
}
