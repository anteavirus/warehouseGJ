// ShiftsGamemodeTimer.cs
using UnityEngine;
using System.Collections;

public class ShiftsGamemodeTimer : GenericTimer
{
    [System.Serializable]
    public class Shift
    {
        public string shiftName;
        public float startTime; // in seconds from midnight
        public float endTime;   // in seconds from midnight
        public float timeScale = 1.0f; // Speed multiplier for this shift
    }

    [Header("Shift Settings")]
    [SerializeField] private Shift[] shifts;
    [SerializeField] private float currentTimeOfDay = 28800f; // 8:00 AM in seconds
    [SerializeField] private float totalDayLength = 86400f; // 24 hours in seconds

    [Header("Visual Feedback")]
    [SerializeField] private GameObject[] shiftChangeEffects;
    [SerializeField] private float timeJumpEffectDuration = 2f;

    private int currentShiftIndex = 0;
    private float shiftTimeElapsed = 0f;
    private Coroutine timeJumpCoroutine;

    public override void Initialize(GameManager gm)
    {
        gameManager = gm;
        if (shifts.Length > 0)
        {
            currentTimeOfDay = shifts[0].startTime;
        }
    }

    public override void UpdateTimer()
    {
        if (!enabledTimer || !gameManager.gameStarted) return;

        if (shifts.Length == 0) return;

        Shift currentShift = shifts[currentShiftIndex];

        // Calculate time progression based on shift time scale
        float deltaTime = Time.deltaTime * currentShift.timeScale;
        currentTimeOfDay += deltaTime;
        shiftTimeElapsed += deltaTime;

        // Check if shift ended
        if (currentTimeOfDay >= currentShift.endTime)
        {
            StartNextShift();
        }

        UpdateTimerUI();
    }

    private void StartNextShift()
    {
        OrdersManager.Instance.ClearAllOrders();
        if (timeJumpCoroutine != null)
            StopCoroutine(timeJumpCoroutine);

        timeJumpCoroutine = StartCoroutine(ShiftTransition());
    }

    private IEnumerator ShiftTransition()
    {
        // Show shift change effect
        if (shiftChangeEffects.Length > 0)
        {
            foreach (var effect in shiftChangeEffects)
            {
                if (effect != null)
                    effect.SetActive(true);
            }
        }

        // Wait for effect to play
        yield return new WaitForSeconds(timeJumpEffectDuration);

        // Move to next shift
        currentShiftIndex = (currentShiftIndex + 1) % shifts.Length;
        Shift nextShift = shifts[currentShiftIndex];

        // Jump time to start of next shift
        currentTimeOfDay = nextShift.startTime;
        shiftTimeElapsed = 0f;

        // Hide effects
        if (shiftChangeEffects.Length > 0)
        {
            foreach (var effect in shiftChangeEffects)
            {
                if (effect != null)
                    effect.SetActive(false);
            }
        }

        // Trigger any shift-change events
        OnShiftChanged(nextShift);

        timeJumpCoroutine = null;
    }

    private void OnShiftChanged(Shift newShift)
    {
        // Notify other systems about shift change
        Debug.Log($"Shift changed to: {newShift.shiftName}");

        // You could trigger events here like:
        // - Different customer types
        // - Changed difficulty
        // - Special shift-specific orders
        // - Environmental changes
    }

    private void UpdateTimerUI()
    {
        if (gameManager.timerUI != null)
        {
            Shift currentShift = shifts[currentShiftIndex];
            float shiftProgress = (currentTimeOfDay - currentShift.startTime) /
                                 (currentShift.endTime - currentShift.startTime);

            gameManager.timerUI.fillAmount = 1f - shiftProgress; // Countdown style

            // Update timer text to show current time
            string timeString = FormatTime(currentTimeOfDay);
            // You'd need to access a TextMeshPro component for this
        }
    }

    private string FormatTime(float secondsFromMidnight)
    {
        int hours = Mathf.FloorToInt(secondsFromMidnight / 3600) % 24;
        int minutes = Mathf.FloorToInt((secondsFromMidnight % 3600) / 60);
        return $"{hours:00}:{minutes:00}";
    }

    public override void ResetTimer()
    {
        // For shift timer, reset might mean something different
        // Maybe restore some time within the current shift?
        Shift currentShift = shifts[currentShiftIndex];
        float shiftDuration = currentShift.endTime - currentShift.startTime;
        currentTimeOfDay = Mathf.Max(currentTimeOfDay - 300f, currentShift.startTime); // Restore 5 minutes
    }

    public override void StartTimer()
    {
        enabledTimer = true;
    }

    public override void StopTimer()
    {
        enabledTimer = false;
    }

    public override bool IsTimeUp()
    {
        // Shift timer doesn't "end" in the same way - it cycles
        // You might want a different game over condition for shifts
        return false; // Or implement shift-based game over
    }

    public float GetTimeLeft()
    {
        if (shifts.Length == 0) return 0f;
        Shift currentShift = shifts[currentShiftIndex];
        return currentShift.endTime - currentTimeOfDay;
    }

    public float GetProgress()
    {
        if (shifts.Length == 0) return 0f;
        Shift currentShift = shifts[currentShiftIndex];
        return (currentTimeOfDay - currentShift.startTime) /
               (currentShift.endTime - currentShift.startTime);
    }

    public Shift GetCurrentShift()
    {
        return shifts.Length > 0 ? shifts[currentShiftIndex] : null;
    }
}