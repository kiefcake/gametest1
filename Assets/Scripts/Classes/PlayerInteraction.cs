using UnityEngine;
using DungeonCrawler.World;

namespace DungeonCrawler.Classes
{
    // E fires whatever the crosshair is currently resting on -- vendors, the dungeon gate,
    // and the hub-return gate all share this one Interactable hook rather than each needing
    // its own input handling (see PlayerHUD's look-at raycast for the matching prompt text,
    // same range, same trigger-inclusive raycast).
    [RequireComponent(typeof(PlayerCharacter))]
    public class PlayerInteraction : MonoBehaviour
    {
        public KeyCode interactKey = KeyCode.E;
        public float interactRange = 4f;

        private PlayerCharacter player;

        private void Awake()
        {
            player = GetComponent<PlayerCharacter>();
        }

        private void Update()
        {
            if (player.health != null && player.health.IsDowned) return;
            if (!Input.GetKeyDown(interactKey)) return;
            if (Camera.main == null) return;

            if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward,
                out RaycastHit hit, interactRange, ~0, QueryTriggerInteraction.Collide))
            {
                hit.collider.GetComponentInParent<Interactable>()?.onInteract?.Invoke();
            }
        }
    }
}
