using UnityEngine;
using TMPro;

public class ChestUI : MonoBehaviour
{
    public static ChestUI Instance { get; private set; }

    [Header("Chest UI Panel")]
    [SerializeField] private GameObject chestPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Transform slotsParent;

    private ChestSlotUI[] uiSlots;
    private ChestStorage currentChest;

    public ChestStorage CurrentChest => currentChest;
    public bool IsOpen => chestPanel != null && chestPanel.activeSelf;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (slotsParent == null && chestPanel != null)
            slotsParent = chestPanel.transform;

        if (slotsParent != null)
            uiSlots = slotsParent.GetComponentsInChildren<ChestSlotUI>(true);

        if (chestPanel != null)
            chestPanel.SetActive(false);
    }

    public void Open(ChestStorage chest)
    {
        if (chest == null) return;

        if (currentChest != null)
            currentChest.OnChestChanged -= RefreshUI;

        currentChest = chest;
        currentChest.OnChestChanged += RefreshUI;

        if (titleText != null)
            titleText.text = chest.ChestTitle;

        if (chestPanel != null)
            chestPanel.SetActive(true);

        if (UIManager.Instance != null)
            UIManager.Instance.OpenChestInventory();

        RefreshUI();
    }

    public void Close()
    {
        if (currentChest != null)
        {
            currentChest.OnChestChanged -= RefreshUI;
            currentChest = null;
        }

        if (chestPanel != null)
            chestPanel.SetActive(false);

        if (ItemTooltipUI.Instance != null)
            ItemTooltipUI.Instance.Hide();
    }

    public void RefreshUI()
    {
        if (currentChest == null || uiSlots == null) return;

        var dataSlots = currentChest.Slots;
        for (int i = 0; i < uiSlots.Length; i++)
        {
            uiSlots[i].slotIndex = i;

            if (i < dataSlots.Length)
            {
                uiSlots[i].gameObject.SetActive(true);
                uiSlots[i].SetData(dataSlots[i]);
            }
            else
            {
                uiSlots[i].ClearSlot();
                uiSlots[i].gameObject.SetActive(false);
            }
        }
    }
}