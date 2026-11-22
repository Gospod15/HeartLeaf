using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct ShapedCraftingIngredient
{
    [Range(0, 14)] public int slotIndex;
    public ItemData item;
    [Min(1)] public int amount;
}

[CreateAssetMenu(fileName = "New Recipe", menuName = "Inventory/Crafting Recipe")]
public class CraftingRecipe : ScriptableObject
{
    [Header("Type of recipe")]
    public bool isShapeless = false;

    [Header("Ingredients")]
    public List<ShapedCraftingIngredient> ingredients = new List<ShapedCraftingIngredient>();

    [Header("Result of craft")]
    public ItemData resultItem;
    [Min(1)] public int resultAmount = 1;
}