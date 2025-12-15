using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; 

public class PauseMenuController : MonoBehaviour
{
    [Header("UI Елементи")]
    public GameObject pauseMenuPanel;

    private bool isPaused = false;

    void Start()
    {
        Time.timeScale = 1f; 
        isPaused = false;

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            GameObject inventory = GameObject.Find("Inventory"); 
            if (inventory != null && inventory.activeSelf)
            {
                inventory.SetActive(false);
                return;
            }

            if (isPaused) ResumeGame();
            else PauseGame();
        }
    }

    public void PauseGame()
    {
        pauseMenuPanel.SetActive(true);
        Time.timeScale = 0.1f;
        isPaused = true;
    }

    public void ResumeGame()
    {
        pauseMenuPanel.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;
    }


    public void OnExitButtonClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu"); 
    }
}