using System;
using UnityEngine;

namespace DungeonCrawler.Abilities
{
    // Second per-player currency, alongside PlayerWallet's gold -- spent exclusively on
    // ability ranks (see AbilityCaster.RankUp), never on potions/gear, so investing in your
    // build and investing in raw stats don't compete for the same wallet. Earned from
    // combat contribution (see LootDropper.RegisterEssenceContribution) rather than a flat
    // per-kill amount, so support-oriented play (a heal, a cleanse, a debuff that helps
    // land someone else's kill) pays out too, not just landing the killing blow.
    //
    // Deliberately resets every run: this component lives on the player GameObject
    // BeginRun() creates fresh each time, same as Health/Mana/AbilityCaster -- per-run
    // ability progression rather than a persistent account level is the whole point (see
    // full-game-scope.md's locked "no XP levels" decision).
    public class Essence : MonoBehaviour
    {
        public int Amount { get; private set; }
        public event Action OnChanged;

        public void Add(int amount)
        {
            if (amount <= 0) return;
            Amount += amount;
            OnChanged?.Invoke();
        }

        public bool Spend(int amount)
        {
            if (amount <= 0 || Amount < amount) return false;
            Amount -= amount;
            OnChanged?.Invoke();
            return true;
        }
    }
}
