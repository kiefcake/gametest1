using System.Collections.Generic;
using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Enemies;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Abilities
{
    // Attach to the player character. Holds the 3 (extensible) abilities for the
    // equipped class and handles casting, cooldowns, mana cost, and per-run rank/rune
    // progression (see Essence.cs and full-game-scope.md's "ability runes, not per-ability
    // levels or a persistent XP curve" design -- ranks are the numeric-power half of that,
    // runes are the behavior-changing half, both funded by the in-run Essence currency so
    // neither competes with gold/potions for the same wallet).
    public class AbilityCaster : MonoBehaviour
    {
        public List<AbilityData> abilities = new List<AbilityData>(); // set from ClassDefinition on spawn
        public bool ultimateUnlocked = false; // gates the Ultimate slot per design (unlocked later)

        private readonly Dictionary<AbilityData, float> cooldownTimers = new Dictionary<AbilityData, float>();
        // Cached alongside cooldownTimers so Update doesn't allocate a new List from
        // Dictionary.Keys every frame -- built once in Init, not per-frame.
        private readonly List<AbilityData> abilityListCache = new List<AbilityData>();
        private readonly Dictionary<AbilityData, int> ranks = new Dictionary<AbilityData, int>();
        private readonly Dictionary<AbilityData, AbilityRune> selectedRunes = new Dictionary<AbilityData, AbilityRune>();

        private Health health;
        private Mana mana;
        private StatBlock stats;
        private StatusEffectController statusController;
        private Essence essence;

        // Essence cost to advance FROM rank 1->2 and 2->3 -- index by current rank (1 or
        // 2); rank 3 is the cap, CanRankUp already guards against indexing past this.
        private static readonly int[] RankUpCost = { 0, 15, 35 };

        public void Init(Health health, Mana mana, StatBlock stats, StatusEffectController statusController, Essence essence = null)
        {
            this.health = health;
            this.mana = mana;
            this.stats = stats;
            this.statusController = statusController;
            this.essence = essence;

            cooldownTimers.Clear();
            abilityListCache.Clear();
            ranks.Clear();
            selectedRunes.Clear();
            foreach (var a in abilities)
            {
                cooldownTimers[a] = 0f;
                abilityListCache.Add(a);
                ranks[a] = 1;
                selectedRunes[a] = AbilityRune.None;
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

        // --- Rank/rune progression --------------------------------------------------

        public int GetRank(AbilityData ability) => ranks.TryGetValue(ability, out int r) ? r : 1;

        public bool CanRankUp(AbilityData ability, out int cost)
        {
            int rank = GetRank(ability);
            if (rank >= 3) { cost = 0; return false; }
            cost = RankUpCost[rank];
            return true;
        }

        // Called from the trainer/shrine UI. Spends essence, returns false (no essence
        // lost) if already maxed or the player can't afford it.
        public bool RankUp(AbilityData ability)
        {
            if (!CanRankUp(ability, out int cost)) return false;
            if (essence == null || !essence.Spend(cost)) return false;
            ranks[ability] = GetRank(ability) + 1;
            return true;
        }

        public AbilityRune GetRune(AbilityData ability) => selectedRunes.TryGetValue(ability, out var r) ? r : AbilityRune.None;

        // An ability with id == None (predates this system, or intentionally has no rune
        // content) never offers a choice, regardless of rank.
        public bool CanChooseRune(AbilityData ability) =>
            ability.id != AbilityId.None && GetRank(ability) >= 3 && GetRune(ability) == AbilityRune.None;

        // One-time choice -- once picked, locked in for the rest of the run (matches every
        // other per-run system here: nothing about a class resets mid-run, only at the
        // next BeginRun).
        public bool ChooseRune(AbilityData ability, AbilityRune rune)
        {
            if (!CanChooseRune(ability) || (rune != AbilityRune.A && rune != AbilityRune.B)) return false;
            selectedRunes[ability] = rune;
            return true;
        }

        // Ranks 2/3 scale an ability's raw numbers up. Damage/heal get the biggest jump
        // (that's the "I feel stronger" hit on the very next cast); duration/magnitude
        // scale more gently -- several magnitudes are already 0-1 fractional multipliers
        // (ArmorBreak's 0.5 = +50% damage taken), and scaling those as aggressively as raw
        // damage would get absurd at rank 3. Cooldown only improves a little, so ranking
        // up never trivializes an ability's pacing.
        private static float RankScale(int rank, float mult2, float mult3) => rank >= 3 ? mult3 : rank == 2 ? mult2 : 1f;
        private float DamageScale(AbilityData a) => RankScale(GetRank(a), 1.35f, 1.75f);
        private float MagnitudeScale(AbilityData a) => RankScale(GetRank(a), 1.15f, 1.3f);
        private float CooldownScale(AbilityData a) => RankScale(GetRank(a), 0.92f, 0.84f);

        // --- Casting -----------------------------------------------------------------

        public bool TryCast(AbilitySlot slot, GameObject target)
        {
            if (statusController != null && statusController.IsParalyzed) return false;
            if (slot == AbilitySlot.Ultimate && !ultimateUnlocked) return false;

            var ability = abilities.Find(a => a.slot == slot);
            if (ability == null) return false;
            if (cooldownTimers.TryGetValue(ability, out float remaining) && remaining > 0f) return false;
            if (mana != null && !mana.TryConsume(ability.manaCost)) return false; // not enough mana -- cast fails, cooldown NOT started

            Cast(ability, target);
            cooldownTimers[ability] = ability.cooldown * CooldownScale(ability);
            return true;
        }

        private void Cast(AbilityData ability, GameObject target)
        {
            if (target == null) return;
            var rune = GetRune(ability);

            // Rank-3 rune choices that turn an otherwise single-target ability into an
            // AoE (or grow an existing AoE's radius) are resolved here, before the normal
            // AoE/single-target branch. Everything else about the cast (damage/status/
            // heal numbers, and any "also affects a second target" extras) stays in
            // ApplyAbilityEffects/ApplyRuneExtras below.
            bool aoe = ability.isAoE;
            float radius = ability.aoeRadius;
            Vector3 origin = target.transform.position;

            if (rune == AbilityRune.A)
            {
                switch (ability.id)
                {
                    case AbilityId.ShieldSlam: aoe = true; radius = 2.5f; break; // Cleave
                    case AbilityId.BulwarkStance: aoe = true; radius = 4f; origin = transform.position; break; // Aegis
                    case AbilityId.Empower: aoe = true; radius = 4f; origin = transform.position; break; // Crusader's Zeal
                    case AbilityId.Chronoshift: aoe = true; radius = 4f; break; // Temporal Rift
                }
            }
            if (rune == AbilityRune.A && ability.id == AbilityId.Unbreakable) radius *= 1.5f; // Guardian's Cry -- taunt should reach further than the base heal AoE; actual taunt logic lives in ApplyRuneExtras below

            if (aoe)
            {
                // Beneficial AoEs (heal, or a positive self-buff like Fortified/Empowered)
                // skip EnemyBase colliders -- without this, Sacred Ground would heal an
                // enemy standing too close to the Priest, and Unbreakable would buff an
                // enemy's defense right along with the caster's. A harmful AoE instead
                // skips the caster's own collider, so a rune-granted AoE (Cleave, Temporal
                // Rift) never hits yourself.
                bool beneficial = ability.healAmount > 0f
                    || ability.appliesStatus == StatusEffectType.Fortified
                    || ability.appliesStatus == StatusEffectType.Empowered;

                var hits = Physics.OverlapSphere(origin, radius);
                foreach (var hit in hits)
                {
                    if (!beneficial && hit.gameObject == gameObject) continue;
                    if (beneficial && hit.GetComponentInParent<EnemyBase>() != null) continue;
                    ApplyAbilityEffects(ability, hit.gameObject, rune);
                }
            }
            else
            {
                ApplyAbilityEffects(ability, target, rune);
            }

            ApplyRuneExtras(ability, rune, target);
        }

        private void ApplyAbilityEffects(AbilityData ability, GameObject target, AbilityRune rune)
        {
            if (ability.isCleanse)
            {
                target.GetComponent<StatusEffectController>()?.CleanseAll();
            }
            if (ability.healAmount > 0f)
            {
                float potency = 1f + (stats != null ? stats.GetValue(StatType.WIS) * 0.01f : 0f);
                float healAmount = ability.healAmount * DamageScale(ability) * potency;

                // Overflow -- a bigger save specifically when it matters most, rather than
                // a flat bonus every cast.
                if (rune == AbilityRune.A && ability.id == AbilityId.MendingLight)
                {
                    var h = target.GetComponent<Health>();
                    if (h != null && h.CurrentHP <= h.maxHP * 0.3f) healAmount *= 1.6f;
                }

                target.GetComponent<IHealth>()?.Heal(healAmount);
            }
            if (ability.damage > 0f)
            {
                float empowerMod = statusController != null && statusController.HasEffect(StatusEffectType.Empowered)
                    ? 1f + statusController.GetMagnitude(StatusEffectType.Empowered) : 1f;
                float scaledDamage = ability.damage * DamageScale(ability) * (1f + (stats != null ? stats.GetValue(StatType.ATT) * 0.01f : 0f)) * empowerMod;

                // Judgment -- rewards a target the party has already debuffed.
                if (rune == AbilityRune.B && ability.id == AbilityId.HolySmite)
                {
                    var sc = target.GetComponent<StatusEffectController>();
                    if (sc != null && (sc.HasEffect(StatusEffectType.ArmorBreak) || sc.HasEffect(StatusEffectType.Bleed)
                        || sc.HasEffect(StatusEffectType.Weaken) || sc.HasEffect(StatusEffectType.Curse) || sc.HasEffect(StatusEffectType.Poison)))
                    {
                        scaledDamage *= 1.5f;
                    }
                }
                // Execute -- up to double damage as the target nears death.
                if (rune == AbilityRune.B && ability.id == AbilityId.DeathMark)
                {
                    var h = target.GetComponent<Health>();
                    if (h != null && h.maxHP > 0f) scaledDamage *= 1f + (1f - h.CurrentHP / h.maxHP);
                }

                target.GetComponent<IHealth>()?.TakeDamage(scaledDamage, ignoreDef: false);

                // Virulent -- when this poisoned target eventually dies, its poison
                // spreads to whatever's still standing nearby.
                if (rune == AbilityRune.A && ability.id == AbilityId.VenomBolt)
                {
                    var targetHealth = target.GetComponent<Health>();
                    if (targetHealth != null)
                    {
                        Vector3 deathPos = target.transform.position;
                        float spreadMag = ability.statusMagnitude * MagnitudeScale(ability);
                        float spreadDur = ability.statusDuration * MagnitudeScale(ability);
                        System.Action onDeath = null;
                        onDeath = () =>
                        {
                            targetHealth.OnDeath -= onDeath;
                            var nearby = Physics.OverlapSphere(deathPos, 3f);
                            foreach (var n in nearby)
                            {
                                if (n.GetComponentInParent<EnemyBase>() == null) continue;
                                n.GetComponentInParent<StatusEffectController>()?.ApplyEffect(StatusEffectType.Poison, spreadDur, spreadMag);
                            }
                        };
                        targetHealth.OnDeath += onDeath;
                    }
                }
            }
            if (ability.appliesStatus != StatusEffectType.None)
            {
                float dur = ability.statusDuration * MagnitudeScale(ability);
                float mag = ability.statusMagnitude * MagnitudeScale(ability);

                if (rune == AbilityRune.B && ability.id == AbilityId.Icicle) dur *= 1.5f; // Deep Freeze

                target.GetComponent<StatusEffectController>()?.ApplyEffect(ability.appliesStatus, dur, mag);

                if (rune == AbilityRune.B && ability.id == AbilityId.Hex) // Vulnerability
                {
                    target.GetComponent<StatusEffectController>()?.ApplyEffect(StatusEffectType.Curse, dur, 0.2f);
                }
            }

            ImpactBurst.Spawn(target.transform.position + Vector3.up, ImpactColor(ability));
        }

        // Rune behavior that doesn't fit inside the single-target effect pass above --
        // extra self-buffs, a second affected enemy, taunts, and similar one-off
        // additions layered on top of whatever ApplyAbilityEffects already did.
        private void ApplyRuneExtras(AbilityData ability, AbilityRune rune, GameObject primaryTarget)
        {
            if (rune == AbilityRune.None) return;

            switch (ability.id)
            {
                case AbilityId.ShieldSlam when rune == AbilityRune.B: // Riposte
                    statusController?.ApplyEffect(StatusEffectType.Fortified, 2f, 0.4f);
                    break;

                case AbilityId.BulwarkStance when rune == AbilityRune.B: // Retaliation -- a DEF-scaled burst on cast
                    {
                        float dmg = 10f * (1f + (stats != null ? stats.GetValue(StatType.DEF) * 0.05f : 0f));
                        var hits = Physics.OverlapSphere(transform.position, 3f);
                        foreach (var hit in hits)
                        {
                            if (hit.gameObject == gameObject) continue;
                            if (hit.GetComponentInParent<EnemyBase>() == null) continue;
                            hit.GetComponentInParent<IHealth>()?.TakeDamage(dmg, ignoreDef: false);
                        }
                    }
                    break;

                case AbilityId.Unbreakable when rune == AbilityRune.A: // Guardian's Cry -- taunt everyone it reached
                    {
                        var hits = Physics.OverlapSphere(transform.position, ability.aoeRadius * 1.5f);
                        foreach (var hit in hits)
                        {
                            hit.GetComponentInParent<EnemyBase>()?.SetTarget(transform);
                        }
                    }
                    break;
                case AbilityId.Unbreakable when rune == AbilityRune.B: // Adamant
                    health?.Heal(health.maxHP * 0.15f);
                    break;

                case AbilityId.Empower when rune == AbilityRune.B: // Momentum -- starts stronger instead of ramping over time
                    statusController?.ApplyEffect(StatusEffectType.Empowered,
                        ability.statusDuration * MagnitudeScale(ability), ability.statusMagnitude * MagnitudeScale(ability) * 1.4f);
                    break;

                case AbilityId.Hex when rune == AbilityRune.A: // Spreading Hex
                    ApplySecondaryEnemyEffect(primaryTarget, StatusEffectType.Weaken,
                        ability.statusDuration * MagnitudeScale(ability), ability.statusMagnitude * MagnitudeScale(ability));
                    break;

                case AbilityId.Chronoshift when rune == AbilityRune.B: // Haste -- a personal burst window instead of pure CC
                    statusController?.ApplyEffect(StatusEffectType.Empowered, ability.statusDuration, 0.3f);
                    break;

                case AbilityId.VenomBolt when rune == AbilityRune.B: // Concentrated -- pierces to a second nearby target
                    ApplySecondaryEnemyEffect(primaryTarget, StatusEffectType.Poison,
                        ability.statusDuration * MagnitudeScale(ability), ability.statusMagnitude * MagnitudeScale(ability),
                        extraDamage: ability.damage * DamageScale(ability) * 0.6f);
                    break;

                case AbilityId.Icicle when rune == AbilityRune.A: // Shatter -- punishes finishing a target
                    {
                        var h = primaryTarget.GetComponent<Health>();
                        if (h != null && h.CurrentHP <= h.maxHP * 0.25f)
                        {
                            var hits = Physics.OverlapSphere(primaryTarget.transform.position, 3f);
                            foreach (var hit in hits)
                            {
                                if (hit.gameObject == primaryTarget) continue;
                                if (hit.GetComponentInParent<EnemyBase>() == null) continue;
                                hit.GetComponentInParent<IHealth>()?.TakeDamage(ability.damage * DamageScale(ability) * 0.7f, ignoreDef: false);
                            }
                        }
                    }
                    break;
                case AbilityId.Icicle when rune == AbilityRune.B: // Deep Freeze's bonus damage half (duration handled in ApplyAbilityEffects)
                    primaryTarget.GetComponent<IHealth>()?.TakeDamage(ability.damage * DamageScale(ability) * 0.5f, ignoreDef: false);
                    break;

                case AbilityId.HolySmite when rune == AbilityRune.A: // Wildfire
                    ApplySecondaryEnemyEffect(primaryTarget, StatusEffectType.Sick,
                        ability.statusDuration * MagnitudeScale(ability), ability.statusMagnitude * MagnitudeScale(ability));
                    break;

                case AbilityId.Rebirth when rune == AbilityRune.A: // Guardian Angel
                    primaryTarget.GetComponent<StatusEffectController>()?.ApplyEffect(StatusEffectType.Fortified, 4f, 0.4f);
                    break;
                case AbilityId.Rebirth when rune == AbilityRune.B: // Second Wind
                    mana?.Regen(mana.maxMP * 0.5f);
                    break;

                case AbilityId.MendingLight when rune == AbilityRune.B: // Purify
                    primaryTarget.GetComponent<StatusEffectController>()?.ApplyEffect(StatusEffectType.Fortified, 2f, 0.3f);
                    break;

                case AbilityId.DeathMark when rune == AbilityRune.A: // Plague
                    {
                        var hits = Physics.OverlapSphere(primaryTarget.transform.position, 3f);
                        foreach (var hit in hits)
                        {
                            if (hit.gameObject == primaryTarget) continue;
                            if (hit.GetComponentInParent<EnemyBase>() == null) continue;
                            hit.GetComponentInParent<StatusEffectController>()?.ApplyEffect(
                                StatusEffectType.Curse, ability.statusDuration * MagnitudeScale(ability), ability.statusMagnitude * MagnitudeScale(ability));
                        }
                    }
                    break;
            }
        }

        // Finds the nearest OTHER enemy near primaryTarget and applies a status effect
        // (optionally plus flat damage) to it too -- shared by every "also afflicts a
        // second target" rune (Spreading Hex, Concentrated, Wildfire).
        private void ApplySecondaryEnemyEffect(GameObject primaryTarget, StatusEffectType effect, float duration, float magnitude, float extraDamage = 0f)
        {
            if (primaryTarget == null) return;
            var hits = Physics.OverlapSphere(primaryTarget.transform.position, 6f);
            foreach (var hit in hits)
            {
                if (hit.gameObject == primaryTarget) continue;
                if (hit.GetComponentInParent<EnemyBase>() == null) continue;
                hit.GetComponentInParent<StatusEffectController>()?.ApplyEffect(effect, duration, magnitude);
                if (extraDamage > 0f) hit.GetComponentInParent<IHealth>()?.TakeDamage(extraDamage, ignoreDef: false);
                break; // only the nearest one
            }
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
