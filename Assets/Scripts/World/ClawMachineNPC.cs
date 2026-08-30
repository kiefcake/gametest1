using UnityEngine;
using DungeonCrawler.Inventory;

namespace DungeonCrawler.World
{
    // Pure data holder for the fairground claw machine, same split as VendorNPC: HubLayout
    // builds the cabinet geometry plus an empty-prize-pool component and an Interactable,
    // GameBootstrap fills in the real prize pool once it can resolve ItemData references
    // (see GameBootstrap.WireMinigames) and wires the Interactable to open ClawMachineUI.
    public class ClawMachineNPC : MonoBehaviour
    {
        public ItemData[] prizePool = new ItemData[0];
        public int cost = 15;
    }
}
