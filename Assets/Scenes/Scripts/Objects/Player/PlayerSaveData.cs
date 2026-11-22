using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InventorySlotSaveData
{
    public int slotIndex;
    public string itemId;
    public int amount;
    public int durability;
}

[Serializable]
public class PlayerSaveData
{
    public bool hasData;

    public float posX;
    public float posY;

    public float currentHealth;
    public float currentFeed;
    public float currentStamina;

    public List<InventorySlotSaveData> mainInventory = new List<InventorySlotSaveData>();
    public List<InventorySlotSaveData> armoryInventory = new List<InventorySlotSaveData>();
}

public interface ISaveablePlayer
{
    PlayerSaveData CaptureState();
    void RestoreState(PlayerSaveData data, Func<string, ItemData> itemResolver);
}

public interface ISaveableInventory
{
    void CaptureInventory(List<InventorySlotSaveData> mainSlots, List<InventorySlotSaveData> armorySlots);
    void RestoreInventory(List<InventorySlotSaveData> mainSlots, List<InventorySlotSaveData> armorySlots, Func<string, ItemData> itemResolver);
}