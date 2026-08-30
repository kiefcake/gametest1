using UnityEngine;

namespace DungeonCrawler.Classes
{
    // Solo-testing safety net. Proximity revive (see Party/PartyReviveController) needs a
    // second player standing over you -- a solo run will never have one, and every input
    // script (PlayerMovement, PlayerAbilityInput, AutoAttack, PlayerDash, PlayerInteraction)
    // already no-ops forever while IsDowned. Without this, going down was a permanent dead
    // end. After recoveryDelay with no revive, this fires onRunFailed once so whoever's
    // listening (GameBootstrap) can send the player back to the hub instead of leaving them
    // stuck on a frozen screen.
    [RequireComponent(typeof(PlayerCharacter))]
    public class DownedRecovery : MonoBehaviour
    {
        public float recoveryDelay = 8f;
        public System.Action onRunFailed;

        private PlayerCharacter player;
        private float timer;
        private bool waiting;

        public float SecondsRemaining => waiting ? Mathf.Max(0f, timer) : 0f;

        private void Awake()
        {
            player = GetComponent<PlayerCharacter>();
            if (player.health != null) player.health.OnDeath += HandleDowned;
        }

        private void OnDestroy()
        {
            if (player != null && player.health != null) player.health.OnDeath -= HandleDowned;
        }

        private void HandleDowned()
        {
            waiting = true;
            timer = recoveryDelay;
        }

        private void Update()
        {
            if (!waiting) return;
            // A party member's revive clears IsDowned out from under this timer -- stop
            // waiting rather than firing onRunFailed after the fact.
            if (player.health == null || !player.health.IsDowned) { waiting = false; return; }

            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                waiting = false;
                onRunFailed?.Invoke();
            }
        }
    }
}
