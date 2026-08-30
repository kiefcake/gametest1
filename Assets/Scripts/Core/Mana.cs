using UnityEngine;

namespace DungeonCrawler.Core
{
    // Mirrors Health's pattern for a second resource pool. Added because AbilityData
    // already had a manaCost field that nothing was reading -- abilities were
    // effectively free to spam. This closes that gap.
    public class Mana : MonoBehaviour
    {
        public float maxMP = 60f;
        public float CurrentMP { get; private set; }

        private void Awake()
        {
            CurrentMP = maxMP;
        }

        // refill: true snaps current to the new max (e.g. on class init).
        // refill: false clamps current down if the new max is lower, but otherwise
        // leaves it alone (e.g. after a stat potion changes max MP mid-run).
        public void SetMax(float newMax, bool refill)
        {
            maxMP = newMax;
            CurrentMP = refill ? maxMP : Mathf.Min(CurrentMP, maxMP);
        }

        public bool TryConsume(float amount)
        {
            if (CurrentMP < amount) return false;
            CurrentMP -= amount;
            return true;
        }

        public void Regen(float amount)
        {
            CurrentMP = Mathf.Min(maxMP, CurrentMP + amount);
        }
    }
}
