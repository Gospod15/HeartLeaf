using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;

public class InventoryDragController : MonoBehaviour
{
    public static InventoryDragController Instance { get; private set; }

    [Header("Cursor UI")]
    [SerializeField] private Image dragIcon;
    [SerializeField] private TextMeshProUGUI dragAmountText;

    [Header("Drop Outside Settings")]
    [SerializeField] private GameObject universalDropPrefab;
    [SerializeField] private List<RectTransform> protectedPanels = new List<RectTransform>();
    [SerializeField] private Transform playerTransform;

    public ItemData heldItem { get; private set; }
    public int heldAmount { get; private set; }
    private int heldDurability = -1;
    private int sourceSlotIndex = -1;
    private bool isCraftedItem = false;

    public bool IsDragging => heldItem != null && heldAmount > 0;

    private Canvas parentCanvas;
    private int ignoreOutsideClickFrame = -1;

    private void Awake()
    {
        Instance = this;
        if (universalDropPrefab != null)
            ItemInWorldManager.DefaultDropPrefab = universalDropPrefab;

        if (dragIcon != null)
        {
            dragIcon.raycastTarget = false;
            dragIcon.enabled = false;
            parentCanvas = dragIcon.GetComponentInParent<Canvas>();
        }
        if (dragAmountText != null)
        {
            dragAmountText.raycastTarget = false;
            dragAmountText.enabled = false;
        }
    }

