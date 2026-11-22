using UnityEngine;

[CreateAssetMenu(fileName = "New Axe", menuName = "Inventory/Tools/Axe")]
public class AxeItemData : ToolItemData
{
    public float baseTreeDamage = 1f;
    public float hitRadius = 0.4f;

    public override bool Use(ToolUseContext context)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(context.targetWorldPos, hitRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            TreeScript tree = hits[i].GetComponent<TreeScript>();
            if (tree == null)
                tree = hits[i].GetComponentInParent<TreeScript>();

            if (tree != null)
            {
                tree.TakeDamage(baseTreeDamage * GetFinalSpeed(), context.playerWorldPos);
                return true;
            }
        }
        return false;
    }
}