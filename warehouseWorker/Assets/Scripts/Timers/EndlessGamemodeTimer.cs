// EndlessGamemodeTimer.cs
using Mirror;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndlessGamemodeTimer : ElGenerico<EndlessGamemodeTimer>
{
    [Header("Endless Timer Settings")]
    [SerializeField] private float maxTimer = 30f;
    [SerializeField] private float timerRestore = 1f;

    [SyncVar(hook = nameof(OnTimeLeftChanged))]
    private float timeLeftSync;

    private float timeLeft;          
    private float progressTimer;
    private Image timerUI;

    public override void Initialize(GameManager gm)
    {
        gameManager = gm;
        timeLeft = maxTimer;
        gamemode = "endless";
        StartCoroutine(nameof(AtSomePointDoYourJob));
    }

    private void OnTimeLeftChanged(float _, float newVal)
    {
        if (!isServer)   // clients update their local copy and UI
        {
            timeLeft = newVal;
            UpdateTimerUI();
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        // UI setup – runs on every client
        StartCoroutine(AtSomePointDoYourJob());
    }

    IEnumerator AtSomePointDoYourJob()
    {
        yield return new WaitUntil(() => PlayerController.LocalPlayer != null);
        var player = PlayerController.LocalPlayer.GetComponent<SerializableDictionaryObjectContainer>();
        timerUI = ((GameObject)player.Fetch("timerCircle")).GetComponent<Image>(); // i know it's a gameobject because of how lazy my fucking ass is
        timerUI.gameObject.SetActive(true);
        ((GameObject)player.Fetch("timerFire")).SetActive(true);
        yield break;
    }

    // EDITED: UpdateTimer now only runs on server for synchronization
    public override void UpdateTimer()
    {
        if (gameManager == null) gameManager = GameManager.Instance; // BAD, Don't care anymore
        if (!enabledTimer || !gameManager.gameStarted) return;
        
        // EDITED: Only server updates timer to keep it synchronized
        if (!Mirror.NetworkServer.active) return;

        // Calculate difficulty-based time decay
        float difficultyMultiplier = CalculateDifficultyMultiplier();
        timeLeft -= Time.deltaTime / (difficultyMultiplier + 0.01f);
        timeLeftSync = timeLeft;  

        progressTimer = timeLeft / maxTimer;
        UpdateTimerUI();
    }

    // EDITED: CalculateDifficultyMultiplier now properly uses gameManager's currentDifficulty
    private float CalculateDifficultyMultiplier()
    {
        // EDITED: Use currentDifficulty from gameManager (now public)
        float currentDifficulty = gameManager != null ? gameManager.currentDifficulty : 0.5f;
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