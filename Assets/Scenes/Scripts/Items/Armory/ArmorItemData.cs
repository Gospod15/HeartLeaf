using UnityEngine;

public enum ArmorSlotType
{
    Head,
    Chest,
    Legs
}

[CreateAssetMenu(fileName = "New Armor", menuName = "Inventory/Armor Item")]
public class ArmorItemData : ItemData
{
    [Header("Armor Settings")]
    public ArmorSlotType armorSlotType = ArmorSlotType.Chest;

    [Tooltip("Maximum armor durability")]
    public int maxDurability = 100;

    [Tooltip("Defense value (e.g., 5 flat defense)")]
    public float defenseCoefficient = 5f;

    [Range(0f, 1f)]
    [Tooltip("Damage reduction percentage (0.2 = -20% incoming damage)")]
    public float damageReductionPercent = 0.15f;

    private void OnEnable()
    {
        itemType = ItemType.Armor;
        isStackable = false;
        maxStackSize = 1;
    }

    public override string GetTooltipStats()
    {
        return $"Slot: {armorSlotType}\nDefense: +{defenseCoefficient} ({Mathf.RoundToInt(damageReductionPercent * 100)}%)\nDurability: {maxDurability}";
    }
}