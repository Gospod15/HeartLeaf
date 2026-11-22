using UnityEngine;
using UnityEngine.Tilemaps;

public abstract class ToolItemData : ItemData
{
    [Header("Tool Stats")]
    public float baseSpeed = 1f;
    public float useRange = 2f;
    public float staminaCost = 5f;

    public float GetFinalSpeed()
    {
        float multiplier = rarity switch
        {
            ItemRarity.Rare => 1.25f,
            ItemRarity.Epic => 1.5f,
            ItemRarity.Legendary => 2.0f,
            _ => 1.0f
        };
        return baseSpeed * multiplier;
    }

    public override string GetTooltipStats()
    {
        return $"Work Speed: {GetFinalSpeed():0.##}";
    }

    public abstract bool Use(ToolUseContext context);
}

public abstract class TerrainToolItemData : ToolItemData
{
    [Header("Terrain Rules")]
    [SerializeField] protected LayerMask blockingObstaclesMask;

    protected Tilemap GetTargetTilemap(ToolUseContext context)
    {
        if (context.groundTilemap != null)
            return context.groundTilemap;

        if (context.plantSystem != null && context.plantSystem.tilemap != null)
            return context.plantSystem.tilemap;

        if (WorldGenerator.instance != null)
            return WorldGenerator.instance.groundTilemap;

        return null;
    }

    protected bool IsCellBlockedByObstacle(Tilemap map, Vector3Int cellPos)
    {
        if (blockingObstaclesMask.value == 0) return false;

        Vector3 center = map.GetCellCenterWorld(cellPos);
        Vector2 boxSize = (Vector2)map.cellSize * 0.7f;
        return Physics2D.OverlapBox(center, boxSize, 0f, blockingObstaclesMask) != null;
    }

    protected void SaveTerrainChange(Vector3Int cellPos, byte changeId)
    {
        if (WorldSaveManager.instance != null)
        {
            WorldSaveManager.instance.RecordNaturalChange(new Vector2Int(cellPos.x, cellPos.y), changeId);
        }
    }
}