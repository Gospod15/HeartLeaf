using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LootEntry
{
    public ItemData item;
    [Range(0f, 100f)] public float dropChance = 50f;
    [Min(1)] public int minAmount = 1;
    [Min(1)] public int maxAmount = 3;
}

[CreateAssetMenu(fileName = "New Chest Loot Table", menuName = "Inventory/Chest Loot Table")]
public class ChestLootTable : ScriptableObject
{
    [Header("Loot Generation Settings")]
    [Tooltip("Maximum unique items per chest, 0 means unlimited")]
    [Min(0)] public int maxUniqueDrops = 4;
    public bool shuffleSlots = true;
    public List<LootEntry> entries = new List<LootEntry>();

    public void PopulateSlots(InventorySlotData[] targetSlots)
    {
        if (targetSlots == null || targetSlots.Length == 0) return;

        for (int i = 0; i < targetSlots.Length; i++)
        {
            if (targetSlots[i] == null) targetSlots[i] = new InventorySlotData();
            else targetSlots[i].Clear();
        }

        List<LootEntry> pool = new List<LootEntry>(entries);
        if (shuffleSlots)
        {
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int rnd = UnityEngine.Random.Range(0, i + 1);
                (pool[i], pool[rnd]) = (pool[rnd], pool[i]);
            }
        }

        List<int> freeIndices = new List<int>(targetSlots.Length);
        for (int i = 0; i < targetSlots.Length; i++) freeIndices.Add(i);

        int generatedCount = 0;

        foreach (var entry in pool)
        {
            if (entry == null || entry.item == null || freeIndices.Count == 0) continue;
            if (maxUniqueDrops > 0 && generatedCount >= maxUniqueDrops) break;

            if (UnityEngine.Random.Range(0f, 100f) <= entry.dropChance)
            {
                int min = Mathf.Max(1, entry.minAmount);
                int max = Mathf.Max(min, entry.maxAmount);
                int amount = entry.item.isStackable
                    ? Mathf.Min(UnityEngine.Random.Range(min, max + 1), entry.item.maxStackSize)
                    : 1;

                int pickPos = shuffleSlots ? UnityEngine.Random.Range(0, freeIndices.Count) : 0;
                int slotIndex = freeIndices[pickPos];
                freeIndices.RemoveAt(pickPos);

                targetSlots[slotIndex].Set(entry.item, amount);
                generatedCount++;
            }
        }
    }
}