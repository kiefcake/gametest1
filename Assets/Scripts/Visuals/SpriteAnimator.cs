using UnityEngine;

namespace DungeonCrawler.Visuals
{
    // Lightweight procedural animation for a static sprite -- no frame-based art exists
    // yet (Sprites/ only has single still images per enemy), so this is code-driven
    // motion instead: a continuous idle bob so nothing reads as a frozen cutout, plus a
    // quick scale-punch that EnemyBase.Attack triggers so an attack has a visible tell.
    // Attached automatically by SpriteVisual.Attach.
    public class SpriteAnimator : MonoBehaviour
    {
        public float bobHeight = 0.08f;
        public float bobSpeed = 4f;

        private Vector3 baseLocalPos;
        private Vector3 baseScale;
        private float bobPhase;
        private float pulseTimer;
        private const float PulseDuration = 0.2f;

        private void Start()
        {
            baseLocalPos = transform.localPosition;
            baseScale = transform.localScale;
            bobPhase = Random.Range(0f, Mathf.PI * 2f); // desyncs multiple enemies bobbing in lockstep
        }

        private void Update()
        {
            float bob = Mathf.Sin(Time.time * bobSpeed + bobPhase) * bobHeight;
            transform.localPosition = baseLocalPos + new Vector3(0, bob, 0);

            if (pulseTimer > 0f)
            {
                pulseTimer -= Time.deltaTime;
                float t = Mathf.Clamp01(pulseTimer / PulseDuration);
                float punch = Mathf.Sin(t * Mathf.PI) * 0.35f; // grows then settles back over the pulse
                transform.localScale = baseScale * (1f + punch);
            }
            else
            {
                transform.localScale = baseScale;
            }
        }

        public void PulseAttack()
        {
            pulseTimer = PulseDuration;
        }
    }
}
