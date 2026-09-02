using UnityEngine;
using DungeonCrawler.Classes;
using DungeonCrawler.World;
using DungeonCrawler.Audio;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Enemies
{
    // The abyss dungeon's real spellcaster -- distinct from RangedImp's straight-line
    // dodgeable bolt in both threat and feel: a delayed ground-targeted AoE that commits
    // to wherever the player was standing when the cast started (so they have to actually
    // move off it, not just sidestep a projectile) and a short defensive blink when
    // someone gets in melee range, so it can't just be walked down like every other
    // enemy. Squishy in exchange -- a real caster, not a bruiser.
    public class AbyssMage : EnemyBase
    {
        public float preferredRange = 8f;
        public float blinkTriggerRange = 3f;
        public float blinkCooldown = 4f;
        public float castTelegraphTime = 1.1f;
        public float aoeRadius = 2.2f;
        public float aoeDamage = 14f;

        private float rangedAttackTimer;
        private float blinkTimer;
        private bool casting;
        private float castElapsed;
        private Vector3 castTargetPos;
        private GameObject telegraphGO;

        protected override void Awake()
        {
            enemyName = "Abyss Mage";
            healthBarHeight = 1.9f;
            moveSpeed = 1.3f;

            base.Awake();

            attackDamage = 0f; // never melees -- Attack() is never called, everything routes through BeginCast/ResolveCast
            attackCooldown = 2.6f;
            health.maxHP *= 0.8f;
            health.SetCurrentHP(health.maxHP);
        }

        // Floating caster, not a humanoid -- the only enemy in this file with no legs,
        // reads as hovering/channeling rather than planted like a melee brute.
        protected override void AttachVisual()
        {
            var built = ProceduralMonster.FloatingCaster(transform, new ProceduralMonster.FloatingSpec
            {
                robeColor = new Color(0.35f, 0.4f, 0.75f),
                accentColor = new Color(0.7f, 0.85f, 1f),
                scale = 1f, orb = true
            });
            visualRenderers = built.renderers;
            spriteAnimator = built.root.gameObject.AddComponent<SpriteAnimator>();
            spriteAnimator.bobHeight = 0.08f;
            spriteAnimator.bobSpeed = 2.2f;
        }

        protected override void Update()
        {
            if (health.IsDowned || target == null) return;
            if (statusController.IsParalyzed) return;

            if (casting)
            {
                TickCast();
                return;
            }

            if (blinkTimer > 0f) blinkTimer -= Time.deltaTime;

            float dist = Vector3.Distance(transform.position, target.position);

            if (dist < blinkTriggerRange && blinkTimer <= 0f)
            {
                Blink();
                return;
            }

            if (dist > preferredRange)
            {
                MoveTowardTarget(); // reuses EnemyBase's weave/separation approach logic
            }
            else
            {
                rangedAttackTimer -= Time.deltaTime;
                if (rangedAttackTimer <= 0f)
                {
                    BeginCast();
                    rangedAttackTimer = attackCooldown * Random.Range(0.85f, 1.15f);
                }
            }
        }

        private void Blink()
        {
            Vector3 away = (transform.position - target.position).normalized;
            // Routed through the CharacterController like every other enemy move, so a
            // blink stops at a wall instead of teleporting straight through one.
            Move(away * 5f);
            blinkTimer = blinkCooldown;
            SfxLibrary.PlayAt(SfxLibrary.Dash, transform.position, 0.3f); // reuses the dash whoosh -- reads as a short teleport
        }

        // Commits to the player's position the instant the cast starts, not when it
        // resolves -- the telegraph is the warning, and standing still to watch it land is
        // what actually gets punished.
        private void BeginCast()
        {
            casting = true;
            castElapsed = 0f;
            castTargetPos = target.position;
            SetInvulnerable(true); // channel is the telegraph -- now backed by actual damage immunity, same as the bosses

            telegraphGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            telegraphGO.name = "MageTelegraph";
            var col = telegraphGO.GetComponent<Collider>();
            if (col != null) Destroy(col);
            telegraphGO.transform.position = castTargetPos + Vector3.up * 0.02f;
            telegraphGO.transform.localScale = new Vector3(aoeRadius * 2f, 0.02f, aoeRadius * 2f);
            var renderer = telegraphGO.GetComponent<Renderer>();
            if (renderer != null) renderer.material = new Material(Shader.Find("Standard")) { color = new Color(0.4f, 0.5f, 1f, 0.6f) };
            var glow = telegraphGO.AddComponent<PortalGlow>();
            glow.colorA = new Color(0.3f, 0.4f, 0.9f);
            glow.colorB = new Color(0.7f, 0.8f, 1f);
            glow.speed = 4f; // fast pulse -- reads as "about to go off," not ambient decoration
        }

        private void TickCast()
        {
            castElapsed += Time.deltaTime;
            if (castElapsed >= castTelegraphTime) ResolveCast();
        }

        private void ResolveCast()
        {
            casting = false;
            if (telegraphGO != null) Destroy(telegraphGO);
            SetInvulnerable(false);

            var hits = Physics.OverlapSphere(castTargetPos, aoeRadius);
            foreach (var hit in hits)
            {
                var pc = hit.GetComponentInParent<PlayerCharacter>();
                if (pc != null && pc.health != null) pc.health.TakeDamage(aoeDamage, ignoreDef: false);
            }
            SfxLibrary.PlayAt(SfxLibrary.Hit, castTargetPos, 0.5f);

            FireRingBurst();
        }

        // Turns the single ground-AoE telegraph into a two-layer dodge: the marked ground
        // (above) AND an outward ring of bolts from the mage's own position, fired the
        // instant the cast resolves. Full 360-degree spread -- unlike RangedImp's forward
        // fan, this has no "safe side" to duck to.
        private void FireRingBurst()
        {
            const int ringCount = 8;
            Vector3 origin = transform.position + Vector3.up;
            Color ringColor = new Color(0.45f, 0.75f, 1f); // cold blue, distinct from RangedImp's magenta bolts but in the mage's own tint family

            for (int i = 0; i < ringCount; i++)
            {
                Vector3 dir = Quaternion.Euler(0, i * 45f, 0) * Vector3.forward;
                Projectile.Spawn(origin, dir, speed: 7f, damage: 7f, color: ringColor);
            }
        }

        protected override void HandleDeath()
        {
            if (telegraphGO != null) Destroy(telegraphGO);
            base.HandleDeath();
        }
    }
}
