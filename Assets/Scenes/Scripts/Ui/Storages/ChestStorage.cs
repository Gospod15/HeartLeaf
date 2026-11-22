using System;
using UnityEngine;

public class ChestStorage : MonoBehaviour, IInteractable
{
    [Header("Chest Settings")]
    [SerializeField] private string chestId;
    [SerializeField] private string chestTitle = "Chest";
    [SerializeField] private int capacity = 12;
    [SerializeField] private ChestLootTable lootTable;
    [SerializeField] private bool generateLootOnStart = true;

    [Header("Auto Close Distance")]
    [SerializeField] private float maxOpenDistance = 3.0f;

    [SerializeField] private InventorySlotData[] slots;
    private bool isLootGenerated = false;
    private Transform currentInteractor;

    public string ChestId
    {
        get
        {
            if (string.IsNullOrEmpty(chestId))
            {
                Vector3 pos = transform.position;
                chestId = $"chest_{Mathf.RoundToInt(pos.x * 100f)}_{Mathf.RoundToInt(pos.y * 100f)}";
            }
            return chestId;
        }
    }

    public string ChestTitle => chestTitle;
    public InventorySlotData[] Slots => slots;
    public int Capacity => capacity;
    public bool IsLootGenerated => isLootGenerated;

    public string InteractionPrompt => $"Open {chestTitle}";

    public event Action OnChestChanged;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void Start()
    {
        if (WorldSaveManager.instance != null)
        {
            WorldSaveManager.instance.RegisterChest(this);
            if (WorldSaveManager.instance.TryRestoreChest(this))
                return;
        }

        if (generateLootOnStart && !isLootGenerated)
        {
            GenerateLoot();
        }
    }

    private void OnDestroy()
    {
        if (WorldSaveManager.instance != null)
        {
            WorldSaveManager.instance.UnregisterChest(this);
        }
    }

    private void Update()
    {
        if (ChestUI.Instance != null && ChestUI.Instance.CurrentChest == this && currentInteractor != null)
        {
            if (Vector2.Distance(transform.position, currentInteractor.position) > maxOpenDistance)
            {
                ChestUI.Instance.Close();
                if (UIManager.Instance != null)
                    UIManager.Instance.SetInventoryState(false);
                currentInteractor = null;
            }
        }
    }

    public bool CanInteract(GameObject interactor)
    {
        return interactor != null;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor)) return;

        if (ChestUI.Instance != null && ChestUI.Instance.IsOpen)
        {
            ChestUI.Instance.Close();
            if (UIManager.Instance != null)
                UIManager.Instance.SetInventoryState(false);
            currentInteractor = null;
            return;
        }

        currentInteractor = interactor.transform;
        OpenChest();
    }

    private void EnsureInitialized()
    {
        if (capacity < 1) capacity = 1;
        if (slots == null || slots.Length != capacity)
        {
            slots = new InventorySlotData[capacity];
            for (int i = 0; i < capacity; i++)
                slots[i] = new InventorySlotData();
        }
    }

    public void GenerateLoot()
    {
        EnsureInitialized();
        if (lootTable != null)
        {
            lootTable.PopulateSlots(slots);
        }
        isLootGenerated = true;
        NotifyChanged();
    }

    public void OpenChest()
    {
        EnsureInitialized();
        if (!isLootGenerated)
            GenerateLoot();

        if (ChestUI.Instance != null)
            ChestUI.Instance.Open(this);
    }

    public void SetSlotDirect(int index, ItemData item, int amount, int durability = -1)
    {
        if (index < 0 || index >= slots.Length) return;

        if (item == null || amount <= 0)
            slots[index].Clear();
        else
            slots[index].Set(item, amount, durability);

        NotifyChanged();
    }

    public void RestoreSavedState(ChestSaveData saveData, Func<string, ItemData> itemResolver)
    {
        if (saveData == null) return;

        capacity = Mathf.Max(1, saveData.capacity);
        EnsureInitialized();

        for (int i = 0; i < slots.Length; i++)
            slots[i].Clear();

        if (saveData.slots != null && itemResolver != null)
        {
            foreach (var savedSlot in saveData.slots)
            {
                if (savedSlot.slotIndex < 0 || savedSlot.slotIndex >= slots.Length) continue;
                ItemData item = itemResolver(savedSlot.itemId);
                if (item != null && savedSlot.amount > 0)
                {
                    slots[savedSlot.slotIndex].Set(item, savedSlot.amount);
                }
            }
        }

        isLootGenerated = saveData.isLootGenerated;
        NotifyChanged();
    }

    public void NotifyChanged()
    {
        if (WorldSaveManager.instance != null)
            WorldSaveManager.instance.RecordChestState(this);

        OnChestChanged?.Invoke();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(chestId) && gameObject.scene.IsValid())
        {
            chestId = Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}