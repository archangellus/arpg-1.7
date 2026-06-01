using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class DayNightTimeSlider : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DayNightCycle dayNightCycle;
    [SerializeField] private Slider timeOfDaySlider;
    [SerializeField] private TMP_Text timeLabel;

    [Header("Display")]
    [SerializeField] private bool show24HourTime = true;
    [SerializeField] private string labelPrefix = "Time: ";

    private bool isUpdatingSlider;

    private void Awake()
    {
        if (timeOfDaySlider == null)
        {
            timeOfDaySlider = GetComponent<Slider>();
        }

        if (dayNightCycle == null)
        {
            dayNightCycle = FindFirstObjectByType<DayNightCycle>();
        }

        ConfigureSlider();
    }

    private void OnEnable()
    {
        if (timeOfDaySlider == null)
        {
            timeOfDaySlider = GetComponent<Slider>();
        }

        ConfigureSlider();

        if (timeOfDaySlider == null)
        {
            return;
        }

        timeOfDaySlider.onValueChanged.AddListener(SetTimeOfDayFromSlider);
        RefreshSliderFromCycle();
    }

    private void OnDisable()
    {
        if (timeOfDaySlider != null)
        {
            timeOfDaySlider.onValueChanged.RemoveListener(SetTimeOfDayFromSlider);
        }
    }

    private void Update()
    {
        RefreshSliderFromCycle();
    }

    private void ConfigureSlider()
    {
        if (timeOfDaySlider == null)
        {
            return;
        }

        timeOfDaySlider.minValue = 0f;
        timeOfDaySlider.maxValue = 1f;
        timeOfDaySlider.wholeNumbers = false;
    }

    private void SetTimeOfDayFromSlider(float sliderValue)
    {
        if (isUpdatingSlider || dayNightCycle == null)
        {
            return;
        }

        dayNightCycle.SetCurrentTimeOfDay(sliderValue);
        UpdateLabel(sliderValue);
    }

    private void RefreshSliderFromCycle()
    {
        if (timeOfDaySlider == null || dayNightCycle == null)
        {
            return;
        }

        float currentTimeOfDay = dayNightCycle.GetCurrentTimeOfDay();
        isUpdatingSlider = true;
        timeOfDaySlider.SetValueWithoutNotify(currentTimeOfDay);
        isUpdatingSlider = false;
        UpdateLabel(currentTimeOfDay);
    }

    private void UpdateLabel(float timeOfDay)
    {
        if (timeLabel == null)
        {
            return;
        }

        if (!show24HourTime)
        {
            timeLabel.text = labelPrefix + timeOfDay.ToString("0.00");
            return;
        }

        float totalMinutes = Mathf.Repeat(timeOfDay, 1f) * 24f * 60f;
        int hours = Mathf.FloorToInt(totalMinutes / 60f);
        int minutes = Mathf.FloorToInt(totalMinutes % 60f);
        timeLabel.text = $"{labelPrefix}{hours:00}:{minutes:00}";
    }
}