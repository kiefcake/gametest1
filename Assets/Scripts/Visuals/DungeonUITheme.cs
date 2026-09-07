using UnityEngine;

namespace DungeonCrawler.Visuals
{
    // Shared color tokens for the "Grimy Little Descent" UI kit (the Claude Design canvas
    // handed off for a Unity reskin) -- carved stone, iron rivets, one warm ember accent.
    // Centralized here so every UI file pulls the same exact hex values instead of each
    // re-deriving its own close-but-not-quite approximation, the way ShopUI/PauseMenuUI/
    // StatScreenUI/CharacterSelectUI each currently do with their own local Color consts.
    public static class DungeonUITheme
    {
        // The kit's display face -- panel titles, boss/vendor names, class names. Body
        // text (labels, numbers, descriptions) stays on the project's existing builtin
        // font everywhere; this is only for the handful of "title" elements per screen,
        // matching the kit's own "Display · panel titles · boss names · damage crits"
        // usage note. Falls back to the builtin font if the import hasn't happened yet
        // (e.g. Unity hasn't focused/reimported since the .ttf was added) so a missing
        // font asset degrades gracefully instead of null-refing every screen at once.
        private static Font _displayFont;
        public static Font DisplayFont
        {
            get
            {
                if (_displayFont == null)
                    _displayFont = Resources.Load<Font>("Fonts/GrenzeGotisch") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _displayFont;
            }
        }

        public static readonly Color Ground = new Color32(0x16, 0x18, 0x26, 0xFF);
        public static readonly Color Surface = new Color32(0x23, 0x25, 0x32, 0xFF);
        public static readonly Color SurfaceRaised = new Color32(0x1B, 0x1D, 0x2B, 0xFF);
        public static readonly Color Border = new Color32(0x3F, 0x42, 0x4D, 0xFF);
        public static readonly Color BorderBright = new Color32(0x5B, 0x5F, 0x72, 0xFF);

        public static readonly Color TextPrimary = new Color32(0xF3, 0xF5, 0xFE, 0xFF);
        public static readonly Color TextBody = new Color32(0xE9, 0xE9, 0xED, 0xFF);
        public static readonly Color TextMuted = new Color32(0xB2, 0xB6, 0xCA, 0xFF);
        public static readonly Color TextFaint = new Color32(0x93, 0x97, 0xAB, 0xFF);

        public static readonly Color Ember = new Color32(0xE8, 0x91, 0x2F, 0xFF);
        public static readonly Color EmberBright = new Color32(0xF6, 0xAB, 0x5A, 0xFF);
        public static readonly Color EmberDim = new Color32(0x8A, 0x52, 0x20, 0xFF);
        public static readonly Color EmberFill = new Color32(0x2A, 0x1B, 0x12, 0xFF);
        public static readonly Color EmberFillHover = new Color32(0x3D, 0x24, 0x15, 0xFF);

        public static readonly Color HpFill = new Color32(0xC0, 0x39, 0x2B, 0xFF);
        public static readonly Color HpGhost = new Color32(0x6B, 0x2C, 0x22, 0xFF);
        public static readonly Color HpBacking = new Color32(0x1B, 0x14, 0x20, 0xFF);
        public static readonly Color HpLowOverlay = new Color32(0xFF, 0x5F, 0x3C, 0xFF);

        // The kit's own "Essence" bar is the arcane resource pool next to Vigor/HP -- that's
        // this game's Mana. The game's real Essence is a separate integer currency spent on
        // ability ranks (see Abilities/Essence.cs), shown in the Rank & Rune panel instead.
        public static readonly Color ManaFill = new Color32(0x91, 0x84, 0xD9, 0xFF);
        public static readonly Color ManaBacking = new Color32(0x1A, 0x18, 0x26, 0xFF);
        public static readonly Color RankEssence = new Color32(0xB5, 0xAB, 0xFC, 0xFF);

        public static readonly Color Gold = new Color32(0xD4, 0xA5, 0x37, 0xFF);
        public static readonly Color Heal = new Color32(0x6F, 0xAE, 0x5A, 0xFF);
        public static readonly Color DamageDealt = new Color32(0xF3, 0xF5, 0xFE, 0xFF);
        public static readonly Color DamageTaken = new Color32(0xFF, 0x5F, 0x3C, 0xFF);

        // Per-dungeon accent -- applied only to UI shown inside that dungeon, per the kit's
        // "recolor exactly one secondary accent, and only inside that dungeon" rule.
        public static readonly Color AccentWastes = new Color32(0xD9, 0x4A, 0x2B, 0xFF);
        public static readonly Color AccentFrost = new Color32(0x8F, 0xD3, 0xEA, 0xFF);
        public static readonly Color AccentMarsh = new Color32(0x8F, 0xAE, 0x3C, 0xFF);
        public static readonly Color AccentSnake = new Color32(0xB4, 0x83, 0xD9, 0xFF);
    }
}
