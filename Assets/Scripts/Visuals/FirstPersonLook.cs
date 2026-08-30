using UnityEngine;

namespace DungeonCrawler.Visuals
{
    // Attach to the camera once it's parented under the player (see GameBootstrap). Yaw
    // (mouse X) rotates the player body itself, so PlayerMovement's transform.forward/
    // right -- and therefore WASD -- turns with the camera. Pitch (mouse Y) rotates only
    // this camera, the standard FPS split so looking up/down doesn't tilt the ground
    // plane the player walks on.
    public class FirstPersonLook : MonoBehaviour
    {
        public Transform playerBody;
        public float mouseSensitivity = 2.5f;
        public float minPitch = -80f;
        public float maxPitch = 80f;

        private float pitch;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            // Cursor lock/unlock on Escape is owned by PauseMenuUI now, not here -- having
            // both read Escape in the same frame risked them fighting over the same toggle.
            // This just respects whatever PauseMenuUI (or anything else) set lockState to.
            if (Cursor.lockState != CursorLockMode.Locked) return;

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            if (playerBody != null) playerBody.Rotate(Vector3.up * mouseX);

            pitch = Mathf.Clamp(pitch - mouseY, minPitch, maxPitch);
            transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
