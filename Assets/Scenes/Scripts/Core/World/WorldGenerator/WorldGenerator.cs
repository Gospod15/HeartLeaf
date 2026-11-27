using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum OreType
{
    Coal,      
    Copper,
    Iron,     
    Emerald,  
    Uranium   
}

public enum BiomeType
{
    Lake,   
    River,     
    Beach,    
    Plains,    
    Forest,    
    Mountain  
}

[Serializable]
public class OreSpawnConfig
{
    public string oreName;
    public OreType oreType;
    public GameObject[] orePrefabs;

    [Range(0f, 1f)]
    public float spawnChance = 0.03f;

    [Range(0f, 1f)]
    public float minMountainDepth = 0.1f;
}

[Serializable]
public class WorldGenConfigData
{
    public int seed = 12345;
    public bool useRandomSeed = false;
    public bool loadSavedWorld = true;
    public int chunkSize = 16;
    public int cameraChunkPadding = 2;
    public int unloadBufferChunks = 2;

    public float noiseScale = 0.0012f;
    public float waterLevel = 0.11f;
    public float sandLevel = 0.15f;
    public float mountainStartLevel = 0.74f;
    public float forestNoiseScale = 0.0012f;
    public float forestThreshold = 0.54f;

    public bool generateRivers = true;
    public float riverNoiseScale = 0.0012f;
    public float riverWidth = 0.022f;
    public float riverSandBankWidth = 0.011f;

    public float grassVariationChance = 0.25f;

    public float forestTreeChance = 0.14f;
    public float forestEdgeTreeChance = 0.035f;
    public float forestEdgeWidth = 0.1f;

    public float forestBushChance = 0.004f;
    public float forestEdgeBushChance = 0.0005f;
    public float plainsBushChance = 0f;

    public float plainsFoothillRockChance = 0.005f;
    public float plainsBaseRockChance = 0.0004f;
    public float mountainRockChance = 0.045f;

    public float plainsSmallStoneChance = 0.0025f;
    public float beachSmallStoneChance = 0.005f;
    public float mountainSmallStoneChance = 0.009f;

    public float mineEntranceChance = 0.004f;
    public int mineDarkenRadius = 3;
}

public partial class WorldGenerator : MonoBehaviour
{
    public static WorldGenerator instance;

    [Header("JSON Конфіг Генератора")]
    public TextAsset configJsonAsset;

    [Header("Тайлмапи та Контейнери")]
    public Tilemap groundTilemap;
    public Tilemap riverTilemap;
    public Tilemap decorationTilemap;
    public Transform propsContainer;
    public Transform treeContainer;
    public Transform bushContainer;
    public Transform playerTransform;
    public Camera targetCamera;

    [Header("Тайли Рельєфу та Змін")]
    public TileBase waterTile;
    public TileBase sandTile;
    public TileBase baseGrassTile;
    public TileBase[] grassVariationTiles;
    public TileBase tilledDirtTile;
    public TileBase holeTile;
    public Tilemap holeCollisionTilemap;
    public TileBase[] mountainTiles;

    [Header("Префаби Об'єктів Світу")]
    public GameObject[] treePrefabs;
    public GameObject[] bushPrefabs;
    public GameObject[] rockPrefabs;
    public GameObject[] smallStonePrefabs;
    public OreSpawnConfig[] mountainOres;
    public GameObject mineEntrancePrefab;
    public Color mineDarkTint = new Color(0.42f, 0.42f, 0.50f, 1f);

    private int seed = 12345;
    private bool useRandomSeed = false;
    private bool loadSavedWorld = true;
    private int chunkSize = 16;
    private int cameraChunkPadding = 2;
    private int unloadBufferChunks = 2;

    private float noiseScale = 0.0012f;
    private float waterLevel = 0.11f;
    private float sandLevel = 0.15f;
    private float mountainStartLevel = 0.74f;
    private float forestNoiseScale = 0.0012f;
    private float forestThreshold = 0.54f;

    private bool generateRivers = true;
    private float riverNoiseScale = 0.0012f;
    private float riverWidth = 0.022f;
    private float riverSandBankWidth = 0.011f;

    private float grassVariationChance = 0.25f;

    private float forestTreeChance = 0.14f;
    private float forestEdgeTreeChance = 0.035f;
    private float forestEdgeWidth = 0.1f;

    private float forestBushChance = 0.004f;
    private float forestEdgeBushChance = 0.0005f;
    private float plainsBushChance = 0f;

    private float plainsFoothillRockChance = 0.005f;
    private float plainsBaseRockChance = 0.0004f;
    private float mountainRockChance = 0.045f;

    private float plainsSmallStoneChance = 0.0025f;
    private float beachSmallStoneChance = 0.005f;
    private float mountainSmallStoneChance = 0.009f;

    private float mineEntranceChance = 0.004f;
    private int mineDarkenRadius = 3;

