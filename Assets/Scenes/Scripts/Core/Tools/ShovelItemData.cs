using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "New Shovel", menuName = "Inventory/Tools/Shovel")]
public class ShovelItemData : TerrainToolItemData
{
    [Header("Shovel Hole Settings")]
    public TileBase holeTile;
    public TileBase[] diggableTiles;
    public TileBase[] blockedTiles;

    [Header("Depth & Collision")]
    [Min(1)] public int maxDepth = 3;
    [Min(1)] public int collisionStartDepth = 2;
    public TileBase collisionColliderTile;
    public Color[] depthColors = new Color[]
    {
        new Color(0.75f, 0.75f, 0.75f, 1f),
        new Color(0.52f, 0.52f, 0.52f, 1f),
        new Color(0.32f, 0.32f, 0.32f, 1f)
    };

    private static readonly Dictionary<Vector3Int, int> cellDepths = new Dictionary<Vector3Int, int>();

    public static int GetHoleDepth(Vector3Int cellPos)
    {
        return cellDepths.TryGetValue(cellPos, out int depth) ? depth : 0;
    }

    public static void ClearHoleData()
    {
        cellDepths.Clear();
    }

    public override string GetTooltipStats()
    {
        return $"Dig Speed: {GetFinalSpeed():0.##}\nMax Hole Depth: {maxDepth}";
    }

    public override bool Use(ToolUseContext context)
    {
        Tilemap targetMap = GetTargetTilemap(context);
        if (targetMap == null || holeTile == null) return false;

        Vector3Int cellPos = targetMap.WorldToCell(context.targetWorldPos);
        TileBase currentTile = targetMap.GetTile(cellPos);
        if (currentTile == null) return false;

        if (context.plantSystem != null && context.plantSystem.IsCellOccupied(cellPos))
            return false;

        if (IsCellBlockedByObstacle(targetMap, cellPos))
            return false;

        int currentDepth = GetHoleDepth(cellPos);

        if (currentDepth == 0)
        {
            if (blockedTiles != null && blockedTiles.Contains(currentTile))
                return false;

            if (diggableTiles != null && diggableTiles.Length > 0 && !diggableTiles.Contains(currentTile))
                return false;
        }

        if (currentDepth >= maxDepth)
            return false;

        int nextDepth = currentDepth + 1;
        cellDepths[cellPos] = nextDepth;

        targetMap.SetTile(cellPos, holeTile);
        targetMap.SetTileFlags(cellPos, TileFlags.None);
        targetMap.SetColor(cellPos, GetColorForDepth(nextDepth));

        if (nextDepth >= collisionStartDepth && context.holeCollisionTilemap != null)
        {
            TileBase colliderTile = collisionColliderTile != null ? collisionColliderTile : holeTile;
            context.holeCollisionTilemap.SetTile(cellPos, colliderTile);
        }

        SaveTerrainChange(cellPos, (byte)(10 + nextDepth));
        return true;
    }

    private Color GetColorForDepth(int depth)
    {
        if (depthColors != null && depthColors.Length > 0)
        {
            int idx = Mathf.Clamp(depth - 1, 0, depthColors.Length - 1);
            return depthColors[idx];
        }

        float brightness = Mathf.Lerp(0.75f, 0.3f, (float)(depth - 1) / Mathf.Max(1, maxDepth - 1));
        return new Color(brightness, brightness, brightness, 1f);
    }
}