using UnityEngine;

namespace DungeonCrawler.Visuals
{
    // Plays once on death instead of the model just standing frozen for
    // destroyDelayAfterDeath -- topples onto its side, then sinks slightly into the floor,
    // so a kill actually reads as a kill rather than the corpse looking identical to a
    // live, paused enemy right up until it pops out of existence. Disables
    // SpriteAnimator/ProceduralLimbAnimator on the same GameObject first (if present) so
    // neither fights the topple by also writing localPosition/localRotation every frame.
    public class ProceduralDeathAnimator : MonoBehaviour
    {
        private const float ToppleDuration = 0.45f;
        private const float SinkDistance = 0.6f;

        private Quaternion startRot;
        private Quaternion endRot;
        private Vector3 startPos;
        private Vector3 endPos;
        private float duration;
        private float age;
        private bool playing;

        public void Play(float totalLifetime)
        {
            var bob = GetComponent<SpriteAnimator>();
            if (bob != null) bob.enabled = false;
            var limbs = GetComponent<ProceduralLimbAnimator>();
            if (limbs != null) limbs.enabled = false;

            startRot = transform.localRotation;
            // Topples around local Z rather than pitching forward on X -- a biped falls
            // sideways the way an actual collapse looks, and a legless Serpent (already
            // lying flat along its own forward axis) rolls onto its back instead of ending
            // in a pose indistinguishable from standing.
            endRot = startRot * Quaternion.Euler(0, 0, Random.value < 0.5f ? 82f : -82f);

            startPos = transform.localPosition;
            endPos = startPos + new Vector3(0, -SinkDistance, 0);

            // Sinks over the corpse's whole remaining lifetime, not just the topple, so it
            // keeps visibly settling into the ground right up until it's removed instead of
            // finishing early and sitting motionless.
            duration = Mathf.Max(ToppleDuration, totalLifetime * 0.85f);
            age = 0f;
            playing = true;
        }

        private void Update()
        {
            if (!playing) return;
            age += Time.deltaTime;

            float toppleT = Mathf.Clamp01(age / ToppleDuration);
            float eased = 1f - (1f - toppleT) * (1f - toppleT); // ease-out -- fast at first, settles rather than falling at a constant rate
            transform.localRotation = Quaternion.Slerp(startRot, endRot, eased);

            float sinkT = Mathf.Clamp01(age / duration);
            transform.localPosition = Vector3.Lerp(startPos, endPos, sinkT);
        }
    }
}
