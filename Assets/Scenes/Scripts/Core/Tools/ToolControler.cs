using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;

public class ToolController : MonoBehaviour
{
    [Header("World References")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap holeCollisionTilemap;
    [SerializeField] private WorldGenerator worldGenerator;
    [SerializeField] private PlantPlantsScript plantSystem;

    [Header("Player References")]
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private Camera mainCamera;

    private ToolItemData currentTool;

    private void Awake()
    {
        if (playerStats == null) playerStats = GetComponent<PlayerStats>();
        if (mainCamera == null) mainCamera = Camera.main;
        if (plantSystem == null) plantSystem = FindFirstObjectByType<PlantPlantsScript>();
    }

    private void Update()
    {
        if (UIManager.Instance != null && UIManager.Instance.IsGameplayInputBlocked)
            return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryUseEquippedTool();
        }
    }

    public void Equip(ItemData item)
    {
        currentTool = item as ToolItemData;
    }

    private void TryUseEquippedTool()
    {
        if (currentTool == null || mainCamera == null) return;

        if (playerStats != null && playerStats.CurrentStamina < currentTool.staminaCost)
            return;

        Vector3 targetPos = GetTargetWorldPosition(currentTool.useRange);

        WorldGenerator activeGenerator = worldGenerator != null ? worldGenerator : WorldGenerator.instance;
        Tilemap activeTilemap = groundTilemap != null
            ? groundTilemap
            : (activeGenerator != null ? activeGenerator.groundTilemap : null);

        ToolUseContext context = new ToolUseContext
        {
            playerWorldPos = transform.position,
            targetWorldPos = targetPos,
            playerStats = playerStats,
            groundTilemap = activeTilemap,
            holeCollisionTilemap = holeCollisionTilemap,
            worldGenerator = activeGenerator,
            plantSystem = plantSystem,
            inventory = InventoryManager.instance
        };

        bool usedSuccessfully = currentTool.Use(context);

        if (usedSuccessfully && playerStats != null && currentTool.staminaCost > 0f)
        {
            playerStats.TrySpendStamina(currentTool.staminaCost);
        }
    }

    private Vector3 GetTargetWorldPosition(float maxRange)
    {
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, -mainCamera.transform.position.z));
        mouseWorldPos.z = 0f;

        Vector3 direction = mouseWorldPos - transform.position;
        if (direction.magnitude > maxRange)
        {
            return transform.position + direction.normalized * maxRange;
        }

        return mouseWorldPos;
    }
}