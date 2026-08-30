using UnityEngine;

namespace DungeonCrawler.Visuals
{
    // FPS "weapon in hand" -- a sprite parented directly to the camera at a fixed local
    // offset, so it rides along with every camera move/rotation like a real viewmodel.
    // No arm/hand rig exists, so it's just the class's weapon icon, bobbing while moving
    // and swinging when PlayerAbilityInput lands a cast. Deliberately NOT on
    // PlayerCharacter.LocalVisualLayer -- that layer is excluded from the camera's culling
    // mask (see GameBootstrap), which would make this invisible.
    public class WeaponViewmodel : MonoBehaviour
    {
        private const float SwingDuration = 0.18f;

        private Vector3 baseLocalPos;
        private float bobPhase;
        private float swingTimer;
        private SpriteRenderer sprite;

        public static WeaponViewmodel Attach(Transform camera, Sprite weaponSprite)
        {
            if (weaponSprite == null) return null;

            var go = new GameObject("WeaponViewmodel");
            go.transform.SetParent(camera, false);
            go.transform.localPosition = new Vector3(0.4f, -0.32f, 0.7f);
            go.transform.localRotation = Quaternion.Euler(0, 25, -10);
            go.transform.localScale = Vector3.one * 0.5f;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = weaponSprite;
            sr.sortingOrder = 10;

            var vm = go.AddComponent<WeaponViewmodel>();
            vm.baseLocalPos = go.transform.localPosition;
            vm.sprite = sr;
            return vm;
        }

        // Called when a weapon is equipped (see InventoryUI) so the in-hand view actually
        // reflects what's equipped, not just what the class started with.
        public void SetSprite(Sprite newSprite)
        {
            if (sprite != null && newSprite != null) sprite.sprite = newSprite;
        }

        private void Update()
        {
            float bobX = 0f, bobY = 0f;
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            if (h != 0f || v != 0f)
            {
                bobPhase += Time.deltaTime * 8f;
                bobY = Mathf.Sin(bobPhase) * 0.02f;
                bobX = Mathf.Cos(bobPhase * 0.5f) * 0.015f;
            }

            Vector3 swingOffset = Vector3.zero;
            if (swingTimer > 0f)
            {
                swingTimer -= Time.deltaTime;
                float t = Mathf.Clamp01(swingTimer / SwingDuration);
                swingOffset = new Vector3(-0.15f, 0.1f, 0f) * Mathf.Sin(t * Mathf.PI);
            }

            transform.localPosition = baseLocalPos + new Vector3(bobX, bobY, 0) + swingOffset;
        }

        public void Swing()
        {
            swingTimer = SwingDuration;
        }
    }
}
