using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class HotbarController : MonoBehaviour
{
    public static HotbarController instance;

    [Header("Main links")]
    [SerializeField] private ToolController toolController;
    [SerializeField] private Image[] slotBackgrounds;
    [SerializeField] private Image[] slotIcons;

    [Header("Visible amounts")]
    [SerializeField] private TextMeshProUGUI[] slotAmountTexts;
    [SerializeField] private TextMeshProUGUI selectedItemAmountText;

    [Header("Colors and selectSlots")]
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.92f, 0.016f, 1f);
    [SerializeField] private bool hideEmptyUnselectedSlots = true;

    private int selectedHotbarSlot = 0;
    private const int HOTBAR_START_INDEX = 8;

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    private void OnEnable()
    {
        if (InventoryManager.instance != null)
        {
            InventoryManager.instance.OnInventoryChanged -= UpdateHotbarUI;
            InventoryManager.instance.OnInventoryChanged += UpdateHotbarUI;
            UpdateHotbarUI();
        }
    }

    private void Start()
    {
        if (toolController == null)
            toolController = FindFirstObjectByType<ToolController>();

        if (InventoryManager.instance != null)
        {
            InventoryManager.instance.OnInventoryChanged -= UpdateHotbarUI;
            InventoryManager.instance.OnInventoryChanged += UpdateHotbarUI;
        }

        SelectSlot(selectedHotbarSlot);
    }

    private void OnDestroy()
    {
        if (InventoryManager.instance != null)
            InventoryManager.instance.OnInventoryChanged -= UpdateHotbarUI;
    }

    private void Update()
    {
        if (UIManager.Instance != null && UIManager.Instance.IsGameplayInputBlocked) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) SelectSlot(0);
        else if (Keyboard.current.digit2Key.wasPressedThisFrame) SelectSlot(1);
        else if (Keyboard.current.digit3Key.wasPressedThisFrame) SelectSlot(2);
        else if (Keyboard.current.digit4Key.wasPressedThisFrame) SelectSlot(3);
    }

    public void SelectSlot(int index)
    {
        selectedHotbarSlot = Mathf.Clamp(index, 0, 3);
        UpdateHotbarUI();
    }

    public ItemData GetSelectedItem()
    {
        if (InventoryManager.instance == null) return null;

        int realIndex = HOTBAR_START_INDEX + selectedHotbarSlot;
        var slots = InventoryManager.instance.Slots;

        if (realIndex >= 0 && realIndex < slots.Count && !slots[realIndex].IsEmpty)
        {
            return slots[realIndex].item;
        }

        return null;
    }

    public void UpdateHotbarUI()
    {
        if (InventoryManager.instance == null) return;

        var slots = InventoryManager.instance.Slots;

        if (slotIcons != null)
        {
            for (int i = 0; i < slotIcons.Length; i++)
            {
                if (slotIcons[i] == null) continue;

                int realIndex = HOTBAR_START_INDEX + i;
                bool hasItem = realIndex < slots.Count && !slots[realIndex].IsEmpty && slots[realIndex].item != null;

                if (hasItem)
                {
                    slotIcons[i].sprite = slots[realIndex].item.icon;
                    Color c = slotIcons[i].color;
                    c.a = 1f;
                    slotIcons[i].color = c;
                    slotIcons[i].enabled = true;
                }
                else
                {
                    slotIcons[i].sprite = null;
                    Color c = slotIcons[i].color;
                    c.a = 0f;
                    slotIcons[i].color = c;
                }
            }
        }

        if (slotBackgrounds != null)
        {
            for (int i = 0; i < slotBackgrounds.Length; i++)
            {
                if (slotBackgrounds[i] == null) continue;

                int realIndex = HOTBAR_START_INDEX + i;
                bool isEmpty = realIndex >= slots.Count || slots[realIndex].IsEmpty;
                bool isSelected = i == selectedHotbarSlot;

                Color targetColor = isSelected ? selectedColor : normalColor;
                if (isEmpty && !isSelected && hideEmptyUnselectedSlots)
                {
                    targetColor.a = 0f;
                }

                slotBackgrounds[i].color = targetColor;
            }
        }

        if (slotAmountTexts != null)
        {
            for (int i = 0; i < slotAmountTexts.Length; i++)
            {
                if (slotAmountTexts[i] == null) continue;

                int realIndex = HOTBAR_START_INDEX + i;
                if (realIndex < slots.Count && !slots[realIndex].IsEmpty && slots[realIndex].amount > 1)
                {
                    slotAmountTexts[i].text = slots[realIndex].amount.ToString();
                    slotAmountTexts[i].enabled = true;
                }
                else
                {
                    slotAmountTexts[i].text = "";
                    slotAmountTexts[i].enabled = false;
                }
            }
        }

        int selectedRealIndex = HOTBAR_START_INDEX + selectedHotbarSlot;
        if (selectedRealIndex < slots.Count)
        {
            var selectedSlotData = slots[selectedRealIndex];

            if (toolController != null)
                toolController.Equip(selectedSlotData.item);

            if (selectedItemAmountText != null)
            {
                if (!selectedSlotData.IsEmpty && selectedSlotData.amount > 0)
                {
                    selectedItemAmountText.text = selectedSlotData.amount.ToString();
                    selectedItemAmountText.enabled = true;
                }
                else
                {
                    selectedItemAmountText.text = "";
                    selectedItemAmountText.enabled = false;
                }
            }
        }
    }
}