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
    // The 5 spike parts are driven by pure Y-axis TRANSLATION, not rotation -- unlike
    // the creature roster's limb rigging, this needed none of the pivot-relative export
    // machinery (see ImportedMeshRig's own doc comment for why that exists at all):
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

        private Transform[] spikes;
        private float[] retractedY;
        private bool spikesUp;
        private float phaseTimer;
        private float riseProgress; // 0 = fully retracted, 1 = fully extended
        private readonly Dictionary<GameObject, float> nextTickAt = new Dictionary<GameObject, float>();

        // Called right after the model is instantiated -- spikeParts are the 5
        // "spike_0".."spike_4" children found by name (see DungeonLayout.BuildSpikeTrap).
        public void Init(Transform[] spikeParts)
        {
            spikes = spikeParts;
            retractedY = new float[spikes.Length];
            for (int i = 0; i < spikes.Length; i++) retractedY[i] = spikes[i].localPosition.y;
            phaseTimer = retractedDuration * Random.Range(0.5f, 1f); // desyncs multiple traps in the same room
        }

        private void Update()
        {
            if (spikes == null || spikes.Length == 0) return;

            phaseTimer -= Time.deltaTime;
            if (phaseTimer <= 0f)
            {
                spikesUp = !spikesUp;
                phaseTimer = spikesUp ? extendedDuration : retractedDuration;
            }

            float target = spikesUp ? 1f : 0f;
            riseProgress = Mathf.MoveTowards(riseProgress, target, Time.deltaTime / riseTime);

            for (int i = 0; i < spikes.Length; i++)
            {
                var lp = spikes[i].localPosition;
                spikes[i].localPosition = new Vector3(lp.x, retractedY[i] + riseProgress * extendedHeight, lp.z);
            }
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
