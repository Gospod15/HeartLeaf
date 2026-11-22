using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class WorldGenerator
{
    private float GetTerrainHeight(int worldX, int worldY)
    {
        float sampleX = (worldX + seedOffset.x) * noiseScale;
        float sampleY = (worldY + seedOffset.y) * noiseScale;

        float continental = Mathf.PerlinNoise(sampleX * 0.45f, sampleY * 0.45f);
        float hills = Mathf.PerlinNoise(sampleX, sampleY);
        float detail = Mathf.PerlinNoise(sampleX * 2.6f, sampleY * 2.6f);

        float raw = continental * 0.55f + hills * 0.35f + detail * 0.10f;

        float landmassHeight = Mathf.Clamp01(raw * 0.95f + 0.08f);
        return landmassHeight;
    }

    private float GetRiverCenterDistance(int worldX, int worldY)
    {
        float rx = (worldX + riverSeedOffset.x) * riverNoiseScale;
        float ry = (worldY + riverSeedOffset.y) * riverNoiseScale;

        float warp = (Mathf.PerlinNoise(rx * 2.2f, ry * 2.2f) - 0.5f) * 0.08f;
        float riverNoise = Mathf.PerlinNoise(rx, ry) + warp;
        return Mathf.Abs(riverNoise - 0.5f);
    }

    private float GetForestMoisture(int worldX, int worldY)
    {
        float fx = (worldX + forestSeedOffset.x) * forestNoiseScale;
        float fy = (worldY + forestSeedOffset.y) * forestNoiseScale;
        return Mathf.PerlinNoise(fx, fy);
    }

    private BiomeType DetermineBiome(int worldX, int worldY, float height)
    {
        if (height < waterLevel)
            return BiomeType.Lake;

        if (height >= mountainStartLevel)
            return BiomeType.Mountain;

        if (generateRivers)
        {
            float riverDist = GetRiverCenterDistance(worldX, worldY);
            if (riverDist < riverWidth)
                return BiomeType.River;

            if (riverDist < riverWidth + riverSandBankWidth)
                return BiomeType.Beach;
        }

        if (height < sandLevel)
            return BiomeType.Beach;

        float moisture = GetForestMoisture(worldX, worldY);
        return moisture >= forestThreshold ? BiomeType.Forest : BiomeType.Plains;
    }

    private void PlaceTerrainTile(Vector3Int cellPos, float height, BiomeType biome)
    {
        if (groundTilemap == null) return;

        if (biome == BiomeType.Lake || biome == BiomeType.River)
        {
            if (riverTilemap != null)
            {
                riverTilemap.SetTile(cellPos, waterTile);
                groundTilemap.SetTile(cellPos, sandTile != null ? sandTile : null);
            }
            else
            {
                groundTilemap.SetTile(cellPos, waterTile);
            }
            return;
        }

        if (riverTilemap != null)
            riverTilemap.SetTile(cellPos, null);

        if (biome == BiomeType.Beach)
        {
            groundTilemap.SetTile(cellPos, sandTile);
            return;
        }

        if (biome == BiomeType.Mountain && mountainTiles != null && mountainTiles.Length > 0)
        {
            float normalizedMountain = Mathf.InverseLerp(mountainStartLevel, 1f, height);
            int tierIndex = Mathf.Clamp(
                Mathf.FloorToInt(normalizedMountain * mountainTiles.Length),
                0,
                mountainTiles.Length - 1
            );

            groundTilemap.SetTile(cellPos, mountainTiles[tierIndex]);
            return;
        }

        groundTilemap.SetTile(cellPos, GetRandomGrassTile(cellPos.x, cellPos.y));
    }

    private TileBase GetRandomGrassTile(int worldX, int worldY)
    {
        if (grassVariationTiles != null && grassVariationTiles.Length > 0)
        {
            float roll = GetDeterministicCellRandom(worldX, worldY, 55);
            if (roll <= grassVariationChance)
            {
                int idx = Mathf.FloorToInt(GetDeterministicCellRandom(worldX, worldY, 56) * grassVariationTiles.Length);
                TileBase chosen = grassVariationTiles[Mathf.Clamp(idx, 0, grassVariationTiles.Length - 1)];
                if (chosen != null)
                    return chosen;
            }
        }

        return baseGrassTile;
    }

    private byte GetSavedCellState(Vector2Int chunkCoord, Vector2Int gridPos)
    {
        if (WorldSaveManager.instance == null) return 0;
        return WorldSaveManager.instance.GetCellState(chunkCoord, gridPos);
    }

    private void ApplySavedCellState(Vector3Int cellPos, byte state)
    {
        if (groundTilemap == null) return;

        if (state == 2)
        {
            groundTilemap.SetTile(cellPos, GetRandomGrassTile(cellPos.x, cellPos.y));
        }
        else if (state == 3 && tilledDirtTile != null)
        {
            groundTilemap.SetTile(cellPos, tilledDirtTile);
        }
        else if (state >= 11 && state <= 19 && holeTile != null)
        {
            int depth = state - 10;
            groundTilemap.SetTile(cellPos, holeTile);
            groundTilemap.SetTileFlags(cellPos, TileFlags.None);

            float brightness = depth == 1 ? 0.75f : (depth == 2 ? 0.52f : 0.32f);
            groundTilemap.SetColor(cellPos, new Color(brightness, brightness, brightness, 1f));

            if (depth >= 2 && holeCollisionTilemap != null)
            {
                holeCollisionTilemap.SetTile(cellPos, holeTile);
            }
        }
    }

    private void RestorePlayerPlacedObjects(Vector2Int chunkCoord)
    {
        if (WorldSaveManager.instance == null || groundTilemap == null) return;

        Dictionary<Vector2Int, string> placed = WorldSaveManager.instance.GetPlacedObjectsInChunk(chunkCoord);
        if (placed == null || placed.Count == 0) return;

        Transform parent = propsContainer != null ? propsContainer : transform;

        foreach (var kvp in placed)
        {
            Vector2Int gridPos = kvp.Key;
            string objectId = kvp.Value;

            if (occupiedCells.Contains(gridPos)) continue;

            GameObject prefab = WorldSaveManager.instance.GetPrefabById(objectId);
            if (prefab == null) continue;

            Vector3 worldPos = groundTilemap.GetCellCenterWorld(new Vector3Int(gridPos.x, gridPos.y, 0));
            GameObject spawned = Instantiate(prefab, worldPos, Quaternion.identity, parent);

            if (spawned.TryGetComponent(out WorldObject worldObj))
            {
                worldObj.objectId = objectId;
                worldObj.gridPosition = gridPos;
                worldObj.isPlayerBuilt = true;
            }

            occupiedCells.Add(gridPos);
            chunkSpawnedObjects[chunkCoord].Add(spawned);
        }
    }
}