using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ItemInWorldManager : MonoBehaviour
{
    public static GameObject DefaultDropPrefab { get; set; }
    private static GameObject cachedUniversalPrefab;

    public static readonly List<ItemInWorldManager> ActiveWorldItems = new List<ItemInWorldManager>();

    public const float DEFAULT_DESPAWN_TIME = 480f;

    [Header("Info item")]
    public ItemData itemData;
    public int amount = 1;
    public int durability = -1;
    public bool isNaturalSpawn = false;

    [Header("Time life (sec)")]
    [SerializeField] private float remainingLifetime = DEFAULT_DESPAWN_TIME;
    public float RemainingLifetime => remainingLifetime;

    [Header("Size in the world")]
    [SerializeField] private float targetWorldSize = 0.5f;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (!ActiveWorldItems.Contains(this))
            ActiveWorldItems.Add(this);
    }

    private void OnDisable()
    {
        ActiveWorldItems.Remove(this);
    }

    private void Update()
    {
        if (isNaturalSpawn) return;

        if (remainingLifetime > 0f)
        {
            remainingLifetime -= Time.deltaTime;
            if (remainingLifetime <= 0f)
            {
                Destroy(gameObject);
            }
        }
    }

    public void SetRemainingLifetime(float seconds)
    {
        remainingLifetime = Mathf.Max(0.1f, seconds);
    }

    public void Initialize(ItemData data, int count = 1, int itemDurability = -1, bool isCustomPrefab = false, float lifetime = DEFAULT_DESPAWN_TIME)
    {
        itemData = data;
        amount = Mathf.Max(1, count);
        durability = itemDurability;
        remainingLifetime = lifetime;

        if (!isCustomPrefab)
        {
            ApplyVisualAndScale();
        }
    }

    private void ApplyVisualAndScale()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (itemData != null && itemData.icon != null)
        {
            spriteRenderer.sprite = itemData.icon;

            Vector2 spriteSize = itemData.icon.bounds.size;
            float maxSide = Mathf.Max(spriteSize.x, spriteSize.y);
            if (maxSide > 0f)
            {
                float scaleFactor = targetWorldSize / maxSide;
                transform.localScale = new Vector3(scaleFactor, scaleFactor, 1f);
            }
        }
    }

    public void Pickup()
    {
        if (itemData == null || InventoryManager.instance == null) return;

        bool added = false;

        if (itemData is ArmorItemData)
        {
            var slots = InventoryManager.instance.Slots;
            for (int i = 0; i < InventoryManager.INVENTORY_SLOTS_COUNT && i < slots.Count; i++)
            {
                if (slots[i].IsEmpty)
                {
                    InventoryManager.instance.SetSlotDirect(i, itemData, 1, durability);
                    added = true;
                    break;
                }
            }
        }
        else
        {
            added = InventoryManager.instance.AddItem(itemData, amount);
        }

        if (added)
        {
            if (LootUIText.Instance != null)
            {
                LootUIText.Instance.ShowLoot(itemData, amount);
            }

            if (TryGetComponent(out WorldObject worldObj))
            {
                worldObj.DestroyWorldObject();
            }
            else
            {
                if (isNaturalSpawn && WorldSaveManager.instance != null && WorldGenerator.instance != null && WorldGenerator.instance.groundTilemap != null)
                {
                    Vector3Int cell = WorldGenerator.instance.groundTilemap.WorldToCell(transform.position);
                    Vector2Int gridPos = new Vector2Int(cell.x, cell.y);
                    WorldSaveManager.instance.RecordNaturalChange(gridPos, 1);
                    WorldGenerator.instance.InvalidateCellCache(gridPos);
                }

                Destroy(gameObject);
            }
        }
    }

    public static ItemInWorldManager SpawnDrop(
        ItemData item,
        Vector3 position,
        int amount = 1,
        int durability = -1,
        GameObject fallbackPrefab = null,
        float lifetime = DEFAULT_DESPAWN_TIME,
        bool showLootUI = true)
    {
        if (item == null || amount <= 0) return null;

        bool hasCustomPrefab = item.dropPrefab != null;
        GameObject prefabToSpawn = hasCustomPrefab
            ? item.dropPrefab
            : (fallbackPrefab != null ? fallbackPrefab : DefaultDropPrefab);

        if (prefabToSpawn == null)
        {
            if (cachedUniversalPrefab == null)
                cachedUniversalPrefab = Resources.Load<GameObject>("UniversalDropPrefab");

            prefabToSpawn = cachedUniversalPrefab;
        }

        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"unknown prefab drop for '{item.itemName}'");
            return null;
        }

        GameObject droppedObj = Instantiate(prefabToSpawn, position, Quaternion.identity);
        if (droppedObj.TryGetComponent(out ItemInWorldManager worldItem))
        {
            worldItem.Initialize(item, amount, durability, hasCustomPrefab, lifetime);

            if (showLootUI && LootUIText.Instance != null)
            {
                LootUIText.Instance.ShowLoot(item, -amount);
            }
        }

        return worldItem;
    }
}