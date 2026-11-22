using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class ModifiedCellData
{
    public int x;
    public int y;
    public byte state;
}

[Serializable]
public class PlacedObjectSaveData
{
    public int x;
    public int y;
    public string objectId;
}

[Serializable]
public class DroppedItemSaveData
{
    public string itemId;
    public float posX;
    public float posY;
    public int amount;
    public int durability;
    public float remainingLifetime;
}

[Serializable]
public class ChestSlotSaveData
{
    public int slotIndex;
    public string itemId;
    public int amount;
}

[Serializable]
public class ChestSaveData
{
    public string chestId;
    public int capacity;
    public bool isLootGenerated;
    public List<ChestSlotSaveData> slots = new List<ChestSlotSaveData>();
}

[Serializable]
public class ChunkSaveData
{
    public int chunkX;
    public int chunkY;
    public List<ModifiedCellData> modifiedCells = new List<ModifiedCellData>();
    public List<PlacedObjectSaveData> placedObjects = new List<PlacedObjectSaveData>();
}

[Serializable]
public class WorldSaveFile
{
    public int worldSeed;
    public string lastSavedUtc;
    public WorldSettingsData worldSettings = new WorldSettingsData();
    public PlayerSaveData playerData = new PlayerSaveData();
    public List<ChunkSaveData> chunks = new List<ChunkSaveData>();
    public List<DroppedItemSaveData> droppedItems = new List<DroppedItemSaveData>();
    public List<ChestSaveData> chests = new List<ChestSaveData>();
}

[Serializable]
public class WorldSaveSummary
{
    public string saveSlotName;
    public string worldName;
    public int worldSeed;
    public int difficulty;
    public string lastSavedUtc;
}

[Serializable]
public class PrefabEntry
{
    public string id;
    public GameObject prefab;
}

public class WorldSaveManager : MonoBehaviour
{
    public static WorldSaveManager instance;

    [Header("Save Path Settings")]
    [SerializeField] private string folderName = "Heartleaf";
    [SerializeField] private string savesSubfolder = "Saves";
    [SerializeField] private string defaultSaveName = "world_save";
    [SerializeField] private bool autoLoadOnStart = true;

    [Header("Chunks and Prefabs Settings")]
    public int chunkSize = 16;
    [SerializeField] private List<PrefabEntry> buildablePrefabs = new List<PrefabEntry>();

    [Header("Item Database")]
    [SerializeField] private List<ItemData> allItems = new List<ItemData>();

    private Dictionary<Vector2Int, Dictionary<Vector2Int, byte>> modifiedCellsByChunk = new Dictionary<Vector2Int, Dictionary<Vector2Int, byte>>();
    private Dictionary<Vector2Int, Dictionary<Vector2Int, string>> placedObjectsByChunk = new Dictionary<Vector2Int, Dictionary<Vector2Int, string>>();
    private Dictionary<string, GameObject> prefabLookup = new Dictionary<string, GameObject>();
    private Dictionary<string, ItemData> itemLookup = new Dictionary<string, ItemData>();
    private Dictionary<string, ChestSaveData> savedChestsById = new Dictionary<string, ChestSaveData>();
    private HashSet<ChestStorage> activeChests = new HashSet<ChestStorage>();

    private List<DroppedItemSaveData> pendingDroppedItems = new List<DroppedItemSaveData>();
    private ISaveablePlayer saveablePlayer;
    private WorldSettingsData activeWorldSettings = new WorldSettingsData();
    private string activeSaveSlot;
    private int currentWorldSeed;
    private bool hasSavedOnExit = false;