    private readonly HashSet<Vector2Int> loadedChunks = new HashSet<Vector2Int>();
    private readonly Dictionary<Vector2Int, List<GameObject>> chunkSpawnedObjects = new Dictionary<Vector2Int, List<GameObject>>();
    private readonly HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
    private readonly List<Vector2Int> chunksToUnloadBuffer = new List<Vector2Int>();

    private Vector2 seedOffset;
    private Vector2 riverSeedOffset;
    private Vector2 forestSeedOffset;
    private Vector2Int lastCenterChunk = new Vector2Int(int.MinValue, int.MinValue);
    private int lastLoadRadiusX = -1;
    private int lastLoadRadiusY = -1;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        if (targetCamera == null) targetCamera = Camera.main;

        LoadConfigFromJson();
    }

    private void LoadConfigFromJson()
    {
        string jsonText = null;

#if UNITY_EDITOR
        string[] found = Directory.GetFiles(Application.dataPath, "world_gen_config.json", SearchOption.AllDirectories);
        if (found.Length > 0)
        {
            jsonText = File.ReadAllText(found[0]);
        }
#endif

        if (string.IsNullOrEmpty(jsonText) && configJsonAsset != null)
        {
            jsonText = configJsonAsset.text;
        }

        if (string.IsNullOrEmpty(jsonText))
        {
            Debug.LogWarning("world_gen_config.json не знайдено використовуються базові налаштування");
            return;
        }

        WorldGenConfigData cfg = JsonUtility.FromJson<WorldGenConfigData>(jsonText);
        if (cfg == null)
        {
            Debug.LogError("Помилка парсингу world_gen_config.json");
            return;
        }

        seed = cfg.seed;
        useRandomSeed = cfg.useRandomSeed;
        loadSavedWorld = cfg.loadSavedWorld;
        chunkSize = Mathf.Max(4, cfg.chunkSize);
        cameraChunkPadding = Mathf.Max(1, cfg.cameraChunkPadding);
        unloadBufferChunks = Mathf.Max(1, cfg.unloadBufferChunks);

        noiseScale = cfg.noiseScale;
        waterLevel = cfg.waterLevel;
        sandLevel = cfg.sandLevel;
        mountainStartLevel = cfg.mountainStartLevel;
        forestNoiseScale = cfg.forestNoiseScale;
        forestThreshold = cfg.forestThreshold;

        generateRivers = cfg.generateRivers;
        riverNoiseScale = cfg.riverNoiseScale;
        riverWidth = cfg.riverWidth;
        riverSandBankWidth = cfg.riverSandBankWidth;

        grassVariationChance = cfg.grassVariationChance;

        forestTreeChance = cfg.forestTreeChance;
        forestEdgeTreeChance = cfg.forestEdgeTreeChance;
        forestEdgeWidth = cfg.forestEdgeWidth;

        forestBushChance = cfg.forestBushChance;
        forestEdgeBushChance = cfg.forestEdgeBushChance;
        plainsBushChance = cfg.plainsBushChance;

        plainsFoothillRockChance = cfg.plainsFoothillRockChance;
        plainsBaseRockChance = cfg.plainsBaseRockChance;
        mountainRockChance = cfg.mountainRockChance;

        plainsSmallStoneChance = cfg.plainsSmallStoneChance;
        beachSmallStoneChance = cfg.beachSmallStoneChance;
        mountainSmallStoneChance = cfg.mountainSmallStoneChance;

        mineEntranceChance = cfg.mineEntranceChance;
        mineDarkenRadius = cfg.mineDarkenRadius;
    }

    private void Start()
    {
        if (loadSavedWorld && WorldSaveManager.instance != null && WorldSaveManager.instance.LoadWorld(out int savedSeed))
        {
            seed = savedSeed;
            InitSeedOffset();
            UpdateChunksAroundCamera(true);
            WorldSaveManager.instance.RestoreDroppedItems();
        }
        else
        {
            if (useRandomSeed)
                seed = UnityEngine.Random.Range(-999999, 999999);

            InitSeedOffset();
            UpdateChunksAroundCamera(true);
        }
    }

    private void Update()
    {
        UpdateChunksAroundCamera(false);
    }

    private void InitSeedOffset()
    {
        System.Random prng = new System.Random(seed);
        seedOffset = new Vector2(prng.Next(-10000, 10000), prng.Next(-10000, 10000));
        riverSeedOffset = new Vector2(prng.Next(-10000, 10000), prng.Next(-10000, 10000));
        forestSeedOffset = new Vector2(prng.Next(-10000, 10000), prng.Next(-10000, 10000));
    }
    
    //Перезавантаження
    public void InvalidateCellCache(Vector2Int cellPos) => occupiedCells.Remove(cellPos);
    public void InvalidateCellCache(Vector3Int cellPos) => occupiedCells.Remove(new Vector2Int(cellPos.x, cellPos.y));
    public void InvalidateCellCache(Vector3 worldPos)
    {
        if (groundTilemap == null) return;
        Vector3Int cell = groundTilemap.WorldToCell(worldPos);
        occupiedCells.Remove(new Vector2Int(cell.x, cell.y));
    }

    public bool IsCellOccupied(Vector2Int cellPos) => occupiedCells.Contains(cellPos);
    public bool IsCellOccupied(Vector3Int cellPos) => occupiedCells.Contains(new Vector2Int(cellPos.x, cellPos.y));
    public void RegisterOccupiedCell(Vector2Int cellPos) => occupiedCells.Add(cellPos);

    private void UpdateChunksAroundCamera(bool forceUpdate)
    {
        if (targetCamera == null) targetCamera = Camera.main;

        Vector3 focusPos = targetCamera != null
            ? targetCamera.transform.position
            : (playerTransform != null ? playerTransform.position : Vector3.zero);

        Vector2Int centerChunk = WorldToChunkCoord(focusPos);

        int radiusX = cameraChunkPadding + 1;
        int radiusY = cameraChunkPadding + 1;

        if (targetCamera != null && targetCamera.orthographic)
        {
            float halfHeight = targetCamera.orthographicSize;
            float halfWidth = halfHeight * targetCamera.aspect;
            radiusX = Mathf.CeilToInt(halfWidth / chunkSize) + cameraChunkPadding;
            radiusY = Mathf.CeilToInt(halfHeight / chunkSize) + cameraChunkPadding;
        }

        if (!forceUpdate && centerChunk == lastCenterChunk && radiusX == lastLoadRadiusX && radiusY == lastLoadRadiusY)
            return;

        lastCenterChunk = centerChunk;
        lastLoadRadiusX = radiusX;
        lastLoadRadiusY = radiusY;

        for (int x = -radiusX; x <= radiusX; x++)
        {
            for (int y = -radiusY; y <= radiusY; y++)
            {
                Vector2Int coord = new Vector2Int(centerChunk.x + x, centerChunk.y + y);
                if (!loadedChunks.Contains(coord))
                {
                    GenerateChunk(coord);
                    loadedChunks.Add(coord);
                }
            }
        }

        int unloadLimitX = radiusX + unloadBufferChunks;
        int unloadLimitY = radiusY + unloadBufferChunks;

        chunksToUnloadBuffer.Clear();
        foreach (var coord in loadedChunks)
        {
            if (Mathf.Abs(coord.x - centerChunk.x) > unloadLimitX ||
                Mathf.Abs(coord.y - centerChunk.y) > unloadLimitY)
            {
                chunksToUnloadBuffer.Add(coord);
            }
        }

        for (int i = 0; i < chunksToUnloadBuffer.Count; i++)
        {
            UnloadChunk(chunksToUnloadBuffer[i]);
        }
    }

    public Vector2Int WorldToChunkCoord(Vector3 worldPos)
    {
        int cx = Mathf.FloorToInt(worldPos.x / chunkSize);
        int cy = Mathf.FloorToInt(worldPos.y / chunkSize);
        return new Vector2Int(cx, cy);
    }

    private void GenerateChunk(Vector2Int chunkCoord)
    {
        if (!chunkSpawnedObjects.ContainsKey(chunkCoord))
            chunkSpawnedObjects[chunkCoord] = new List<GameObject>();

        int startX = chunkCoord.x * chunkSize;
        int startY = chunkCoord.y * chunkSize;

        for (int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < chunkSize; y++)
            {
                int worldX = startX + x;
                int worldY = startY + y;
                Vector2Int gridPos = new Vector2Int(worldX, worldY);
                Vector3Int cellPos = new Vector3Int(worldX, worldY, 0);

                float height = GetTerrainHeight(worldX, worldY);
                BiomeType biome = DetermineBiome(worldX, worldY, height);
                PlaceTerrainTile(cellPos, height, biome);

                byte savedState = GetSavedCellState(chunkCoord, gridPos);
                if (savedState != 0)
                {
                    ApplySavedCellState(cellPos, savedState);
                    continue;
                }

                SpawnBiomeObjects(chunkCoord, cellPos, height, biome);
            }
        }

        RestorePlayerPlacedObjects(chunkCoord);
    }

    private void UnloadChunk(Vector2Int chunkCoord)
    {
        if (chunkSpawnedObjects.TryGetValue(chunkCoord, out var objects))
        {
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null)
                    Destroy(objects[i]);
            }
            objects.Clear();
            chunkSpawnedObjects.Remove(chunkCoord);
        }

        int startX = chunkCoord.x * chunkSize;
        int startY = chunkCoord.y * chunkSize;

        for (int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < chunkSize; y++)
            {
                int worldX = startX + x;
                int worldY = startY + y;
                Vector3Int cellPos = new Vector3Int(worldX, worldY, 0);

                occupiedCells.Remove(new Vector2Int(worldX, worldY));

                if (groundTilemap != null)
                {
                    groundTilemap.SetTile(cellPos, null);
                    groundTilemap.SetColor(cellPos, Color.white);
                }

                if (riverTilemap != null)
                {
                    riverTilemap.SetTile(cellPos, null);
                }

                if (holeCollisionTilemap != null)
                {
                    holeCollisionTilemap.SetTile(cellPos, null);
                }
            }
        }

        loadedChunks.Remove(chunkCoord);
    }
}