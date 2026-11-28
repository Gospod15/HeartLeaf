using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private Transform slotsParent;
    [SerializeField] private int startIndex = 0;
    private InventorySlot[] uiSlots;

    private void Awake()
    {
        if (slotsParent == null) slotsParent = transform;
        uiSlots = slotsParent.GetComponentsInChildren<InventorySlot>(true);
    }

    private void Start()
    {
        if (InventoryManager.instance != null)
        {
            InventoryManager.instance.OnInventoryChanged -= RefreshUI;
            InventoryManager.instance.OnInventoryChanged += RefreshUI;
            RefreshUI();
        }
    }

    private void OnEnable()
    {
        if (InventoryManager.instance != null)
        {
            InventoryManager.instance.OnInventoryChanged -= RefreshUI;
            InventoryManager.instance.OnInventoryChanged += RefreshUI;
            RefreshUI();
        }
    }

    private void OnDisable()
    {
        if (InventoryManager.instance != null)
        {
            InventoryManager.instance.OnInventoryChanged -= RefreshUI;
        }
    }

    public void RefreshUI()
    {
        if (InventoryManager.instance == null || uiSlots == null) return;

        var dataSlots = InventoryManager.instance.Slots;
        for (int i = 0; i < uiSlots.Length; i++)
        {
            int realIndex = startIndex + i;
            uiSlots[i].slotIndex = realIndex;

            if (realIndex < dataSlots.Count && !dataSlots[realIndex].IsEmpty)
            {
                uiSlots[i].AddItem(dataSlots[realIndex].item, dataSlots[realIndex].amount);
            }
            else
            {
                uiSlots[i].ClearSlot();
            }
        }
    }
}