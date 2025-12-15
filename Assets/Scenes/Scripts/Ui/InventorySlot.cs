using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class InventorySlot : MonoBehaviour, IPointerClickHandler
{
    public Image iconComponent;
    public TextMeshProUGUI amountText;
    
    public ItemData item;
    public int amount;
    
    public bool isShopSlot = false;
    public int slotIndex = -1; 

    private float lastClickTime;
    private const float DOUBLE_CLICK_SPEED = 0.3f;

    public void AddItem(ItemData newItem, int count)
    {
        item = newItem;
        amount = count;

        iconComponent.sprite = item.icon;
        iconComponent.enabled = true;

        if (amount > 1 || (item.isStackable && !isShopSlot))
        {
            amountText.text = amount.ToString();
            amountText.enabled = true;
        }
        else
        {
            amountText.enabled = false;
        }
    }

    public void ClearSlot()
    {
        item = null;
        amount = 0;
        // slotIndex не скидаємо, бо він прив'язаний до порядку UI
        iconComponent.sprite = null;
        iconComponent.enabled = false;
        if (amountText != null) amountText.enabled = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            float timeSinceLastClick = Time.time - lastClickTime;
            bool isDoubleClick = timeSinceLastClick <= DOUBLE_CLICK_SPEED;

            if (isShopSlot)
            {
                if (ShopManager.instance != null && slotIndex != -1)
                {
                    if (isDoubleClick)
                        ShopManager.instance.TryBuyItem(slotIndex);
                    else
                        ShopManager.instance.SelectShopItem(slotIndex);
                }
            }
            else 
            {
                // 1. Якщо магазин відкритий -> ПРОДАЄМО
                if (ShopManager.instance != null && ShopManager.instance.shopUI.activeSelf)
                {
                    if (item != null) ContextMenuController.instance.OpenMenu(item, this);
                    if (isDoubleClick) ShopManager.instance.TrySellItemFromInventory(item);
                }
                else 
                {
                    ContextMenuController.instance.OpenMenu(item, this);
                    if (isDoubleClick) TryUseItem();
                }
            }

            lastClickTime = Time.time;
        }
    }

    private void TryUseItem()
    {
        if (item == null) return;
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player == null) return;

        if (item.itemType == ItemType.Food && item.isEatable)
        {
            player.Eat(item.FeedAmount);
            amount--;
            if (amount <= 0) {
                ClearSlot();
                if (ContextMenuController.instance != null) ContextMenuController.instance.ClearSelection();
            } else {
                AddItem(item, amount);
            }
        }
        else if (item.itemType == ItemType.Tool)
        {
            player.EquipItem(item);
        }
    }
}