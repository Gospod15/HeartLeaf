using UnityEngine;

[CreateAssetMenu(fileName = "New Seed", menuName = "Inventory/Tools/Seed")]
public class SeedItemData : ToolItemData
{
    public override string GetTooltipStats() => "Can be planted on tilled soil";

    public override bool Use(ToolUseContext context)
    {
        if (context.plantSystem == null || context.inventory == null) return false;

        bool planted = context.plantSystem.PlantWheat(context.targetWorldPos, context.groundTilemap);
        if (planted)
        {
            context.inventory.RemoveItem(this, 1);
            return true;
        }
        return false;
    }
}