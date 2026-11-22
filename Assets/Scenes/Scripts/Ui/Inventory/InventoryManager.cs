using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class InventorySlotData
{
    public ItemData item;
    public int amount;
    public int currentDurability;

    public bool IsEmpty => item == null || amount <= 0;

    public void Set(ItemData newItem, int newAmount, int durability = -1)
    {
        item = newItem;
        amount = newAmount;

        if (newItem is ArmorItemData armor)
            currentDurability = durability >= 0 ? durability : armor.maxDurability;
        else
            currentDurability = 0;
    }

    public void Clear()
    {
        item = null;
        amount = 0;
        currentDurability = 0;
    }
}

public class InventoryManager : MonoBehaviour, ISaveableInventory
{
    public static InventoryManager instance { get; private set; }

    public const int INVENTORY_SLOTS_COUNT = 12;
    public const int CRAFT_START_INDEX = 12;
    public const int CRAFT_SLOTS_COUNT = 15;
    public const int ARMOR_START_INDEX = 27;
    public const int ARMOR_SLOTS_COUNT = 3;

    [Header("Setup inventory")]
    [SerializeField] private int capacity = 30;

    [SerializeField] private List<InventorySlotData> slots = new List<InventorySlotData>();

    public IReadOnlyList<InventorySlotData> Slots => slots;
    public int Capacity => capacity;

    public event Action OnInventoryChanged;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (capacity < ARMOR_START_INDEX + ARMOR_SLOTS_COUNT)
            capacity = ARMOR_START_INDEX + ARMOR_SLOTS_COUNT;

