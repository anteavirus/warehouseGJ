// EndlessGamemodeTimer.cs
using UnityEngine;

public class EndlessGamemodeTimer : GenericTimer
{
    [Header("Endless Timer Settings")]
    [SerializeField] private float maxTimer = 30f;
    [SerializeField] private float timerRestore = 1f;

    private float timeLeft;
    private float progressTimer;

    public override void Initialize(GameManager gm)
    {
        gameManager = gm;
        timeLeft = maxTimer;
    }

    public override void UpdateTimer()
    {
        if (!enabledTimer || !gameManager.gameStarted) return;

        // Calculate difficulty-based time decay
        float difficultyMultiplier = CalculateDifficultyMultiplier();
        timeLeft -= Time.deltaTime / (difficultyMultiplier + 0.01f);

        progressTimer = timeLeft / maxTimer;
        UpdateTimerUI();
    }

    private float CalculateDifficultyMultiplier()
    {
        // This would use gameManager's difficulty settings
        float currentDifficulty = 0f; // Get from gameManager
        bool hasActiveEvents = gameManager.activeEvents.Count > 0;

        if (hasActiveEvents && gameManager.score == 0)
        {
            return Mathf.Lerp(gameManager.maximumDifficulty, gameManager.minimalDifficulty / 4, currentDifficulty);
        }
        else
        {
            return Mathf.Lerp(gameManager.maximumDifficulty, gameManager.minimalDifficulty, currentDifficulty);
        }
    }

    private void UpdateTimerUI()
    {
        if (gameManager.timerUI != null)
        {
            gameManager.timerUI.fillAmount = progressTimer;
            var color = gameManager.timerUI.color;
            color.a = progressTimer;
            gameManager.timerUI.color = color;
        }
    }

    public override void ResetTimer()
    {
        if (gameManager.setdownItem)
        {
            timeLeft = Mathf.Clamp(timeLeft + timerRestore, 0, maxTimer);
            gameManager.setdownItem = false;
        }
    }

    public override void StartTimer()
    {
        enabledTimer = true;
        timeLeft = maxTimer;
    }

    public override void StopTimer()
    {
        enabledTimer = false;
    }

    public override bool IsTimeUp()
    {
        return timeLeft <= 0;
    }

    public float GetTimeLeft()
    {
        return timeLeft;
    }

    public float GetProgress()
    {
        return progressTimer;
    }
}