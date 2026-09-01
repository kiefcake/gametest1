using UnityEngine;

namespace DungeonCrawler.Classes
{
    // Safety net for the procedural geometry's seams -- a gap where two pieces of
    // hand-computed wall/floor/ramp/platform geometry don't quite meet can let the player
    // slip past everything solid, and with nothing below any room to catch them,
    // PlayerMovement's gravity (which never stops applying) turns an ordinary fall into an
    // endless one. Below fallY, snap back to the last safe point instead of leaving the
    // run softlocked while the underlying geometry gap gets tracked down separately.
    public class FallRecovery : MonoBehaviour
    {
        public float fallY = -25f;
        public System.Action onFellOutOfWorld;

        private void Update()
        {
            if (transform.position.y > fallY) return;
            onFellOutOfWorld?.Invoke();
        }
    }
}
