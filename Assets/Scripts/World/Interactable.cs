using System;
using UnityEngine;

namespace DungeonCrawler.World
{
    // Generic "walk up, look at it, press E" marker. Owns only the trigger collider and
    // the prompt text PlayerHUD's look-at raycast reads off it (mirroring how WorldPickup
    // already drives that same label for dropped items) -- what actually happens on
    // interact is entirely up to whoever wires onInteract. GameBootstrap does that wiring
    // once the player (and its inventory/wallet) exist, since vendors and gates are built
    // by HubLayout/DungeonLayout before the player is spawned.
    public class Interactable : MonoBehaviour
    {
        public string prompt = "Interact";
        public Action onInteract;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }
    }
}
