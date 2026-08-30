using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DungeonCrawler.UI
{
    // Unity's Button component only ever fires onClick for the left mouse button (it
    // checks eventData.button internally and no-ops otherwise) -- this sits alongside one
    // to also react to a right-click, without the two interfering. Used by InventoryUI's
    // grid slots so right-click can drop an item instead of using/equipping it.
    public class RightClickHandler : MonoBehaviour, IPointerClickHandler
    {
        public Action onRightClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                onRightClick?.Invoke();
        }
    }
}
