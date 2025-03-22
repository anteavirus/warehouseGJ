using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class SelfAnimatorManipulator : MonoBehaviour
{
    Animator pauseAnimator;
    string key;
    // Start is called before the first frame update
    void Start()
    {
        pauseAnimator = GetComponent<Animator>();
    }

    public void SetKey(string key)
    {
        this.key = key;
    }

    public void ChangeWhosePauseAnimator(GameObject obj)
    {
        pauseAnimator = obj.GetComponent<Animator>();
    }

    public void SetBool(int v) { pauseAnimator.SetBool(key, v > 0); }
    public void SetInteger(int v) { pauseAnimator.SetInteger(key, v); }
    public void SetFloat(float v) { pauseAnimator.SetFloat(key, v); }

    public void ToggleTriggerToAnimator(int value)
    {
        if (value > 0)
            pauseAnimator.SetTrigger(key);
        else
            pauseAnimator.ResetTrigger(key);
    }

    // Fucking animator methods. Can't take more than one input.
    // Can't take booleans? What the fuck is this monstrocity.
}
