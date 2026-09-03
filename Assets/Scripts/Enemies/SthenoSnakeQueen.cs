using System.Collections.Generic;
using UnityEngine;
using DungeonCrawler.Classes;
using DungeonCrawler.Core;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Enemies
{
    // The Snake Pit's boss, recreated from the RealmEye wiki page for "Stheno the Snake
    // Queen (Original)" -- HP/DEF, the exact 3-phase attack cycle (Blades -> Swarm ->
    // Spiral -> back to Blades), the permanent circling Stheno Pets that respawn
    // immediately on death, the Phase-2 Stheno Swarms that cluster around her and despawn
    // on a timer, the frequent invulnerability windows that run independently of phase, and
    // the "stops firing entirely if her target leaves the middle of the room" gate.
    // Damage/HP numbers are scaled down from the source (100/60/100 dmg, 9000 HP) to this
    // project's own balance range (other bosses hit for ~16-45, player classes sit around
    // 100-130 HP) rather than copied as raw numbers, which would one-shot every class here.
    //
    // Never approaches or melees -- she's "found in the middle of the boss room" and stays
    // there, so this fully overrides Update() instead of reusing EnemyBase's move-or-attack
    // loop, the same reason AbyssMage/RangedImp do.
    public class SthenoSnakeQueen : EnemyBase
    {
        private enum Phase { Blades, Swarm, Spiral }
        private Phase phase = Phase.Blades;
        private float phaseTimer; // only meaningful during Spiral -- Blades advances on volley count, Swarm on swarmDespawnTimer
        private int phaseVolleys;
        private float attackTimer;

        // "If her target backs out of the center of the room, she will stop firing" --
        // arenaCenter is her own spawn point (she never moves), and this radius is tuned to
        // roughly the boss room's own footprint (see DungeonLayout.roomWidth/roomDepth).
        private const float RoomCenterGateRadius = 13f;
        private Vector3 arenaCenter;

        private const int PetCount = 2;
        private readonly List<SthenoMinion> pets = new List<SthenoMinion>();
        private float petRespawnTimer;

        private readonly List<SthenoMinion> swarms = new List<SthenoMinion>();
        private float swarmDespawnTimer;

        // "Throughout all her phases she has frequent invulnerability periods" -- this
        // toggles on its own cycle independent of which phase is active, rather than being
        // tied to a specific attack's telegraph like every other boss in this game.
        private float invulnTimer;
        private bool inInvulnWindow;

        private float spiralAngle;
        private float bombTimer;

        protected override void Awake()
        {
            enemyName = "Stheno the Snake Queen";
            healthBarHeight = 3.2f;
            healthBarWidth = 2.4f;

            base.Awake();

            // Matches the other three dungeon bosses' HP scale (FrostLich/SwampWarden sit
            // at 1100-1150, AbyssFinalDemon at 1200) rather than a naive proportional
            // scale-down of the source wiki's 9,000 HP -- damage numbers below are scaled
            // down from the source (100/60/100) since this game's classes sit around
            // 100-130 HP and would otherwise be one-shot, but HP itself follows this
            // project's own already-established boss-tier convention instead. No defense
            // value either, matching every other boss here -- they rely entirely on a big
            // HP pool, not damage mitigation.
            health.maxHP = 1150f;
            health.SetCurrentHP(health.maxHP);
            attackDamage = 0f; // never melees -- Attack() is never called

            arenaCenter = transform.position;
            phaseTimer = 3f;
        }

        // A boss-scale Serpent -- same archetype as the trash Pit Snakes/Dart Throwers,
        // just much bigger and in a regal purple/gold instead of their earthy tones, so she
        // reads as "queen of the snakes" rather than just a large trash mob.
        protected override void AttachVisual()
        {
            var built = ProceduralMonster.Serpent(transform, new ProceduralMonster.SerpentSpec
            {
                bodyColor = new Color(0.45f, 0.15f, 0.5f),
                accentColor = new Color(0.95f, 0.85f, 0.2f),
                scale = 2.2f, length = 9f
            });
            visualRenderers = built.renderers;
            spriteAnimator = built.root.gameObject.AddComponent<SpriteAnimator>();
            spriteAnimator.bobHeight = 0.05f;
            spriteAnimator.bobSpeed = 1.2f;
        }

        protected override void Update()
        {
            if (health.IsDowned) return;

            EnsurePets();
            TickInvuln();

            if (target == null) return;

            // Swarms despawn on their own timer regardless of the center-of-room gate below
            // -- they're a phase-scoped effect, not part of "stopped firing."
            if (phase == Phase.Swarm)
            {
                swarmDespawnTimer -= Time.deltaTime;
                if (swarmDespawnTimer <= 0f)
                {
                    DespawnSwarms();
                    AdvancePhase();
                }
            }

            bool targetInRange = Vector3.Distance(target.position, arenaCenter) <= RoomCenterGateRadius;
            if (!targetInRange) return;

            attackTimer -= Time.deltaTime;

            switch (phase)
            {
                case Phase.Blades: TickBladePhase(); break;
                case Phase.Swarm: TickSwarmPhase(); break;
                case Phase.Spiral: TickSpiralPhase(); break;
            }
        }

        // Fires pairs of Blades in 4 directions, one pair aimed at the nearest player, for
        // several volleys before switching to Phase 2 -- exactly the source material's
        // Phase 1 description.
        private void TickBladePhase()
        {
            const float volleyInterval = 0.55f;
            const int volleysBeforeSwitch = 6;
            if (attackTimer > 0f) return;
            attackTimer = volleyInterval;

            float baseAngle = AngleToTarget();
            for (int i = 0; i < 4; i++)
                FireBladePair(baseAngle + i * 90f);

            phaseVolleys++;
            if (phaseVolleys >= volleysBeforeSwitch) AdvancePhase();
        }

        private void FireBladePair(float angleDeg)
        {
            Vector3 origin = transform.position + Vector3.up;
            foreach (float offset in new[] { -4f, 4f })
            {
                Vector3 dir = Quaternion.Euler(0, angleDeg + offset, 0) * Vector3.forward;
                Projectile.Spawn(origin, dir, 8f, 26f, new Color(0.85f, 0.75f, 0.9f));
            }
        }

        // Swarms already cluster around her firing their own shots (see SthenoMinion) --
        // she herself just fires single Blades in 4 directions per the source material,
        // rather than the paired volley of Phase 1.
        private void TickSwarmPhase()
        {
            const float volleyInterval = 0.8f;
            if (attackTimer > 0f) return;
            attackTimer = volleyInterval;

            float baseAngle = AngleToTarget();
            Vector3 origin = transform.position + Vector3.up;
            for (int i = 0; i < 4; i++)
            {
                Vector3 dir = Quaternion.Euler(0, baseAngle + i * 90f, 0) * Vector3.forward;
                Projectile.Spawn(origin, dir, 8f, 26f, new Color(0.85f, 0.75f, 0.9f));
            }
        }

        // A 6-directional spiral of Blinding White Bullets that rotates in one direction,
        // plus a bomb roughly every second -- both straight from the source material's
        // Phase 3 description.
        private void TickSpiralPhase()
        {
            const float volleyInterval = 0.35f;
            const float rotateSpeed = 25f; // degrees/sec the whole 6-spoke pattern rotates

            spiralAngle += rotateSpeed * Time.deltaTime;

            if (attackTimer <= 0f)
            {
                attackTimer = volleyInterval;
                Vector3 origin = transform.position + Vector3.up;
                for (int i = 0; i < 6; i++)
                {
                    Vector3 dir = Quaternion.Euler(0, spiralAngle + i * 60f, 0) * Vector3.forward;
                    Projectile.Spawn(origin, dir, 7f, 16f, new Color(0.95f, 0.95f, 1f),
                        appliedEffect: StatusEffectType.Blind, effectDuration: 1.4f, effectMagnitude: 1f);
                }
            }

            bombTimer -= Time.deltaTime;
            if (bombTimer <= 0f)
            {
                bombTimer = 1f;
                if (target != null) BombDetonator.Spawn(target.position);
            }

            phaseTimer -= Time.deltaTime;
            if (phaseTimer <= 0f) AdvancePhase();
        }

        private float AngleToTarget()
        {
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            return Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
        }

        private void AdvancePhase()
        {
            phaseVolleys = 0;
            attackTimer = 0f;
            switch (phase)
            {
                case Phase.Blades:
                    phase = Phase.Swarm;
                    SpawnSwarms();
                    swarmDespawnTimer = 8f;
                    break;
                case Phase.Swarm:
                    phase = Phase.Spiral;
                    phaseTimer = 7f;
                    spiralAngle = 0f;
                    bombTimer = 1f;
                    break;
                case Phase.Spiral:
                    phase = Phase.Blades;
                    break;
            }
        }

        // Permanent adds, present from the moment she's spawned -- respawns whichever one
        // is missing shortly after it dies rather than waiting for all of them to die
        // first, matching "any pets that are killed will be respawned immediately."
        private void EnsurePets()
        {
            pets.RemoveAll(p => p == null);
            if (pets.Count >= PetCount) return;

            petRespawnTimer -= Time.deltaTime;
            if (petRespawnTimer <= 0f)
            {
                petRespawnTimer = 0.4f;
                pets.Add(SpawnMinion(isSwarm: false));
            }
        }

        private void SpawnSwarms()
        {
            const int swarmCount = 5;
            for (int i = 0; i < swarmCount; i++)
                swarms.Add(SpawnMinion(isSwarm: true));
        }

        private void DespawnSwarms()
        {
            foreach (var s in swarms)
                if (s != null) Destroy(s.gameObject);
            swarms.Clear();
        }

        private SthenoMinion SpawnMinion(bool isSwarm)
        {
            var go = new GameObject(isSwarm ? "SthenoSwarm" : "SthenoPet");
            go.transform.SetParent(transform.parent); // same dungeonRoot as everything else, not a child of Stheno herself
            go.transform.position = transform.position + new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f));
            go.AddComponent<Health>();
            go.AddComponent<StatusEffectController>();
            var minion = go.AddComponent<SthenoMinion>();
            minion.orbitCenter = transform;
            minion.orbitRadius = isSwarm ? 2.5f : 4f;
            minion.orbitSpeed = isSwarm ? 140f : 90f;
            minion.appliesSlow = !isSwarm;
            go.AddComponent<AggroController>();

            var health = go.GetComponent<Health>();
            health.maxHP = isSwarm ? 20f : 45f;
            health.SetCurrentHP(health.maxHP);
            if (!isSwarm)
            {
                var petRef = minion;
                health.OnDeath += () => pets.Remove(petRef);
            }
            return minion;
        }

        private void TickInvuln()
        {
            invulnTimer -= Time.deltaTime;
            if (invulnTimer > 0f) return;

            if (inInvulnWindow)
            {
                SetInvulnerable(false);
                inInvulnWindow = false;
                invulnTimer = Random.Range(3f, 5f); // vulnerable window between invuln pulses
            }
            else
            {
                SetInvulnerable(true);
                inInvulnWindow = true;
                invulnTimer = Random.Range(0.8f, 1.4f); // brief invuln pulse
            }
        }

        protected override void HandleDeath()
        {
            DespawnSwarms();
            foreach (var p in pets) if (p != null) Destroy(p.gameObject);
            pets.Clear();
            base.HandleDeath();
        }

        // A single timed bomb -- grows a brief warning telegraph, then deals a burst of AoE
        // damage and destroys itself. Self-contained (own MonoBehaviour) rather than Stheno
        // tracking a list of pending bomb timers herself, since Phase 3 throws one roughly
        // every second and several can be live in flight at once.
        private class BombDetonator : MonoBehaviour
        {
            private const float FuseTime = 0.6f;
            private const float Radius = 2.5f;
            private const float Damage = 32f;
            private static readonly Color StartColor = new Color(0.9f, 0.85f, 0.2f);
            private static readonly Color EndColor = new Color(1f, 0.3f, 0.1f);
            private float age;

            public static void Spawn(Vector3 pos)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "SthenoBomb";
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                go.transform.position = pos + Vector3.up * 0.05f;
                go.transform.localScale = new Vector3(Radius * 2f, 0.02f, Radius * 2f);
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null) renderer.material = new Material(Shader.Find("Standard")) { color = StartColor };
                go.AddComponent<BombDetonator>();
            }

            private void Update()
            {
                age += Time.deltaTime;
                float t = Mathf.Clamp01(age / FuseTime);
                var renderer = GetComponent<Renderer>();
                if (renderer != null) renderer.material.color = Color.Lerp(StartColor, EndColor, t);

                if (age >= FuseTime)
                {
                    var hits = Physics.OverlapSphere(transform.position, Radius);
                    foreach (var hit in hits)
                    {
                        var player = hit.GetComponentInParent<PlayerCharacter>();
                        if (player != null && player.health != null) player.health.TakeDamage(Damage, ignoreDef: true);
                    }
                    ImpactBurst.Spawn(transform.position + Vector3.up * 0.3f, new Color(1f, 0.5f, 0.1f));
                    Destroy(gameObject);
                }
            }
        }
    }
}
