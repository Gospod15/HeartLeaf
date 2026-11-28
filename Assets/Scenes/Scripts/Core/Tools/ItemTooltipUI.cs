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

    [Header("Common Rarity")]
    [SerializeField] private string commonPrefix = "Common";
    [SerializeField] private Color commonColor = Color.white;

    [Header("Rare Rarity")]
    [SerializeField] private string rarePrefix = "Rare";
    [SerializeField] private Color rareColor = new Color(0.2f, 0.6f, 1f, 1f);

    [Header("Epic Rarity")]
    [SerializeField] private string epicPrefix = "Epic";
    [SerializeField] private Color epicColor = new Color(0.7f, 0.3f, 1f, 1f);

    [Header("Legendary Rarity")]
    [SerializeField] private string legendaryPrefix = "Legendary";
    [SerializeField] private Color legendaryColor = new Color(1f, 0.75f, 0.1f, 1f);

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
        ApplyRarity(item.rarity);

        string stats = item.GetTooltipStats();
        statsText.text = stats;
        statsText.gameObject.SetActive(!string.IsNullOrEmpty(stats));

        descriptionText.text = item.description;
        descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(item.description));

        targetAlpha = 1f;
        FollowMouse();
    }

    private void ApplyRarity(ItemRarity rarity)
    {
        if (rarityText == null) return;

        switch (rarity)
        {
            case ItemRarity.Common:
                rarityText.text = commonPrefix;
                rarityText.color = commonColor;
                break;
            case ItemRarity.Rare:
                rarityText.text = rarePrefix;
                rarityText.color = rareColor;
                break;
            case ItemRarity.Epic:
                rarityText.text = epicPrefix;
                rarityText.color = epicColor;
                break;
            case ItemRarity.Legendary:
                rarityText.text = legendaryPrefix;
                rarityText.color = legendaryColor;
                break;
        }
    }

    public void Hide()
    {
        targetAlpha = 0f;
    }

    private void FollowMouse()
    {
        if (Mouse.current == null) return;

        RectTransform parentRect = rectTransform.parent as RectTransform;
        if (parentRect == null) return;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Camera uiCam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? parentCanvas.worldCamera
            : null;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, uiCam, out Vector2 localPos))
        {
            rectTransform.anchoredPosition = localPos + cursorOffset;
        }
    }
}