using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "New Hoe", menuName = "Inventory/Tools/Hoe")]
public class HoeItemData : TerrainToolItemData
{
    [Header("Hoe Settings")]
    public TileBase tilledDirtTile;
    public TileBase[] tillableTiles;
    public TileBase[] nonTillableTiles;

    public override bool Use(ToolUseContext context)
    {
        Tilemap targetMap = GetTargetTilemap(context);
        if (targetMap == null) return false;

        TileBase farmlandTile = tilledDirtTile != null
            ? tilledDirtTile
            : (context.plantSystem != null ? context.plantSystem.TargetDirt : null);

        if (farmlandTile == null) return false;

        Vector3Int cellPos = targetMap.WorldToCell(context.targetWorldPos);
        TileBase currentTile = targetMap.GetTile(cellPos);
        if (currentTile == null || currentTile == farmlandTile) return false;

        if (context.plantSystem != null && context.plantSystem.IsCellOccupied(cellPos))
            return false;

        if (nonTillableTiles != null && nonTillableTiles.Contains(currentTile))
            return false;

        if (tillableTiles != null && tillableTiles.Length > 0 && !tillableTiles.Contains(currentTile))
            return false;

        if (IsCellBlockedByObstacle(targetMap, cellPos))
            return false;

        targetMap.SetTile(cellPos, farmlandTile);
        targetMap.SetTileFlags(cellPos, TileFlags.None);
        targetMap.SetColor(cellPos, Color.white);

        if (context.plantSystem != null && context.plantSystem.tilemap == null)
            context.plantSystem.tilemap = targetMap;

        SaveTerrainChange(cellPos, 3);
        return true;
    }
}