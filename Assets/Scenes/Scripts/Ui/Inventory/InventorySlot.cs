using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class InventorySlot : MonoBehaviour,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [Header("UI")]
    public Image iconComponent;
    public Sprite emptySlotSprite;
    public TextMeshProUGUI amountText;

    [Header("Data info slot")]
    public ItemData item;
    public int amount;
    public int slotIndex = -1;

    private float lastClickTime;
    private const float DOUBLE_CLICK_SPEED = 0.3f;

    public void AddItem(ItemData newItem, int count)
    {
        item = newItem;
        amount = count;

        if (iconComponent != null)
        {
            iconComponent.sprite = item.icon;
            iconComponent.enabled = true;
            Color c = iconComponent.color;
            c.a = item.icon != null ? 1f : 0f;
            iconComponent.color = c;
        }

        if (amountText != null)
        {
            if (amount > 1 || item.isStackable)
            {
                amountText.text = amount.ToString();
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
        item = null;
        amount = 0;

        if (iconComponent != null)
        {
            iconComponent.sprite = emptySlotSprite;
            iconComponent.enabled = true;
            Color c = iconComponent.color;
            c.a = emptySlotSprite != null ? 1f : 0f;
            iconComponent.color = c;
        }

        if (amountText != null) amountText.enabled = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (item != null && ItemTooltipUI.Instance != null)
        {
            ItemTooltipUI.Instance.Show(item);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (ItemTooltipUI.Instance != null)
        {
            ItemTooltipUI.Instance.Hide();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (slotIndex < 0) return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            float timeSinceLastClick = Time.time - lastClickTime;
            bool isDoubleClick = timeSinceLastClick <= DOUBLE_CLICK_SPEED;

            if (isDoubleClick && item != null && (InventoryDragController.Instance == null || !InventoryDragController.Instance.IsDragging))
            {
                TryUseItem();
            }
            else if (InventoryDragController.Instance != null)
            {
                InventoryDragController.Instance.HandleLeftClickOnSlot(slotIndex);
            }

            if (ItemTooltipUI.Instance != null)
                ItemTooltipUI.Instance.Hide();

            lastClickTime = Time.time;
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (InventoryDragController.Instance != null && InventoryDragController.Instance.IsDragging)
            {
                InventoryDragController.Instance.HandleRightClickOnSlot(slotIndex);
            }
            else if (item != null && ContextMenuController.Instance != null)
            {
                ContextMenuController.Instance.SelectSlot(this);
            }
        }
    }

    private void TryUseItem()
    {
        if (item == null) return;

        if (item.itemType == ItemType.Food && item.isEatable)
        {
            PlayerStats stats = FindFirstObjectByType<PlayerStats>();
            if (stats != null)
            {
                stats.Eat(item.FeedAmount);
                if (InventoryManager.instance != null)
                    InventoryManager.instance.RemoveFromSlot(slotIndex, 1);

                if (item == null && ItemTooltipUI.Instance != null)
                    ItemTooltipUI.Instance.Hide();
            }
        }
        else if (item.itemType == ItemType.Tool || item.itemType == ItemType.Weapon)
        {
            ToolController toolController = FindFirstObjectByType<ToolController>();
            if (toolController != null)
                toolController.Equip(item);
        }
    }
}