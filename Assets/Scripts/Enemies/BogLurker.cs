using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Loot;
using DungeonCrawler.Visuals;

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
            healthBarHeight = 1.5f;

            base.Awake();
        }

        // Amorphous swamp-muck creature -- Blob archetype rather than Humanoid, no limbs.
        protected override void AttachVisual()
        {
            var built = ProceduralMonster.Blob(transform, new ProceduralMonster.BlobSpec
            {
                bodyColor = new Color(0.3f, 0.45f, 0.25f),
                accentColor = new Color(0.7f, 0.9f, 0.3f),
                scale = 1f
            });
            visualRenderers = built.renderers;
            spriteAnimator = built.root.gameObject.AddComponent<SpriteAnimator>();
            spriteAnimator.bobHeight = 0.06f;
            spriteAnimator.bobSpeed = 2.5f;
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
