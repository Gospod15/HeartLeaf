using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [Header("Target")]
    public Transform target;
    [SerializeField] private Vector2 baseOffset = new Vector2(0f, 0.5f);

    [Header("Smooth of following")]
    [Tooltip("Time follofing (less = sharp, more = smoother)")]
    [SerializeField] private float followSmoothTime = 0.14f;

    [Header("Look-Ahead")]
    [SerializeField] private float walkLookAhead = 0.7f;
    [SerializeField] private float sprintLookAhead = 1.8f;
    [SerializeField] private float lookAheadSmoothTime = 0.25f;

    [Header("Zoom And Sprint")]
    [SerializeField] private float zoomStep = 1f;
    [SerializeField] private float minZoom = 3.5f;
    [SerializeField] private float maxZoom = 10f;
    [SerializeField] private float zoomSmoothTime = 0.2f;
    [SerializeField] private float sprintZoomBonus = 1.15f;

    [Header("X-Ray")]
    public LayerMask treeLayer;
    public LayerMask bushesLayer;
    [Range(0.1f, 1f)] public float transparency = 0.5f;
    public float checkRadius = 1.5f;
    public float yOffsetCheck = 0.5f;

    private Camera cam;
    private PlayerMovement playerMovement;

    private float baseTargetZoom;
    private float currentZoomVelocity;
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
    }

    private void Start()
    {
        baseTargetZoom = cam.orthographicSize;
        if (target != null)
        {
            playerMovement = target.GetComponent<PlayerMovement>();
            transform.position = new Vector3(target.position.x + baseOffset.x, target.position.y + baseOffset.y, fixedZ);
        }
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
        float scrollInput = 0f;
        if (Mouse.current != null && (UIManager.Instance == null || !UIManager.Instance.IsGameplayInputBlocked))
        {
            float rawScroll = Mouse.current.scroll.ReadValue().y;
            if (rawScroll > 0f) scrollInput = 1f;
            else if (rawScroll < 0f) scrollInput = -1f;
        }

        if (scrollInput != 0f)
        {
            baseTargetZoom = Mathf.Clamp(baseTargetZoom - scrollInput * zoomStep, minZoom, maxZoom);
        }

        bool isSprinting = playerMovement != null && playerMovement.IsSprinting;
        float finalTargetZoom = baseTargetZoom + (isSprinting ? sprintZoomBonus : 0f);

        cam.orthographicSize = Mathf.SmoothDamp(
            cam.orthographicSize,
            finalTargetZoom,
            ref currentZoomVelocity,
            zoomSmoothTime
        );
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