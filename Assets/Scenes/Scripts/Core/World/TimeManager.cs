using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [Header("Lighting References")]
    [SerializeField] private Image nightOverlay;

    [Header("Ambient Audio")]
    [SerializeField] private AudioSource primaryAudioSource;
    [SerializeField] private AudioSource secondaryAudioSource;
    [SerializeField] private AudioClip dayAmbientClip;
    [SerializeField] private AudioClip nightAmbientClip;
    [SerializeField][Range(0f, 1f)] private float ambientVolume = 0.5f;
    [SerializeField] private float audioFadeDuration = 2.5f;

    [Header("Time Settings")]
    [SerializeField] private float timeSpeed = 60f;
    [SerializeField] private int startDay = 1;
    [SerializeField][Range(0, 23)] private int startHour = 8;
    [SerializeField][Range(0, 59)] private int startMinute = 0;
    [SerializeField] private float dayStartHour = 6f;
    [SerializeField] private float nightStartHour = 20f;

    private Gradient overlayGradient;
    private int currentDay;
    private float timeOfDaySeconds;
    private bool isNight;
    private bool usePrimarySource = true;
    private Coroutine audioFadeRoutine;

    private const float SecondsPerDay = 86400f;

    public event Action<int> OnDayChanged;
    public event Action OnDayStarted;
    public event Action OnNightStarted;

    public int CurrentDay => currentDay;
    public int Hours => Mathf.FloorToInt(timeOfDaySeconds / 3600f) % 24;
    public int Minutes => Mathf.FloorToInt((timeOfDaySeconds % 3600f) / 60f);
    public float NormalizedTime => timeOfDaySeconds / SecondsPerDay;
    public float CurrentHourFloat => timeOfDaySeconds / 3600f;
    public bool IsNight => isNight;
    public string FormattedTime => $"{Hours:00}:{Minutes:00}";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        BuildGradient();
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();

        currentDay = Mathf.Max(1, startDay);
        timeOfDaySeconds = (startHour * 3600f) + (startMinute * 60f);

        isNight = CheckIsNight(CurrentHourFloat);
        ApplyLighting(NormalizedTime);
        InitializeAmbientAudio();
    }

    private void Update()
    {
        if (nightOverlay == null)
        {
            ResolveOverlayOnly();
        }

        float effectiveSpeed = timeSpeed;
        if (WorldSaveManager.instance != null && WorldSaveManager.instance.ActiveWorldSettings != null)
        {
            float mult = WorldSaveManager.instance.ActiveWorldSettings.dayLengthMultiplier;
            if (mult > 0.01f)
                effectiveSpeed /= mult;
        }

        AdvanceTime(Time.deltaTime * effectiveSpeed);
        ApplyLighting(NormalizedTime);
        UpdateDayNightState();
    }

    public void SetTime(int day, int hour, int minute)
    {
        currentDay = Mathf.Max(1, day);
        timeOfDaySeconds = Mathf.Clamp(hour, 0, 23) * 3600f + Mathf.Clamp(minute, 0, 59) * 60f;

        bool newNightState = CheckIsNight(CurrentHourFloat);
        if (newNightState != isNight)
        {
            isNight = newNightState;
            SwitchAmbientAudio(isNight);
        }

        ApplyLighting(NormalizedTime);
    }

    private void ResolveOverlayOnly()
    {
        GameObject overlayObj = GameObject.Find("DayNightOverlay");
        if (overlayObj != null)
        {
            nightOverlay = overlayObj.GetComponent<Image>();
            if (nightOverlay != null)
            {
                nightOverlay.raycastTarget = false;
                nightOverlay.transform.SetAsFirstSibling();
            }
        }
    }

    private void ResolveReferences()
    {
        if (nightOverlay == null)
        {
            ResolveOverlayOnly();
        }
        else
        {
            nightOverlay.raycastTarget = false;
            nightOverlay.transform.SetAsFirstSibling();
        }

        if (primaryAudioSource == null)
        {
            primaryAudioSource = GetComponent<AudioSource>();
            if (primaryAudioSource == null)
                primaryAudioSource = gameObject.AddComponent<AudioSource>();
        }

        if (secondaryAudioSource == null)
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != primaryAudioSource)
                {
                    secondaryAudioSource = sources[i];
                    break;
                }
            }

            if (secondaryAudioSource == null)
                secondaryAudioSource = gameObject.AddComponent<AudioSource>();
        }

        primaryAudioSource.playOnAwake = false;
        secondaryAudioSource.playOnAwake = false;
    }

    private void BuildGradient()
    {
        overlayGradient = new Gradient();
        overlayGradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.04f, 0.06f, 0.22f), 0.00f),
                new GradientColorKey(new Color(0.95f, 0.42f, 0.18f), 0.23f),
                new GradientColorKey(new Color(0.04f, 0.06f, 0.22f), 0.30f),
                new GradientColorKey(new Color(0.04f, 0.06f, 0.22f), 0.78f),
                new GradientColorKey(new Color(0.95f, 0.38f, 0.16f), 0.85f),
                new GradientColorKey(new Color(0.04f, 0.06f, 0.22f), 0.93f),
                new GradientColorKey(new Color(0.04f, 0.06f, 0.22f), 1.00f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.75f, 0.00f),
                new GradientAlphaKey(0.30f, 0.23f),
                new GradientAlphaKey(0.00f, 0.30f),
                new GradientAlphaKey(0.00f, 0.78f),
                new GradientAlphaKey(0.35f, 0.85f),
                new GradientAlphaKey(0.75f, 0.93f),
                new GradientAlphaKey(0.75f, 1.00f)
            }
        );
    }

    private void AdvanceTime(float deltaGameSeconds)
    {
        timeOfDaySeconds += deltaGameSeconds;
        while (timeOfDaySeconds >= SecondsPerDay)
        {
            timeOfDaySeconds -= SecondsPerDay;
            currentDay++;
            OnDayChanged?.Invoke(currentDay);
        }
    }

    private bool CheckIsNight(float hour)
    {
        return hour >= nightStartHour || hour < dayStartHour;
    }

    private void UpdateDayNightState()
    {
        bool shouldBeNight = CheckIsNight(CurrentHourFloat);
        if (shouldBeNight == isNight) return;

        isNight = shouldBeNight;
        SwitchAmbientAudio(isNight);

        if (isNight)
            OnNightStarted?.Invoke();
        else
            OnDayStarted?.Invoke();
    }

    private void ApplyLighting(float normalizedTime)
    {
        if (nightOverlay == null || overlayGradient == null) return;

        if (!nightOverlay.gameObject.activeInHierarchy)
            nightOverlay.gameObject.SetActive(true);

        if (!nightOverlay.enabled)
            nightOverlay.enabled = true;

        nightOverlay.color = overlayGradient.Evaluate(normalizedTime);
    }

    private void InitializeAmbientAudio()
    {
        if (primaryAudioSource == null) return;

        AudioClip initialClip = isNight ? nightAmbientClip : dayAmbientClip;
        if (initialClip == null) return;

        primaryAudioSource.clip = initialClip;
        primaryAudioSource.loop = true;
        primaryAudioSource.volume = ambientVolume;
        primaryAudioSource.Play();

        if (secondaryAudioSource != null)
        {
            secondaryAudioSource.loop = true;
            secondaryAudioSource.volume = 0f;
        }
    }

    private void SwitchAmbientAudio(bool night)
    {
        AudioClip targetClip = night ? nightAmbientClip : dayAmbientClip;
        if (targetClip == null || primaryAudioSource == null) return;

        if (audioFadeRoutine != null)
            StopCoroutine(audioFadeRoutine);

        audioFadeRoutine = StartCoroutine(CrossfadeAudioRoutine(targetClip));
    }

    private IEnumerator CrossfadeAudioRoutine(AudioClip nextClip)
    {
        AudioSource outgoing = usePrimarySource ? primaryAudioSource : secondaryAudioSource;
        AudioSource incoming = usePrimarySource ? secondaryAudioSource : primaryAudioSource;
        usePrimarySource = !usePrimarySource;

        incoming.clip = nextClip;
        incoming.volume = 0f;
        incoming.Play();

        float startOutgoingVolume = outgoing.volume;
        float elapsed = 0f;

        while (elapsed < audioFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / audioFadeDuration);
            outgoing.volume = Mathf.Lerp(startOutgoingVolume, 0f, t);
            incoming.volume = Mathf.Lerp(0f, ambientVolume, t);
            yield return null;
        }

        outgoing.volume = 0f;
        outgoing.Stop();
        incoming.volume = ambientVolume;
        audioFadeRoutine = null;
    }
}