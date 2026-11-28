using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class HouseInteriorController : MonoBehaviour
{
    [Header("Roof + Chimneys")]
    [SerializeField] private Tilemap[] roofTilemaps;

    [Header("Interior Ambient Audio (Optional)")]
    [Tooltip("Existing AudioSource(s) inside this building (e.g., fireplace, clock)")]
    [SerializeField] private AudioSource[] interiorAudioSources;
    [Tooltip("Optional clip to auto-play looped while inside this building")]
    [SerializeField] private AudioClip interiorAmbientClip;
    [Range(0f, 1f)][SerializeField] private float interiorClipVolume = 0.6f;

    [Header("Settings")]
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private string playerTag = "Player";

    private bool isPlayerInside = false;
    private Coroutine fadeCoroutine;
    private Coroutine audioFadeCoroutine;
    private AudioSource generatedClipSource;
    private float[] initialInteriorVolumes;

    private void Awake()
    {
        SetRoofAlpha(1f);
        SetupInteriorAudio();
    }

    private void SetupInteriorAudio()
    {
        if (interiorAmbientClip != null)
        {
            generatedClipSource = gameObject.AddComponent<AudioSource>();
            generatedClipSource.clip = interiorAmbientClip;
            generatedClipSource.loop = true;
            generatedClipSource.playOnAwake = false;
            generatedClipSource.volume = 0f;
        }

        if (interiorAudioSources != null && interiorAudioSources.Length > 0)
        {
            initialInteriorVolumes = new float[interiorAudioSources.Length];
            for (int i = 0; i < interiorAudioSources.Length; i++)
            {
                if (interiorAudioSources[i] != null)
                {
                    initialInteriorVolumes[i] = interiorAudioSources[i].volume;
                    interiorAudioSources[i].volume = 0f;
                    interiorAudioSources[i].Stop();
                }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag) || isPlayerInside) return;

        isPlayerInside = true;
        StartRoofFade(0f);
        StartInteriorAudioFade(true);

        if (ScreenOverlayController.Instance != null)
            ScreenOverlayController.Instance.EnterInterior(fadeDuration);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag) || !isPlayerInside) return;

        isPlayerInside = false;
        StartRoofFade(1f);
        StartInteriorAudioFade(false);

        if (ScreenOverlayController.Instance != null)
            ScreenOverlayController.Instance.ExitInterior(fadeDuration);
    }

    private void StartInteriorAudioFade(bool fadeIn)
    {
        if (!isActiveAndEnabled) return;

        if (audioFadeCoroutine != null)
            StopCoroutine(audioFadeCoroutine);

        audioFadeCoroutine = StartCoroutine(FadeInteriorAudioRoutine(fadeIn));
    }

    private IEnumerator FadeInteriorAudioRoutine(bool fadeIn)
    {
        float startGeneratedVol = generatedClipSource != null ? generatedClipSource.volume : 0f;
        float targetGeneratedVol = fadeIn ? interiorClipVolume : 0f;

        if (fadeIn && generatedClipSource != null && !generatedClipSource.isPlaying)
            generatedClipSource.Play();

        float[] startSourceVols = null;
        if (interiorAudioSources != null && interiorAudioSources.Length > 0)
        {
            startSourceVols = new float[interiorAudioSources.Length];
            for (int i = 0; i < interiorAudioSources.Length; i++)
            {
                AudioSource src = interiorAudioSources[i];
                if (src == null) continue;

                startSourceVols[i] = src.volume;
                if (fadeIn && !src.isPlaying && src.clip != null)
                    src.Play();
            }
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);

            if (generatedClipSource != null)
                generatedClipSource.volume = Mathf.Lerp(startGeneratedVol, targetGeneratedVol, t);

            if (interiorAudioSources != null && startSourceVols != null)
            {
                for (int i = 0; i < interiorAudioSources.Length; i++)
                {
                    AudioSource src = interiorAudioSources[i];
                    if (src == null) continue;

                    float targetVol = fadeIn ? initialInteriorVolumes[i] : 0f;
                    src.volume = Mathf.Lerp(startSourceVols[i], targetVol, t);
                }
            }

            yield return null;
        }

        if (generatedClipSource != null)
        {
            generatedClipSource.volume = targetGeneratedVol;
            if (!fadeIn && generatedClipSource.isPlaying)
                generatedClipSource.Stop();
        }

        if (interiorAudioSources != null)
        {
            for (int i = 0; i < interiorAudioSources.Length; i++)
            {
                AudioSource src = interiorAudioSources[i];
                if (src == null) continue;

                src.volume = fadeIn ? initialInteriorVolumes[i] : 0f;
                if (!fadeIn && src.isPlaying)
                    src.Stop();
            }
        }

        audioFadeCoroutine = null;
    }

    private void StartRoofFade(float targetAlpha)
    {
        if (!isActiveAndEnabled)
        {
            SetRoofAlpha(targetAlpha);
            return;
        }

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeRoofRoutine(targetAlpha));
    }

    private IEnumerator FadeRoofRoutine(float targetAlpha)
    {
        float startAlpha = (roofTilemaps != null && roofTilemaps.Length > 0 && roofTilemaps[0] != null)
            ? roofTilemaps[0].color.a : (isPlayerInside ? 1f : 0f);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            SetRoofAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }

        SetRoofAlpha(targetAlpha);
        fadeCoroutine = null;
    }

    private void SetRoofAlpha(float alpha)
    {
        if (roofTilemaps == null) return;

        foreach (var tm in roofTilemaps)
        {
            if (tm == null) continue;
            Color c = tm.color;
            c.a = alpha;
            tm.color = c;
        }
    }
}