using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Налаштування")]

    public string gameSceneName = "MainGame"; 

    [Header("Нова Гра")]
    public TMP_InputField nameInputField;

    [Header("Завантаження")]
    public Transform slotsContainer;
    public GameObject slotPrefab;
    public Button loadButton;
    public Button deleteButton;

    private SaveSlotUI selectedSlot; 

    void Start()
    {
        if (loadButton != null) loadButton.interactable = false;
        if (deleteButton != null) deleteButton.interactable = false;
    }

    public void OnStartGamePressed()
    {

        string name = nameInputField.text;
        
        SceneManager.LoadScene(gameSceneName);
    }


    public void OnSlotSelected(SaveSlotUI slot)
    {

        if (selectedSlot != null) selectedSlot.SetVisualSelected(false);

        selectedSlot = slot;
        selectedSlot.SetVisualSelected(true);


        if (loadButton != null) loadButton.interactable = true;
        if (deleteButton != null) deleteButton.interactable = true;
    }


    public void OnDeletePressed()
    {
        if (selectedSlot != null)
        {
            string path = Path.Combine(Application.persistentDataPath, selectedSlot.fileName);
            
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            selectedSlot = null;
            
            if (loadButton != null) loadButton.interactable = false;
            if (deleteButton != null) deleteButton.interactable = false;
        }
    }

    public void CreditsMenu()
     {
          SceneManager.LoadScene("Credits");
     }
    public void ExitApplication()
        {
            Application.Quit();
        }
}