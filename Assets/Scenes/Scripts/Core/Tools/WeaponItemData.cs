using UnityEngine;

[CreateAssetMenu(fileName = "New Weapon", menuName = "Inventory/Weapons/Weapon")]
public class WeaponItemData : ToolItemData
{
    [Header("Weapon Stats")]
    public float baseDamage = 10f;
    public bool isRanged = false;
    public ItemData requiredAmmo;

    public float GetFinalDamage()
    {
        float multiplier = rarity switch
        {
            ItemRarity.Rare => 1.3f,
            ItemRarity.Epic => 1.7f,
            ItemRarity.Legendary => 2.3f,
            _ => 1.0f
        };
        return baseDamage * multiplier;
    }

    public override string GetTooltipStats()
    {
        string weaponType = isRanged ? "Ranged" : "Melee";
        return $"{weaponType}\nDamage: {GetFinalDamage():0.#}\nAttack Speed: {GetFinalSpeed():0.##}";
    }

    public override bool Use(ToolUseContext context)
    {
        if (isRanged && requiredAmmo != null)
        {
            if (context.inventory == null || !context.inventory.HasItem(requiredAmmo, 1))
                return false;

            context.inventory.RemoveItem(requiredAmmo, 1);
        }

        Collider2D hit = Physics2D.OverlapCircle(context.targetWorldPos, 0.6f);
        return hit != null;
    }
}