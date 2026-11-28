using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Panel of istruments")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private GameObject hotbarPanel;
    [SerializeField] private GameObject characterPanel;
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private GameObject craftingPanel;
    [SerializeField] private GameObject chestPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject pauseSettingsPanel;

    [Header("Player Info UI")]
    [SerializeField] private TextMeshProUGUI playerNameText;

    [Header("Pause Settings UI")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Toggle fullscreenToggle;

    [Header("Scenes & Save Path")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private string savesFolderName = "Heartleaf";
    [SerializeField] private string savesSubfolder = "Saves";

    [Header("Inventory position")]
    [SerializeField] private Vector2 defaultInventoryPosition = Vector2.zero;
    [SerializeField] private Vector2 chestInventoryPosition = new Vector2(261f, 78.2f);

    private RectTransform inventoryRect;
    private PlayerInteractor playerInteractor;
    private bool isDialogueOpen = false;

    public GameObject InventoryPanel => inventoryPanel;
    public GameObject HotbarPanel => hotbarPanel;
    public GameObject CharacterPanel => characterPanel;
    public GameObject CraftingPanel => craftingPanel;
    public GameObject ChestPanel => chestPanel;

    public bool IsInventoryOpen => inventoryPanel != null && inventoryPanel.activeSelf;
    public bool IsShopOpen => shopPanel != null && shopPanel.activeSelf;
    public bool IsChestOpen => chestPanel != null && chestPanel.activeSelf;
    public bool IsPauseOpen => (pausePanel != null && pausePanel.activeSelf) ||
                               (pauseSettingsPanel != null && pauseSettingsPanel.activeSelf);
    public bool IsDialogueOpen => isDialogueOpen;

    public bool IsGameplayInputBlocked =>
        IsInventoryOpen ||
        IsShopOpen ||
        IsChestOpen ||
        IsPauseOpen ||
        isDialogueOpen ||
        (craftingPanel != null && craftingPanel.activeSelf);

    public event Action<bool> OnInventoryStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (inventoryPanel != null)
        {
            inventoryRect = inventoryPanel.GetComponent<RectTransform>();
        }
    }

    private void Start()
    {
        CloseAllWindows();
        if (hotbarPanel != null) hotbarPanel.SetActive(true);
        LoadSettingsUI();
        UpdatePlayerNameDisplay();
        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.tabKey.wasPressedThisFrame && !IsPauseOpen && !isDialogueOpen)
        {
            HandleTabPressed();
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            HandleEscape();
        }
    }

    public void UpdatePlayerNameDisplay()
    {
        if (playerNameText == null) return;

        string resolvedName = ResolveCurrentPlayerName();
        if (!string.IsNullOrWhiteSpace(resolvedName))
        {
            playerNameText.text = resolvedName;
        }
    }

    private string ResolveCurrentPlayerName()
    {
        if (GameSessionContext.PendingWorldSettings != null &&
            !string.IsNullOrWhiteSpace(GameSessionContext.PendingWorldSettings.playerName))
        {
            return GameSessionContext.PendingWorldSettings.playerName;
        }

        string slotName = GameSessionContext.CurrentSaveSlot;
        if (!string.IsNullOrWhiteSpace(slotName))
        {
            string roamingPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string savePath = Path.Combine(roamingPath, savesFolderName, savesSubfolder, $"{slotName}.json");

            if (File.Exists(savePath))
            {
                try
                {
                    WorldSaveFile saveFile = JsonUtility.FromJson<WorldSaveFile>(File.ReadAllText(savePath));
                    if (saveFile != null && saveFile.worldSettings != null &&
                        !string.IsNullOrWhiteSpace(saveFile.worldSettings.playerName))
                    {
                        return saveFile.worldSettings.playerName;
                    }
                }
                catch
                {
                }
            }
        }

        return "Player";
    }

    private void HandleTabPressed()
    {
        if (IsChestOpen)
        {
            if (ChestUI.Instance != null)
                ChestUI.Instance.Close();
            SetInventoryState(false);
            return;
        }

        if (IsShopOpen)
        {
            CloseShop();
            SetInventoryState(false);
            return;
        }

        if (IsInventoryOpen)
        {
            SetInventoryState(false);
            return;
        }

        if (playerInteractor == null)
            playerInteractor = FindFirstObjectByType<PlayerInteractor>();

        if (playerInteractor != null && playerInteractor.TryOpenNearbyChest())
            return;

        SetInventoryState(true);
    }

    public void SetDialogueState(bool isOpen)
    {
        isDialogueOpen = isOpen;
        if (hotbarPanel != null && !IsInventoryOpen)
            hotbarPanel.SetActive(!isOpen);
    }

    public void ToggleInventory()
    {
        SetInventoryState(!IsInventoryOpen);
    }

    public void SetInventoryState(bool isOpen)
    {
        if (!isOpen)
        {
            if (InventoryDragController.Instance != null)
                InventoryDragController.Instance.ReturnHeldItemOnClose();

            if (InventoryManager.instance != null)
                InventoryManager.instance.ReturnCraftingItemsToInventory();

            if (ContextMenuController.Instance != null)
                ContextMenuController.Instance.ClearSelection();

            if (ChestUI.Instance != null && ChestUI.Instance.IsOpen)
                ChestUI.Instance.Close();
        }
        else
        {
            UpdatePlayerNameDisplay();
        }

        if (inventoryRect != null)
            inventoryRect.anchoredPosition = defaultInventoryPosition;

        if (inventoryPanel != null) inventoryPanel.SetActive(isOpen);
        if (characterPanel != null) characterPanel.SetActive(isOpen);
        if (craftingPanel != null) craftingPanel.SetActive(isOpen);
        if (hotbarPanel != null) hotbarPanel.SetActive(!isOpen);

        OnInventoryStateChanged?.Invoke(isOpen);
    }

    public void OpenChestInventory()
    {
        if (inventoryRect != null)
            inventoryRect.anchoredPosition = chestInventoryPosition;

        if (inventoryPanel != null) inventoryPanel.SetActive(true);
        if (characterPanel != null) characterPanel.SetActive(false);
        if (craftingPanel != null) craftingPanel.SetActive(false);
        if (hotbarPanel != null) hotbarPanel.SetActive(false);

        OnInventoryStateChanged?.Invoke(true);
    }

    public void OpenShop()
    {
        if (shopPanel != null) shopPanel.SetActive(true);
        SetInventoryState(true);
    }

    public void CloseShop()
    {
        if (shopPanel != null) shopPanel.SetActive(false);
    }

    public void ToggleCrafting()
    {
        if (craftingPanel == null) return;
        bool nextState = !craftingPanel.activeSelf;
        craftingPanel.SetActive(nextState);
        if (nextState) SetInventoryState(true);
    }

    public void SetPauseState(bool isPaused)
    {
        if (pauseSettingsPanel != null) pauseSettingsPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(isPaused);
        Time.timeScale = isPaused ? 0f : 1f;
    }

    public void ResumeGame()
    {
        SetPauseState(false);
    }

    public void OpenPauseSettings()
    {
        LoadSettingsUI();
        if (pausePanel != null) pausePanel.SetActive(false);
        if (pauseSettingsPanel != null) pauseSettingsPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void ClosePauseSettings()
    {
        if (pauseSettingsPanel != null) pauseSettingsPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(true);
    }

    public void SaveAndReturnToMainMenu()
    {
        if (InventoryDragController.Instance != null)
            InventoryDragController.Instance.ReturnHeldItemOnClose();

        if (InventoryManager.instance != null)
            InventoryManager.instance.ReturnCraftingItemsToInventory();

        TriggerWorldSave();

        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void TriggerWorldSave()
    {
        if (WorldSaveManager.instance == null) return;

        Type saveType = WorldSaveManager.instance.GetType();
        string[] candidateMethods = { "SaveWorld", "SaveGame", "ForceSave", "OnApplicationQuit" };

        for (int i = 0; i < candidateMethods.Length; i++)
        {
            MethodInfo method = saveType.GetMethod(
                candidateMethods[i],
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null
            );

            if (method != null)
            {
                method.Invoke(WorldSaveManager.instance, null);
                return;
            }
        }
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

    private void HandleEscape()
    {
        if (pauseSettingsPanel != null && pauseSettingsPanel.activeSelf)
        {
            ClosePauseSettings();
            return;
        }
        if (isDialogueOpen && DialogueManager.Instance != null)
        {
            DialogueManager.Instance.EndDialogue();
            return;
        }
        if (IsChestOpen && ChestUI.Instance != null)
        {
            ChestUI.Instance.Close();
            SetInventoryState(false);
            return;
        }
        if (IsShopOpen) { CloseShop(); return; }
        if (craftingPanel != null && craftingPanel.activeSelf) { craftingPanel.SetActive(false); return; }
        if (IsInventoryOpen) { SetInventoryState(false); return; }

        SetPauseState(!IsPauseOpen);
    }

    private void CloseAllWindows()
    {
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (characterPanel != null) characterPanel.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);
        if (craftingPanel != null) craftingPanel.SetActive(false);
        if (chestPanel != null) chestPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (pauseSettingsPanel != null) pauseSettingsPanel.SetActive(false);
    }
}