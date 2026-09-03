using UnityEngine;

namespace DungeonCrawler.Visuals
{
    // Swings a ProceduralMonster.Humanoid's arm/leg pivots back and forth while its owner
    // is actually moving, and eases back to a neutral pose when stationary -- SpriteAnimator's
    // idle bob already sells "alive," this is what specifically sells "walking" rather than
    // sliding around like a mannequin on rails.
    //
    // Tracks moveTracker's position delta each frame rather than reading some "is moving"
    // flag off the owning enemy -- EnemyBase has no such flag today, and every enemy
    // subclass decides movement differently (MoveTowardTarget, a boss's own retreat/
    // approach logic, etc.), so watching actual position change is the one thing that
    // works identically no matter which of them is driving this frame's motion.
    public class ProceduralLimbAnimator : MonoBehaviour
    {
        // The enemy's own top-level transform, NOT this component's transform -- this
        // component lives on the model root, which SpriteAnimator bobs vertically every
        // frame; reading THAT position back would misread the idle bob itself as walking.
        public Transform moveTracker;
        public Transform leftHip, rightHip, leftShoulder, rightShoulder;
        public float swingAmount = 25f;
        public float swingSpeed = 6f;

        private Vector3 lastPos;
        private float phase;
        private float currentSwing;

        private void Start()
        {
            lastPos = Tracked.position;
            phase = Random.Range(0f, Mathf.PI * 2f); // desyncs multiple enemies walking in lockstep
        }

        private Transform Tracked => moveTracker != null ? moveTracker : transform;

        private void Update()
        {
            Vector3 pos = Tracked.position;
            float moved = (pos - lastPos).magnitude;
            lastPos = pos;

            if (moved > 0.0015f)
            {
                phase += Time.deltaTime * swingSpeed;
                currentSwing = Mathf.Sin(phase) * swingAmount;
            }
            else
            {
                currentSwing = Mathf.Lerp(currentSwing, 0f, Time.deltaTime * 8f);
            }

            // Pivots' rest local rotation is always identity by construction (see
            // ProceduralMonster.BuildLimbPivot) -- no rest-pose caching needed, this is the
            // whole rotation. Arms swing at a reduced fraction of the legs' amount and
            // opposite phase to each matching-side leg, the usual counter-swing walk read.
            if (leftHip != null) leftHip.localRotation = Quaternion.Euler(currentSwing, 0, 0);
            if (rightHip != null) rightHip.localRotation = Quaternion.Euler(-currentSwing, 0, 0);
            if (leftShoulder != null) leftShoulder.localRotation = Quaternion.Euler(-currentSwing * 0.6f, 0, 0);
            if (rightShoulder != null) rightShoulder.localRotation = Quaternion.Euler(currentSwing * 0.6f, 0, 0);
        }
    }
}
