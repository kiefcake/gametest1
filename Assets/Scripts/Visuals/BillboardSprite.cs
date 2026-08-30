using UnityEngine;

namespace DungeonCrawler.Visuals
{
    // RotMG-style billboarding: the sprite always faces the camera regardless of the
    // camera's angle, rather than being a flat decal lying in world space. Cheap enough
    // to run per-instance since the sprite count in a test encounter is tiny.
    public class BillboardSprite : MonoBehaviour
    {
        private void LateUpdate()
        {
            if (Camera.main == null) return;
            transform.rotation = Camera.main.transform.rotation;
        }
    }
}
