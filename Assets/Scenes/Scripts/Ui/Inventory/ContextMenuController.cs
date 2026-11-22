using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class ContextMenuController : MonoBehaviour
{
    public static ContextMenuController Instance { get; private set; }

    [Header("UI Contex menu")]
    [SerializeField] private RectTransform menuPanel;
    [SerializeField] private Vector2 cursorOffset = new Vector2(8f, 8f);
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI itemPriceText;

    [Header("Settings")]
    [SerializeField] private Transform playerTransform;

    private InventorySlot selectedSlot;
    private Canvas parentCanvas;
    private RectTransform canvasRect;

    public bool HasSelection => selectedSlot != null && selectedSlot.item != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (menuPanel == null)
            menuPanel = GetComponent<RectTransform>();

        if (menuPanel != null)
        {
            menuPanel.pivot = new Vector2(0f, 0f);
            parentCanvas = menuPanel.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
                canvasRect = parentCanvas.transform as RectTransform;
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
            UIManager.Instance.OnInventoryStateChanged += OnInventoryToggled;

        ClearSelection();
    }

    private void OnDestroy()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.OnInventoryStateChanged -= OnInventoryToggled;
    }

    private void Update()
    {
        if (UIManager.Instance != null && !UIManager.Instance.IsInventoryOpen)
            return;

        if (HasSelection && Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
        {
            DropSelectedItem();
        }
    }

    private void OnInventoryToggled(bool isOpen)
    {
        if (!isOpen)
        {
            ClearSelection();
            if (ItemTooltipUI.Instance != null)
                ItemTooltipUI.Instance.Hide();
        }
    }

    public void SelectSlot(InventorySlot slot)
    {
        if (slot == null || slot.item == null)
        {
            ClearSelection();
            return;
        }

        selectedSlot = slot;
        if (itemNameText != null) itemNameText.text = slot.item.itemName;
        if (itemPriceText != null) itemPriceText.text = $"Цена: {slot.item.GetSellValue()}";

        if (menuPanel != null)
        {
            menuPanel.gameObject.SetActive(true);
            menuPanel.SetAsLastSibling();
            PositionMenuAtCursor();
        }
    }

    private void PositionMenuAtCursor()
    {
        if (menuPanel == null || Mouse.current == null) return;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        RectTransform targetParent = menuPanel.parent as RectTransform;
        if (targetParent == null) targetParent = canvasRect;
        if (targetParent == null) return;

        Camera uiCam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? parentCanvas.worldCamera
            : null;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(targetParent, screenPos, uiCam, out Vector2 localPoint))
        {
            Vector2 pos = localPoint + cursorOffset;

            Rect parentBounds = targetParent.rect;
            Vector2 menuSize = menuPanel.rect.size;

            float minX = parentBounds.xMin;
            float maxX = parentBounds.xMax - menuSize.x;
            float minY = parentBounds.yMin;
            float maxY = parentBounds.yMax - menuSize.y;

            pos.x = (maxX >= minX) ? Mathf.Clamp(pos.x, minX, maxX) : minX;
            pos.y = (maxY >= minY) ? Mathf.Clamp(pos.y, minY, maxY) : minY;

            menuPanel.anchoredPosition = pos;
        }
    }

    public void DropSelectedItem()
    {
        if (!HasSelection) return;

        ItemData itemToDrop = selectedSlot.item;

        if (itemToDrop.dropPrefab != null && playerTransform != null)
        {
            Vector3 offset = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0f);
            Instantiate(itemToDrop.dropPrefab, playerTransform.position + offset, Quaternion.identity);
        }

        if (InventoryManager.instance != null)
        {
            InventoryManager.instance.RemoveFromSlot(selectedSlot.slotIndex, 1);
        }

        if (selectedSlot.item == null || selectedSlot.amount <= 0)
        {
            ClearSelection();
        }
    }

    public void ClearSelection()
    {
        selectedSlot = null;
        if (itemNameText != null) itemNameText.text = string.Empty;
        if (itemPriceText != null) itemPriceText.text = string.Empty;
        if (menuPanel != null && menuPanel.gameObject != gameObject)
            menuPanel.gameObject.SetActive(false);
    }
}