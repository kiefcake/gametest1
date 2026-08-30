using UnityEngine;
using UnityEngine.EventSystems;

namespace DungeonCrawler.UI
{
    // Attach to any UI element (inventory slot, equipment slot) to show the shared
    // tooltip on hover. `content` is set/updated externally (InventoryUI.Redraw) since
    // the item behind a slot changes as the inventory changes.
    public class HoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string content;
        private bool hovering;

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovering = true;
            if (!string.IsNullOrEmpty(content)) TooltipUI.Show(content, eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovering = false;
            TooltipUI.Hide();
        }

        // Redraw() can change `content` while the mouse never left the slot (e.g. picking
        // something up while hovering the now-filled slot) -- re-show so it doesn't stay
        // stuck on stale/empty text until the next enter/exit.
        public void RefreshIfHovering()
        {
            if (!hovering) return;
            if (string.IsNullOrEmpty(content)) TooltipUI.Hide();
            else TooltipUI.Show(content, Input.mousePosition);
        }
    }
}