    private void Start()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }

        if (UIManager.Instance != null)
        {
            RegisterPanel(UIManager.Instance.InventoryPanel);
            RegisterPanel(UIManager.Instance.CharacterPanel);
            RegisterPanel(UIManager.Instance.CraftingPanel);
            RegisterPanel(UIManager.Instance.HotbarPanel);
            RegisterPanel(UIManager.Instance.ChestPanel);
        }
    }

    private void RegisterPanel(GameObject panelObj)
    {
        if (panelObj == null) return;
        RectTransform rect = panelObj.GetComponent<RectTransform>();
        if (rect != null && !protectedPanels.Contains(rect))
            protectedPanels.Add(rect);
    }

    private void Update()
    {
        if (!IsDragging) return;

        UpdateIconPosition();
        HandleOutsideDropInput();
    }

    private void HandleOutsideDropInput()
    {
        if (Mouse.current == null || Time.frameCount == ignoreOutsideClickFrame) return;

        bool leftClicked = Mouse.current.leftButton.wasPressedThisFrame;
        bool rightClicked = Mouse.current.rightButton.wasPressedThisFrame;

        if (!leftClicked && !rightClicked) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();

        if (IsPointerInsideInventoryOrSlots(mousePos)) return;

        if (leftClicked)
        {
            DropItemToWorld(heldItem, heldAmount, heldDurability);
            ClearHeldItem();
        }
        else if (rightClicked)
        {
            DropItemToWorld(heldItem, 1, heldDurability);
            heldAmount--;

            if (heldAmount <= 0)
                ClearHeldItem();
            else
                RefreshCursorVisual();
        }
    }

    private bool IsPointerInsideInventoryOrSlots(Vector2 screenPos)
    {
        Camera uiCam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? parentCanvas.worldCamera
            : null;

        for (int i = 0; i < protectedPanels.Count; i++)
        {
            RectTransform panel = protectedPanels[i];
            if (panel != null && panel.gameObject.activeInHierarchy)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(panel, screenPos, uiCam))
                    return true;
            }
        }

        if (EventSystem.current != null)
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = screenPos };
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            foreach (var hit in results)
            {
                if (hit.gameObject.GetComponentInParent<InventorySlot>() != null ||
                    hit.gameObject.GetComponentInParent<ChestSlotUI>() != null ||
                    hit.gameObject.GetComponentInParent<CraftingGridController>() != null ||
                    hit.gameObject.GetComponentInParent<ContextMenuController>() != null)
                {
                    return true;
                }

                Transform hitTransform = hit.gameObject.transform;
                for (int i = 0; i < protectedPanels.Count; i++)
                {
                    if (protectedPanels[i] != null && hitTransform.IsChildOf(protectedPanels[i]))
                        return true;
                }
            }
        }

        return false;
    }

    private void DropItemToWorld(ItemData item, int amount, int durability = -1)
    {
        if (item == null || amount <= 0) return;

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }

        if (playerTransform == null) return;

        for (int i = 0; i < amount; i++)
        {
            Vector3 offset = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0f);
            ItemInWorldManager.SpawnDrop(item, playerTransform.position + offset, 1, durability, universalDropPrefab);
        }
    }

    private void UpdateIconPosition()
    {
        if (dragIcon == null || Mouse.current == null) return;
        Vector2 screenPos = Mouse.current.position.ReadValue();

        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            RectTransform canvasRect = parentCanvas.transform as RectTransform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, parentCanvas.worldCamera, out Vector2 localPos))
            {
                dragIcon.rectTransform.anchoredPosition = localPos;
            }
        }
        else
        {
            dragIcon.transform.position = screenPos;
        }
    }

    private void RefreshCursorVisual()
    {
        if (!IsDragging)
        {
            if (dragIcon != null) { dragIcon.sprite = null; dragIcon.enabled = false; }
            if (dragAmountText != null) dragAmountText.enabled = false;
            return;
        }

        if (dragIcon != null)
        {
            dragIcon.transform.SetAsLastSibling();
            dragIcon.sprite = heldItem.icon;
            dragIcon.enabled = true;
            UpdateIconPosition();
        }

        if (dragAmountText != null)
        {
            dragAmountText.text = heldAmount > 1 ? heldAmount.ToString() : "";
            dragAmountText.enabled = heldAmount > 1;
        }
    }

    public void HandleLeftClickOnSlot(int slotIndex)
    {
        if (InventoryManager.instance == null) return;
        ignoreOutsideClickFrame = Time.frameCount;

        var slot = InventoryManager.instance.Slots[slotIndex];

        if (!IsDragging)
        {
            if (slot.IsEmpty) return;

            heldItem = slot.item;
            heldAmount = slot.amount;
            heldDurability = slot.currentDurability;
            sourceSlotIndex = slotIndex;
            isCraftedItem = false;

            InventoryManager.instance.SetSlotDirect(slotIndex, null, 0);
            RefreshCursorVisual();
            return;
        }

        if (!InventoryManager.instance.CanPlaceItemInSlot(slotIndex, heldItem))
            return;

        if (slot.IsEmpty)
        {
            InventoryManager.instance.SetSlotDirect(slotIndex, heldItem, heldAmount, heldDurability);
            ClearHeldItem();
        }
        else if (slot.item == heldItem && heldItem.isStackable && slot.amount < heldItem.maxStackSize)
        {
            int spaceLeft = heldItem.maxStackSize - slot.amount;
            int toAdd = Mathf.Min(spaceLeft, heldAmount);

            InventoryManager.instance.SetSlotDirect(slotIndex, heldItem, slot.amount + toAdd);
            heldAmount -= toAdd;

            if (heldAmount <= 0) ClearHeldItem();
            else RefreshCursorVisual();
        }
        else
        {
            ItemData tempItem = slot.item;
            int tempAmount = slot.amount;
            int tempDurability = slot.currentDurability;

            InventoryManager.instance.SetSlotDirect(slotIndex, heldItem, heldAmount, heldDurability);
            heldItem = tempItem;
            heldAmount = tempAmount;
            heldDurability = tempDurability;
            sourceSlotIndex = slotIndex;
            isCraftedItem = false;
            RefreshCursorVisual();
        }
    }

    public void HandleRightClickOnSlot(int slotIndex)
    {
        if (!IsDragging || InventoryManager.instance == null) return;
        ignoreOutsideClickFrame = Time.frameCount;

        if (!InventoryManager.instance.CanPlaceItemInSlot(slotIndex, heldItem))
            return;

        var slot = InventoryManager.instance.Slots[slotIndex];

        if (slot.IsEmpty)
        {
            InventoryManager.instance.SetSlotDirect(slotIndex, heldItem, 1, heldDurability);
            heldAmount--;
            if (heldAmount <= 0) ClearHeldItem();
            else RefreshCursorVisual();
        }
        else if (slot.item == heldItem && heldItem.isStackable && slot.amount < heldItem.maxStackSize)
        {
            InventoryManager.instance.SetSlotDirect(slotIndex, heldItem, slot.amount + 1);
            heldAmount--;
            if (heldAmount <= 0) ClearHeldItem();
            else RefreshCursorVisual();
        }
    }

    public void HandleLeftClickOnChestSlot(ChestStorage chest, int slotIndex)
    {
        if (chest == null || slotIndex < 0 || slotIndex >= chest.Slots.Length) return;
        ignoreOutsideClickFrame = Time.frameCount;

        var slot = chest.Slots[slotIndex];

        if (!IsDragging)
        {
            if (slot.IsEmpty) return;

            heldItem = slot.item;
            heldAmount = slot.amount;
            heldDurability = slot.currentDurability;
            sourceSlotIndex = -1;
            isCraftedItem = false;

            chest.SetSlotDirect(slotIndex, null, 0);
            RefreshCursorVisual();
            return;
        }

        if (slot.IsEmpty)
        {
            chest.SetSlotDirect(slotIndex, heldItem, heldAmount, heldDurability);
            ClearHeldItem();
        }
        else if (slot.item == heldItem && heldItem.isStackable && slot.amount < heldItem.maxStackSize)
        {
            int spaceLeft = heldItem.maxStackSize - slot.amount;
            int toAdd = Mathf.Min(spaceLeft, heldAmount);

            chest.SetSlotDirect(slotIndex, heldItem, slot.amount + toAdd);
            heldAmount -= toAdd;

            if (heldAmount <= 0) ClearHeldItem();
            else RefreshCursorVisual();
        }
        else
        {
            ItemData tempItem = slot.item;
            int tempAmount = slot.amount;
            int tempDurability = slot.currentDurability;

            chest.SetSlotDirect(slotIndex, heldItem, heldAmount, heldDurability);
            heldItem = tempItem;
            heldAmount = tempAmount;
            heldDurability = tempDurability;
            sourceSlotIndex = -1;
            isCraftedItem = false;
            RefreshCursorVisual();
        }
    }

    public void HandleRightClickOnChestSlot(ChestStorage chest, int slotIndex)
    {
        if (!IsDragging || chest == null || slotIndex < 0 || slotIndex >= chest.Slots.Length) return;
        ignoreOutsideClickFrame = Time.frameCount;

        var slot = chest.Slots[slotIndex];

        if (slot.IsEmpty)
        {
            chest.SetSlotDirect(slotIndex, heldItem, 1, heldDurability);
            heldAmount--;
            if (heldAmount <= 0) ClearHeldItem();
            else RefreshCursorVisual();
        }
        else if (slot.item == heldItem && heldItem.isStackable && slot.amount < heldItem.maxStackSize)
        {
            chest.SetSlotDirect(slotIndex, heldItem, slot.amount + 1);
            heldAmount--;
            if (heldAmount <= 0) ClearHeldItem();
            else RefreshCursorVisual();
        }
    }

    public bool TryPickUpCraftResult(ItemData resultItem, int resultAmount)
    {
        if (resultItem == null || resultAmount <= 0 || InventoryManager.instance == null)
            return false;

        ignoreOutsideClickFrame = Time.frameCount;

        if (!IsDragging)
        {
            if (resultAmount > resultItem.maxStackSize && !resultItem.isStackable)
                return false;

            if (!InventoryManager.instance.CanAddItem(resultItem, resultAmount))
                return false;

            heldItem = resultItem;
            heldAmount = resultAmount;
            heldDurability = (resultItem is ArmorItemData armor) ? armor.maxDurability : 0;
            sourceSlotIndex = -1;
            isCraftedItem = true;
            RefreshCursorVisual();
            return true;
        }

        if (heldItem == resultItem && heldItem.isStackable && heldAmount + resultAmount <= heldItem.maxStackSize)
        {
            heldAmount += resultAmount;
            isCraftedItem = true;
            RefreshCursorVisual();
            return true;
        }

        if (InventoryManager.instance.AddItem(resultItem, resultAmount))
        {
            return true;
        }

        return false;
    }

    private void ClearHeldItem()
    {
        heldItem = null;
        heldAmount = 0;
        heldDurability = -1;
        sourceSlotIndex = -1;
        isCraftedItem = false;
        RefreshCursorVisual();
    }

    public void ReturnHeldItemOnClose()
    {
        if (!IsDragging) return;

        if (InventoryManager.instance != null)
        {
            if (!isCraftedItem && sourceSlotIndex >= 0 && InventoryManager.instance.Slots[sourceSlotIndex].IsEmpty)
            {
                InventoryManager.instance.SetSlotDirect(sourceSlotIndex, heldItem, heldAmount, heldDurability);
            }
            else if (!InventoryManager.instance.AddItem(heldItem, heldAmount))
            {
                DropItemToWorld(heldItem, heldAmount, heldDurability);
            }
        }

        ClearHeldItem();
    }
}