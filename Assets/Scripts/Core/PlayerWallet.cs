using System;
using UnityEngine;

namespace DungeonCrawler.Core
{
    // Single soft currency (see design doc "Economy: Single soft currency"). Lives on the
    // player GameObject. Enemies pay directly into whichever wallet FindObjectOfType turns
    // up (see LootDropper) -- fine for solo testing, same shortcut the rest of this project
    // already takes for singleton-ish lookups (FirstPersonLook, InventoryUI, etc).
    public class PlayerWallet : MonoBehaviour
    {
        public int Gold { get; private set; }
        public event Action OnChanged;

        public void Add(int amount)
        {
            if (amount <= 0) return;
            Gold += amount;
            OnChanged?.Invoke();
        }

        public bool Spend(int amount)
        {
            if (amount <= 0 || Gold < amount) return false;
            Gold -= amount;
            OnChanged?.Invoke();
            return true;
        }
    }
}
