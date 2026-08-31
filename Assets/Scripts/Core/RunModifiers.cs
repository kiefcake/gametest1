namespace DungeonCrawler.Core
{
    // Prototype of the run-modifier system from full-game-scope.md Section 6 -- optional,
    // player-chosen flags that trade risk for variance/replayability. Deliberately just
    // one modifier for now (the doc's own suggested first step: "prototype one modifier
    // system... before building content") -- this proves the shape (a static flag read by
    // an existing system, reset per run) before adding more toggles.
    public static class RunModifiers
    {
        public static bool DoubleDamageTaken;

        public static void ResetAll()
        {
            DoubleDamageTaken = false;
        }
    }
}
