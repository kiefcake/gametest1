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
        // How far the model steps forward into an attack before returning, in local units
        // (fraction of its own body -- this sits on the model root, whose parent's scale
        // already accounts for the enemy's own size, so a flat offset would look wrong on
        // a boss-scale model; see PulseAttack). 0 disables the lunge entirely -- some
        // callers (a caster mid-cast-telegraph, say) may want the scale-punch without the
        // model visibly stepping.
        public float lungeDistance = 0.18f;

        // A side-to-side root lean on top of the bob -- root-transform-only, same as
        // everything else here, so it's safe for every real imported mesh regardless of
        // whether that mesh has any rig to swing (it doesn't -- see the 12 creature OBJ
        // imports, which bake each part's geometry in world space with no joint pivots to
        // rotate around; a per-limb walk cycle would need the export pipeline reworked to
        // preserve pivots, which wasn't done here). This is the honest, safe substitute:
        // a whole-body waddle instead of frozen-bob-only, with zero risk to the meshes
        // already shipped. 0 disables it (see TrainingDummy, which should read as inert).
        public float swayAngle = 3f;
        public float swaySpeed = 2.5f;

        private Vector3 baseLocalPos;
        private Vector3 baseScale;
        private Quaternion baseLocalRot;
        private float bobPhase;
        private float pulseTimer;
        private const float PulseDuration = 0.2f;

        private void Start()
        {
            baseLocalPos = transform.localPosition;
            baseScale = transform.localScale;
            baseLocalRot = transform.localRotation;
            bobPhase = Random.Range(0f, Mathf.PI * 2f); // desyncs multiple enemies bobbing in lockstep
        }

        private void Update()
        {
            float bob = Mathf.Sin(Time.time * bobSpeed + bobPhase) * bobHeight;
            float sway = swayAngle != 0f ? Mathf.Sin(Time.time * swaySpeed + bobPhase * 0.7f) * swayAngle : 0f;
            transform.localRotation = baseLocalRot * Quaternion.Euler(0, 0, sway);

            if (pulseTimer > 0f)
            {
                pulseTimer -= Time.deltaTime;
                float t = Mathf.Clamp01(pulseTimer / PulseDuration);
                float punch = Mathf.Sin(t * Mathf.PI); // rises then settles back to 0 over the pulse
                transform.localScale = baseScale * (1f + punch * 0.35f);
                // Local +Z is always the enemy's own forward -- EnemyBase.LateUpdate keeps
                // every enemy turned to face its target, so a step along the model's own
                // local Z reads as "lunging at what it's attacking" regardless of which way
                // the enemy happens to be facing at the time.
                transform.localPosition = baseLocalPos + new Vector3(0, bob, punch * lungeDistance);
            }
            else
            {
                transform.localScale = baseScale;
                transform.localPosition = baseLocalPos + new Vector3(0, bob, 0);
            }
        }

        public void PulseAttack()
        {
            pulseTimer = PulseDuration;
        }
    }
}
