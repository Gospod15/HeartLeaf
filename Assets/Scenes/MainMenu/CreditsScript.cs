using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
public class CreditsScript : MonoBehaviour
{
    public TMP_Text Credits;
    public Button Button;
    public float speed = 1;
    void Start()
    {
        
    }

    void Update()
    {
        if (Credits.transform.position.y < 100117)
        {
            Credits.transform.position += Vector3.up * speed * Time.deltaTime;
        }
    }

    public void BackMenu()
     {
          SceneManager.LoadScene("MainMenu");
     }    
}
