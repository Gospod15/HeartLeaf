using UnityEngine;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class NPCNameplateUI : MonoBehaviour
{
    public static NPCNameplateUI Instance { get; private set; }

    [Header("UI Elements")]
    [SerializeField] private RectTransform nameplateRect;
    [SerializeField] private TextMeshProUGUI nameText;

    [Header("Anchoring and Animation")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.35f, 0f);
    [SerializeField] private float fadeSpeed = 10f;

    [Header("Highlight Colors")]
    [SerializeField] private Color normalTextColor = new Color(0.9f, 0.9f, 0.9f, 1f);
    [SerializeField] private Color highlightTextColor = new Color(1f, 0.85f, 0.35f, 1f);

    private CanvasGroup canvasGroup;
    private Camera mainCamera;
    private Transform currentTarget;
    private float targetHeadOffsetY = 0f;
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
        if (nameplateRect == null)
            nameplateRect = GetComponent<RectTransform>();

        mainCamera = Camera.main;
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
    }

    private void LateUpdate()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
        {
            targetAlpha = 0f;
        }

        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.unscaledDeltaTime);

        if (currentTarget != null && canvasGroup.alpha > 0.001f)
        {
            if (mainCamera == null) mainCamera = Camera.main;
            Vector3 headWorldPos = currentTarget.position + new Vector3(worldOffset.x, targetHeadOffsetY + worldOffset.y, worldOffset.z);
            Vector3 screenPos = mainCamera.WorldToScreenPoint(headWorldPos);
            nameplateRect.position = screenPos;
        }
    }

    public void ShowForTarget(Transform target, string npcName, bool isHighlighted = true)
    {
        if (target == null || string.IsNullOrEmpty(npcName))
        {
            Hide();
            return;
        }

        currentTarget = target;
        targetAlpha = 1f;

        SpriteRenderer sr = target.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
            targetHeadOffsetY = sr.bounds.max.y - target.position.y;
        else
            targetHeadOffsetY = 1.0f;

        if (nameText != null)
        {
            nameText.text = isHighlighted ? $"[E] {npcName}" : npcName;
            nameText.color = isHighlighted ? highlightTextColor : normalTextColor;
        }
    }

    public void Hide()
    {
        targetAlpha = 0f;
    }
}