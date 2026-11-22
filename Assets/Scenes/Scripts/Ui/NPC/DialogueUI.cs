using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueUI : MonoBehaviour
{
    [Header("Panel & Text")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private TextMeshProUGUI npcNameText;
    [SerializeField] private Image npcPortraitImage;
    [SerializeField] private TextMeshProUGUI dialogueBodyText;

    [Header("Choices")]
    [SerializeField] private Transform choicesContainer;
    [SerializeField] private Button choiceButtonPrefab;

    private readonly List<GameObject> spawnedButtons = new List<GameObject>();

    private void Awake()
    {
        if (rootPanel != null)
            rootPanel.SetActive(false);
    }

    public void Show()
    {
        if (rootPanel != null)
            rootPanel.SetActive(true);
    }

    public void Hide()
    {
        ClearChoices();
        if (rootPanel != null)
            rootPanel.SetActive(false);
    }

    public void SetSpeaker(string speakerName, Sprite portrait)
    {
        if (npcNameText != null)
            npcNameText.text = speakerName;

        if (npcPortraitImage != null)
        {
            npcPortraitImage.sprite = portrait;
            npcPortraitImage.gameObject.SetActive(portrait != null);
        }
    }

    public void SetBodyText(string text)
    {
        if (dialogueBodyText != null)
        {
            dialogueBodyText.gameObject.SetActive(!string.IsNullOrEmpty(text));
            dialogueBodyText.text = text;
        }
    }

    public void ClearChoices()
    {
        for (int i = 0; i < spawnedButtons.Count; i++)
        {
            if (spawnedButtons[i] != null)
                Destroy(spawnedButtons[i]);
        }
        spawnedButtons.Clear();

        if (choicesContainer != null)
            choicesContainer.gameObject.SetActive(false);
    }

    public void DisplayChoices(List<DialogueChoice> choices, Action<DialogueChoice> onSelected)
    {
        ClearChoices();
        if (choiceButtonPrefab == null || choicesContainer == null) return;

        SetBodyText(string.Empty);
        choicesContainer.gameObject.SetActive(true);

        foreach (var choice in choices)
        {
            Button btn = Instantiate(choiceButtonPrefab, choicesContainer);
            spawnedButtons.Add(btn.gameObject);

            TextMeshProUGUI btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
                btnText.text = choice.choiceText;

            DialogueChoice capturedChoice = choice;
            btn.onClick.AddListener(() => onSelected?.Invoke(capturedChoice));
        }
    }
}