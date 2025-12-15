using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SaveSlotUI : MonoBehaviour
{
    [Header("UI Components")]
    public TextMeshProUGUI nameText;
    public Image backgroundImage;

    [HideInInspector] public string fileName;
    private MainMenuManager menuManager;

    public void Setup(string file, string playerName, MainMenuManager manager)
    {
        fileName = file;
        nameText.text = playerName;
        menuManager = manager;

        GetComponent<Button>().onClick.AddListener(OnClicked);
    }

    void OnClicked()
    {
        menuManager.OnSlotSelected(this);
    }

    public void SetVisualSelected(bool isSelected)
    {
        if (isSelected)
            backgroundImage.color = Color.green;
        else
            backgroundImage.color = Color.white;
    }
}