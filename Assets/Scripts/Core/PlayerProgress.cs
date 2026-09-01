using UnityEngine;

namespace DungeonCrawler.Core
{
    // This project's first persistent (cross-session) player state -- everything else so
    // far is session-scoped and rebuilds fresh every run (see RunModifiers, and every
    // World/* generator). PlayerPrefs is the simplest fit for a handful of small unlock
    // flags; move to a real save file if this ever grows past that.
    //
    // Tracks each dungeon boss defeated at least once (ever, not necessarily in the same
    // run) and derives a single "beaten the game once" unlock from all three -- the gate
    // for Hardcore mode, per the user's "earned, like in PEAK" request rather than a free
    // toggle available from the start.
    public static class PlayerProgress
    {
        private const string AbyssBossKey = "Progress.AbyssBossDefeated";
        private const string FrozenCryptBossKey = "Progress.FrozenCryptBossDefeated";
        private const string SunkenRuinsBossKey = "Progress.SunkenRuinsBossDefeated";
        private const string HardcoreUnlockedKey = "Progress.HardcoreUnlocked";

        public static bool HardcoreUnlocked => PlayerPrefs.GetInt(HardcoreUnlockedKey, 0) == 1;

        public static void MarkAbyssBossDefeated() => MarkDefeated(AbyssBossKey);
        public static void MarkFrozenCryptBossDefeated() => MarkDefeated(FrozenCryptBossKey);
        public static void MarkSunkenRuinsBossDefeated() => MarkDefeated(SunkenRuinsBossKey);

        private static void MarkDefeated(string key)
        {
            PlayerPrefs.SetInt(key, 1);

            bool allThree = PlayerPrefs.GetInt(AbyssBossKey, 0) == 1
                && PlayerPrefs.GetInt(FrozenCryptBossKey, 0) == 1
                && PlayerPrefs.GetInt(SunkenRuinsBossKey, 0) == 1;
            if (allThree) PlayerPrefs.SetInt(HardcoreUnlockedKey, 1);

            PlayerPrefs.Save();
        }
    }
}
