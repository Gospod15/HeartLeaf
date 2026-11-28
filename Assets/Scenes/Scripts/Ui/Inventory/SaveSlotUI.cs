using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class SaveSlotUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI worldNameText;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI playtimeText;
    [SerializeField] private Button playButton;

    [Header("Selection Colors & Bounce")]
    [SerializeField] private Color normalTitleColor = Color.white;
    [SerializeField] private Color selectedTitleColor = Color.yellow;
    [SerializeField] private float bounceScale = 1.15f;
    [SerializeField] private float bounceDuration = 0.22f;

    public string SaveSlotName { get; private set; }
    private MainMenuManager menuManager;
    private Vector3 defaultTitleScale = Vector3.one;
    private Coroutine bounceCoroutine;

    private void Awake()
    {
        if (worldNameText != null)
            defaultTitleScale = worldNameText.rectTransform.localScale;
    }

    public void Setup(string slotName, string worldName, string playerName, float playtimeSeconds, MainMenuManager manager)
    {
        SaveSlotName = slotName;
        menuManager = manager;

        if (worldNameText != null)
        {
            worldNameText.text = worldName;
            worldNameText.color = normalTitleColor;
            worldNameText.rectTransform.localScale = defaultTitleScale;
        }

        if (playerNameText != null)
            playerNameText.text = playerName;

        if (playtimeText != null)
        {
            TimeSpan time = TimeSpan.FromSeconds(Mathf.Max(0f, playtimeSeconds));
            playtimeText.text = string.Format("{0:D2}:{1:D2}:{2:D2}", (int)time.TotalHours, time.Minutes, time.Seconds);
        }

        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OnPlayClicked);
        }
    }

    public void SetSelected(bool isSelected)
    {
        if (worldNameText == null) return;

        worldNameText.color = isSelected ? selectedTitleColor : normalTitleColor;

        if (bounceCoroutine != null)
        {
            StopCoroutine(bounceCoroutine);
            bounceCoroutine = null;
        }

        if (isSelected)
        {
            bounceCoroutine = StartCoroutine(BounceTitleRoutine());
        }
        else
        {
            worldNameText.rectTransform.localScale = defaultTitleScale;
        }
    }

    private IEnumerator BounceTitleRoutine()
    {
        RectTransform titleRect = worldNameText.rectTransform;
        Vector3 peakScale = defaultTitleScale * bounceScale;
        float halfDuration = Mathf.Max(0.02f, bounceDuration * 0.5f);

        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / halfDuration));
            titleRect.localScale = Vector3.LerpUnclamped(defaultTitleScale, peakScale, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / halfDuration));
            titleRect.localScale = Vector3.LerpUnclamped(peakScale, defaultTitleScale, t);
            yield return null;
        }

        titleRect.localScale = defaultTitleScale;
        bounceCoroutine = null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (menuManager != null)
            menuManager.SelectSaveSlot(this);
    }

    private void OnPlayClicked()
    {
        if (menuManager != null)
            menuManager.LoadWorldBySlot(SaveSlotName);
    }
}