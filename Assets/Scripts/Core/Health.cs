using System;
using UnityEngine;

namespace DungeonCrawler.Core
{
    public class Health : MonoBehaviour, IHealth
    {
        public float maxHP = 100f;
        public float defense = 0f;

        // Private setter -- external code should go through TakeDamage/Heal/Revive/
        // SetCurrentHP rather than mutating HP directly, so nothing can bypass the
        // downed check or overheal logic.
        public float CurrentHP { get; private set; }

        public bool IsDowned { get; private set; }

        // Fired exactly once when HP hits zero (guarded by the IsDowned early-return
        // in TakeDamage, so it can't double-fire). Consumers decide what "death" means
        // for them: EnemyBase destroys the GameObject, PartyReviveController treats a
        // downed player as revivable. Health itself stays agnostic of which is which.
        public event Action OnDeath;

        // Fired with the actual amount applied (post-mitigation for damage, post-overheal-
        // clamp for heals) -- HealthVFX listens for these to spawn floating numbers/hit
        // flashes without every damage source (abilities, enemy melee, DoT ticks) needing
        // to know about visuals itself.
        public event Action<float> OnDamaged;
        public event Action<float> OnHealed;

        // Revive config, per locked design decision:
        // proximity channel (~3s within ~2m), interruptible, shared charge pool.
        public float reviveChannelTime = 3f;
        public float reviveRange = 2f;

        // Assigned by the owner (PlayerCharacter/EnemyBase) alongside the existing reverse
        // reference (StatusEffectController.health) -- lets TakeDamage check the wearer's
        // own buffs (Fortified) without Health depending on who owns it.
        public StatusEffectController statusController;

        private void Awake()
        {
            CurrentHP = maxHP;
        }

        // Used by initialization code (e.g. PlayerCharacter.Initialize) to set starting
        // HP after maxHP is finalized, without going through the damage/heal path.
        public void SetCurrentHP(float value)
        {
            CurrentHP = Mathf.Clamp(value, 0f, maxHP);
        }

        public void TakeDamage(float amount, bool ignoreDef)
        {
            if (IsDowned) return;
            float mitigated = ignoreDef ? amount : Mathf.Max(1f, amount - defense);
            if (statusController != null && statusController.HasEffect(StatusEffectType.Fortified))
                mitigated *= 1f - statusController.GetMagnitude(StatusEffectType.Fortified);

            CurrentHP -= mitigated;
            OnDamaged?.Invoke(mitigated);
            if (CurrentHP <= 0f)
            {
                CurrentHP = 0f;
                Down();
            }
        }

        public void Heal(float amount)
        {
            if (IsDowned) return;
            // Sick (StatusEffectType) existed in the enum and was applied by nothing, and
            // checked by nothing -- a completely inert debuff. Wired here, not per-caller,
            // so it uniformly reduces healing from every source (abilities, regen, revive)
            // the same way Fortified uniformly reduces damage above.
            if (statusController != null && statusController.HasEffect(StatusEffectType.Sick))
                amount *= 1f - statusController.GetMagnitude(StatusEffectType.Sick);

            float healed = Mathf.Min(maxHP, CurrentHP + amount) - CurrentHP;
            CurrentHP += healed;
            if (healed > 0f) OnHealed?.Invoke(healed);
        }

        private void Down()
        {
            IsDowned = true;
            OnDeath?.Invoke();
        }

        // Called by PartyReviveController once a channel completes successfully.
        public void Revive(float reviveHpFraction = 0.5f)
        {
            if (!IsDowned) return;
            IsDowned = false;
            CurrentHP = maxHP * reviveHpFraction;
        }
    }
}
