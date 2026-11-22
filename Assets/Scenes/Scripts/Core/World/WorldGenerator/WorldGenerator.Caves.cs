using UnityEngine;
using UnityEngine.Tilemaps;

public partial class WorldGenerator
{
    private bool TrySpawnMineZone(Vector2Int chunkCoord, Vector3Int cellPos, float mountainDepth)
    {
        if (mountainDepth < 0.25f) return false;

        float roll = GetDeterministicCellRandom(cellPos.x, cellPos.y, 777);
        if (roll > mineEntranceChance) return false;

        if (mineEntrancePrefab != null)
        {
            SpawnPropAtCell(chunkCoord, cellPos, mineEntrancePrefab, propsContainer);
        }

        DarkenTilesAroundMine(cellPos, mineDarkenRadius);
        return true;
    }

    private void DarkenTilesAroundMine(Vector3Int centerCell, int radius)
    {
        if (groundTilemap == null || radius <= 0) return;

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                Vector3Int targetCell = new Vector3Int(centerCell.x + dx, centerCell.y + dy, 0);
                if (!groundTilemap.HasTile(targetCell)) continue;

                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist > radius) continue;

                float t = Mathf.Clamp01(dist / radius);
                Color finalColor = Color.Lerp(mineDarkTint, Color.white, t);

                groundTilemap.SetTileFlags(targetCell, TileFlags.None);
                groundTilemap.SetColor(targetCell, finalColor);
            }
        }
    }
}