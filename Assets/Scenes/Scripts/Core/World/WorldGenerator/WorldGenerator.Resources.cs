using UnityEngine;

public partial class WorldGenerator
{
    private float GetDeterministicCellRandom(int worldX, int worldY, int salt)
    {
        int hash = seed ^ (worldX * 374761393) ^ (worldY * 668265263) ^ (salt * 1442695041);
        hash = (hash ^ (hash >> 13)) * 1274126177;
        hash ^= (hash >> 16);
        return (hash & 0x7FFFFFFF) / (float)int.MaxValue;
    }

    private void SpawnBiomeObjects(Vector2Int chunkCoord, Vector3Int cellPos, float height, BiomeType biome)
    {
        switch (biome)
        {
            case BiomeType.Lake:
            case BiomeType.River:
                return;

            case BiomeType.Beach:
                TrySpawnSmallStone(chunkCoord, cellPos, beachSmallStoneChance);
                break;

            case BiomeType.Forest:
                {
                    float moisture = GetForestMoisture(cellPos.x, cellPos.y);
                    float forestDepth = Mathf.InverseLerp(forestThreshold, Mathf.Min(1f, forestThreshold + 0.25f), moisture);
                    float treeChance = Mathf.Lerp(forestTreeChance * 0.65f, forestTreeChance, forestDepth);

                    if (TrySpawnTree(chunkCoord, cellPos, treeChance)) return;
                    if (TrySpawnBush(chunkCoord, cellPos, forestBushChance)) return;
                    if (TrySpawnRock(chunkCoord, cellPos, GetFoothillRockChance(height))) return;
                    TrySpawnSmallStone(chunkCoord, cellPos, plainsSmallStoneChance * 0.6f);
                    break;
                }

            case BiomeType.Plains:
                {
                    float moisture = GetForestMoisture(cellPos.x, cellPos.y);
                    float edgeFactor = Mathf.InverseLerp(forestThreshold - forestEdgeWidth, forestThreshold, moisture);
                    float edgeCurve = edgeFactor * edgeFactor;

                    float edgeTreeChance = edgeCurve * forestEdgeTreeChance;
                    if (edgeTreeChance > 0f && TrySpawnTree(chunkCoord, cellPos, edgeTreeChance)) return;

                    float bushChance = plainsBushChance + (edgeCurve * forestEdgeBushChance);
                    if (TrySpawnBush(chunkCoord, cellPos, bushChance)) return;

                    float rockChance = GetFoothillRockChance(height);
                    if (TrySpawnRock(chunkCoord, cellPos, rockChance)) return;

                    float smallStoneChance = Mathf.Lerp(plainsSmallStoneChance * 0.6f, plainsSmallStoneChance * 1.4f, GetFoothillFactor(height));
                    TrySpawnSmallStone(chunkCoord, cellPos, smallStoneChance);
                    break;
                }

            case BiomeType.Mountain:
                float mountainDepth = Mathf.InverseLerp(mountainStartLevel, 1f, height);

                if (TrySpawnMineZone(chunkCoord, cellPos, mountainDepth)) return;
                if (TrySpawnMountainOre(chunkCoord, cellPos, mountainDepth)) return;
                if (TrySpawnRock(chunkCoord, cellPos, mountainRockChance)) return;
                TrySpawnSmallStone(chunkCoord, cellPos, mountainSmallStoneChance);
                break;
        }
    }

    private float GetFoothillFactor(float height)
    {
        float foothillStart = Mathf.Max(sandLevel, mountainStartLevel - 0.18f);
        return Mathf.InverseLerp(foothillStart, mountainStartLevel, height);
    }

    private float GetFoothillRockChance(float height)
    {
        float factor = GetFoothillFactor(height);
        return Mathf.Lerp(plainsBaseRockChance, plainsFoothillRockChance, factor * factor);
    }

    private bool TrySpawnTree(Vector2Int chunkCoord, Vector3Int cellPos, float chance)
    {
        if (chance <= 0f || treePrefabs == null || treePrefabs.Length == 0) return false;

        float roll = GetDeterministicCellRandom(cellPos.x, cellPos.y, 77);
        if (roll > chance) return false;

        int prefabIndex = Mathf.FloorToInt(GetDeterministicCellRandom(cellPos.x, cellPos.y, 78) * treePrefabs.Length);
        GameObject prefab = treePrefabs[Mathf.Clamp(prefabIndex, 0, treePrefabs.Length - 1)];

        Transform targetParent = treeContainer != null ? treeContainer : propsContainer;
        return SpawnPropAtCell(chunkCoord, cellPos, prefab, targetParent) != null;
    }

