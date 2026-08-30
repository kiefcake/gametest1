using UnityEngine;
using DungeonCrawler.Audio;

namespace DungeonCrawler.Classes
{
    // Quick directional burst -- press to dodge out of an incoming hit or reposition
    // around an enemy, the spacing tool combat was missing. Dashes in whatever direction
    // WASD is currently held (sideways/backward included), or straight forward if nothing's
    // held. Goes through CharacterController.Move rather than raw transform math, same as
    // PlayerMovement, so a dash can't punch through walls.
    [RequireComponent(typeof(PlayerCharacter))]
    public class PlayerDash : MonoBehaviour
    {
        public KeyCode dashKey = KeyCode.LeftShift;
        public float dashDistance = 4.5f;
        public float dashDuration = 0.18f;
        public float cooldown = 1.2f;

        private PlayerCharacter player;
        private CharacterController controller;
        private float cooldownTimer;
        private float dashTimer;
        private Vector3 dashVelocity;

        private void Awake()
        {
            player = GetComponent<PlayerCharacter>();
            controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

            if (dashTimer > 0f)
            {
                dashTimer -= Time.deltaTime;
                controller?.Move(dashVelocity * Time.deltaTime);
                return;
            }

            if (player.health != null && player.health.IsDowned) return;
            if (player.statusController != null && player.statusController.IsParalyzed) return;
            if (!Input.GetKeyDown(dashKey) || cooldownTimer > 0f) return;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 dir = transform.right * h + transform.forward * v;
            if (dir.sqrMagnitude < 0.01f) dir = transform.forward; // no direction held -- dash forward by default

            dir.Normalize();
            dashVelocity = dir * (dashDistance / dashDuration);
            dashTimer = dashDuration;
            cooldownTimer = cooldown;
            SfxLibrary.PlayAt(SfxLibrary.Dash, transform.position, 0.3f);
        }
    }
}
