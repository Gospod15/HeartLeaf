using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LootUIText : MonoBehaviour
{
    public static LootUIText Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI lootTextPrefab;
    [SerializeField] private Transform spawnContainer;

    [Header("Animation Settings")]
    [SerializeField] private float moveUpDistance = 60f;
    [SerializeField] private float displayDuration = 1.6f;
    [SerializeField] private float fadeStartRatio = 0.5f;

    [Header("Grouping Settings")]
    [SerializeField] private float combineWindow = 0.15f;

    [Header("Rarity Colors")]
    [SerializeField] private Color commonColor = Color.white;
    [SerializeField] private Color rareColor = new Color(0.25f, 0.6f, 1f, 1f);
    [SerializeField] private Color epicColor = new Color(0.75f, 0.3f, 1f, 1f);
    [SerializeField] private Color legendaryColor = new Color(1f, 0.7f, 0.15f, 1f);

    //допоміжний клас щоб склеювати однакові предмети якщо вони викидаються циклом по 1 шт
    private class ActiveLootEntry
    {
        public ItemData item;
        public int totalAmount;
        public TextMeshProUGUI textComponent;
        public float lastUpdateTime;
    }

    private readonly List<ActiveLootEntry> recentEntries = new List<ActiveLootEntry>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (spawnContainer == null)
        {
            spawnContainer = transform;
        }
    }

    //показ спливаючого тексту: amount > 0 це підбір (+N), amount < 0 це викидання (-N)
    public void ShowLoot(ItemData item, int amount)
    {
        if (item == null || amount == 0 || lootTextPrefab == null) return;

        bool isPositive = amount > 0;

        //шукаємо чи не з'являвся цей самий предмет щойно (щоб склеїти 30 разів по -1 в одне -30)
        for (int i = recentEntries.Count - 1; i >= 0; i--)
        {
            ActiveLootEntry entry = recentEntries[i];

            //якщо напис вже зник або минуло забагато часу — видаляємо зі списку
            if (entry.textComponent == null || Time.unscaledTime - entry.lastUpdateTime > combineWindow)
            {
                recentEntries.RemoveAt(i);
                continue;
            }

            bool entryIsPositive = entry.totalAmount > 0;

            //якщо це той самий предмет і той самий знак (+ або -) — просто додаємо кількість
            if (entry.item == item && entryIsPositive == isPositive)
            {
                entry.totalAmount += amount;
                entry.lastUpdateTime = Time.unscaledTime;
                UpdateTextVisual(entry.textComponent, item, entry.totalAmount);
                return;
            }
        }

        //якщо свіжого напису немає — створюємо новий рядок
        TextMeshProUGUI textInstance = Instantiate(lootTextPrefab, spawnContainer);
        textInstance.gameObject.SetActive(true);
        UpdateTextVisual(textInstance, item, amount);

        ActiveLootEntry newEntry = new ActiveLootEntry();
        newEntry.item = item;
        newEntry.totalAmount = amount;
        newEntry.textComponent = textInstance;
        newEntry.lastUpdateTime = Time.unscaledTime;
        recentEntries.Add(newEntry);

        StartCoroutine(AnimateFloatingText(textInstance));
    }

    //оновлює сам текст і колір за рідкістю
    private void UpdateTextVisual(TextMeshProUGUI targetText, ItemData item, int amount)
    {
        string sign = amount > 0 ? "+" : "";
        string displayName = !string.IsNullOrEmpty(item.itemName) ? item.itemName : item.name;
        targetText.text = $"{sign}{amount} ({displayName})";
        targetText.color = GetColorForRarity(item.rarity);
    }

    //вибір кольору тексту залежно від рідкості предмета
    private Color GetColorForRarity(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Rare:
                return rareColor;
            case ItemRarity.Epic:
                return epicColor;
            case ItemRarity.Legendary:
                return legendaryColor;
            default:
                return commonColor;
        }
    }

    //анімація підйому тексту знизу вгору та плавного зникнення
    private IEnumerator AnimateFloatingText(TextMeshProUGUI targetText)
    {
        RectTransform rect = targetText.rectTransform;
        Vector2 startPos = rect.anchoredPosition;
        Vector2 endPos = startPos + new Vector2(0f, moveUpDistance);

        float elapsed = 0f;

        while (elapsed < displayDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / displayDuration);

            //рух знизу вверх
            rect.anchoredPosition = Vector2.Lerp(startPos, endPos, progress);

            //плавне зникнення у другій половині часу
            if (progress >= fadeStartRatio)
            {
                float fadeProgress = (progress - fadeStartRatio) / (1f - fadeStartRatio);
                Color c = targetText.color;
                c.a = Mathf.Lerp(1f, 0f, fadeProgress);
                targetText.color = c;
            }

            yield return null;
        }

        Destroy(targetText.gameObject);
    }
}