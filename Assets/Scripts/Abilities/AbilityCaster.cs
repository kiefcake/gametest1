using System.Collections.Generic;
using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Enemies;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Abilities
{
    // Attach to the player character. Holds the 3 (extensible) abilities for the
    // equipped class and handles casting, cooldowns, and mana cost.
    public class AbilityCaster : MonoBehaviour
    {
        public List<AbilityData> abilities = new List<AbilityData>(); // set from ClassDefinition on spawn
        public bool ultimateUnlocked = false; // gates the Ultimate slot per design (unlocked later)

        private readonly Dictionary<AbilityData, float> cooldownTimers = new Dictionary<AbilityData, float>();
        // Cached alongside cooldownTimers so Update doesn't allocate a new List from
        // Dictionary.Keys every frame -- built once in Init, not per-frame.
        private readonly List<AbilityData> abilityListCache = new List<AbilityData>();

        private Health health;
        private Mana mana;
        private StatBlock stats;
        private StatusEffectController statusController;

        public void Init(Health health, Mana mana, StatBlock stats, StatusEffectController statusController)
        {
            this.health = health;
            this.mana = mana;
            this.stats = stats;
            this.statusController = statusController;

            cooldownTimers.Clear();
            abilityListCache.Clear();
            foreach (var a in abilities)
            {
                cooldownTimers[a] = 0f;
                abilityListCache.Add(a);
            }
        }

        private void Update()
        {
            for (int i = 0; i < abilityListCache.Count; i++)
            {
                var a = abilityListCache[i];
                if (cooldownTimers[a] > 0f) cooldownTimers[a] -= Time.deltaTime;
            }
        }

        // Read-only, for HUD cooldown-sweep display -- doesn't touch cast/cooldown logic.
        public float GetCooldownRemaining(AbilityData ability)
        {
            return cooldownTimers.TryGetValue(ability, out float remaining) ? Mathf.Max(0f, remaining) : 0f;
        }

        // Testing hook only (see Core.DebugTools) -- lets a reset-cooldowns hotkey clear
        // the dictionary Update() ticks down without exposing it directly.
        public void SetCooldown(AbilityData ability, float value)
        {
            if (cooldownTimers.ContainsKey(ability)) cooldownTimers[ability] = value;
        }

        public bool TryCast(AbilitySlot slot, GameObject target)
        {
            if (statusController != null && statusController.IsParalyzed) return false;
            if (slot == AbilitySlot.Ultimate && !ultimateUnlocked) return false;

            var ability = abilities.Find(a => a.slot == slot);
            if (ability == null) return false;
            if (cooldownTimers.TryGetValue(ability, out float remaining) && remaining > 0f) return false;
            if (mana != null && !mana.TryConsume(ability.manaCost)) return false; // not enough mana -- cast fails, cooldown NOT started

            Cast(ability, target);
            cooldownTimers[ability] = ability.cooldown;
            return true;
        }

        private void Cast(AbilityData ability, GameObject target)
        {
            if (target == null) return;

            if (ability.isAoE)
            {
                // Hit every collider within aoeRadius of the target's position. Uses the
                // target's position as the AoE origin (works for both "cast at an ally's
                // feet" heals and "cast at an enemy" damage AoEs).
                //
                // Beneficial AoEs (heal, or a positive self-buff like Fortified/Empowered)
                // skip EnemyBase colliders -- without this, Sacred Ground would heal an
                // enemy standing too close to the Priest, and Unbreakable would buff an
                // enemy's defense right along with the caster's.
                bool beneficial = ability.healAmount > 0f
                    || ability.appliesStatus == StatusEffectType.Fortified
                    || ability.appliesStatus == StatusEffectType.Empowered;

                var hits = Physics.OverlapSphere(target.transform.position, ability.aoeRadius);
                foreach (var hit in hits)
                {
                    if (beneficial && hit.GetComponentInParent<EnemyBase>() != null) continue;
                    ApplyAbilityEffects(ability, hit.gameObject);
                }
            }
            else
            {
                ApplyAbilityEffects(ability, target);
            }
        }

        private void ApplyAbilityEffects(AbilityData ability, GameObject target)
        {
            if (ability.isCleanse)
            {
                target.GetComponent<StatusEffectController>()?.CleanseAll();
            }
            if (ability.healAmount > 0f)
            {
                float potency = 1f + (stats != null ? stats.GetValue(StatType.WIS) * 0.01f : 0f);
                target.GetComponent<IHealth>()?.Heal(ability.healAmount * potency);
            }
            if (ability.damage > 0f)
            {
                float empowerMod = statusController != null && statusController.HasEffect(StatusEffectType.Empowered)
                    ? 1f + statusController.GetMagnitude(StatusEffectType.Empowered) : 1f;
                float scaledDamage = ability.damage * (1f + (stats != null ? stats.GetValue(StatType.ATT) * 0.01f : 0f)) * empowerMod;
                target.GetComponent<IHealth>()?.TakeDamage(scaledDamage, ignoreDef: false);
            }
            if (ability.appliesStatus != StatusEffectType.None)
            {
                target.GetComponent<StatusEffectController>()?.ApplyEffect(
                    ability.appliesStatus, ability.statusDuration, ability.statusMagnitude);
            }

            ImpactBurst.Spawn(target.transform.position + Vector3.up, ImpactColor(ability));
        }

        // One representative color per cast rather than per sub-effect, so multi-effect
        // abilities (e.g. damage + status) get a single burst, not overlapping ones.
        private static Color ImpactColor(AbilityData ability)
        {
            if (ability.healAmount > 0f) return new Color(0.35f, 0.9f, 0.4f);
            switch (ability.appliesStatus)
            {
                case StatusEffectType.Poison: return new Color(0.4f, 0.75f, 0.2f);
                case StatusEffectType.Paralyze: return new Color(0.5f, 0.8f, 1f);
                case StatusEffectType.Fortified:
                case StatusEffectType.Empowered: return new Color(0.95f, 0.85f, 0.3f);
                case StatusEffectType.None: break;
                default: return new Color(0.6f, 0.3f, 0.75f); // generic debuff (ArmorBreak/Bleed/Weaken/Curse/Sick)
            }
            if (ability.damage > 0f) return new Color(0.95f, 0.5f, 0.15f);
            return Color.white;
        }
    }
}