    private bool TrySpawnBush(Vector2Int chunkCoord, Vector3Int cellPos, float chance)
    {
        if (chance <= 0f || bushPrefabs == null || bushPrefabs.Length == 0) return false;

        float roll = GetDeterministicCellRandom(cellPos.x, cellPos.y, 88);
        if (roll > chance) return false;

        int prefabIndex = Mathf.FloorToInt(GetDeterministicCellRandom(cellPos.x, cellPos.y, 89) * bushPrefabs.Length);
        GameObject prefab = bushPrefabs[Mathf.Clamp(prefabIndex, 0, bushPrefabs.Length - 1)];

        Transform targetParent = bushContainer != null ? bushContainer : propsContainer;
        return SpawnPropAtCell(chunkCoord, cellPos, prefab, targetParent) != null;
    }

    private bool TrySpawnRock(Vector2Int chunkCoord, Vector3Int cellPos, float chance)
    {
        if (chance <= 0f || rockPrefabs == null || rockPrefabs.Length == 0) return false;

        float roll = GetDeterministicCellRandom(cellPos.x, cellPos.y, 101);
        if (roll > chance) return false;

        int prefabIndex = Mathf.FloorToInt(GetDeterministicCellRandom(cellPos.x, cellPos.y, 102) * rockPrefabs.Length);
        GameObject prefab = rockPrefabs[Mathf.Clamp(prefabIndex, 0, rockPrefabs.Length - 1)];

        return SpawnPropAtCell(chunkCoord, cellPos, prefab, propsContainer) != null;
    }

    private bool TrySpawnSmallStone(Vector2Int chunkCoord, Vector3Int cellPos, float chance)
    {
        if (chance <= 0f || smallStonePrefabs == null || smallStonePrefabs.Length == 0) return false;

        float roll = GetDeterministicCellRandom(cellPos.x, cellPos.y, 131);
        if (roll > chance) return false;

        int prefabIndex = Mathf.FloorToInt(GetDeterministicCellRandom(cellPos.x, cellPos.y, 132) * smallStonePrefabs.Length);
        GameObject prefab = smallStonePrefabs[Mathf.Clamp(prefabIndex, 0, smallStonePrefabs.Length - 1)];

        GameObject spawned = SpawnPropAtCell(chunkCoord, cellPos, prefab, propsContainer);
        if (spawned != null)
        {
            if (!spawned.TryGetComponent(out WorldObject worldObj))
            {
                worldObj = spawned.AddComponent<WorldObject>();
                worldObj.gridPosition = new Vector2Int(cellPos.x, cellPos.y);
                worldObj.isPlayerBuilt = false;
            }

            if (spawned.TryGetComponent(out ItemInWorldManager worldItem))
            {
                worldItem.isNaturalSpawn = true;
            }
            return true;
        }
        return false;
    }

    private bool TrySpawnMountainOre(Vector2Int chunkCoord, Vector3Int cellPos, float mountainDepth)
    {
        if (mountainOres == null || mountainOres.Length == 0) return false;

        for (int i = 0; i < mountainOres.Length; i++)
        {
            OreSpawnConfig ore = mountainOres[i];
            if (ore == null || ore.orePrefabs == null || ore.orePrefabs.Length == 0)
                continue;

            if (mountainDepth < ore.minMountainDepth)
                continue;

            float roll = GetDeterministicCellRandom(cellPos.x, cellPos.y, 200 + i * 17);
            if (roll <= ore.spawnChance)
            {
                int variantIdx = Mathf.FloorToInt(
                    GetDeterministicCellRandom(cellPos.x, cellPos.y, 300 + i * 31) * ore.orePrefabs.Length
                );
                GameObject selectedOrePrefab = ore.orePrefabs[Mathf.Clamp(variantIdx, 0, ore.orePrefabs.Length - 1)];

                SpawnPropAtCell(chunkCoord, cellPos, selectedOrePrefab, propsContainer);
                return true;
            }
        }

        return false;
    }

    private GameObject SpawnPropAtCell(Vector2Int chunkCoord, Vector3Int cellPos, GameObject prefab, Transform customParent = null)
    {
        if (prefab == null || groundTilemap == null) return null;

        Vector2Int gridPos = new Vector2Int(cellPos.x, cellPos.y);
        if (occupiedCells.Contains(gridPos)) return null;

        Vector3 worldPos = groundTilemap.GetCellCenterWorld(cellPos);
        Transform parent = customParent != null ? customParent : (propsContainer != null ? propsContainer : transform);

        GameObject obj = Instantiate(prefab, worldPos, Quaternion.identity, parent);

        if (obj.TryGetComponent(out WorldObject worldObj))
        {
            worldObj.gridPosition = gridPos;
            worldObj.isPlayerBuilt = false;
        }

        occupiedCells.Add(gridPos);
        chunkSpawnedObjects[chunkCoord].Add(obj);
        return obj;
    }
}