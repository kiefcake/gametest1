using UnityEngine;
using DungeonCrawler.Audio;
using DungeonCrawler.Inventory;
using DungeonCrawler.Visuals;
using DungeonCrawler.World;

namespace DungeonCrawler.Loot
{
    // Attach alongside Health. Subscribes to Health.OnDeath rather than polling
    // CurrentHP every frame -- death is a one-shot event, so an event subscription
    // is both cheaper and clearer than a per-frame check with a hasDropped guard flag.
    [RequireComponent(typeof(Core.Health))]
    public class LootDropper : MonoBehaviour
    {
        public LootTable lootTable;
        public GameObject pickupPrefab; // simple stand-in: a small cube/quad with a WorldPickup component
        public int minGold = 0;
        public int maxGold = 0;
        // A boss dropping loot on the floor for players to scramble over reads worse than a
        // treasure chest -- set true for the boss only (see GameBootstrap.SpawnBoss).
        // Trash mobs (imps) keep the normal floor-scatter below.
        public bool dropAsChest = false;
        private Core.Health health;

        private void Awake()
        {
            health = GetComponent<Core.Health>();
            health.OnDeath += DropLoot;
        }

        private void OnDestroy()
        {
            if (health != null) health.OnDeath -= DropLoot;
        }

        private void DropLoot()
        {
            if (maxGold > 0)
            {
                int amount = UnityEngine.Random.Range(minGold, maxGold + 1);
                var wallet = FindObjectOfType<Core.PlayerWallet>();
                if (wallet != null && amount > 0)
                {
                    wallet.Add(amount);
                    DamageNumber.SpawnGold(transform.position + Vector3.up * 1.6f, amount);
                    SfxLibrary.PlayAt(SfxLibrary.Gold, transform.position, 0.3f);
                }
            }

            if (lootTable == null) return;
            var drops = lootTable.RollDrops();
            if (drops.Count == 0) return;

            if (dropAsChest)
            {
                Chest.Spawn(transform.position + Vector3.forward * 0.6f, drops);
                return;
            }

            foreach (var item in drops)
            {
                Vector3 offset = new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), 0, UnityEngine.Random.Range(-0.5f, 0.5f));
                GameObject pickup;
                if (pickupPrefab != null)
                {
                    pickup = Instantiate(pickupPrefab, transform.position + offset, Quaternion.identity);
                }
                else
                {
                    // No pickup prefab assigned -- stand one up from the item's own icon
                    // (billboarded, like enemy sprites) instead of an unlabeled cube, so
                    // drops are identifiable on sight during testing.
                    pickup = new GameObject("Pickup_" + item.itemName);
                    pickup.transform.position = transform.position + offset;
                    var col = pickup.AddComponent<SphereCollider>();
                    col.isTrigger = true;
                    col.radius = 0.4f;
                    if (item.icon != null)
                        SpriteVisual.Attach(pickup.transform, item.icon, new Vector3(0, 0.5f, 0), scale: 0.4f);
                }
                var wp = pickup.GetComponent<WorldPickup>();
                if (wp == null) wp = pickup.AddComponent<WorldPickup>();
                wp.item = item;
            }
        }
    }

    // Sits on a world-space loot drop -- from a kill, a chest, or a player's own drop (see
    // ItemDropper). Used to grab itself the instant a collider touched it, which is wrong
    // once a drop might be sitting there for someone else to see and choose to take (or
    // leave) rather than have it vanish out from under them the moment they walk past.
    // Same Interactable+E convention vendors/chests/gates already use.
    public class WorldPickup : MonoBehaviour
    {
        public ItemData item;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            var interactable = gameObject.AddComponent<Interactable>();
            interactable.prompt = "Pick up (E)";
            interactable.onInteract = TryPickup;
        }

        private void TryPickup()
        {
            // Solo play -- same FindObjectOfType shortcut PlayerWallet's own lookups
            // already take rather than threading a reference through every spawn path.
            var inv = FindObjectOfType<InventorySystem>();
            if (inv == null || item == null) return;
            if (inv.AddItem(item))
            {
                SfxLibrary.PlayAt(SfxLibrary.Pickup, transform.position, 0.3f);
                Destroy(gameObject);
            }
        }
    }
}
