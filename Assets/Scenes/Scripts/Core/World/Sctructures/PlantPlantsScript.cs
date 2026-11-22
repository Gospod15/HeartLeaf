using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PlantPlantsScript : MonoBehaviour
{
    public Tilemap tilemap;

    [Header("Growth Tiles Settings")]
    public TileBase[] LevelOfGrowthWheat;
    public ItemData wheatItemProduct;
    public ItemData wheatItemSeeds;
    public TileBase[] LevelOfGrowthWatermelon;

    [Header("Farmland Tile Settings")]
    public TileBase TargetDirt;
    public int timeofwheatgrowth = 10;

    private readonly HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();

    private Tilemap ResolveTilemap(Tilemap fallbackMap = null)
    {
        if (tilemap != null) return tilemap;
        if (fallbackMap != null)
        {
            tilemap = fallbackMap;
            return tilemap;
        }
        if (WorldGenerator.instance != null && WorldGenerator.instance.groundTilemap != null)
        {
            tilemap = WorldGenerator.instance.groundTilemap;
            return tilemap;
        }
        return null;
    }

    public bool IsCellOccupied(Vector3Int cellPos)
    {
        return occupiedCells.Contains(cellPos);
    }

    public bool PlantWheat(Vector3 plantPosition, Tilemap fallbackMap = null)
    {
        Tilemap activeMap = ResolveTilemap(fallbackMap);
        if (activeMap == null || TargetDirt == null || LevelOfGrowthWheat == null || LevelOfGrowthWheat.Length == 0)
            return false;

        Vector3Int cellPos = activeMap.WorldToCell(plantPosition);
        if (occupiedCells.Contains(cellPos))
            return false;

        TileBase currentTile = activeMap.GetTile(cellPos);
        if (currentTile == TargetDirt)
        {
            occupiedCells.Add(cellPos);
            StartCoroutine(GrowWheatRoutine(activeMap, cellPos));
            return true;
        }

        return false;
    }

    private IEnumerator GrowWheatRoutine(Tilemap activeMap, Vector3Int pos)
    {
        float waitTime = (float)timeofwheatgrowth / LevelOfGrowthWheat.Length;

        foreach (TileBase stageTile in LevelOfGrowthWheat)
        {
            if (activeMap == null) yield break;
            activeMap.SetTile(pos, stageTile);
            yield return new WaitForSeconds(waitTime);
        }
    }

    public bool TryHarvestPlant(Vector3 playerPos)
    {
        Tilemap activeMap = ResolveTilemap();
        if (activeMap == null || LevelOfGrowthWheat == null || LevelOfGrowthWheat.Length == 0)
            return false;

        TileBase matureWheat = LevelOfGrowthWheat[LevelOfGrowthWheat.Length - 1];
        Vector3Int centerCell = activeMap.WorldToCell(playerPos);

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector3Int checkCell = new Vector3Int(centerCell.x + x, centerCell.y + y, 0);
                if (activeMap.GetTile(checkCell) != matureWheat)
                    continue;

                occupiedCells.Remove(checkCell);
                activeMap.SetTile(checkCell, TargetDirt);

                Vector3 dropOrigin = activeMap.GetCellCenterWorld(checkCell);
                int wheatDropCount = Random.Range(1, 4);

                for (int i = 0; i < wheatDropCount; i++)
                {
                    if (wheatItemProduct != null)
                    {
                        Vector3 randomOffset = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.2f, 0.2f), 0f);
                        ItemInWorldManager.SpawnDrop(wheatItemProduct, dropOrigin + randomOffset, 1);
                    }
                }

                int seedDropCount = Random.Range(1, 3);
                if (InventoryManager.instance != null && wheatItemSeeds != null)
                {
                    InventoryManager.instance.AddItem(wheatItemSeeds, seedDropCount);
                }

                return true;
            }
        }

        return false;
    }
}