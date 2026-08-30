using System.Collections.Generic;
using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Classes;

namespace DungeonCrawler.Party
{
    // One of these lives on a run-level manager object (not per-player).
    // Implements the locked design: proximity channel (~2m, ~3s, interruptible by damage),
    // shared pool of revive charges per run (default 3).
    public class PartyReviveController : MonoBehaviour
    {
        // Range/channel time live on Health.reviveRange / Health.reviveChannelTime --
        // NOT duplicated here. A downed target's own Health is the single source of
        // truth (it's also what a UI progress ring would read from), so this class
        // only owns what's genuinely shared across the party: the charge pool.
        public int maxRevivesPerRun = 3;
        public int revivesRemaining;

        private class ChannelState
        {
            public PlayerCharacter reviver;
            public PlayerCharacter target;
            public float progress;
        }

        private readonly List<ChannelState> activeChannels = new List<ChannelState>();

        private void Awake()
        {
            revivesRemaining = maxRevivesPerRun;
        }

        // Call every frame (e.g. from input handling) while a player is holding
        // "revive" near a downed ally. Handles starting, progressing, and completing
        // the channel, and cancels automatically if the reviver takes damage or
        // moves out of range (call CancelChannel manually from your damage handler
        // if you want interrupt-on-damage, since Health doesn't callback out yet).
        public bool TryChannelRevive(PlayerCharacter reviver, PlayerCharacter target, float deltaTime)
        {
            if (revivesRemaining <= 0) return false;
            if (!target.health.IsDowned) return false;

            float dist = Vector3.Distance(reviver.transform.position, target.transform.position);
            if (dist > target.health.reviveRange)
            {
                CancelChannel(reviver);
                return false;
            }

            var state = activeChannels.Find(c => c.reviver == reviver);
            if (state == null)
            {
                state = new ChannelState { reviver = reviver, target = target, progress = 0f };
                activeChannels.Add(state);
            }

            state.progress += deltaTime;
            if (state.progress >= target.health.reviveChannelTime)
            {
                target.health.Revive();
                revivesRemaining--;
                activeChannels.Remove(state);
                return true; // revive completed this frame
            }

            return false; // still channeling
        }

        public void CancelChannel(PlayerCharacter reviver)
        {
            activeChannels.RemoveAll(c => c.reviver == reviver);
        }

        public float GetChannelProgress01(PlayerCharacter reviver, PlayerCharacter target)
        {
            var state = activeChannels.Find(c => c.reviver == reviver && c.target == target);
            if (state == null) return 0f;
            return Mathf.Clamp01(state.progress / target.health.reviveChannelTime);
        }
    }
}
