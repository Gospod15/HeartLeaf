using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.Linq;

public class WorldGenerator : MonoBehaviour
{
    public static WorldGenerator instance;

    [Header("Mining Settings")]
    public ItemData stoneItem;

    [Header("Налаштування Чанків")]
    public int chunkSize = 25; 
    public int tileBuffer = 5;
    
    [Header("Сід")]
    public int seed;
    public bool randomSeed = true;

    [Header("Компоненти")]
    public Camera mainCamera;      
    public Transform treeParent;
    public Transform BushParent;

    [Header("Тайлмапи")]
    public Tilemap groundTilemap;  
    
    [Header("Тайли")]
    public TileBase waterTile;
    public TileBase sandTile;
    public TileBase[] grassTiles;
    public TileBase[] DetailsTileOnGrass;
    public TileBase mountainTile; 

    [Header("Рослинність")]
    public GameObject[] BushPrefabs;
    public GameObject[] treePrefabs;
    public LayerMask treeLayer; 
    public LayerMask BushLayer; 

    [Header("Біоми")]
    public float scale = 0.1f;
    [Range(0, 1)] public float waterLevel = 0.2f;
    [Range(0, 1)] public float sandThreshold = 0.25f;
    [Range(0, 1)] public float mountainLevel = 0.85f;
    [Range(0, 1)] public float forestThreshold = 0.4f;
    [Range(0, 1)] public float BushZones = 5f;

    private Dictionary<Vector2Int, List<GameObject>> activeChunks = new Dictionary<Vector2Int, List<GameObject>>();

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        if (randomSeed) {
            seed = Random.Range(-1000,-1000);
            Debug.Log("Сгенерований сід світу" + seed);
        }

        if (treeParent == null) Debug.Log("Немає батьківского об'єкта дерева");
        if (mainCamera == null) mainCamera = Camera.main;

