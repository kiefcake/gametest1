using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DungeonCrawler.Inventory
{
    // Minimal drag source for one inventory grid slot's icon. This project has no other
    // drag-and-drop (every other item interaction is click-based -- see InventoryUI's own
    // "Left-click to use/equip -- Right-click to drop" hint); this exists specifically so
    // a potion can be dragged onto its matching PotionBeltSlotUI. Lives on the icon child
    // rather than the slot root so it can't interfere with the slot root's existing
    // Button (left-click use/equip) and RightClickHandler (right-click drop).
    public class DraggableInventoryIcon : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public int slotIndex;
        public InventorySystem inventory;

        // One shared ghost-icon object reused across every drag rather than
        // Instantiate/Destroy per drag -- there's only ever one drag in flight at a time.
        private static GameObject ghost;
        private static Image ghostImage;
        private static RectTransform ghostRect;

        public void OnBeginDrag(PointerEventData eventData)
        {
            var item = inventory != null ? inventory.GetAt(slotIndex) : null;
            if (item == null) { eventData.pointerDrag = null; return; }

            var rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas == null) { eventData.pointerDrag = null; return; }

            if (ghost == null)
            {
                ghost = new GameObject("DragGhost", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                ghostImage = ghost.GetComponent<Image>();
                ghostRect = ghost.GetComponent<RectTransform>();
                ghostRect.sizeDelta = new Vector2(48, 48);
                ghost.GetComponent<CanvasGroup>().blocksRaycasts = false; // never itself the drop target
            }
            ghost.transform.SetParent(rootCanvas.transform, false);
            ghost.transform.SetAsLastSibling();
            ghostImage.sprite = item.icon;
            ghostImage.color = item.icon != null ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            ghost.SetActive(true);
            ghostRect.position = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (ghostRect != null && ghost.activeSelf) ghostRect.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (ghost != null) ghost.SetActive(false);
        }
    }
}
