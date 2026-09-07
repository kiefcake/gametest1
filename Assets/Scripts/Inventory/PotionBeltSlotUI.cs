using UnityEngine;
using UnityEngine.EventSystems;
using DungeonCrawler.Classes;

namespace DungeonCrawler.Inventory
{
    // Drop target for dragging a matching potion out of the general inventory grid --
    // the belt's own role/capacity rules live on PotionBelt itself; this just resolves
    // "what was under the cursor" back into an inventory slot and defers to it. Also
    // doubles as a mouse-only quaff (Z/X is the fast path, but a click should still work).
    public class PotionBeltSlotUI : MonoBehaviour, IDropHandler, IPointerClickHandler
    {
        public PotionBelt belt;
        public PotionBelt.Role role;
        public InventorySystem inventory;
        public PlayerCharacter player;

        public void OnDrop(PointerEventData eventData)
        {
            if (belt == null || inventory == null) return;
            var source = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<DraggableInventoryIcon>() : null;
            if (source == null || source.inventory != inventory) return;

            var item = inventory.GetAt(source.slotIndex);
            if (item == null) return;
            if (belt.TryFill(role, item)) inventory.RemoveAt(source.slotIndex);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (belt == null || player == null) return;
            if (belt.TryQuaff(role, player)) player.RefreshDerivedStats();
        }
    }
}
