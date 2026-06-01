using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public class DayNightCycle : MonoBehaviour
{
    [Header("Lights")]
    public Light sunLight;
    public Light moonLight;

    [Header("Day Night Settings")]
    public float dayDuration = 60f; // Duration of a full day in seconds
    [Range(0, 1)]
    public float currentTimeOfDay = 0f; // 0 = Midnight, 0.5 = Noon

    [Header("Sun Light Settings")]
    public float sunEmission = 1.0f;
    public float sunTemperature = 6500f;
    public float sunIntensity = 1.0f;

    [Header("Moon Light Settings")]
    public float moonEmission = 1.0f;
    public float moonTemperature = 4000f;
    public float moonIntensity = 0.5f;

    [Header("Sky and Glow Settings")]
    public GameObject Sky;
    public GameObject Glow;

    [Header("Sky and Glow Speed Multiplier")]
    public float skyGlowSpeedMultiplier = 1.0f; // Default multiplier for speed

    [Header("Initial Animation Offset")]
    [Range(0, 1)]
    public float initialAnimationOffset = 0f; // Preset initial position for the animations

    [Header("Invert Animation Direction")]
    public bool invertAnimationDirection = false; // Switch to invert the animation direction

    [Header("Volume Profile Post Exposure")]
    [Tooltip("Optional URP Volume Profile to update automatically during the day/night cycle.")]
    public VolumeProfile volumeProfile;

    [Tooltip("When enabled, the assigned Volume Profile's Color Adjustments > Post Exposure value is updated from time of day.")]
    public bool controlPostExposure = true;

    [Tooltip("Post Exposure value used at noon. Example: 0.")]
    public float dayPostExposure = 0f;

    [Tooltip("Post Exposure value used at midnight. Example: -1.")]
    public float nightPostExposure = -1f;

    [Tooltip("If enabled, Color Adjustments is added to the assigned Volume Profile when it is missing.")]
    public bool addColorAdjustmentsIfMissing = true;

    [Header("Day Night Events")]
    public UnityEvent OnDayStart; // Event triggered when day starts
    public UnityEvent OnNightStart; // Event triggered when night starts

    private float timeMultiplier;
    private float previousDayDuration;
    private float previousTimeOfDay;

    private bool isDay;

    private void OnValidate()
    {
        currentTimeOfDay = Mathf.Repeat(currentTimeOfDay, 1f);

        ApplyLightSettings();
        RotateLights();
        HandleLightActivation();
        UpdateSkyAndGlowProgress(true); // Force update on validate
        UpdateVolumePostExposure();
    }

    private void Start()
    {
        previousDayDuration = dayDuration;
        previousTimeOfDay = currentTimeOfDay;
        isDay = currentTimeOfDay < 0.5f;
        ApplyLightSettings();
        UpdateSkyAndGlowProgress(true); // Force update on start
        UpdateVolumePostExposure();
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            UpdateTimeOfDay();
        }

        RotateLights();
        HandleLightActivation();
        UpdateVolumePostExposure();

        if (dayDuration != previousDayDuration || currentTimeOfDay != previousTimeOfDay)
        {
            UpdateSkyAndGlowProgress();
            CheckDayNightTransition();
            previousDayDuration = dayDuration;
            previousTimeOfDay = currentTimeOfDay;
        }
    }

    private void UpdateTimeOfDay()
    {
        if (dayDuration <= 0f)
        {
            return;
        }

        timeMultiplier = 24 / dayDuration;
        currentTimeOfDay += (Time.deltaTime / dayDuration) * timeMultiplier;
        currentTimeOfDay = Mathf.Repeat(currentTimeOfDay, 1f); // Keep in the range of 0 to 1
    }

    private void RotateLights()
    {
        float sunRotation = currentTimeOfDay * 360f - 90f; // Sun starts at -90 degrees
        float moonRotation = (currentTimeOfDay * 360f - 270f) % 360f; // Moon starts at -270 degrees

        if (sunLight != null)
        {
            sunLight.transform.rotation = Quaternion.Euler(sunRotation, 170f, 0f);
        }

        if (moonLight != null)
        {
            moonLight.transform.rotation = Quaternion.Euler(moonRotation, 170f, 0f);
        }
    }

    private void HandleLightActivation()
    {
        if (sunLight != null)
        {
            float sunAngle = sunLight.transform.eulerAngles.x;

            // Activate/deactivate sun based on its angle
            if (sunAngle > 0 && sunAngle < 180)
            {
                if (!sunLight.gameObject.activeSelf)
                    sunLight.gameObject.SetActive(true);
            }
            else
            {
                if (sunLight.gameObject.activeSelf)
                    sunLight.gameObject.SetActive(false);
            }
        }

        if (moonLight != null)
        {
            float moonAngle = moonLight.transform.eulerAngles.x;

            // Activate/deactivate moon based on its angle
            if (moonAngle > 0 && moonAngle < 180)
            {
                if (!moonLight.gameObject.activeSelf)
                    moonLight.gameObject.SetActive(true);
            }
            else
            {
                if (moonLight.gameObject.activeSelf)
                    moonLight.gameObject.SetActive(false);
            }
        }
    }

    private void ApplyLightSettings()
    {
        if (sunLight != null)
        {
            sunLight.colorTemperature = sunTemperature;
            sunLight.intensity = sunIntensity;
            // Apply emission or other settings as needed
        }

        if (moonLight != null)
        {
            moonLight.colorTemperature = moonTemperature;
            moonLight.intensity = moonIntensity;
            // Apply emission or other settings as needed
        }
    }

    private void UpdateSkyAndGlowProgress(bool forceUpdate = false)
    {
        if (Sky == null || Glow == null || dayDuration <= 0)
        {
            return;
        }

        Animator skyAnimator = Sky.GetComponent<Animator>();
        Animator glowAnimator = Glow.GetComponent<Animator>();

        if (skyAnimator == null || glowAnimator == null)
        {
            return;
        }

        float adjustedDuration = dayDuration * (1 - Mathf.Abs(0.5f - currentTimeOfDay) * 2); // Adjust duration based on time of day
        adjustedDuration = Mathf.Max(adjustedDuration, 0.0001f);

        // Set the animator speed using the multiplier only
        float speed = (1 / adjustedDuration) * skyGlowSpeedMultiplier;
        skyAnimator.speed = speed;
        glowAnimator.speed = speed;

        // Update the animator's time based on currentTimeOfDay, initialAnimationOffset, and direction inversion
        float normalizedTime = (invertAnimationDirection ? 1f - currentTimeOfDay : currentTimeOfDay) + initialAnimationOffset;
        normalizedTime %= 1f; // Ensure the normalized time stays within the [0, 1] range

        // Only update the animation time if necessary or forced
        if (forceUpdate || !Mathf.Approximately(normalizedTime, currentTimeOfDay))
        {
            skyAnimator.Play(0, 0, normalizedTime);
            glowAnimator.Play(0, 0, normalizedTime);
        }
    }

    private void UpdateVolumePostExposure()
    {
        if (!controlPostExposure || volumeProfile == null)
        {
            return;
        }

        if (!volumeProfile.TryGet(out ColorAdjustments colorAdjustments))
        {
            if (!addColorAdjustmentsIfMissing)
            {
                return;
            }

            colorAdjustments = volumeProfile.Add<ColorAdjustments>(true);
        }

        colorAdjustments.active = true;
        colorAdjustments.postExposure.overrideState = true;
        colorAdjustments.postExposure.value = GetCurrentPostExposure();
    }

    private float GetDayBlend()
    {
        float time = Mathf.Repeat(currentTimeOfDay, 1f);
        return 1f - Mathf.Abs(time - 0.5f) * 2f;
    }

    public float GetCurrentPostExposure()
    {
        return Mathf.Lerp(nightPostExposure, dayPostExposure, GetDayBlend());
    }

    public void SetCurrentTimeOfDay(float timeOfDay)
    {
        currentTimeOfDay = Mathf.Repeat(timeOfDay, 1f);

        RotateLights();
        HandleLightActivation();
        UpdateSkyAndGlowProgress(true);
        UpdateVolumePostExposure();
        CheckDayNightTransition();

        previousDayDuration = dayDuration;
        previousTimeOfDay = currentTimeOfDay;
    }

    private void CheckDayNightTransition()
    {
        bool newIsDay = currentTimeOfDay < 0.5f;

        if (newIsDay != isDay)
        {
            isDay = newIsDay;

            if (isDay)
            {
                OnDayStart.Invoke();
            }
            else
            {
                OnNightStart.Invoke();
            }
        }
    }

    public float GetCurrentTimeOfDay()
    {
        return currentTimeOfDay;
    }
}
