using UnityEngine;
using System.Collections.Generic;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager instance;
    public Transform slotsParent;
    public InventorySlot[] slots;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        slots = slotsParent.GetComponentsInChildren<InventorySlot>();
    }

    public void RefreshUI()
    {
        if (slots == null) return;

        foreach (InventorySlot slot in slots)
        {
            if (slot.item != null)
            {
                slot.AddItem(slot.item, slot.amount);
            }
            else
            {
                slot.ClearSlot();
            }
        }
    }

    public bool AddItem(ItemData item, int amount)
    {
        if (item.isStackable)
        {
            foreach (InventorySlot slot in slots)
            {
                if (slot.item == item && slot.amount < item.maxStackSize)
                {
                    int spaceInSlot = item.maxStackSize - slot.amount;
                    int amountToAdd = Mathf.Min(spaceInSlot, amount);

                    slot.amount += amountToAdd;
                    slot.AddItem(item, slot.amount); 
                    
                    amount -= amountToAdd;
                    if (amount <= 0) return true;
                }
            }
        }

        if (amount > 0)
        {
            foreach (InventorySlot slot in slots)
            {
                if (slot.item == null)
                {
                    int amountToAdd = Mathf.Min(item.maxStackSize, amount);
                    slot.AddItem(item, amountToAdd);
                    
                    amount -= amountToAdd;
                    if (amount <= 0) return true;
                }
            }
        }

        if (amount > 0)
        {
            return false;
        }
        
        return true;
    }

    public int GetItemAmount(ItemData itemToCheck)
    {
        int total = 0;
        foreach (InventorySlot slot in slots)
        {
            if (slot.item != null && slot.item == itemToCheck)
            {
                total += slot.amount;
            }
        }
        return total;
    }

    public void RemoveItem(ItemData itemToRemove, int amountToRemove)
    {
        foreach (InventorySlot slot in slots)
        {
            if (slot.item == itemToRemove)
            {
                if (slot.amount >= amountToRemove)
                {
                    slot.amount -= amountToRemove;
                    amountToRemove = 0;
                }
                else
                {
                    amountToRemove -= slot.amount;
                    slot.amount = 0;
                }

                if (slot.amount <= 0) slot.ClearSlot();
                else slot.AddItem(slot.item, slot.amount);

                if (amountToRemove <= 0) return;
            }
        }
    }
}