        if (slots.Count < capacity)
        {
            slots.Clear();
            for (int i = 0; i < capacity; i++)
            {
                slots.Add(new InventorySlotData());
            }
        }
    }


    public void CaptureInventory(List<InventorySlotSaveData> mainSlots, List<InventorySlotSaveData> armorySlots)
    {
        EnsureInitialized();
        mainSlots.Clear();
        armorySlots.Clear();

        int mainLimit = Mathf.Min(INVENTORY_SLOTS_COUNT, slots.Count);
        for (int i = 0; i < mainLimit; i++)
        {
            if (!slots[i].IsEmpty)
            {
                mainSlots.Add(CreateSlotSaveData(i, slots[i]));
            }
        }

        int craftEnd = Mathf.Min(CRAFT_START_INDEX + CRAFT_SLOTS_COUNT, slots.Count);
        for (int i = CRAFT_START_INDEX; i < craftEnd; i++)
        {
            if (!slots[i].IsEmpty)
            {
                mainSlots.Add(CreateSlotSaveData(-1, slots[i]));
            }
        }

        // 2. Слоти броні (27..29)
        int armorEnd = Mathf.Min(ARMOR_START_INDEX + ARMOR_SLOTS_COUNT, slots.Count);
        for (int i = ARMOR_START_INDEX; i < armorEnd; i++)
        {
            if (!slots[i].IsEmpty)
            {
                armorySlots.Add(CreateSlotSaveData(i, slots[i]));
            }
        }
    }

    public void RestoreInventory(List<InventorySlotSaveData> mainSlots, List<InventorySlotSaveData> armorySlots, Func<string, ItemData> itemResolver)
    {
        EnsureInitialized();

        foreach (var slot in slots)
        {
            slot.Clear();
        }

        if (itemResolver == null)
        {
            NotifyInventoryChanged();
            return;
        }

        if (mainSlots != null)
        {
            List<InventorySlotSaveData> overflowCraftItems = new List<InventorySlotSaveData>();

            foreach (var savedSlot in mainSlots)
            {
                if (savedSlot == null || string.IsNullOrEmpty(savedSlot.itemId) || savedSlot.amount <= 0)
                    continue;

                ItemData item = itemResolver(savedSlot.itemId);
                if (item == null) continue;

                if (savedSlot.slotIndex >= 0 && savedSlot.slotIndex < INVENTORY_SLOTS_COUNT && slots[savedSlot.slotIndex].IsEmpty)
                {
                    slots[savedSlot.slotIndex].Set(item, savedSlot.amount, savedSlot.durability);
                }
                else
                {
                    overflowCraftItems.Add(savedSlot);
                }
            }

            foreach (var extra in overflowCraftItems)
            {
                ItemData item = itemResolver(extra.itemId);
                if (item == null) continue;

                if (item is ArmorItemData)
                {
                    for (int s = 0; s < INVENTORY_SLOTS_COUNT; s++)
                    {
                        if (slots[s].IsEmpty)
                        {
                            slots[s].Set(item, 1, extra.durability);
                            break;
                        }
                    }
                }
                else
                {
                    AddItemInternal(item, extra.amount);
                }
            }
        }

        if (armorySlots != null)
        {
            foreach (var savedArmor in armorySlots)
            {
                if (savedArmor == null || string.IsNullOrEmpty(savedArmor.itemId) || savedArmor.amount <= 0)
                    continue;

                ItemData item = itemResolver(savedArmor.itemId);
                if (item == null) continue;

                int idx = savedArmor.slotIndex;
                if (idx >= ARMOR_START_INDEX && idx < ARMOR_START_INDEX + ARMOR_SLOTS_COUNT && CanPlaceItemInSlot(idx, item))
                {
                    slots[idx].Set(item, 1, savedArmor.durability);
                }
            }
        }

        NotifyInventoryChanged();
    }

    private InventorySlotSaveData CreateSlotSaveData(int index, InventorySlotData slotData)
    {
        string id = !string.IsNullOrEmpty(slotData.item.name) ? slotData.item.name : slotData.item.itemName;
        return new InventorySlotSaveData
        {
            slotIndex = index,
            itemId = id,
            amount = slotData.amount,
            durability = slotData.currentDurability
        };
    }

    public bool IsArmorSlot(int slotIndex)
    {
        return slotIndex >= ARMOR_START_INDEX && slotIndex < ARMOR_START_INDEX + ARMOR_SLOTS_COUNT;
    }

    public bool CanPlaceItemInSlot(int slotIndex, ItemData item)
    {
        if (!IsValidIndex(slotIndex) || item == null) return false;

        if (IsArmorSlot(slotIndex))
        {
            if (item is not ArmorItemData armor) return false;

            int armorLocalIndex = slotIndex - ARMOR_START_INDEX; // 0 Head, 1 Chest, 2 Legs
            return (int)armor.armorSlotType == armorLocalIndex;
        }

        return true;
    }

    public float GetTotalDefenseCoefficient()
    {
        float total = 0f;
        for (int i = ARMOR_START_INDEX; i < ARMOR_START_INDEX + ARMOR_SLOTS_COUNT && i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty && slots[i].item is ArmorItemData armor && slots[i].currentDurability > 0)
            {
                total += armor.defenseCoefficient;
            }
        }
        return total;
    }

    public float GetTotalDamageReduction()
    {
        float totalReduction = 0f;
        for (int i = ARMOR_START_INDEX; i < ARMOR_START_INDEX + ARMOR_SLOTS_COUNT && i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty && slots[i].item is ArmorItemData armor && slots[i].currentDurability > 0)
            {
                totalReduction += armor.damageReductionPercent;
            }
        }
        return Mathf.Clamp(totalReduction, 0f, 0.9f);
    }

    public void DamageEquippedArmor(int durabilityLoss = 1)
    {
        if (durabilityLoss <= 0) return;
        bool changed = false;

        for (int i = ARMOR_START_INDEX; i < ARMOR_START_INDEX + ARMOR_SLOTS_COUNT && i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty && slots[i].item is ArmorItemData)
            {
                slots[i].currentDurability -= durabilityLoss;
                if (slots[i].currentDurability <= 0)
                {
                    slots[i].Clear();
                }
                changed = true;
            }
        }

        if (changed)
            NotifyInventoryChanged();
    }

    public bool CanAddItem(ItemData item, int amount)
    {
        if (item == null || amount <= 0) return false;

        int remaining = amount;
        int limit = Mathf.Min(INVENTORY_SLOTS_COUNT, slots.Count);

        if (item.isStackable)
        {
            for (int i = 0; i < limit; i++)
            {
                var slot = slots[i];
                if (slot.item == item && slot.amount < item.maxStackSize)
                {
                    remaining -= (item.maxStackSize - slot.amount);
                    if (remaining <= 0) return true;
                }
            }
        }

        for (int i = 0; i < limit; i++)
        {
            if (slots[i].IsEmpty)
            {
                remaining -= item.isStackable ? item.maxStackSize : 1;
                if (remaining <= 0) return true;
            }
        }

        return false;
    }

    public bool AddItem(ItemData item, int amount)
    {
        if (!CanAddItem(item, amount)) return false;

        AddItemInternal(item, amount);
        NotifyInventoryChanged();
        return true;
    }

    private void AddItemInternal(ItemData item, int amount)
    {
        int remaining = amount;
        int limit = Mathf.Min(INVENTORY_SLOTS_COUNT, slots.Count);

        if (item.isStackable)
        {
            for (int i = 0; i < limit; i++)
            {
                var slot = slots[i];
                if (slot.item == item && slot.amount < item.maxStackSize)
                {
                    int spaceInSlot = item.maxStackSize - slot.amount;
                    int amountToAdd = Mathf.Min(spaceInSlot, remaining);

                    slot.amount += amountToAdd;
                    remaining -= amountToAdd;

                    if (remaining <= 0) return;
                }
            }
        }

        for (int i = 0; i < limit; i++)
        {
            var slot = slots[i];
            if (slot.IsEmpty)
            {
                int amountToAdd = item.isStackable ? Mathf.Min(item.maxStackSize, remaining) : 1;
                slot.Set(item, amountToAdd);
                remaining -= amountToAdd;

                if (remaining <= 0) return;
            }
        }
    }

    public void SetSlotDirect(int index, ItemData item, int amount, int durability = -1)
    {
        if (!IsValidIndex(index)) return;

        if (item == null || amount <= 0)
            slots[index].Clear();
        else
            slots[index].Set(item, amount, durability);

        NotifyInventoryChanged();
    }

    public void ReturnCraftingItemsToInventory()
    {
        int craftEnd = Mathf.Min(CRAFT_START_INDEX + CRAFT_SLOTS_COUNT, slots.Count);
        for (int i = CRAFT_START_INDEX; i < craftEnd; i++)
        {
            if (!slots[i].IsEmpty)
            {
                ItemData item = slots[i].item;
                int amount = slots[i].amount;
                int durability = slots[i].currentDurability;
                slots[i].Clear();

                if (item is ArmorItemData)
                {
                    for (int s = 0; s < INVENTORY_SLOTS_COUNT; s++)
                    {
                        if (slots[s].IsEmpty)
                        {
                            slots[s].Set(item, 1, durability);
                            break;
                        }
                    }
                }
                else
                {
                    AddItemInternal(item, amount);
                }
            }
        }
        NotifyInventoryChanged();
    }

    public bool RemoveItem(ItemData itemToRemove, int amountToRemove)
    {
        if (itemToRemove == null || amountToRemove <= 0) return false;
        if (!HasItem(itemToRemove, amountToRemove)) return false;

        int remaining = amountToRemove;
        int limit = Mathf.Min(INVENTORY_SLOTS_COUNT, slots.Count);

        for (int i = limit - 1; i >= 0; i--)
        {
            var slot = slots[i];
            if (slot.item == itemToRemove)
            {
                int toTake = Mathf.Min(slot.amount, remaining);
                slot.amount -= toTake;
                remaining -= toTake;

                if (slot.amount <= 0)
                {
                    slot.Clear();
                }

                if (remaining <= 0) break;
            }
        }

        NotifyInventoryChanged();
        return true;
    }

    public bool RemoveFromSlot(int slotIndex, int amount = 1)
    {
        if (!IsValidIndex(slotIndex) || amount <= 0) return false;

        var slot = slots[slotIndex];
        if (slot.IsEmpty || slot.amount < amount) return false;

        slot.amount -= amount;
        if (slot.amount <= 0)
        {
            slot.Clear();
        }

        NotifyInventoryChanged();
        return true;
    }

    public void SwapSlots(int indexA, int indexB)
    {
        if (!IsValidIndex(indexA) || !IsValidIndex(indexB) || indexA == indexB) return;

        ItemData tempItem = slots[indexA].item;
        int tempAmount = slots[indexA].amount;
        int tempDurability = slots[indexA].currentDurability;

        slots[indexA].Set(slots[indexB].item, slots[indexB].amount, slots[indexB].currentDurability);
        slots[indexB].Set(tempItem, tempAmount, tempDurability);

        NotifyInventoryChanged();
    }

    public int GetItemAmount(ItemData itemToCheck)
    {
        if (itemToCheck == null) return 0;

        int total = 0;
        int limit = Mathf.Min(INVENTORY_SLOTS_COUNT, slots.Count);
        for (int i = 0; i < limit; i++)
        {
            if (slots[i].item == itemToCheck)
            {
                total += slots[i].amount;
            }
        }
        return total;
    }

    public bool HasItem(ItemData item, int requiredAmount = 1)
    {
        return GetItemAmount(item) >= requiredAmount;
    }

    public void ClearAll()
    {
        foreach (var slot in slots)
        {
            slot.Clear();
        }
        NotifyInventoryChanged();
    }

    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < slots.Count;
    }

    private void NotifyInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }
}