        UpdateChunks();
    }

    void Update()
    {
        UpdateChunks();
    }


    void UpdateChunks()
    {
        if (mainCamera == null) return;

        float cameraHeight = mainCamera.orthographicSize;
        float cameraWidth = cameraHeight * mainCamera.aspect;
        Vector2 camPos = mainCamera.transform.position;

        float minX = camPos.x - cameraWidth - tileBuffer;
        float maxX = camPos.x + cameraWidth + tileBuffer;
        float minY = camPos.y - cameraHeight - tileBuffer;
        float maxY = camPos.y + cameraHeight + tileBuffer;

        int minChunkX = Mathf.FloorToInt(minX / chunkSize);
        int maxChunkX = Mathf.FloorToInt(maxX / chunkSize);
        int minChunkY = Mathf.FloorToInt(minY / chunkSize);
        int maxChunkY = Mathf.FloorToInt(maxY / chunkSize);

        List<Vector2Int> visibleChunks = new List<Vector2Int>();
        for (int x = minChunkX; x <= maxChunkX; x++)
        {
            for (int y = minChunkY; y <= maxChunkY; y++)
            {
                visibleChunks.Add(new Vector2Int(x, y));
            }
        }

        List<Vector2Int> chunksToUnload = new List<Vector2Int>();
        foreach (var chunk in activeChunks.Keys)
        {
            if (!visibleChunks.Contains(chunk)) chunksToUnload.Add(chunk);
        }
        foreach (var chunk in chunksToUnload) UnloadChunk(chunk);

        foreach (var chunk in visibleChunks)
        {
            if (!activeChunks.ContainsKey(chunk)) GenerateChunk(chunk);
        }
    }

    void UnloadChunk(Vector2Int chunkCoord)
    {
        if (activeChunks.TryGetValue(chunkCoord, out List<GameObject> trees))
        {
            foreach (var tree in trees) if (tree != null) Destroy(tree);
            activeChunks.Remove(chunkCoord);
        }

        int startX = chunkCoord.x * chunkSize;
        int startY = chunkCoord.y * chunkSize;

        for (int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < chunkSize; y++)
            {
                Vector3Int pos = new Vector3Int(startX + x, startY + y, 0);
                groundTilemap.SetTile(pos, null);
            }
        }
    }

    void GenerateChunk(Vector2Int chunkCoord)
    {
        List<GameObject> chunkTrees = new List<GameObject>();
        List<GameObject> chunkBushs = new List<GameObject>();
        int startX = chunkCoord.x * chunkSize;
        int startY = chunkCoord.y * chunkSize;

        for (int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < chunkSize; y++)
            {
                int globalX = startX + x;
                int globalY = startY + y;
                Vector3Int pos = new Vector3Int(globalX, globalY, 0);

                float height = GetNoise(globalX, globalY, seed, scale);
                bool isOccupied = false; 

                TileBase tileToPlace = null; 

                if (height < waterLevel)
                {
                    tileToPlace = waterTile;
                    isOccupied = true;
                }
                else if (height < sandThreshold)
                {
                    tileToPlace = sandTile;
                    isOccupied = true;
                }
                else 
                {
                    if (height > mountainLevel && mountainTile != null)
                    {
                        tileToPlace = mountainTile;
                        isOccupied = true;
                    }
                    else if (DetailsTileOnGrass != null && DetailsTileOnGrass.Length > 0 && 
                             GetPseudoRandomFloat(globalX, globalY) > 0.99f)
                    {
                        tileToPlace = DetailsTileOnGrass[GetPseudoRandom(globalX, globalY, DetailsTileOnGrass.Length)];
                    }
                    else
                    {
                        if (grassTiles != null && grassTiles.Length > 0)
                        {
                            tileToPlace = grassTiles[GetPseudoRandom(globalX, globalY, grassTiles.Length)];
                        }
                    }
                }

                if (tileToPlace != null)
                {
                    groundTilemap.SetTile(pos, tileToPlace);
                }

                if (!isOccupied && treePrefabs != null && treePrefabs.Length > 0)
                {
                    float moisture = GetNoise(globalX, globalY, seed + 500, scale * 0.8f);
                    bool isForest = moisture > forestThreshold;

                    float treeChance = isForest ? 0.15f : 0.00005f; 
                    float randomVal = GetPseudoRandomFloat(globalX, globalY);

                    if (randomVal < treeChance)
                    {
                        if (!Physics2D.OverlapCircle(new Vector2(globalX + 0.5f, globalY + 0.5f), 0.4f, treeLayer))
                        {
                            Vector3 spawnPos = new Vector3(globalX + 0.5f, globalY + 0.5f, -0.1f);
                            
                            GameObject prefab = treePrefabs[GetPseudoRandom(globalX, globalY, treePrefabs.Length)];
                            GameObject newTree = Instantiate(prefab, spawnPos, Quaternion.identity, treeParent);
                            chunkTrees.Add(newTree);
                        }
                    }
                }

                if (!isOccupied && BushPrefabs != null && BushPrefabs.Length > 0)
                {
                    float moisture = GetNoise(globalX, globalY, seed + 6390, scale * 10.15f);
                    bool isBushZone= moisture > BushZones;

                    float BushChance = isBushZone ? 0.003f : 0.000065f; 
                    float randomVal = GetPseudoRandomFloat(globalX, globalY);

                    if (randomVal < BushChance)
                    {
                        if (!Physics2D.OverlapCircle(new Vector2(globalX + 0.5f, globalY + 0.5f), 0.4f, BushLayer))
                        {
                            Vector3 spawnPos = new Vector3(globalX + Random.Range(-1.5f,2.5f), globalY + Random.Range(-0.35f,-0.8f), -0.1f);
                            
                            GameObject prefab = BushPrefabs[GetPseudoRandom(globalX, globalY, BushPrefabs.Length)];
                            GameObject newBush = Instantiate(prefab, spawnPos, Quaternion.identity, BushParent);
                            chunkBushs.Add(newBush);
                        }
                    }
                }
            }
        }
        activeChunks.Add(chunkCoord, chunkTrees);
    }

    float GetPseudoRandomFloat(int x, int y)
    {
        float v = Mathf.Sin(x * 12.9898f + y * 78.233f + seed) * 43758.5453f;
        return v - Mathf.Floor(v);
    }

    int GetPseudoRandom(int x, int y, int length)
    {
        if (length == 0) return 0;
        int coordSeed = (x * 73856093) ^ (y * 19349663) ^ seed;
        return Mathf.Abs(coordSeed % length);
    }

    float GetNoise(int x, int y, int s, float sc)
    {
        return Mathf.PerlinNoise((x * sc) + s, (y * sc) + s);
    }

    public bool TryMineStone(Vector3 playerWorldPos)
    {
        Vector3Int centerPos = groundTilemap.WorldToCell(playerWorldPos);

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector3Int checkPos = new Vector3Int(centerPos.x + x, centerPos.y + y, 0);
                
                TileBase currentTile = groundTilemap.GetTile(checkPos);

                if (currentTile != null && DetailsTileOnGrass.Contains(currentTile))
                {
                    bool added = InventoryManager.instance.AddItem(stoneItem,1);
                    
                    if (added)
                    {
                        TileBase randomGrass = grassTiles[Random.Range(0, grassTiles.Length)];
                        groundTilemap.SetTile(checkPos, randomGrass);

                        Debug.Log("Викопано камінь!");
                        return true; 
                    }
                    else
                    {
                        Debug.Log("Інвентар повний!");
                        return false;
                    }
                }
            }
        }
        return false;
    }
}