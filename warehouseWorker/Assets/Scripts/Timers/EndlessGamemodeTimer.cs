// EndlessGamemodeTimer.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndlessGamemodeTimer : ElGenerico<EndlessGamemodeTimer>
{
    [Header("Endless Timer Settings")]
    [SerializeField] private float maxTimer = 30f;
    [SerializeField] private float timerRestore = 1f;

    private float timeLeft;
    private float progressTimer;
    private Image timerUI;

    public override void Initialize(GameManager gm)
    {
        gameManager = gm;
        timeLeft = maxTimer;
        gamemode = "endless";
        var player = FindObjectOfType<PlayerController>().GetComponent<SerializableDictionaryObjectContainer>();
        timerUI = ((GameObject)player.Fetch("timerCircle")).GetComponent<Image>(); // i know it's a gameobject because of how lazy my fucking ass is
        timerUI.gameObject.SetActive(true);
        ((GameObject)player.Fetch("timerFire")).SetActive(true);
    }

    public override void UpdateTimer()
    {
        if (gameManager == null) gameManager = GameManager.Instance; // BAD, Don't care anymore
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
        if (timerUI != null)
        {
            timerUI.fillAmount = progressTimer;
            var color = timerUI.color;
            color.a = progressTimer;
            timerUI.color = color;
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