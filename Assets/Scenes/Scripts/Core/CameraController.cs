using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [Header("Target")]
    public Transform target;
    [SerializeField] private Vector2 baseOffset = new Vector2(0f, 0.5f);

    [Header("Smooth Following")]
    [Tooltip("Follow smoothing time (less = sharper, more = smoother)")]
    [SerializeField] private float followSmoothTime = 0.14f;

    [Header("Look-Ahead")]
    [SerializeField] private float walkLookAhead = 0.7f;
    [SerializeField] private float sprintLookAhead = 1.8f;
    [SerializeField] private float lookAheadSmoothTime = 0.25f;

    [Header("Pixel Perfect Smooth Zoom & Sprint")]
    [Tooltip("Base resolution at 1.0 zoom scale")]
    [SerializeField] private Vector2Int baseReferenceResolution = new Vector2Int(960, 540);
    [Tooltip("Starting zoom multiplier")]
    [SerializeField] private float startZoomMultiplier = 2.0f;
    [Tooltip("Minimum multiplier (max zoom out)")]
    [SerializeField] private float minZoomMultiplier = 0.75f;
    [Tooltip("Maximum multiplier (max zoom in)")]
    [SerializeField] private float maxZoomMultiplier = 5.0f;
    [Tooltip("Mouse scroll sensitivity")]
    [SerializeField] private float scrollSensitivity = 0.35f;
    [Tooltip("Zoom smoothing time")]
    [SerializeField] private float zoomSmoothTime = 0.25f;
    [Tooltip("Zoom out amount while sprinting")]
    [SerializeField] private float sprintZoomOutMultiplier = 0.25f;

    [Header("Fallback Zoom (without PixelPerfectCamera)")]
    [SerializeField] private float fallbackZoomStep = 1f;
    [SerializeField] private float fallbackMinZoom = 2.5f;
    [SerializeField] private float fallbackMaxZoom = 14f;
    [SerializeField] private float fallbackSprintBonus = 1.15f;

    [Header("X-Ray")]
    public LayerMask treeLayer;
    public LayerMask bushesLayer;
    [Range(0.1f, 1f)] public float transparency = 0.5f;
    public float checkRadius = 1.5f;
    public float yOffsetCheck = 0.5f;

    private Camera cam;
    private PixelPerfectCamera pixelPerfectCam;
    private PlayerMovement playerMovement;

    private float targetZoomMultiplier;
    private float currentZoomMultiplier;
    private float zoomMultiplierVelocity;

    private float baseTargetOrthoSize;
    private float orthoZoomVelocity;
    private float fixedZ = -10f;

    private Vector3 followVelocity;
    private Vector2 currentLookAhead;
    private Vector2 lookAheadVelocity;

    private float shakeTimer;
    private float shakeIntensity;

    private List<SpriteRenderer> obscuredTrees = new List<SpriteRenderer>();
    private List<SpriteRenderer> obscuredBushes = new List<SpriteRenderer>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        cam = GetComponent<Camera>();
        pixelPerfectCam = GetComponent<PixelPerfectCamera>();
    }

    private void Start()
    {
        baseTargetOrthoSize = cam.orthographicSize;

        if (pixelPerfectCam != null && baseReferenceResolution.x <= 0)
        {
            baseReferenceResolution = new Vector2Int(pixelPerfectCam.refResolutionX, pixelPerfectCam.refResolutionY);
        }

        targetZoomMultiplier = Mathf.Clamp(startZoomMultiplier, minZoomMultiplier, maxZoomMultiplier);
        currentZoomMultiplier = targetZoomMultiplier;

        if (target != null)
        {
            playerMovement = target.GetComponent<PlayerMovement>();
            transform.position = new Vector3(target.position.x + baseOffset.x, target.position.y + baseOffset.y, fixedZ);
        }

        ApplyPixelPerfectZoom();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if (playerMovement == null)
            playerMovement = target.GetComponent<PlayerMovement>();

        HandleZoom();
        HandleMovement();
        HandleTransparency();
    }

    private void HandleZoom()
    {
        float scrollDelta = 0f;
        if (Mouse.current != null && (UIManager.Instance == null || !UIManager.Instance.IsGameplayInputBlocked))
        {
            float rawScroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(rawScroll) > 0.01f)
            {
                // Нормалізуємо значення прокрутки коліщатка
                scrollDelta = Mathf.Sign(rawScroll);
            }
        }

        if (pixelPerfectCam != null && pixelPerfectCam.enabled)
        {
            if (scrollDelta != 0f)
            {
                // Пропорційний крок для плавного зуму на будь-якій дистанції
                float step = scrollSensitivity * Mathf.Max(0.6f, targetZoomMultiplier * 0.5f);
                targetZoomMultiplier = Mathf.Clamp(
                    targetZoomMultiplier + scrollDelta * step,
                    minZoomMultiplier,
                    maxZoomMultiplier
                );
            }

            bool isSprinting = playerMovement != null && playerMovement.IsSprinting;
            float desiredMultiplier = Mathf.Max(
                minZoomMultiplier * 0.85f,
                targetZoomMultiplier - (isSprinting ? sprintZoomOutMultiplier : 0f)
            );

            currentZoomMultiplier = Mathf.SmoothDamp(
                currentZoomMultiplier,
                desiredMultiplier,
                ref zoomMultiplierVelocity,
                zoomSmoothTime
            );

            ApplyPixelPerfectZoom();
            return;
        }

        // Резервний зум, якщо PixelPerfectCamera вимкнено
        if (scrollDelta != 0f)
        {
            baseTargetOrthoSize = Mathf.Clamp(
                baseTargetOrthoSize - scrollDelta * fallbackZoomStep,
                fallbackMinZoom,
                fallbackMaxZoom
            );
        }

        bool sprinting = playerMovement != null && playerMovement.IsSprinting;
        float finalOrtho = baseTargetOrthoSize + (sprinting ? fallbackSprintBonus : 0f);

        cam.orthographicSize = Mathf.SmoothDamp(
            cam.orthographicSize,
            finalOrtho,
            ref orthoZoomVelocity,
            zoomSmoothTime
        );
    }

    private void ApplyPixelPerfectZoom()
    {
        if (pixelPerfectCam == null) return;

        float aspect = (float)baseReferenceResolution.y / Mathf.Max(1, baseReferenceResolution.x);
        float safeMultiplier = Mathf.Max(0.1f, currentZoomMultiplier);

        // Округлюємо до парного числа пікселів для збереження чіткості сітки
        int rawW = Mathf.RoundToInt(baseReferenceResolution.x / safeMultiplier);
        int finalW = Mathf.Clamp(rawW / 2 * 2, 160, 1920);
        int finalH = Mathf.Clamp(Mathf.RoundToInt(finalW * aspect) / 2 * 2, 90, 1080);

        if (pixelPerfectCam.refResolutionX != finalW || pixelPerfectCam.refResolutionY != finalH)
        {
            pixelPerfectCam.refResolutionX = finalW;
            pixelPerfectCam.refResolutionY = finalH;
        }
    }

    private void HandleMovement()
    {
        Vector2 desiredLookAhead = Vector2.zero;
        if (playerMovement != null && playerMovement.MovementDirection.sqrMagnitude > 0.01f)
        {
            float distance = playerMovement.IsSprinting ? sprintLookAhead : walkLookAhead;
            desiredLookAhead = playerMovement.MovementDirection * distance;
        }

        currentLookAhead = Vector2.SmoothDamp(
            currentLookAhead,
            desiredLookAhead,
            ref lookAheadVelocity,
            lookAheadSmoothTime
        );

        Vector3 desiredPosition = new Vector3(
            target.position.x + baseOffset.x + currentLookAhead.x,
            target.position.y + baseOffset.y + currentLookAhead.y,
            fixedZ
        );

        Vector3 smoothedPosition = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref followVelocity,
            followSmoothTime
        );

        Vector3 shakeOffset = Vector3.zero;
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            shakeOffset = (Vector3)Random.insideUnitCircle * shakeIntensity;
        }

        transform.position = new Vector3(
            smoothedPosition.x + shakeOffset.x,
            smoothedPosition.y + shakeOffset.y,
            fixedZ
        );
    }

    public void Shake(float intensity, float duration)
    {
        shakeIntensity = intensity;
        shakeTimer = duration;
    }

    private void HandleTransparency()
    {
        obscuredTrees = UpdateTransparencyForLayer(treeLayer, obscuredTrees);
        obscuredBushes = UpdateTransparencyForLayer(bushesLayer, obscuredBushes);
    }

    private List<SpriteRenderer> UpdateTransparencyForLayer(LayerMask layer, List<SpriteRenderer> currentObscuredList)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(target.position + Vector3.up * yOffsetCheck, checkRadius, layer);
        List<SpriteRenderer> newHits = new List<SpriteRenderer>();

        foreach (var hit in hits)
        {
            if (hit.TryGetComponent(out SpriteRenderer sr))
            {
                if (target.position.y > hit.transform.position.y)
                {
                    Color color = sr.color;
                    color.a = transparency;
                    sr.color = color;
                    newHits.Add(sr);
                }
            }
        }

        foreach (var oldSr in currentObscuredList)
        {
            if (!newHits.Contains(oldSr) && oldSr != null)
            {
                Color color = oldSr.color;
                color.a = 1f;
                oldSr.color = color;
            }
        }

        return newHits;
    }
}