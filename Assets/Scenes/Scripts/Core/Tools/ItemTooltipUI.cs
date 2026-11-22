using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class ItemTooltipUI : MonoBehaviour
{
    public static ItemTooltipUI Instance { get; private set; }

    [Header("Tooltip Text Fields")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI rarityText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Animation and Offset Settings")]
    [SerializeField] private float fadeSpeed = 12f;
    [SerializeField] private Vector2 cursorOffset = new Vector2(18f, -18f);

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private float targetAlpha = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    private void Update()
    {
        if (!Mathf.Approximately(canvasGroup.alpha, targetAlpha))
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.unscaledDeltaTime);
        }

        if (canvasGroup.alpha > 0.01f)
        {
            FollowMouse();
        }
    }

    public void Show(ItemData item)
    {
        if (item == null)
        {
            Hide();
            return;
        }

        transform.SetAsLastSibling();

        titleText.text = item.itemName;

        rarityText.text = item.GetRarityName();
        rarityText.color = item.GetRarityColor();

        string stats = item.GetTooltipStats();
        statsText.text = stats;
        statsText.gameObject.SetActive(!string.IsNullOrEmpty(stats));

        descriptionText.text = item.description;
        descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(item.description));

        targetAlpha = 1f;
        FollowMouse();
    }

    public void Hide()
    {
        targetAlpha = 0f;
    }

    private void FollowMouse()
    {
        if (Mouse.current == null) return;

        Vector2 screenPos = Mouse.current.position.ReadValue() + cursorOffset;

        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            RectTransform canvasRect = parentCanvas.transform as RectTransform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, parentCanvas.worldCamera, out Vector2 localPos))
            {
                rectTransform.anchoredPosition = localPos;
            }
        }
        else
        {
            rectTransform.position = screenPos;
        }
    }
}