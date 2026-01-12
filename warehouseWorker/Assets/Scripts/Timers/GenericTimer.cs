using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GenericTimer : NetworkBehaviour
{
    protected GameManager gameManager;
    public bool enabledTimer;
    public string gamemode;

    void Start()
    {

    }

    public virtual void Initialize(GameManager gm)
    {
        gameManager = gm;
        gamemode = "generic";
    }

    public virtual void UpdateTimer()
    {
        // work is performed when timer is updated
    }

    public virtual void StartTimer()
    {
        // timer start
    }

    public virtual void ResumeTimer()
    {
        // timer resumes work
    }

    public virtual void StopTimer()
    {
        // timer has been stopped
    }

    public virtual void ResetTimer()
    {
        // timer has been reset
    }

    public virtual bool IsTimeUp()
    {
        // can i go home
        return false;
    }
}
