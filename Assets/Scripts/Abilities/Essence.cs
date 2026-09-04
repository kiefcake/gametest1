using System;
using UnityEngine;

namespace DungeonCrawler.Abilities
{
    // Second per-player currency, alongside PlayerWallet's gold -- spent exclusively on
    // ability ranks/runes (see AbilityCaster.RankUp/ChooseRune), never on potions/gear, so
    // investing in your build and investing in raw stats don't compete for the same
    // wallet. Earned as a flat per-kill amount (see LootDropper.minEssence/maxEssence),
    // same shape as gold -- a true damage/support-contribution split would matter more in
    // real multiplayer than it does for solo-testable play right now, and can be layered
    // onto this same Add() call later without changing anything that spends it.
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
