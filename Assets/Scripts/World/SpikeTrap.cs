using System.Collections.Generic;
using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.World
{
    // A telegraphed, periodic floor hazard -- retracted (safe, walk over it freely) most
    // of the time, then rises on a timer and damages anything standing on it while
    // extended, before sinking back down. Unlike LavaHazard (always-on, walk around it),
    // this rewards good timing over route-planning: the tell IS the spike mesh's own
    // rising/falling animation.
    //
    // All 5 spike cones live in ONE model file (Models/Props/spike_trap_spikes, its own
    // separate asset from the base plate -- see ImportedMeshRig.LoadRigGroups) and move
    // as a single rigid group via pure Y-axis TRANSLATION, not rotation -- unlike the
    // creature roster's limb rigging, this needed no pivot-relative export at all:
    // translating a child's local position works correctly regardless of where its
    // geometry was baked, since there's no pivot to get wrong in the first place.
    public class SpikeTrap : MonoBehaviour
    {
        public float retractedDuration = 2.5f;
        public float extendedDuration = 1.2f;
        public float riseTime = 0.25f;
        public float damagePerTick = 12f;
        public float tickInterval = 0.4f;
        public float extendedHeight = 0.22f;

        private Transform spikes;
        private float retractedY;
        private bool spikesUp;
        private float phaseTimer;
        private float riseProgress; // 0 = fully retracted, 1 = fully extended
        private readonly Dictionary<GameObject, float> nextTickAt = new Dictionary<GameObject, float>();

        // Called right after the model is instantiated -- spikesRoot is the whole
        // "spikes" rig group (see DungeonLayout.BuildSpikeTrap). Tolerates null (a
        // missing/stale group file) by just never animating rather than throwing.
        public void Init(Transform spikesRoot)
        {
            spikes = spikesRoot;
            if (spikes != null) retractedY = spikes.localPosition.y;
            phaseTimer = retractedDuration * Random.Range(0.5f, 1f); // desyncs multiple traps in the same room
        }

        private void Update()
        {
            if (spikes == null) return;

            phaseTimer -= Time.deltaTime;
            if (phaseTimer <= 0f)
            {
                spikesUp = !spikesUp;
                phaseTimer = spikesUp ? extendedDuration : retractedDuration;
            }

            float target = spikesUp ? 1f : 0f;
            riseProgress = Mathf.MoveTowards(riseProgress, target, Time.deltaTime / riseTime);

            var lp = spikes.localPosition;
            spikes.localPosition = new Vector3(lp.x, retractedY + riseProgress * extendedHeight, lp.z);
        }

        private void OnTriggerStay(Collider other)
        {
            // Only actually hurts once the spikes are most of the way up -- getting
            // caught right at the very start of the rise shouldn't feel like a coin flip.
            if (riseProgress < 0.6f) return;

            var root = other.transform.root.gameObject;
            if (nextTickAt.TryGetValue(root, out float t) && Time.time < t) return;
            nextTickAt[root] = Time.time + tickInterval;

            other.GetComponentInParent<IHealth>()?.TakeDamage(damagePerTick, ignoreDef: true);
        }
    }
}
