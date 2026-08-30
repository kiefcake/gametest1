using UnityEngine;
using DungeonCrawler.Inventory;

namespace DungeonCrawler.World
{
    [System.Serializable]
    public struct ShopStock
    {
        public ItemData item;
        public int price;
    }

    // Pure data holder for a hub shop stall. HubLayout builds the stall's geometry plus an
    // Interactable with an empty stock array; GameBootstrap fills in the real stock (it
    // needs Resources.Load on the loot table to resolve ItemData references -- see
    // GameBootstrap.WireVendors) and wires the Interactable to open ShopUI.
    public class VendorNPC : MonoBehaviour
    {
        public string vendorName;
        public string flavorText;
        public ShopStock[] stock = new ShopStock[0];
    }
}
