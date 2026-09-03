using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Enemies
{
    // Shared class for both of Stheno the Snake Queen's adds -- Stheno Pets (permanent,
    // orbit at range, apply Slow, respawn immediately when killed -- managed by
    // SthenoSnakeQueen.EnsurePets) and Stheno Swarms (Phase 2 only, orbit tighter and
    // denser, no Slow, despawned on a timer rather than waiting to be killed -- managed by
    // SthenoSnakeQueen.SpawnSwarms/DespawnSwarms). They're mechanically identical enough
    // (circle a point, shoot at the target) that one configurable class is more honest than
    // two near-duplicate ones.
    //
    // Fully overrides Update() rather than reusing EnemyBase's move-or-attack loop: a
    // minion never approaches or melees, it just holds its orbit and fires from there.
    public class SthenoMinion : EnemyBase
    {
        public Transform orbitCenter;
        public float orbitRadius = 4f;
        public float orbitSpeed = 90f; // degrees/sec
        public bool appliesSlow;

        private float orbitAngle;
        private float fireTimer;

        protected override void Awake()
        {
            enemyName = "Stheno Pet";
            healthBarHeight = 0.9f;
            moveSpeed = 0f; // orbit logic below drives position directly, not MoveTowardTarget
            attackDamage = 0f; // never melees

            base.Awake();

            orbitAngle = Random.Range(0f, 360f); // desyncs multiple minions orbiting the same center
        }

        protected override void AttachVisual()
        {
            var built = ProceduralMonster.Serpent(transform, new ProceduralMonster.SerpentSpec
            {
                bodyColor = new Color(0.5f, 0.4f, 0.2f),
                accentColor = new Color(0.9f, 0.8f, 0.3f),
                scale = 0.5f, length = 3f
            });
            visualRenderers = built.renderers;
            spriteAnimator = built.root.gameObject.AddComponent<SpriteAnimator>();
            spriteAnimator.bobHeight = 0.04f;
            spriteAnimator.bobSpeed = 6f;
        }

        protected override void Update()
        {
            if (health.IsDowned) return;

            if (orbitCenter != null)
            {
                // Recomputed fresh every frame from the current angle -- the resulting
                // delta is only ever this frame's small step around the circle (not a
                // sudden jump), so routing it through Move()/CharacterController still
                // gives real swept collision instead of a raw teleport.
                orbitAngle += orbitSpeed * Time.deltaTime;
                Vector3 wantedPos = orbitCenter.position + Quaternion.Euler(0, orbitAngle, 0) * Vector3.forward * orbitRadius;
                Move(wantedPos - transform.position);
            }

            if (target == null) return;
            fireTimer -= Time.deltaTime;
            if (fireTimer <= 0f)
            {
                fireTimer = 2f * Random.Range(0.85f, 1.15f);
                Vector3 origin = transform.position + Vector3.up;
                Vector3 dir = (target.position + Vector3.up) - origin;
                Projectile.Spawn(origin, dir, 6f, 6f, new Color(0.6f, 0.9f, 0.4f),
                    appliedEffect: appliesSlow ? StatusEffectType.Slow : StatusEffectType.None,
                    effectDuration: 1.5f, effectMagnitude: 0.4f);
            }
        }
    }
}
