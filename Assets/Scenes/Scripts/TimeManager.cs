using UnityEngine;
using UnityEngine.UI;

public class TimeManager : MonoBehaviour
{
    public Image NightSprite;
    public AudioSource SoundSource;
    public AudioClip Night;
    public AudioClip Wind;
    public float timeSpeed = 100.0f;
    
    public float Hours = 12;
    public float Minutes = 0;
    public float Seconds = 0.0f;

    private bool db = true; 

    [Range(0, 1)] public float maxDarkness = 0.8f; 

    void Start()
    {
        if (Hours >= 6 && Hours < 20)
        {
            db = true;
            SoundSource.clip = Wind;
            SoundSource.Play();
        }
        else
        {
            db = false;
            SoundSource.clip = Night;
            SoundSource.Play();
        }
    }

    void Update()
    {
        Seconds += Time.deltaTime * timeSpeed;

        if (Seconds >= 60.0f) {Minutes++; Seconds -= 60.0f;}
        if (Minutes >= 60) {Hours++; Minutes = 0;}
        if (Hours >= 24) {Hours = 0;}
        
        float currentGlobalTime = Hours + (Minutes / 60f);
        float currentAlpha;

        if (currentGlobalTime >= 4f && currentGlobalTime < 6f) { // Ранок
            float t = (currentGlobalTime - 4f) / (6f - 4f);
            currentAlpha = Mathf.Lerp(maxDarkness, 0f, t);
        }
        else if (currentGlobalTime >= 6f && currentGlobalTime < 20f) { // День
            currentAlpha = 0f;
        }
        else if (currentGlobalTime >= 20f && currentGlobalTime < 22f) { // Вечір
            float t = (currentGlobalTime - 20f) / (22f - 20f);
            currentAlpha = Mathf.Lerp(0f, maxDarkness, t);
        }
        else { // Ніч
            currentAlpha = maxDarkness;
        }

        if (NightSprite != null)
        {
            Color color = NightSprite.color;
            color.a = currentAlpha;
            NightSprite.color = color;
        }

        if (currentGlobalTime >= 6f && currentGlobalTime < 20f)
        {
            if (!db)
            {
                SoundSource.clip = Wind;
                SoundSource.Play();
                db = true;
            }
        }
        else if (currentGlobalTime >= 20f || currentGlobalTime < 6f)
        {
            if (db)
            {
                SoundSource.clip = Night;
                SoundSource.Play();
                db = false;
            }
        }
    }
}