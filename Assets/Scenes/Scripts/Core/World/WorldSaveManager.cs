using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

//дані про змінену клітинку (грядка, яма, зрубане дерево)
[Serializable]
public class ModifiedCellData
{
    public int x;
    public int y;
    public byte state;
}

//дані про об'єкт який поставив гравець
[Serializable]
public class PlacedObjectSaveData
{
    public int x;
    public int y;
    public string objectId;
}

//дані про викинутий на землю предмет
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

//один слот всередині скрині
[Serializable]
public class ChestSlotSaveData
{
    public int slotIndex;
    public string itemId;
    public int amount;
}

//збереження конкретної скрині
[Serializable]
public class ChestSaveData
{
    public string chestId;
    public int capacity;
    public bool isLootGenerated;
    public List<ChestSlotSaveData> slots = new List<ChestSlotSaveData>();
}

//усі зміни в межах одного чанка
[Serializable]
public class ChunkSaveData
{
    public int chunkX;
    public int chunkY;
    public List<ModifiedCellData> modifiedCells = new List<ModifiedCellData>();
    public List<PlacedObjectSaveData> placedObjects = new List<PlacedObjectSaveData>();
}

//головна структура файлу збереження світу
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

//коротка інформація про сейв для меню вибору світу
[Serializable]
public class WorldSaveSummary
{
    public string saveSlotName;
    public string worldName;
    public int worldSeed;
    public int difficulty;
    public string lastSavedUtc;
}

