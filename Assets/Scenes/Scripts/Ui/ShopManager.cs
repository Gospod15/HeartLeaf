using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

[System.Serializable]
public class ShopItem
{
    public ItemData item;
    public int amount;
    public int LocalPrice = 0; 
}

public class ShopManager : MonoBehaviour
{
    public static ShopManager instance;

    [Header("Валюта")]
    public ItemData currencyItem;
    public int Multiple;

    [Header("UI Елементи")]
    public GameObject shopUI;
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI itemPriceText; 
    public TextMeshProUGUI feedbackText; 

    [Header("Товари")]
    public Transform slotsParent;
    public List<ShopItem> itemsForSale; 

    private InventorySlot[] shopSlots;

    void Awake() { instance = this; }

    void Start()
    {
        shopSlots = slotsParent.GetComponentsInChildren<InventorySlot>();
        CloseShop();
        SetupShop();
    }

    void SetupShop()
    {
        for (int i = 0; i < shopSlots.Length; i++)
        {
            shopSlots[i].isShopSlot = true;
            shopSlots[i].slotIndex = i;

            if (i < itemsForSale.Count)
            {
                UpdateSlotUI(i);
            }
            else
            {
                shopSlots[i].ClearSlot();
            }
        }
    }

    // --- Оновлений метод додавання товару в магазин ---
    public void AddItemToShop(ItemData itemToAdd, int amountToAdd, int customPrice = -1)
    {
        // 1. Шукаємо існуючий товар
        for (int i = 0; i < itemsForSale.Count; i++)
        {
            if (itemsForSale[i].item == itemToAdd)
            {
                itemsForSale[i].amount += amountToAdd;
                
                // Якщо передали нову ціну - оновлюємо
                if (customPrice > 0) 
                {
                    itemsForSale[i].LocalPrice = customPrice;
                }

                UpdateSlotUI(i);
                return;
            }
        }

        // 2. Якщо товару немає - створюємо новий слот
        if (itemsForSale.Count < shopSlots.Length)
        {
            ShopItem newItem = new ShopItem();
            newItem.item = itemToAdd;
            newItem.amount = amountToAdd;

            // Встановлюємо ціну: або кастомну, або стандартну з ItemData
            if (customPrice > 0)
            {
                newItem.LocalPrice = customPrice;
            }
            else
            {
                newItem.LocalPrice = itemToAdd.Sellprice; // Важливо: переконайтесь, що в ItemData є поле 'price' або 'Sellprice'
            }

            itemsForSale.Add(newItem);
            SetupShop(); // Перебудовуємо вітрину
        }
    }

    int GetPrice(int index)
    {
        if (index < 0 || index >= itemsForSale.Count) return 0;
        ShopItem shopEntry = itemsForSale[index];
        // Якщо LocalPrice > 0, беремо його, інакше стандартну ціну
        return (shopEntry.LocalPrice > 0) ? shopEntry.LocalPrice : shopEntry.item.Sellprice; 
    }

    void UpdateSlotUI(int index)
    {
        if (index < 0 || index >= itemsForSale.Count) return;
        ShopItem shopEntry = itemsForSale[index];
        shopSlots[index].AddItem(shopEntry.item, shopEntry.amount);
    }

    void AddEarningsToShop(int amountToAdd)
    {
        if (currencyItem == null || amountToAdd <= 0) return;
        AddItemToShop(currencyItem, amountToAdd, 1); 
    }

    bool RemoveMoneyFromShop(int amountToRemove)
    {
        if (currencyItem == null) return false;

        for (int i = 0; i < itemsForSale.Count; i++)
        {
            if (itemsForSale[i].item == currencyItem)
            {
                if (itemsForSale[i].amount >= amountToRemove)
                {
                    itemsForSale[i].amount -= amountToRemove;
                    UpdateSlotUI(i);
                    
                    if (itemsForSale[i].amount == 0)
                    {
                        itemsForSale.RemoveAt(i); SetupShop();
                    }
                    return true;
                }
                else
                {
                    return false; 
                }
            }
        }
        return false;
    }

    // --- Купівля гравцем ---
    public void TryBuyItem(int index)
    {
        if (index < 0 || index >= itemsForSale.Count) return;
        if(currencyItem == null) return;

        ShopItem shopEntry = itemsForSale[index];
        int price = GetPrice(index);
        int playerMoney = InventoryManager.instance.GetItemAmount(currencyItem);

        // Перевірка (припускаємо, що у ItemData є CanBuy або просто дозволяємо все)
        if (!shopEntry.item.CanBuy) return; 

        if (playerMoney >= price) 
        {
            bool added = InventoryManager.instance.AddItem(shopEntry.item, 1);

            if (added) 
            {
                InventoryManager.instance.RemoveItem(currencyItem, price);
                
                shopEntry.amount--; 
                
                AddEarningsToShop(price); // Гроші йдуть в касу магазину
                
                if (shopEntry.amount > 0)
                {
                    UpdateSlotUI(index);
                    SelectShopItem(index);
                }
                else
                {
                    shopSlots[index].ClearSlot();
                    ClearInfo();
                    itemsForSale.RemoveAt(index); // Можна розкоментувати, якщо хочете видаляти пусті слоти
                    SetupShop();
                }
            }
        } 
    }

    // --- Продаж гравцем ---
    public void TrySellItemFromInventory(ItemData itemToSell)
    {
        if (itemToSell == null || itemToSell.CanSell == false || itemToSell == currencyItem) return;
        if (!shopUI.activeSelf) return;

        
        if (Multiple == 0) {Multiple = 1;}
        int sellPrice = itemToSell.Sellprice / Multiple;
        if (sellPrice < 0) sellPrice = 0;
        if (sellPrice == 1) sellPrice = 1;

        if (sellPrice > 0)
        {
            int shopMoney = GetShopMoneyAmount();
            if (shopMoney < sellPrice)
            {
                Debug.Log("В магазина немає грошей");
                return;
            }
            
            bool moneyRemoved = RemoveMoneyFromShop(sellPrice);
            if (!moneyRemoved) return;
        }

        InventoryManager.instance.RemoveItem(itemToSell, 1);
        InventoryManager.instance.AddItem(currencyItem, sellPrice);
        
        AddItemToShop(itemToSell, 1); 
    }

    int GetShopMoneyAmount()
    {
        if (currencyItem == null) return 0;
        for (int i = 0; i < itemsForSale.Count; i++)
        {
            if (itemsForSale[i].item == currencyItem)
            {
                return itemsForSale[i].amount;
            }
        }
        return 0;
    }
    public void SelectShopItem(int index)
    {
        if (index < 0 || index >= itemsForSale.Count) return;
        
        ShopItem shopEntry = itemsForSale[index];
        int price = GetPrice(index);

        itemNameText.text = shopEntry.item.itemName;
        itemPriceText.text = $"{price} монет";
    }

    void ClearInfo()
    {
        itemNameText.text = "";
        itemPriceText.text = "";
    }

    public void OpenShop() { shopUI.SetActive(true); }
    public void CloseShop() { shopUI.SetActive(false); ClearInfo();}
}