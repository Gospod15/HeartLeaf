using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class CraftingGridController : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Grid Settings")]
    [SerializeField] private int craftStartIndex = 12;
    [SerializeField] private int craftSlotCount = 15;

    [Header("Saved Recipes")]
    [SerializeField] private List<CraftingRecipe> recipes = new List<CraftingRecipe>();

    [Header("Result Slot UI")]
    [SerializeField] private Image resultIcon;
    [SerializeField] private TextMeshProUGUI resultAmountText;

    private CraftingRecipe matchedRecipe;

    private void Awake()
    {
        ClearResultVisual();
    }

    private void Start()
    {
        if (InventoryManager.instance != null)
            InventoryManager.instance.OnInventoryChanged += EvaluateCraftingGrid;

        ClearResultVisual();
    }

    private void OnDestroy()
    {
        if (InventoryManager.instance != null)
            InventoryManager.instance.OnInventoryChanged -= EvaluateCraftingGrid;
    }

    public void EvaluateCraftingGrid()
    {
        if (InventoryManager.instance == null) return;

        matchedRecipe = null;

        foreach (var recipe in recipes)
        {
            if (recipe == null || recipe.resultItem == null || recipe.ingredients.Count == 0 || recipe.isShapeless)
                continue;

            if (IsExactMatch(recipe))
            {
                matchedRecipe = recipe;
                break;
            }
        }

        if (matchedRecipe == null)
        {
            foreach (var recipe in recipes)
            {
                if (recipe == null || recipe.resultItem == null || recipe.ingredients.Count == 0 || !recipe.isShapeless)
                    continue;

                if (IsShapelessMatch(recipe))
                {
                    matchedRecipe = recipe;
                    break;
                }
            }
        }

        if (matchedRecipe != null && matchedRecipe.resultItem != null)
        {
            if (resultIcon != null)
            {
                resultIcon.sprite = matchedRecipe.resultItem.icon;
                resultIcon.enabled = true;
                SetIconAlpha(1f); 
            }
            if (resultAmountText != null)
            {
                resultAmountText.text = matchedRecipe.resultAmount > 1 ? matchedRecipe.resultAmount.ToString() : "";
                resultAmountText.enabled = matchedRecipe.resultAmount > 1;
            }
        }
        else
        {
            ClearResultVisual();
        }
    }

    private bool IsExactMatch(CraftingRecipe recipe)
    {
        var allSlots = InventoryManager.instance.Slots;

        for (int localSlot = 0; localSlot < craftSlotCount; localSlot++)
        {
            int globalIndex = craftStartIndex + localSlot;
            if (globalIndex >= allSlots.Count) return false;

            var currentSlot = allSlots[globalIndex];

            bool slotRequiredByRecipe = false;
            foreach (var req in recipe.ingredients)
            {
                if (req.slotIndex == localSlot)
                {
                    slotRequiredByRecipe = true;
                    if (currentSlot.IsEmpty || currentSlot.item != req.item || currentSlot.amount < req.amount)
                        return false;
                    break;
                }
            }

            if (!slotRequiredByRecipe && !currentSlot.IsEmpty)
                return false;
        }

        return true;
    }

    private bool IsShapelessMatch(CraftingRecipe recipe)
    {
        var allSlots = InventoryManager.instance.Slots;

        Dictionary<ItemData, int> itemsInGrid = new Dictionary<ItemData, int>();
        for (int localSlot = 0; localSlot < craftSlotCount; localSlot++)
        {
            int globalIndex = craftStartIndex + localSlot;
            if (globalIndex >= allSlots.Count) break;

            var slot = allSlots[globalIndex];
            if (!slot.IsEmpty)
            {
                if (itemsInGrid.ContainsKey(slot.item))
                    itemsInGrid[slot.item] += slot.amount;
                else
                    itemsInGrid[slot.item] = slot.amount;
            }
        }

        Dictionary<ItemData, int> requiredItems = new Dictionary<ItemData, int>();
        foreach (var req in recipe.ingredients)
        {
            if (req.item == null) return false;

            if (requiredItems.ContainsKey(req.item))
                requiredItems[req.item] += req.amount;
            else
                requiredItems[req.item] = req.amount;
        }

        if (itemsInGrid.Count != requiredItems.Count)
            return false;

        foreach (var kvp in requiredItems)
        {
            if (!itemsInGrid.TryGetValue(kvp.Key, out int gridAmount) || gridAmount != kvp.Value)
                return false;
        }

        return true;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (matchedRecipe == null || InventoryDragController.Instance == null) return;

        CraftingRecipe recipeToConsume = matchedRecipe;

        bool crafted = InventoryDragController.Instance.TryPickUpCraftResult(
            recipeToConsume.resultItem,
            recipeToConsume.resultAmount
        );

        if (crafted)
        {
            ConsumeIngredients(recipeToConsume);
        }
    }

    private void ConsumeIngredients(CraftingRecipe recipe)
    {
        if (InventoryManager.instance == null || recipe == null) return;

        if (recipe.isShapeless)
        {
            var allSlots = InventoryManager.instance.Slots;
            foreach (var req in recipe.ingredients)
            {
                int remaining = req.amount;
                for (int localSlot = 0; localSlot < craftSlotCount; localSlot++)
                {
                    int globalIndex = craftStartIndex + localSlot;
                    if (globalIndex >= allSlots.Count) break;

                    var slot = allSlots[globalIndex];
                    if (!slot.IsEmpty && slot.item == req.item)
                    {
                        int toRemove = Mathf.Min(slot.amount, remaining);
                        InventoryManager.instance.RemoveFromSlot(globalIndex, toRemove);
                        remaining -= toRemove;

                        if (remaining <= 0) break;
                    }
                }
            }
        }
        else
        {
            foreach (var req in recipe.ingredients)
            {
                int globalIndex = craftStartIndex + req.slotIndex;
                InventoryManager.instance.RemoveFromSlot(globalIndex, req.amount);
            }
        }
    }

    private void ClearResultVisual()
    {
        matchedRecipe = null;
        if (resultIcon != null)
        {
            resultIcon.sprite = null;
            SetIconAlpha(0f);
        }
        if (resultAmountText != null)
        {
            resultAmountText.text = "";
            resultAmountText.enabled = false;
        }
    }

    private void SetIconAlpha(float alpha)
    {
        if (resultIcon == null) return;
        Color c = resultIcon.color;
        c.a = alpha;
        resultIcon.color = c;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (matchedRecipe != null && matchedRecipe.resultItem != null && ItemTooltipUI.Instance != null)
            ItemTooltipUI.Instance.Show(matchedRecipe.resultItem);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (ItemTooltipUI.Instance != null)
            ItemTooltipUI.Instance.Hide();
    }

#if UNITY_EDITOR
    [Header("Editor Folder Path")]
    [SerializeField] private string editorRecipesFolder = "Assets/Scenes/Items/Scriptable/Recipe";

    [ContextMenu("Load All Recipes From Folder")]
    private void LoadRecipesInEditor()
    {
        recipes.Clear();
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:CraftingRecipe", new[] { editorRecipesFolder });
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            CraftingRecipe recipe = UnityEditor.AssetDatabase.LoadAssetAtPath<CraftingRecipe>(path);
            if (recipe != null)
                recipes.Add(recipe);
        }
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}