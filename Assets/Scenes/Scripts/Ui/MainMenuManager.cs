using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("Scenes & Save Path")]
    [SerializeField] private string gameSceneName = "MainGame";
    [SerializeField] private string authorsSceneName = "Credits";
    [SerializeField] private string savesFolderName = "Heartleaf";
    [SerializeField] private string savesSubfolder = "Saves";

    [Header("Panels (RectTransform)")]
    [SerializeField] private RectTransform menuPanel;
    [SerializeField] private RectTransform panelMenu;
    [SerializeField] private RectTransform settingsPanel;

    [Header("Panel Slide Animation")]
    [SerializeField] private float slideDuration = 0.35f;
    [SerializeField] private float menuHiddenOffsetX = -900f;
    [SerializeField] private float subPanelHiddenOffsetY = -800f;

    [Header("Saves List")]
    [SerializeField] private Transform slotsContainer;
    [SerializeField] private SaveSlotUI slotPrefab;

    [Header("Create World Inputs")]
    [SerializeField] private TMP_InputField entryWorldName;
    [SerializeField] private TMP_InputField entryWorldSeed;
    [SerializeField] private TMP_InputField entryPlayerName;

    [Header("Settings UI")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Toggle fullscreenToggle;

    private SaveSlotUI selectedSlot;

    private Vector2 menuShownPos;
    private Vector2 panelMenuShownPos;
    private Vector2 settingsShownPos;

    private Coroutine menuAnimCoroutine;
    private Coroutine panelMenuAnimCoroutine;
    private Coroutine settingsAnimCoroutine;

    private void Awake()
    {
        if (menuPanel != null) menuShownPos = menuPanel.anchoredPosition;
        if (panelMenu != null) panelMenuShownPos = panelMenu.anchoredPosition;
        if (settingsPanel != null) settingsShownPos = settingsPanel.anchoredPosition;
    }

    private void Start()
    {
        Time.timeScale = 1f;
        SetupSeedInputValidation();
        LoadSettingsUI();
        SetInstantInitialState();
    }

    private void SetInstantInitialState()
    {
        if (menuPanel != null)
        {
            menuPanel.gameObject.SetActive(true);
            menuPanel.anchoredPosition = menuShownPos;
        }

        if (panelMenu != null)
        {
            panelMenu.anchoredPosition = panelMenuShownPos + new Vector2(0f, subPanelHiddenOffsetY);
            panelMenu.gameObject.SetActive(false);
        }

        if (settingsPanel != null)
        {
            settingsPanel.anchoredPosition = settingsShownPos + new Vector2(0f, subPanelHiddenOffsetY);
            settingsPanel.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (IsEscapePressed())
        {
            if (panelMenu != null && panelMenu.gameObject.activeSelf)
            {
                ClosePanelMenu();
            }
            else if (settingsPanel != null && settingsPanel.gameObject.activeSelf)
            {
                CloseSettingsPanel();
            }
        }
    }

    private bool IsEscapePressed()
    {
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }

    private void SetupSeedInputValidation()
    {
        if (entryWorldSeed == null) return;

        entryWorldSeed.contentType = TMP_InputField.ContentType.Custom;
        entryWorldSeed.characterLimit = 10;
        entryWorldSeed.onValueChanged.RemoveListener(SanitizeSeedInput);
        entryWorldSeed.onValueChanged.AddListener(SanitizeSeedInput);
    }

    private void SanitizeSeedInput(string rawValue)
    {
        if (entryWorldSeed == null || string.IsNullOrEmpty(rawValue)) return;

        StringBuilder clean = new StringBuilder(rawValue.Length);
        for (int i = 0; i < rawValue.Length; i++)
        {
            char c = rawValue[i];
            if (c >= '0' && c <= '9')
            {
                clean.Append(c);
            }
            else if (c == '-' && clean.Length == 0)
            {
                clean.Append(c);
            }
        }

        string filtered = clean.ToString();
        if (filtered != rawValue)
        {
            entryWorldSeed.SetTextWithoutNotify(filtered);
            entryWorldSeed.caretPosition = filtered.Length;
        }
    }

    public void OpenPanelMenu()
    {
        if (settingsPanel != null && settingsPanel.gameObject.activeSelf)
        {
            AnimatePanel(settingsPanel, ref settingsAnimCoroutine, settingsShownPos + new Vector2(0f, subPanelHiddenOffsetY), false);
        }

        if (menuPanel != null)
        {
            AnimatePanel(menuPanel, ref menuAnimCoroutine, menuShownPos + new Vector2(menuHiddenOffsetX, 0f), false);
        }

        if (panelMenu != null)
        {
            if (!panelMenu.gameObject.activeSelf)
            {
                panelMenu.anchoredPosition = panelMenuShownPos + new Vector2(0f, subPanelHiddenOffsetY);
                panelMenu.gameObject.SetActive(true);
            }
            AnimatePanel(panelMenu, ref panelMenuAnimCoroutine, panelMenuShownPos, true);
        }

        RefreshSavesList();
    }

    public void ClosePanelMenu()
    {
        selectedSlot = null;

        if (panelMenu != null && panelMenu.gameObject.activeSelf)
        {
            AnimatePanel(panelMenu, ref panelMenuAnimCoroutine, panelMenuShownPos + new Vector2(0f, subPanelHiddenOffsetY), false);
        }

        if (menuPanel != null)
        {
            if (!menuPanel.gameObject.activeSelf)
            {
                menuPanel.anchoredPosition = menuShownPos + new Vector2(menuHiddenOffsetX, 0f);
                menuPanel.gameObject.SetActive(true);
            }
            AnimatePanel(menuPanel, ref menuAnimCoroutine, menuShownPos, true);
        }
    }

    public void OpenSettingsPanel()
    {
        if (panelMenu != null && panelMenu.gameObject.activeSelf)
        {
            AnimatePanel(panelMenu, ref panelMenuAnimCoroutine, panelMenuShownPos + new Vector2(0f, subPanelHiddenOffsetY), false);
        }

        if (menuPanel != null)
        {
            AnimatePanel(menuPanel, ref menuAnimCoroutine, menuShownPos + new Vector2(menuHiddenOffsetX, 0f), false);
        }

        if (settingsPanel != null)
        {
            if (!settingsPanel.gameObject.activeSelf)
            {
                settingsPanel.anchoredPosition = settingsShownPos + new Vector2(0f, subPanelHiddenOffsetY);
                settingsPanel.gameObject.SetActive(true);
            }
            AnimatePanel(settingsPanel, ref settingsAnimCoroutine, settingsShownPos, true);
        }
    }

    public void CloseSettingsPanel()
    {
        if (settingsPanel != null && settingsPanel.gameObject.activeSelf)
        {
            AnimatePanel(settingsPanel, ref settingsAnimCoroutine, settingsShownPos + new Vector2(0f, subPanelHiddenOffsetY), false);
        }

        if (menuPanel != null)
        {
            if (!menuPanel.gameObject.activeSelf)
            {
                menuPanel.anchoredPosition = menuShownPos + new Vector2(menuHiddenOffsetX, 0f);
                menuPanel.gameObject.SetActive(true);
            }
            AnimatePanel(menuPanel, ref menuAnimCoroutine, menuShownPos, true);
        }
    }

    private void AnimatePanel(RectTransform target, ref Coroutine runningCoroutine, Vector2 destination, bool keepActiveAtEnd)
    {
        if (runningCoroutine != null)
            StopCoroutine(runningCoroutine);

        runningCoroutine = StartCoroutine(SlideRoutine(target, destination, keepActiveAtEnd));
    }

    private IEnumerator SlideRoutine(RectTransform target, Vector2 destination, bool keepActiveAtEnd)
    {
        Vector2 startPos = target.anchoredPosition;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, slideDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            target.anchoredPosition = Vector2.LerpUnclamped(startPos, destination, t);
            yield return null;
        }

        target.anchoredPosition = destination;
        if (!keepActiveAtEnd)
            target.gameObject.SetActive(false);
    }

    public void OpenAuthorsScene()
    {
        if (string.IsNullOrWhiteSpace(authorsSceneName)) return;
        Time.timeScale = 1f;
        SceneManager.LoadScene(authorsSceneName);
    }

    public void RefreshSavesList()
    {
        selectedSlot = null;
        if (slotsContainer == null || slotPrefab == null) return;

        for (int i = slotsContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(slotsContainer.GetChild(i).gameObject);
        }

        string dir = GetSavesDirectory();
        string[] files = Directory.GetFiles(dir, "*.json");

        Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));

        for (int i = 0; i < files.Length; i++)
        {
            try
            {
                WorldSaveFile saveFile = JsonUtility.FromJson<WorldSaveFile>(File.ReadAllText(files[i]));
                if (saveFile == null) continue;

                string slotName = Path.GetFileNameWithoutExtension(files[i]);
                WorldSettingsData settings = saveFile.worldSettings ?? new WorldSettingsData();

                string worldTitle = !string.IsNullOrWhiteSpace(settings.worldName) ? settings.worldName : slotName;
                string playerTitle = !string.IsNullOrWhiteSpace(settings.playerName) ? settings.playerName : "Player";

                SaveSlotUI slotInstance = Instantiate(slotPrefab, slotsContainer);
                slotInstance.Setup(slotName, worldTitle, playerTitle, saveFile.playTimeSeconds, this);
            }
            catch
            {
            }
        }
    }

    public void SelectSaveSlot(SaveSlotUI slot)
    {
        if (selectedSlot != null)
            selectedSlot.SetSelected(false);

        selectedSlot = slot;

        if (selectedSlot != null)
            selectedSlot.SetSelected(true);
    }

    public void LoadWorldBySlot(string slotName)
    {
        if (string.IsNullOrEmpty(slotName)) return;

        GameSessionContext.LoadExistingWorld(slotName);
        SceneManager.LoadScene(gameSceneName);
    }

    public void DeleteSelectedWorld()
    {
        if (selectedSlot == null) return;

        DeleteWorldBySlot(selectedSlot.SaveSlotName);
    }

    public void DeleteWorldBySlot(string slotName)
    {
        if (string.IsNullOrEmpty(slotName)) return;

        string path = Path.Combine(GetSavesDirectory(), $"{slotName}.json");
        if (File.Exists(path))
            File.Delete(path);

        RefreshSavesList();
    }

    public void OnCreateWorldPressed()
    {
        string rawWorldName = entryWorldName != null ? entryWorldName.text.Trim() : "";
        if (string.IsNullOrEmpty(rawWorldName))
            rawWorldName = "Bizzare world";

        string uniqueSlotName = GetUniqueSlotName(rawWorldName);

        string playerName = entryPlayerName != null ? entryPlayerName.text.Trim() : "";
        if (string.IsNullOrEmpty(playerName))
            playerName = "petruk";

        int finalSeed = ParseOrGenerateSeed(entryWorldSeed != null ? entryWorldSeed.text : "");

        WorldSettingsData newSettings = new WorldSettingsData
        {
            worldName = rawWorldName,
            playerName = playerName,
            worldSeed = finalSeed,
            difficulty = 1
        };

        WorldSaveFile initialSave = new WorldSaveFile
        {
            worldSeed = finalSeed,
            playTimeSeconds = 0f,
            lastSavedUtc = DateTime.UtcNow.ToString("o"),
            worldSettings = newSettings
        };

        string savePath = Path.Combine(GetSavesDirectory(), $"{uniqueSlotName}.json");
        File.WriteAllText(savePath, JsonUtility.ToJson(initialSave, true));

        GameSessionContext.StartNewWorld(uniqueSlotName, playerName, finalSeed);
        GameSessionContext.PendingWorldSettings.worldName = rawWorldName;

        SceneManager.LoadScene(gameSceneName);
    }

    private int ParseOrGenerateSeed(string rawSeed)
    {
        if (string.IsNullOrWhiteSpace(rawSeed) || rawSeed.Trim() == "-")
            return UnityEngine.Random.Range(-999999, 999999);

        if (int.TryParse(rawSeed.Trim(), out int numericSeed))
            return numericSeed;

        return UnityEngine.Random.Range(-999999, 999999);
    }

    private string GetUniqueSlotName(string desiredName)
    {
        string baseClean = GameSessionContext.SanitizeFileName(desiredName);
        string dir = GetSavesDirectory();
        string candidate = baseClean;
        int counter = 1;

        while (File.Exists(Path.Combine(dir, $"{candidate}.json")))
        {
            candidate = $"{baseClean}_{counter}";
            counter++;
        }

        return candidate;
    }

    private void LoadSettingsUI()
    {
        float savedVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        AudioListener.volume = savedVolume;
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = savedVolume;
            masterVolumeSlider.onValueChanged.RemoveAllListeners();
            masterVolumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        bool isFullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        Screen.fullScreen = isFullscreen;
        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = isFullscreen;
            fullscreenToggle.onValueChanged.RemoveAllListeners();
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        }
    }

    public void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("MasterVolume", value);
        PlayerPrefs.Save();
    }

    public void OnFullscreenChanged(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void ExitApplication()
    {
        Application.Quit();
    }

    private string GetSavesDirectory()
    {
        string roamingPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string fullDir = Path.Combine(roamingPath, savesFolderName, savesSubfolder);
        if (!Directory.Exists(fullDir))
            Directory.CreateDirectory(fullDir);
        return fullDir;
    }
}