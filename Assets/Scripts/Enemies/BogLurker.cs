using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Loot;

namespace DungeonCrawler.Enemies
{
    // The Sunken Ruins' basic melee enemy -- ImpDemon/FrostSkeleton's structural twin
    // (same EnemyBase move-or-attack loop, same on-hit-chance-to-debuff shape) but applies
    // Poison instead of Bleed/ArmorBreak: the first enemy-side source of Poison in the
    // codebase (previously only a player ability could apply it), introducing that
    // dungeon's status-effect gimmick per the "tutorialize one status at a time" pacing.
    public class BogLurker : EnemyBase
    {
        public float poisonChance = 0.35f;
        public float poisonDuration = 5f;
        public float poisonDamage = 3f; // damage per tick (StatusEffectController ticks DoTs once per second) -- comparable overall DPS to ImpDemon's Bleed (3/tick over 4s)

        // Single source of truth for "how do I build a wired-up Bog Lurker" -- both
        // GameBootstrap's dungeon populator and SwampWarden's own tank-check add-spawns
        // need this exact sequence, and duplicating it a second time (on top of the six
        // near-identical Spawn* helpers GameBootstrap already has) was avoidable since
        // both call sites were written in the same change.
        public static GameObject Spawn(Vector3 pos)
        {
            var go = new GameObject("BogLurker");
            go.transform.position = pos;
            go.AddComponent<Health>();
            go.AddComponent<StatusEffectController>();
            go.AddComponent<BogLurker>();
            go.AddComponent<AggroController>();
            var loot = go.AddComponent<LootDropper>();
            loot.lootTable = Resources.Load<LootTable>("Data/Loot/AbyssLootTable");
            loot.minGold = 4;
            loot.maxGold = 9;
            return go;
        }

        protected override void Awake()
        {
            enemyName = "Bog Lurker";
            spriteResourcePath = "Sprites/Enemies/Abyss/imp_demon"; // no dedicated sprite yet -- swamp tint below carries the theme
            spriteHeight = 0.9f;
            healthBarHeight = 1.9f;

            base.Awake();

            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.color = new Color(0.35f, 0.5f, 0.3f); // swamp-green -- distinct from the Abyss imps' warm tones and the Crypt skeletons' ice-blue
        }

        protected override void Attack()
        {
            base.Attack();
            if (Random.value < poisonChance)
            {
                target.GetComponent<StatusEffectController>()?.ApplyEffect(
                    StatusEffectType.Poison, poisonDuration, poisonDamage);
            }
        }
    }
}
