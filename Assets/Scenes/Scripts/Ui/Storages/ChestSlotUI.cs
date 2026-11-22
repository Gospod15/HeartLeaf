using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class ChestSlotUI : MonoBehaviour,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [Header("Slot UI Components")]
    public Image iconComponent;
    public Sprite emptySlotSprite;
    public TextMeshProUGUI amountText;

    [HideInInspector] public int slotIndex = -1;
    private ItemData currentItem;

    public void SetData(InventorySlotData data)
    {
        if (data == null || data.IsEmpty)
        {
            ClearSlot();
            return;
        }

        currentItem = data.item;

        if (iconComponent != null)
        {
            iconComponent.sprite = data.item.icon;
            iconComponent.enabled = true;
            Color c = iconComponent.color;
            c.a = data.item.icon != null ? 1f : 0f;
            iconComponent.color = c;
        }

        if (amountText != null)
        {
            if (data.amount > 1 || data.item.isStackable)
            {
                amountText.text = data.amount.ToString();
                amountText.enabled = true;
            }
            else
            {
                amountText.enabled = false;
            }
        }
    }

    public void ClearSlot()
    {
        currentItem = null;

        if (iconComponent != null)
        {
            iconComponent.sprite = emptySlotSprite;
            iconComponent.enabled = true;
            Color c = iconComponent.color;
            c.a = emptySlotSprite != null ? 1f : 0f;
            iconComponent.color = c;
        }

        if (amountText != null)
            amountText.enabled = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentItem != null && ItemTooltipUI.Instance != null)
            ItemTooltipUI.Instance.Show(currentItem);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (ItemTooltipUI.Instance != null)
            ItemTooltipUI.Instance.Hide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (slotIndex < 0 || ChestUI.Instance == null || ChestUI.Instance.CurrentChest == null)
            return;

        if (InventoryDragController.Instance == null) return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            InventoryDragController.Instance.HandleLeftClickOnChestSlot(ChestUI.Instance.CurrentChest, slotIndex);
            if (ItemTooltipUI.Instance != null) ItemTooltipUI.Instance.Hide();
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            InventoryDragController.Instance.HandleRightClickOnChestSlot(ChestUI.Instance.CurrentChest, slotIndex);
        }
    }
}