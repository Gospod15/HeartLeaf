using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "New Pickaxe", menuName = "Inventory/Tools/Pickaxe")]
public class PickaxeItemData : ToolItemData
{
    [Header("Mining Settings")]
    public float baseRockDamage = 1f;
    public float hitRadius = 0.4f;
    public ItemData stoneItem;
    public TileBase[] mineableStoneTiles;
    public TileBase replacementGrassTile;

    public override bool Use(ToolUseContext context)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(context.targetWorldPos, hitRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            RockScript rock = hits[i].GetComponent<RockScript>();
            if (rock == null)
                rock = hits[i].GetComponentInParent<RockScript>();

            if (rock != null)
            {
                rock.TakeDamage(baseRockDamage * GetFinalSpeed());
                return true;
            }
        }

        if (context.groundTilemap == null || context.inventory == null) return false;

        Vector3Int centerPos = context.groundTilemap.WorldToCell(context.targetWorldPos);

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector3Int checkPos = new Vector3Int(centerPos.x + x, centerPos.y + y, 0);
                TileBase currentTile = context.groundTilemap.GetTile(checkPos);

                if (currentTile != null && mineableStoneTiles != null && mineableStoneTiles.Contains(currentTile))
                {
                    if (context.inventory.AddItem(stoneItem, 1))
                    {
                        context.groundTilemap.SetTile(checkPos, replacementGrassTile);

                        if (WorldSaveManager.instance != null)
                        {
                            WorldSaveManager.instance.RecordNaturalChange(new Vector2Int(checkPos.x, checkPos.y), 2);
                        }
                        return true;
                    }
                    return false;
                }
            }
        }
        return false;
    }
}