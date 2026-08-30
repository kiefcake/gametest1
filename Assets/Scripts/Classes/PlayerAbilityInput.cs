using UnityEngine;
using DungeonCrawler.Abilities;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Classes
{
    // AbilityCaster.TryCast() existed with nothing calling it -- abilities were wired
    // end-to-end (cooldowns, mana, damage/heal/status application) but never reachable
    // from input. This closes that gap for testing.
    //
    // LMB used to double as a Basic1 alias, but now that AutoAttack owns LMB (hold to
    // fire, free, DEX-scaled) that would double-fire on the same click -- Basic1 is
    // keyboard-only now, RMB still aliases Basic2.
    //
    // Targeting is a placeholder, not the real design: heals/cleanses aim at self (the
    // only "ally" in a solo test), everything else uses EnemyTargeting (crosshair first,
    // nearest enemy in range as fallback). Real targeting (click-to-target an ally) is
    // follow-up work once there's more than one player to aim at.
    [RequireComponent(typeof(PlayerCharacter))]
    public class PlayerAbilityInput : MonoBehaviour
    {
        public KeyCode basic1Key = KeyCode.Alpha1;
        public KeyCode basic2Key = KeyCode.Alpha2;
        public KeyCode ultimateKey = KeyCode.Alpha3;
        public float castRange = 12f;

        // Set by GameBootstrap after both exist -- purely cosmetic (the swing plays on a
        // successful cast), so a missing reference just means no swing, not a broken cast.
        public WeaponViewmodel viewmodel;

        private PlayerCharacter player;

        private void Awake()
        {
            player = GetComponent<PlayerCharacter>();
        }

        private void Update()
        {
            // Same softlock as PlayerMovement: at 0 HP, keep abilities from firing so a
            // downed player doesn't look like they're still fighting.
            if (player.health != null && player.health.IsDowned) return;

            if (Input.GetKeyDown(basic1Key)) TryCast(AbilitySlot.Basic1);
            if (Input.GetKeyDown(basic2Key) || Input.GetMouseButtonDown(1)) TryCast(AbilitySlot.Basic2);
            if (Input.GetKeyDown(ultimateKey)) TryCast(AbilitySlot.Ultimate);
        }

        private void TryCast(AbilitySlot slot)
        {
            var ability = player.abilityCaster.abilities.Find(a => a.slot == slot);
            if (ability == null) return;

            bool selfTargeted = ability.isSelfTargeted || ability.healAmount > 0f || ability.isCleanse;
            GameObject target = selfTargeted ? gameObject : EnemyTargeting.FindTarget(transform, castRange);
            if (target == null) return;

            if (player.abilityCaster.TryCast(slot, target))
            {
                Debug.Log($"[Ability] Cast {ability.abilityName} at {target.name}");
                viewmodel?.Swing();
            }
        }
    }
}
