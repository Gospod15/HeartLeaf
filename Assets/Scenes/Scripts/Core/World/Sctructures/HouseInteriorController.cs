using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class HouseInteriorController : MonoBehaviour
{
    [Header("Roof + Chimneys")]
    [SerializeField] private Tilemap[] roofTilemaps;

    [Header("Settings")]
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private string playerTag = "Player";

    private bool isPlayerInside = false;
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        SetRoofAlpha(1f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag) || isPlayerInside) return;

        isPlayerInside = true;
        StartRoofFade(0f);

        if (ScreenOverlayController.Instance != null)
            ScreenOverlayController.Instance.EnterInterior(fadeDuration);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag) || !isPlayerInside) return;

        isPlayerInside = false;
        StartRoofFade(1f);

        if (ScreenOverlayController.Instance != null)
            ScreenOverlayController.Instance.ExitInterior(fadeDuration);
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