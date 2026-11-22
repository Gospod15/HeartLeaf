using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ScreenOverlayController : MonoBehaviour
{
    public static ScreenOverlayController Instance { get; private set; }

    [Header("Sorting Layers & Order")]
    [Tooltip("Below house floor (2), above ground (-1) and water (0)")]
    [SerializeField] private int interiorSortingOrder = 1;
    [Tooltip("Above roofs (11) and walls (3), below UI")]
    [SerializeField] private int debuffSortingOrder = 25;

    [Header("Overlay Colors")]
    [Tooltip("Background color outside the house when player is indoors")]
    [SerializeField] private Color interiorColor = Color.black;
    [Tooltip("Default tint color for combat debuffs if no custom tint is passed")]
    [SerializeField] private Color defaultDebuffColor = Color.black;

    [Header("Default Settings")]
    [SerializeField] private float defaultFadeDuration = 0.25f;

    [Header("Exterior Props Hiding")]
    [Tooltip("Layers of exterior props (e.g. Organica) to hide while indoors")]
    [SerializeField] private LayerMask[] exteriorPropsLayers;
    [SerializeField] private Vector2 hideBoxSize = new Vector2(50f, 35f);
    [SerializeField] private float hideCheckInterval = 0.2f;

    private SpriteRenderer overlayRenderer;
    private Coroutine activeFadeCoroutine;
    private Coroutine debuffTimerCoroutine;

    private int activeHousesCount = 0;
    private float debuffTargetAlpha = 0f;
    private Color currentDebuffColor;
    private float hideCheckTimer = 0f;
    private int combinedExteriorMask = 0;

    private readonly List<SpriteRenderer> hiddenRenderers = new List<SpriteRenderer>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        overlayRenderer = GetComponent<SpriteRenderer>();
        currentDebuffColor = defaultDebuffColor;
        RebuildExteriorLayerMask();
        ApplyImmediate(interiorColor, 0f, interiorSortingOrder);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RebuildExteriorLayerMask();
    }
