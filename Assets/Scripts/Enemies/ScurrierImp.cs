using UnityEngine;

namespace DungeonCrawler.Enemies
{
    // Weak-but-fast swarmer -- dies in a couple of hits but closes distance more than
    // twice as fast as a regular imp and attacks nearly three times as often, so it reads
    // as a real threat to ignore rather than free experience. Reuses EnemyBase's own
    // move-or-attack loop unchanged (no kiting/casting logic needed), just with the dials
    // turned toward "fast and fragile" instead of "sturdy and slow."
    public class ScurrierImp : EnemyBase
    {
        protected override void Awake()
        {
            enemyName = "Imp Scurrier";
            spriteResourcePath = "Sprites/Enemies/Abyss/imp_demon"; // no dedicated sprite yet -- a smaller, tinted version of the regular imp reads as a distinct variant
            spriteScale = 0.65f;
            spriteHeight = 0.6f;
            healthBarHeight = 1.3f;
            healthBarWidth = 0.8f;

            moveSpeed = 4.2f; // more than double ImpDemon's default 2
            attackDamage = 3f;
            attackCooldown = 0.7f;
            attackRange = 1f;
            weaveAmount = 0.9f; // extra jittery -- harder to land a clean hit on than its lumbering approach would suggest
            weaveSpeed = 5f;

            base.Awake();

            health.maxHP *= 0.35f;
            health.SetCurrentHP(health.maxHP);

            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.color = new Color(0.55f, 1f, 0.55f); // sickly green -- distinct from the regular/spiked imps' warm tones at a glance
        }
    }
}