//зв'язка ID префабу та самого об'єкта для інспектора
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

    //визначення назви слоту збереження та стартових налаштувань
    private void ResolveActiveSaveSlot()
    {
        string sessionSlot = GameSessionContext.CurrentSaveSlot;
        if (!string.IsNullOrWhiteSpace(sessionSlot))
        {
            activeSaveSlot = GameSessionContext.SanitizeFileName(sessionSlot);
        }
        else
        {
            activeSaveSlot = GameSessionContext.SanitizeFileName(defaultSaveName);
        }

        if (GameSessionContext.IsCreatingNewWorld && GameSessionContext.PendingWorldSettings != null)
        {
            activeWorldSettings = GameSessionContext.PendingWorldSettings;
            currentWorldSeed = activeWorldSettings.worldSeed;
        }
    }

    //створення словників для швидкого пошуку предметів і префабів по ID
    private void BuildLookupTables()
    {
        prefabLookup.Clear();
        foreach (var entry in buildablePrefabs)
        {
            if (!string.IsNullOrEmpty(entry.id) && entry.prefab != null)
            {
                prefabLookup[entry.id] = entry.prefab;
            }
        }

        itemLookup.Clear();
        foreach (var item in allItems)
        {
            if (item == null) continue;

            if (!string.IsNullOrEmpty(item.name))
            {
                itemLookup[item.name] = item;
            }
            if (!string.IsNullOrEmpty(item.itemName) && !itemLookup.ContainsKey(item.itemName))
            {
                itemLookup[item.itemName] = item;
            }
        }
    }

    //пошук гравця на сцені для збереження його позиції та інвентарю
    private void ResolveSaveablePlayer()
    {
        if (saveablePlayer != null) return;

        MonoBehaviour[] allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        for (int i = 0; i < allBehaviours.Length; i++)
        {
            if (allBehaviours[i] is ISaveablePlayer player)
            {
                saveablePlayer = player;
                break;
            }
        }
    }

    public ItemData GetItemById(string itemId)
    {
        if (!string.IsNullOrEmpty(itemId) && itemLookup.TryGetValue(itemId, out ItemData item))
        {
            return item;
        }
        return null;
    }

    private void Start()
    {
        //якщо створюємо новий світ — чистимо старі дані та робимо перший сейв
        if (GameSessionContext.IsCreatingNewWorld)
        {
            ClearRuntimeWorldData();
            SaveWorld(currentWorldSeed);
            GameSessionContext.ClearNewWorldFlag();
            return;
        }

        //якщо генератора світу немає на сцені — вантажимо сейв самі
        if (autoLoadOnStart && WorldGenerator.instance == null)
        {
            if (LoadWorld(out int savedSeed))
            {
                currentWorldSeed = savedSeed;
                RestoreDroppedItems();
            }
        }
    }

    //очищення тимчасових даних в оперативній пам'яті
    private void ClearRuntimeWorldData()
    {
        modifiedCellsByChunk.Clear();
        placedObjectsByChunk.Clear();
        pendingDroppedItems.Clear();
        savedChestsById.Clear();
    }

    //шлях до папки збережень у AppData/Roaming
    private string GetSavesDirectory()
    {
        string roamingPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string fullDir = Path.Combine(roamingPath, folderName, savesSubfolder);
        if (!Directory.Exists(fullDir))
        {
            Directory.CreateDirectory(fullDir);
        }
        return fullDir;
    }

    private string GetSaveFilePath(string saveSlotName)
    {
        string cleanName = GameSessionContext.SanitizeFileName(saveSlotName);
        return Path.Combine(GetSavesDirectory(), $"{cleanName}.json");
    }

    private string GetSaveFilePath()
    {
        return GetSaveFilePath(activeSaveSlot);
    }

    //переведення координат клітинки у координати чанка
    public Vector2Int GetChunkCoord(Vector2Int globalCell)
    {
        int cx = Mathf.FloorToInt((float)globalCell.x / chunkSize);
        int cy = Mathf.FloorToInt((float)globalCell.y / chunkSize);
        return new Vector2Int(cx, cy);
    }

    //запис зміни рельєфу (наприклад, скопали землю або зрубали дерево)
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

    //запис поставленого гравцем об'єкта
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

    //видалення об'єкта гравця зі збережень (коли зламав)
    public void RemovePlacedObject(Vector2Int globalCell)
    {
        Vector2Int chunkCoord = GetChunkCoord(globalCell);
        if (placedObjectsByChunk.TryGetValue(chunkCoord, out var objects))
        {
            objects.Remove(globalCell);
        }
    }

    //отримання збереженого стану клітинки для генератора
    public byte GetCellState(Vector2Int chunkCoord, Vector2Int globalCell)
    {
        if (modifiedCellsByChunk.TryGetValue(chunkCoord, out var cells))
        {
            if (cells.TryGetValue(globalCell, out byte state))
            {
                return state;
            }
        }
        return 0;
    }

    //отримання списку всіх будівель гравця в конкретному чанку
    public Dictionary<Vector2Int, string> GetPlacedObjectsInChunk(Vector2Int chunkCoord)
    {
        if (placedObjectsByChunk.TryGetValue(chunkCoord, out var objects))
        {
            return objects;
        }
        return null;
    }

    public GameObject GetPrefabById(string objectId)
    {
        if (!string.IsNullOrEmpty(objectId) && prefabLookup.TryGetValue(objectId, out GameObject prefab))
        {
            return prefab;
        }
        return null;
    }

    //реєстрація скрині коли її чанк завантажився
    public void RegisterChest(ChestStorage chest)
    {
        if (chest != null)
        {
            activeChests.Add(chest);
        }
    }

    //збереження та відписка скрині коли чанк вивантажується
    public void UnregisterChest(ChestStorage chest)
    {
        if (chest != null)
        {
            RecordChestState(chest);
            activeChests.Remove(chest);
        }
    }

    //зчитування предметів зі скрині у пам'ять збережень
    public void RecordChestState(ChestStorage chest)
    {
        if (chest == null || string.IsNullOrEmpty(chest.ChestId)) return;

        ChestSaveData data = new ChestSaveData();
        data.chestId = chest.ChestId;
        data.capacity = chest.Capacity;
        data.isLootGenerated = chest.IsLootGenerated;

        var slots = chest.Slots;
        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null || slots[i].IsEmpty) continue;

                string id = slots[i].item.itemName;
                if (!string.IsNullOrEmpty(slots[i].item.name))
                {
                    id = slots[i].item.name;
                }

                ChestSlotSaveData slotSave = new ChestSlotSaveData();
                slotSave.slotIndex = i;
                slotSave.itemId = id;
                slotSave.amount = slots[i].amount;
                data.slots.Add(slotSave);
            }
        }

        savedChestsById[chest.ChestId] = data;
    }

    //відновлення речей у скриню при її появі
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

    //головна функція збереження всього світу у JSON файл
    public void SaveWorld(int seed)
    {
        currentWorldSeed = seed;
        activeWorldSettings.worldSeed = seed;
        if (string.IsNullOrWhiteSpace(activeWorldSettings.worldName))
        {
            activeWorldSettings.worldName = activeSaveSlot;
        }

        ResolveSaveablePlayer();

        //оновлюємо дані всіх активних скринь перед записом
        foreach (var chest in activeChests)
        {
            if (chest != null)
            {
                RecordChestState(chest);
            }
        }

        WorldSaveFile saveFile = new WorldSaveFile();
        saveFile.worldSeed = seed;
        saveFile.lastSavedUtc = DateTime.UtcNow.ToString("o");
        saveFile.worldSettings = activeWorldSettings;

        if (saveablePlayer != null)
        {
            saveFile.playerData = saveablePlayer.CaptureState();
        }
        else
        {
            saveFile.playerData = new PlayerSaveData();
        }

        //збираємо всі чанки де є зміни або будівлі
        HashSet<Vector2Int> allChunks = new HashSet<Vector2Int>(modifiedCellsByChunk.Keys);
        allChunks.UnionWith(placedObjectsByChunk.Keys);

        foreach (var chunkCoord in allChunks)
        {
            ChunkSaveData chunkData = new ChunkSaveData();
            chunkData.chunkX = chunkCoord.x;
            chunkData.chunkY = chunkCoord.y;

            if (modifiedCellsByChunk.TryGetValue(chunkCoord, out var cells))
            {
                foreach (var kvp in cells)
                {
                    ModifiedCellData cellSave = new ModifiedCellData();
                    cellSave.x = kvp.Key.x;
                    cellSave.y = kvp.Key.y;
                    cellSave.state = kvp.Value;
                    chunkData.modifiedCells.Add(cellSave);
                }
            }

            if (placedObjectsByChunk.TryGetValue(chunkCoord, out var objects))
            {
                foreach (var kvp in objects)
                {
                    PlacedObjectSaveData objSave = new PlacedObjectSaveData();
                    objSave.x = kvp.Key.x;
                    objSave.y = kvp.Key.y;
                    objSave.objectId = kvp.Value;
                    chunkData.placedObjects.Add(objSave);
                }
            }

            if (chunkData.modifiedCells.Count > 0 || chunkData.placedObjects.Count > 0)
            {
                saveFile.chunks.Add(chunkData);
            }
        }

        //збираємо всі предмети що лежать на землі
        foreach (var worldItem in ItemInWorldManager.ActiveWorldItems)
        {
            if (worldItem == null || worldItem.itemData == null || worldItem.amount <= 0 || worldItem.RemainingLifetime <= 0f)
            {
                continue;
            }

            string id = worldItem.itemData.itemName;
            if (!string.IsNullOrEmpty(worldItem.itemData.name))
            {
                id = worldItem.itemData.name;
            }

            DroppedItemSaveData dropSave = new DroppedItemSaveData();
            dropSave.itemId = id;
            dropSave.posX = worldItem.transform.position.x;
            dropSave.posY = worldItem.transform.position.y;
            dropSave.amount = worldItem.amount;
            dropSave.durability = worldItem.durability;
            dropSave.remainingLifetime = worldItem.RemainingLifetime;
            saveFile.droppedItems.Add(dropSave);
        }

        foreach (var chestData in savedChestsById.Values)
        {
            saveFile.chests.Add(chestData);
        }

        string json = JsonUtility.ToJson(saveFile, true);
        File.WriteAllText(GetSaveFilePath(), json);
    }

    //завантаження світу з JSON файлу
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
        {
            activeWorldSettings.worldName = activeSaveSlot;
        }

        //відновлення стану гравця
        ResolveSaveablePlayer();
        if (saveablePlayer != null && saveFile.playerData != null && saveFile.playerData.hasData)
        {
            saveablePlayer.RestoreState(saveFile.playerData, GetItemById);
        }

        ClearRuntimeWorldData();

        //завантаження змін по чанках у словники
        foreach (var chunkData in saveFile.chunks)
        {
            Vector2Int chunkCoord = new Vector2Int(chunkData.chunkX, chunkData.chunkY);

            if (chunkData.modifiedCells != null && chunkData.modifiedCells.Count > 0)
            {
                var cellDict = new Dictionary<Vector2Int, byte>();
                for (int i = 0; i < chunkData.modifiedCells.Count; i++)
                {
                    var cell = chunkData.modifiedCells[i];
                    cellDict[new Vector2Int(cell.x, cell.y)] = cell.state;
                }
                modifiedCellsByChunk[chunkCoord] = cellDict;
            }

            if (chunkData.placedObjects != null && chunkData.placedObjects.Count > 0)
            {
                var objDict = new Dictionary<Vector2Int, string>();
                for (int i = 0; i < chunkData.placedObjects.Count; i++)
                {
                    var obj = chunkData.placedObjects[i];
                    objDict[new Vector2Int(obj.x, obj.y)] = obj.objectId;
                }
                placedObjectsByChunk[chunkCoord] = objDict;
            }
        }

        if (saveFile.droppedItems != null)
        {
            pendingDroppedItems.AddRange(saveFile.droppedItems);
        }

        if (saveFile.chests != null)
        {
            for (int i = 0; i < saveFile.chests.Count; i++)
            {
                var chestData = saveFile.chests[i];
                if (!string.IsNullOrEmpty(chestData.chestId))
                {
                    savedChestsById[chestData.chestId] = chestData;
                }
            }
        }

        foreach (var chest in activeChests)
        {
            if (chest != null)
            {
                TryRestoreChest(chest);
            }
        }

        return true;
    }

    //спавн викинутих предметів назад у світ після генерації карти
    public void RestoreDroppedItems()
    {
        if (pendingDroppedItems.Count == 0) return;

        for (int i = 0; i < pendingDroppedItems.Count; i++)
        {
            var savedItem = pendingDroppedItems[i];
            if (savedItem.remainingLifetime <= 0f) continue;

            ItemData itemData = GetItemById(savedItem.itemId);
            if (itemData != null)
            {
                Vector3 dropPos = new Vector3(savedItem.posX, savedItem.posY, 0f);
                ItemInWorldManager.SpawnDrop(
                    itemData,
                    dropPos,
                    savedItem.amount,
                    savedItem.durability,
                    null,
                    savedItem.remainingLifetime,
                    false
                );
            }
        }

        pendingDroppedItems.Clear();
    }

    //збереження гри та вихід у головне меню
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

    //отримання списку всіх файлів збережень для меню вибору
    public List<WorldSaveSummary> GetAllSaveSlots()
    {
        List<WorldSaveSummary> results = new List<WorldSaveSummary>();
        string dir = GetSavesDirectory();
        if (!Directory.Exists(dir)) return results;

        string[] files = Directory.GetFiles(dir, "*.json");
        for (int i = 0; i < files.Length; i++)
        {
            try
            {
                WorldSaveFile saveFile = JsonUtility.FromJson<WorldSaveFile>(File.ReadAllText(files[i]));
                if (saveFile == null) continue;

                string slotName = Path.GetFileNameWithoutExtension(files[i]);
                WorldSettingsData settings = saveFile.worldSettings ?? new WorldSettingsData();

                WorldSaveSummary summary = new WorldSaveSummary();
                summary.saveSlotName = slotName;
                summary.worldName = !string.IsNullOrWhiteSpace(settings.worldName) ? settings.worldName : slotName;
                summary.worldSeed = saveFile.worldSeed;
                summary.difficulty = settings.difficulty;
                summary.lastSavedUtc = saveFile.lastSavedUtc;
                results.Add(summary);
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
        {
            File.Delete(path);
        }
    }

    //автозбереження при закритті вікна гри
    private void OnApplicationQuit()
    {
        if (!hasSavedOnExit && instance == this)
        {
            hasSavedOnExit = true;
            SaveWorld(currentWorldSeed);
        }
    }

    //автозбереження при знищенні об'єкта менеджера
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