#endif

    private void RebuildExteriorLayerMask()
    {
        combinedExteriorMask = 0;
        if (exteriorPropsLayers == null) return;

        for (int i = 0; i < exteriorPropsLayers.Length; i++)
        {
            combinedExteriorMask |= exteriorPropsLayers[i].value;
        }
    }

    private void Update()
    {
        if (activeHousesCount <= 0) return;

        hideCheckTimer -= Time.deltaTime;
        if (hideCheckTimer <= 0f)
        {
            hideCheckTimer = hideCheckInterval;
            HidePropsInCameraView();
        }
    }

    public void EnterInterior(float fadeDuration = -1f)
    {
        activeHousesCount++;
        if (activeHousesCount == 1)
        {
            SetExteriorPropsVisible(false);
            RefreshOverlayState(fadeDuration < 0f ? defaultFadeDuration : fadeDuration);
        }
    }

    public void ExitInterior(float fadeDuration = -1f)
    {
        activeHousesCount = Mathf.Max(0, activeHousesCount - 1);
        if (activeHousesCount == 0)
        {
            SetExteriorPropsVisible(true);
            RefreshOverlayState(fadeDuration < 0f ? defaultFadeDuration : fadeDuration);
        }
    }

    // combat mechanics
    public void ApplyTimedDebuff(float intensityAlpha, float duration, float fadeSpeed = 0.2f, Color? tint = null)
    {
        if (debuffTimerCoroutine != null)
            StopCoroutine(debuffTimerCoroutine);

        debuffTimerCoroutine = StartCoroutine(
            DebuffTimerRoutine(intensityAlpha, duration, fadeSpeed, tint ?? defaultDebuffColor)
        );
    }

    public void SetDebuff(float intensityAlpha, float fadeDuration = -1f, Color? tint = null)
    {
        if (debuffTimerCoroutine != null)
            StopCoroutine(debuffTimerCoroutine);
        debuffTimerCoroutine = null;

        debuffTargetAlpha = Mathf.Clamp01(intensityAlpha);
        currentDebuffColor = tint ?? defaultDebuffColor;
        RefreshOverlayState(fadeDuration < 0f ? defaultFadeDuration : fadeDuration);
    }

    public void ClearDebuff(float fadeDuration = -1f)
    {
        SetDebuff(0f, fadeDuration);
    }

    private IEnumerator DebuffTimerRoutine(float intensityAlpha, float duration, float fadeSpeed, Color tint)
    {
        debuffTargetAlpha = Mathf.Clamp01(intensityAlpha);
        currentDebuffColor = tint;
        RefreshOverlayState(fadeSpeed);

        yield return new WaitForSeconds(duration);

        debuffTargetAlpha = 0f;
        RefreshOverlayState(fadeSpeed);
        debuffTimerCoroutine = null;
    }

    private void RefreshOverlayState(float duration)
    {
        bool hasDebuff = debuffTargetAlpha > 0.001f;
        bool isInHouse = activeHousesCount > 0;

        Color targetColor;
        float targetAlpha;
        int targetOrder;

        if (hasDebuff)
        {
            targetColor = currentDebuffColor;
            targetAlpha = isInHouse ? Mathf.Max(debuffTargetAlpha, 0.85f) : debuffTargetAlpha;
            targetOrder = debuffSortingOrder;
        }
        else if (isInHouse)
        {
            targetColor = interiorColor;
            targetAlpha = interiorColor.a;
            targetOrder = interiorSortingOrder;
        }
        else
        {
            targetColor = interiorColor;
            targetAlpha = 0f;
            targetOrder = interiorSortingOrder;
        }

        if (activeFadeCoroutine != null)
            StopCoroutine(activeFadeCoroutine);

        activeFadeCoroutine = StartCoroutine(FadeToState(targetColor, targetAlpha, targetOrder, duration));
    }

    private IEnumerator FadeToState(Color targetColor, float targetAlpha, int targetOrder, float duration)
    {
        overlayRenderer.enabled = true;
        overlayRenderer.sortingOrder = targetOrder;

        Color startColor = overlayRenderer.color;
        float startAlpha = startColor.a;
        float elapsed = 0f;

        if (duration <= 0.001f)
        {
            ApplyImmediate(targetColor, targetAlpha, targetOrder);
            activeFadeCoroutine = null;
            yield break;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            Color c = Color.Lerp(startColor, targetColor, t);
            c.a = Mathf.Lerp(startAlpha, targetAlpha, t);
            overlayRenderer.color = c;

            yield return null;
        }

        ApplyImmediate(targetColor, targetAlpha, targetOrder);
        activeFadeCoroutine = null;
    }

    private void ApplyImmediate(Color color, float alpha, int order)
    {
        overlayRenderer.sortingOrder = order;
        color.a = alpha;
        overlayRenderer.color = color;
        overlayRenderer.enabled = alpha > 0.001f;
    }

    private void SetExteriorPropsVisible(bool visible)
    {
        if (!visible)
        {
            hiddenRenderers.Clear();
            HidePropsInCameraView();
        }
        else
        {
            foreach (var sr in hiddenRenderers)
            {
                if (sr != null)
                    sr.enabled = true;
            }
            hiddenRenderers.Clear();
        }
    }

    private void HidePropsInCameraView()
    {
        if (combinedExteriorMask == 0) return;

        Vector2 center = transform.parent != null ? (Vector2)transform.parent.position : (Vector2)transform.position;
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, hideBoxSize, 0f, combinedExteriorMask);

        foreach (var hit in hits)
        {
            if (hit == null) continue;
            SpriteRenderer sr = hit.GetComponentInParent<SpriteRenderer>();
            if (sr != null && sr.enabled)
            {
                sr.enabled = false;
                hiddenRenderers.Add(sr);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.black;
        Vector3 center = transform.parent != null ? transform.parent.position : transform.position;
        Gizmos.DrawWireCube(new Vector3(center.x, center.y, 0f), hideBoxSize);
    }
}