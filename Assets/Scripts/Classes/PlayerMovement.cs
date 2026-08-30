using UnityEngine;

namespace DungeonCrawler.Classes
{
    // First-person WASD movement. Moves relative to the player's own facing
    // (transform.right/forward), not raw world axes -- FirstPersonLook applies mouse-yaw
    // to this same transform, so turning to look also turns which way "forward" walks.
    [RequireComponent(typeof(PlayerCharacter))]
    public class PlayerMovement : MonoBehaviour
    {
        // CharacterController.Move() never applies gravity on its own -- without pushing a
        // downward component through it every frame, nothing ever pulls the player back
        // down after their vertical position gets disturbed. That was invisible on the
        // flat hub/dungeon floors normally, but a dash's much larger single-frame motion
        // (see PlayerDash) can deflect off a rounded enemy capsule collider and shove the
        // controller upward; with no gravity, that lift never gets undone and the player
        // is stuck floating. This runs every frame (not gated behind the h==0/v==0 early
        // return below) so gravity keeps applying even while standing still.
        private const float Gravity = -20f;
        public float jumpSpeed = 7f;
        public KeyCode jumpKey = KeyCode.Space;
        private float verticalVelocity;

        private PlayerCharacter player;
        private CharacterController controller;

        private void Awake()
        {
            player = GetComponent<PlayerCharacter>();
            controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            // Previously only checked IsParalyzed -- a player at 0 HP (IsDowned) kept
            // moving and fighting normally with no visible sign anything was wrong, since
            // Health.TakeDamage() already no-ops once downed. That reads as "enemies run
            // at me and do nothing": you'd already been downed, silently, with no HP bar
            // to notice it happening.
            if (player.health != null && player.health.IsDowned) return;
            if (player.statusController != null && player.statusController.IsParalyzed) return;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Vector3 move = transform.right * h + transform.forward * v;
            if (move.sqrMagnitude > 1f) move.Normalize();

            float speed = player.Stats != null ? Mathf.Max(1f, player.Stats.GetValue(Core.StatType.SPD)) : 5f;
            Vector3 delta = move * speed * Time.deltaTime;

            if (controller != null)
            {
                // Reset to a small constant push rather than exactly 0 -- CharacterController's
                // own isGrounded flag needs a slight downward motion each frame to stay true
                // on flat ground, and resetting to 0 makes it flicker ungrounded.
                if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -0.5f;
                if (controller.isGrounded && Input.GetKeyDown(jumpKey)) verticalVelocity = jumpSpeed;
                verticalVelocity += Gravity * Time.deltaTime;
                delta.y += verticalVelocity * Time.deltaTime;
                controller.Move(delta);
            }
            else
            {
                transform.position += delta;
            }
        }
    }
}
