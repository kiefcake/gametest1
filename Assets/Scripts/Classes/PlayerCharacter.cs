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

        // No player-body art exists yet (Sprites/ only has weapon/potion icons), so this
        // stands the class up as a role-colored capsule plus a billboarded weapon icon --
        // enough to tell classes apart at a glance during testing. Swap for real sprites
        // by replacing this method once player art exists; nothing outside it depends on
        // the capsule specifically.
        private void BuildVisual(ClassDefinition def)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Visual";
            body.layer = LocalVisualLayer;
            body.transform.SetParent(transform);
            body.transform.localPosition = new Vector3(0, 1f, 0);
            Destroy(body.GetComponent<Collider>()); // the root's CapsuleCollider (added above) is the real one, used for AoE detection

            var renderer = body.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = new Material(Shader.Find("Standard")) { color = ClassDefinition.RoleColor(def.role) };

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
