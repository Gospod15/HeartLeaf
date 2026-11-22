using UnityEngine;
using UnityEngine.Tilemaps;

public enum ItemType { Resource, Food, Tool, Weapon, Armor }
public enum ItemRarity { Common, Rare, Epic, Legendary }

public struct ToolUseContext
{
    public Vector3 playerWorldPos;
    public Vector3 targetWorldPos;
    public Tilemap groundTilemap;
    public Tilemap holeCollisionTilemap;
    public WorldGenerator worldGenerator;
    public PlantPlantsScript plantSystem;
    public InventoryManager inventory;
    public PlayerStats playerStats;
}

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Base Item")]
public class ItemData : ScriptableObject
{
    [Header("Basic Properties")]
    public string itemName;
    [TextArea(2, 4)] public string description;
    public ItemRarity rarity = ItemRarity.Common;
    public Sprite icon;
    public ItemType itemType;
    public bool isStackable = true;
    public int maxStackSize = 30;
    public GameObject dropPrefab;

    [Header("Food Settings")]
    public bool isEatable;
    public int FeedAmount;

    [Header("Shop")]
    public bool CanBuy;
    public int BuyPrice;
    public bool CanSell;
    public int Sellprice;

    public int GetSellValue(int divider = 2)
    {
        return divider > 0 ? Mathf.Max(1, Sellprice / divider) : Sellprice;
    }

    public Color GetRarityColor()
    {
        return Color.black;
    }

    public string GetRarityName()
    {
        return rarity switch
        {
            ItemRarity.Common => "Common",
            ItemRarity.Rare => "Rare",
            ItemRarity.Epic => "Epic",
            ItemRarity.Legendary => "Legendary",
            _ => "Common"
        };
    }

    public virtual string GetTooltipStats()
    {
        if (itemType == ItemType.Food && isEatable)
            return $"Restores hunger: +{FeedAmount}";

        return string.Empty;
    }
}