    public WorldSettingsData ActiveWorldSettings => activeWorldSettings;
    public string ActiveSaveSlot => activeSaveSlot;
    public int CurrentWorldSeed => currentWorldSeed;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(this);
            return;
        }

        ResolveActiveSaveSlot();
        ResolveSaveablePlayer();
        BuildLookupTables();
    }

    private void ResolveActiveSaveSlot()
    {
        string sessionSlot = GameSessionContext.CurrentSaveSlot;
        activeSaveSlot = !string.IsNullOrWhiteSpace(sessionSlot)
            ? GameSessionContext.SanitizeFileName(sessionSlot)
            : GameSessionContext.SanitizeFileName(defaultSaveName);

        if (GameSessionContext.IsCreatingNewWorld && GameSessionContext.PendingWorldSettings != null)
        {
            activeWorldSettings = GameSessionContext.PendingWorldSettings;
            currentWorldSeed = activeWorldSettings.worldSeed;
        }
    }

    private void BuildLookupTables()
    {
        prefabLookup.Clear();
        foreach (var entry in buildablePrefabs)
        {
            if (!string.IsNullOrEmpty(entry.id) && entry.prefab != null)
                prefabLookup[entry.id] = entry.prefab;
        }

        itemLookup.Clear();
        foreach (var item in allItems)
        {
            if (item == null) continue;
            if (!string.IsNullOrEmpty(item.name))
                itemLookup[item.name] = item;
            if (!string.IsNullOrEmpty(item.itemName) && !itemLookup.ContainsKey(item.itemName))
                itemLookup[item.itemName] = item;
        }
    }

    private void ResolveSaveablePlayer()
    {
        if (saveablePlayer != null) return;

        foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (behaviour is ISaveablePlayer player)
            {
                saveablePlayer = player;
                break;
            }
        }
    }

    public ItemData GetItemById(string itemId)
    {
        if (!string.IsNullOrEmpty(itemId) && itemLookup.TryGetValue(itemId, out ItemData item))
            return item;
        return null;
    }

    private void Start()
    {
        if (GameSessionContext.IsCreatingNewWorld)
        {
            ClearRuntimeWorldData();
            SaveWorld(currentWorldSeed);
            GameSessionContext.ClearNewWorldFlag();
            return;
        }

        if (autoLoadOnStart && WorldGenerator.instance == null)
        {
            if (LoadWorld(out int savedSeed))
            {
                currentWorldSeed = savedSeed;
                RestoreDroppedItems();
            }
        }
    }

    private void ClearRuntimeWorldData()
    {
        modifiedCellsByChunk.Clear();
        placedObjectsByChunk.Clear();
        pendingDroppedItems.Clear();
        savedChestsById.Clear();
    }

    private string GetSavesDirectory()
    {
        string roamingPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string fullDir = Path.Combine(roamingPath, folderName, savesSubfolder);
        if (!Directory.Exists(fullDir)) Directory.CreateDirectory(fullDir);
        return fullDir;
    }

    private string GetSaveFilePath(string saveSlotName)
    {
        string cleanName = GameSessionContext.SanitizeFileName(saveSlotName);
        return Path.Combine(GetSavesDirectory(), $"{cleanName}.json");
    }

    private string GetSaveFilePath() => GetSaveFilePath(activeSaveSlot);

    public Vector2Int GetChunkCoord(Vector2Int globalCell)
    {
        return new Vector2Int(
            Mathf.FloorToInt((float)globalCell.x / chunkSize),
            Mathf.FloorToInt((float)globalCell.y / chunkSize)
        );
    }

    public void RecordNaturalChange(Vector2Int globalCell, byte state)
    {
        Vector2Int chunkCoord = GetChunkCoord(globalCell);
        if (!modifiedCellsByChunk.TryGetValue(chunkCoord, out var cells))
        {
            cells = new Dictionary<Vector2Int, byte>();
            modifiedCellsByChunk[chunkCoord] = cells;
        }
        cells[globalCell] = state;
    }

    public void RecordPlacedObject(Vector2Int globalCell, string objectId)
    {
        Vector2Int chunkCoord = GetChunkCoord(globalCell);
        if (!placedObjectsByChunk.TryGetValue(chunkCoord, out var objects))
        {
            objects = new Dictionary<Vector2Int, string>();
            placedObjectsByChunk[chunkCoord] = objects;
        }
        objects[globalCell] = objectId;
    }

    public void RemovePlacedObject(Vector2Int globalCell)
    {
        Vector2Int chunkCoord = GetChunkCoord(globalCell);
        if (placedObjectsByChunk.TryGetValue(chunkCoord, out var objects))
            objects.Remove(globalCell);
    }

    public byte GetCellState(Vector2Int chunkCoord, Vector2Int globalCell)
    {
        if (modifiedCellsByChunk.TryGetValue(chunkCoord, out var cells) &&
            cells.TryGetValue(globalCell, out byte state))
            return state;
        return 0;
    }

    public Dictionary<Vector2Int, string> GetPlacedObjectsInChunk(Vector2Int chunkCoord)
    {
        return placedObjectsByChunk.TryGetValue(chunkCoord, out var objects) ? objects : null;
    }

    public GameObject GetPrefabById(string objectId)
    {
        return !string.IsNullOrEmpty(objectId) && prefabLookup.TryGetValue(objectId, out GameObject prefab) ? prefab : null;
    }

    public void RegisterChest(ChestStorage chest)
    {
        if (chest != null)
            activeChests.Add(chest);
    }

    public void UnregisterChest(ChestStorage chest)
    {
        if (chest != null)
        {
            RecordChestState(chest);
            activeChests.Remove(chest);
        }
    }

    public void RecordChestState(ChestStorage chest)
    {
        if (chest == null || string.IsNullOrEmpty(chest.ChestId)) return;

        ChestSaveData data = new ChestSaveData
        {
            chestId = chest.ChestId,
            capacity = chest.Capacity,
            isLootGenerated = chest.IsLootGenerated
        };

        var slots = chest.Slots;
        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null || slots[i].IsEmpty) continue;

                string id = !string.IsNullOrEmpty(slots[i].item.name) ? slots[i].item.name : slots[i].item.itemName;
                data.slots.Add(new ChestSlotSaveData
                {
                    slotIndex = i,
                    itemId = id,
                    amount = slots[i].amount
                });
            }
        }

        savedChestsById[chest.ChestId] = data;
    }

    public bool TryRestoreChest(ChestStorage chest)
    {
        if (chest == null || string.IsNullOrEmpty(chest.ChestId)) return false;

        if (savedChestsById.TryGetValue(chest.ChestId, out ChestSaveData data))
        {
            chest.RestoreSavedState(data, GetItemById);
            return true;
        }
        return false;
    }

    public void SetWorldSettings(WorldSettingsData settings)
    {
        if (settings == null) return;
        activeWorldSettings = settings;
        currentWorldSeed = settings.worldSeed;
    }

    public void SaveWorld(int seed)
    {
        currentWorldSeed = seed;
        activeWorldSettings.worldSeed = seed;
        if (string.IsNullOrWhiteSpace(activeWorldSettings.worldName))
            activeWorldSettings.worldName = activeSaveSlot;

        ResolveSaveablePlayer();

        foreach (var chest in activeChests)
        {
            if (chest != null)
                RecordChestState(chest);
        }

        WorldSaveFile saveFile = new WorldSaveFile
        {
            worldSeed = seed,
            lastSavedUtc = DateTime.UtcNow.ToString("o"),
            worldSettings = activeWorldSettings,
            playerData = saveablePlayer != null ? saveablePlayer.CaptureState() : new PlayerSaveData()
        };

        HashSet<Vector2Int> allChunks = new HashSet<Vector2Int>(modifiedCellsByChunk.Keys);
        allChunks.UnionWith(placedObjectsByChunk.Keys);

        foreach (var chunkCoord in allChunks)
        {
            ChunkSaveData chunkData = new ChunkSaveData { chunkX = chunkCoord.x, chunkY = chunkCoord.y };

            if (modifiedCellsByChunk.TryGetValue(chunkCoord, out var cells))
            {
                foreach (var kvp in cells)
                    chunkData.modifiedCells.Add(new ModifiedCellData { x = kvp.Key.x, y = kvp.Key.y, state = kvp.Value });
            }

            if (placedObjectsByChunk.TryGetValue(chunkCoord, out var objects))
            {
                foreach (var kvp in objects)
                    chunkData.placedObjects.Add(new PlacedObjectSaveData { x = kvp.Key.x, y = kvp.Key.y, objectId = kvp.Value });
            }

            if (chunkData.modifiedCells.Count > 0 || chunkData.placedObjects.Count > 0)
                saveFile.chunks.Add(chunkData);
        }

        foreach (var worldItem in ItemInWorldManager.ActiveWorldItems)
        {
            if (worldItem == null || worldItem.itemData == null || worldItem.amount <= 0 || worldItem.RemainingLifetime <= 0f)
                continue;

            string id = !string.IsNullOrEmpty(worldItem.itemData.name) ? worldItem.itemData.name : worldItem.itemData.itemName;

            saveFile.droppedItems.Add(new DroppedItemSaveData
            {
                itemId = id,
                posX = worldItem.transform.position.x,
                posY = worldItem.transform.position.y,
                amount = worldItem.amount,
                durability = worldItem.durability,
                remainingLifetime = worldItem.RemainingLifetime
            });
        }

        foreach (var chestData in savedChestsById.Values)
        {
            saveFile.chests.Add(chestData);
        }

        File.WriteAllText(GetSaveFilePath(), JsonUtility.ToJson(saveFile, true));
    }

    public bool LoadWorld(out int loadedSeed)
    {
        loadedSeed = 0;

        if (GameSessionContext.IsCreatingNewWorld)
        {
            loadedSeed = currentWorldSeed;
            ClearRuntimeWorldData();
            SaveWorld(loadedSeed);
            GameSessionContext.ClearNewWorldFlag();
            return true;
        }

        string path = GetSaveFilePath();
        if (!File.Exists(path)) return false;

        WorldSaveFile saveFile = JsonUtility.FromJson<WorldSaveFile>(File.ReadAllText(path));
        if (saveFile == null) return false;

        loadedSeed = saveFile.worldSeed;
        currentWorldSeed = loadedSeed;

        activeWorldSettings = saveFile.worldSettings ?? new WorldSettingsData();
        activeWorldSettings.worldSeed = loadedSeed;
        if (string.IsNullOrWhiteSpace(activeWorldSettings.worldName))
            activeWorldSettings.worldName = activeSaveSlot;

        ResolveSaveablePlayer();
        if (saveablePlayer != null && saveFile.playerData != null && saveFile.playerData.hasData)
        {
            saveablePlayer.RestoreState(saveFile.playerData, GetItemById);
        }

        ClearRuntimeWorldData();

        foreach (var chunkData in saveFile.chunks)
        {
            Vector2Int chunkCoord = new Vector2Int(chunkData.chunkX, chunkData.chunkY);

            if (chunkData.modifiedCells != null && chunkData.modifiedCells.Count > 0)
            {
                var cellDict = new Dictionary<Vector2Int, byte>();
                foreach (var cell in chunkData.modifiedCells)
                    cellDict[new Vector2Int(cell.x, cell.y)] = cell.state;
                modifiedCellsByChunk[chunkCoord] = cellDict;
            }

            if (chunkData.placedObjects != null && chunkData.placedObjects.Count > 0)
            {
                var objDict = new Dictionary<Vector2Int, string>();
                foreach (var obj in chunkData.placedObjects)
                    objDict[new Vector2Int(obj.x, obj.y)] = obj.objectId;
                placedObjectsByChunk[chunkCoord] = objDict;
            }
        }

        if (saveFile.droppedItems != null)
            pendingDroppedItems.AddRange(saveFile.droppedItems);

        if (saveFile.chests != null)
        {
            foreach (var chestData in saveFile.chests)
            {
                if (!string.IsNullOrEmpty(chestData.chestId))
                    savedChestsById[chestData.chestId] = chestData;
            }
        }

        foreach (var chest in activeChests)
        {
            if (chest != null)
                TryRestoreChest(chest);
        }

        return true;
    }

    public void RestoreDroppedItems()
    {
        if (pendingDroppedItems.Count == 0) return;

        foreach (var savedItem in pendingDroppedItems)
        {
            if (savedItem.remainingLifetime <= 0f) continue;

            ItemData itemData = GetItemById(savedItem.itemId);
            if (itemData != null)
            {
                ItemInWorldManager.SpawnDrop(
                    itemData,
                    new Vector3(savedItem.posX, savedItem.posY, 0f),
                    savedItem.amount,
                    savedItem.durability,
                    null,
                    savedItem.remainingLifetime
                );
            }
        }

        pendingDroppedItems.Clear();
    }

    public void SaveAndExitToMenu(string menuSceneName)
    {
        if (!hasSavedOnExit)
        {
            hasSavedOnExit = true;
            SaveWorld(currentWorldSeed);
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName);
    }

    public List<WorldSaveSummary> GetAllSaveSlots()
    {
        List<WorldSaveSummary> results = new List<WorldSaveSummary>();
        string dir = GetSavesDirectory();
        if (!Directory.Exists(dir)) return results;

        string[] files = Directory.GetFiles(dir, "*.json");
        foreach (string file in files)
        {
            try
            {
                WorldSaveFile saveFile = JsonUtility.FromJson<WorldSaveFile>(File.ReadAllText(file));
                if (saveFile == null) continue;

                string slotName = Path.GetFileNameWithoutExtension(file);
                WorldSettingsData settings = saveFile.worldSettings ?? new WorldSettingsData();

                results.Add(new WorldSaveSummary
                {
                    saveSlotName = slotName,
                    worldName = !string.IsNullOrWhiteSpace(settings.worldName) ? settings.worldName : slotName,
                    worldSeed = saveFile.worldSeed,
                    difficulty = settings.difficulty,
                    lastSavedUtc = saveFile.lastSavedUtc
                });
            }
            catch
            {
            }
        }

        return results;
    }

    public bool SaveSlotExists(string saveSlotName)
    {
        return File.Exists(GetSaveFilePath(saveSlotName));
    }

    public void DeleteSaveSlot(string saveSlotName)
    {
        string path = GetSaveFilePath(saveSlotName);
        if (File.Exists(path))
            File.Delete(path);
    }

    private void OnApplicationQuit()
    {
        if (!hasSavedOnExit && instance == this)
        {
            hasSavedOnExit = true;
            SaveWorld(currentWorldSeed);
        }
    }

    private void OnDestroy()
    {
        if (!hasSavedOnExit && instance == this)
        {
            hasSavedOnExit = true;
            SaveWorld(currentWorldSeed);
        }
    }

    [ContextMenu("Delete Current Save File")]
    public void DeleteSaveFile()
    {
        DeleteSaveSlot(activeSaveSlot);
    }

#if UNITY_EDITOR
    [ContextMenu("Auto-Fill All Items From Folder")]
    public void AutoFillAllItems()
    {
        allItems.Clear();
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemData");
        foreach (string guid in guids)
        {
            ItemData item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (item != null && !allItems.Contains(item)) allItems.Add(item);
        }
        UnityEditor.EditorUtility.SetDirty(this);
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
        AutoFillAllItems();
    }
#endif
}