using System.Collections.Generic;
using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Abilities;
using DungeonCrawler.Inventory;
using DungeonCrawler.Visuals;

namespace DungeonCrawler.Classes
{
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(StatusEffectController))]
    [RequireComponent(typeof(AbilityCaster))]
    [RequireComponent(typeof(Mana))]
    public class PlayerCharacter : MonoBehaviour
    {
        // The first-person camera (see GameBootstrap/FirstPersonLook) sits inside this
        // GameObject's own body -- without excluding it from the camera's view, you'd be
        // staring at the inside of your own capsule mesh. GameBootstrap's culling mask
        // excludes this layer; nothing else needs to know about it.
        public const int LocalVisualLayer = 8;

        public ClassDefinition classDefinition;
        public InventorySystem inventory;

        public StatBlock Stats { get; private set; }
        public Health health;
        public Mana mana;
        public StatusEffectController statusController;
        public AbilityCaster abilityCaster;

        // The floating weapon icon above the player (not the first-person viewmodel --
        // see WeaponViewmodel for that). Exposed so equipping a new weapon can update
        // what it shows without rebuilding the whole visual.
        private SpriteRenderer weaponIconRenderer;

        private bool initialized;

        private void Awake()
        {
            // Only wire component references and one-time setup here. Building the
            // StatBlock/abilities from classDefinition is deferred to Initialize(),
            // since Awake() fires the instant AddComponent<PlayerCharacter>() runs --
            // before a spawner has had a chance to assign classDefinition. Calling
            // Initialize() explicitly once the definition is known (either here, if
            // it was set in the Inspector, or from a spawner like GameBootstrap)
            // avoids re-invoking Awake via SendMessage, which would be fragile and
            // silently re-run unrelated setup (collider check, event wiring, etc).
            health = GetComponent<Health>();
            mana = GetComponent<Mana>();
            statusController = GetComponent<StatusEffectController>();
            abilityCaster = GetComponent<AbilityCaster>();
            statusController.health = health;
            health.statusController = statusController;
            health.isPlayer = true; // gates RunModifiers.DoubleDamageTaken -- enemies share this same Health class

            gameObject.AddComponent<HealthVFX>(); // floating damage/heal numbers + hit flash

            // Movement used to be raw transform translation (see PlayerMovement), which
            // ignores colliders entirely -- walls looked solid but the player could walk
            // straight through them. CharacterController gives real sweep-collision.
            // It's itself a Collider, so it also satisfies the check below -- no separate
            // CapsuleCollider gets added on top of it.
            if (GetComponent<CharacterController>() == null)
            {
                var cc = gameObject.AddComponent<CharacterController>();
                cc.height = 2f;
                cc.radius = 0.4f;
                cc.center = new Vector3(0, 1f, 0);
                // Unity's default (45) was right at the edge of what DungeonLayout's ramps
                // actually measure out to -- a rotation bug briefly made one ramp steeper
                // than that entirely (since fixed), but 45 leaves zero margin for any ramp
                // to be even slightly off. Raised well clear of every ramp's real angle
                // (~35 degrees) so a ramp is never the reason movement gets blocked.
                cc.slopeLimit = 60f;
            }

            // AoE abilities use Physics.OverlapSphere, which needs a collider to detect this object.
            if (GetComponent<Collider>() == null)
            {
                var col = gameObject.AddComponent<CapsuleCollider>();
                col.height = 2f;
                col.radius = 0.4f;
            }

            // Movement is direct transform manipulation (see PlayerMovement), not physics --
            // but WorldPickup.OnTriggerEnter needs at least one Rigidbody in a collider pair
            // to fire at all, trigger or not. Without this, nothing ever detected walking
            // into a dropped potion. Kinematic so it doesn't fall/get pushed by physics.
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            if (classDefinition != null)
            {
                Initialize(classDefinition);
            }
        }

        // Call this once classDefinition is known -- safe to call exactly once.
        // Spawners (GameBootstrap, a real character-select flow, etc.) should set
        // classDefinition and then call this directly rather than relying on Awake
        // to have already run with the right data.
        public void Initialize(ClassDefinition def)
        {
            if (initialized) return;
            initialized = true;

            classDefinition = def;
            Stats = def.BuildStatBlock();

            health.maxHP = Stats.GetValue(StatType.HP);
            health.SetCurrentHP(health.maxHP);
            health.defense = Stats.GetValue(StatType.DEF);

            mana.SetMax(Stats.GetValue(StatType.MP), refill: true);

            abilityCaster.abilities = new List<AbilityData>(def.abilities);
            abilityCaster.Init(health, mana, Stats, statusController);

            BuildVisual(def);
        }

        // Was a bare role-colored capsule (the last object in the game still on that art
        // style) -- every enemy in the game moved to a real 3D silhouette via
        // ProceduralMonster this session, so the player's own body was the one glaring
        // holdout. Reuses the same Humanoid archetype every humanoid enemy already uses
        // (no horns/weapon prop -- the weapon reads through the floating icon below and
        // the first-person viewmodel instead) so a teammate's class is still readable at a
        // glance by silhouette and color, exactly like the old capsule was meant to be.
        private void BuildVisual(ClassDefinition def)
        {
            Color roleColor = ClassDefinition.RoleColor(def.role);
            var built = ProceduralMonster.Humanoid(transform, new ProceduralMonster.HumanoidSpec
            {
                bodyColor = roleColor,
                accentColor = Color.Lerp(roleColor, Color.white, 0.6f),
                scale = 1f, horns = false, weapon = false, hunched = false
            });
            built.root.name = "Visual";
            built.root.localPosition = Vector3.zero;

            // Every part needs the layer set individually -- Unity's culling mask checks
            // each GameObject's own layer, it doesn't cascade down from a parent's.
            foreach (var t in built.root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LocalVisualLayer;

            var animator = built.root.gameObject.AddComponent<SpriteAnimator>();
            animator.bobHeight = 0.05f;
            animator.bobSpeed = 2.5f;

            if (def.weaponSprite != null)
            {
                weaponIconRenderer = SpriteVisual.Attach(transform, def.weaponSprite, new Vector3(0, 2.6f, 0), scale: 0.5f, sortingOrder: 1);
                if (weaponIconRenderer != null) weaponIconRenderer.gameObject.layer = LocalVisualLayer;
            }
        }

        // Called when a weapon is equipped (see InventoryUI) -- swaps what the floating
        // icon shows. The first-person viewmodel is updated separately by whoever calls
        // this (it doesn't live on PlayerCharacter).
        public void SetWeaponIcon(Sprite sprite)
        {
            if (weaponIconRenderer != null) weaponIconRenderer.sprite = sprite;
        }

        // Call after a potion is consumed via InventorySystem.UsePotionAt, so
        // Health/Mana pick up any change to max HP/MP/DEF immediately.
        public void RefreshDerivedStats()
        {
            if (Stats == null) return;
            health.maxHP = Stats.GetValue(StatType.HP);
            health.defense = Stats.GetValue(StatType.DEF);
            mana.SetMax(Stats.GetValue(StatType.MP), refill: false);
        }
    }
}
