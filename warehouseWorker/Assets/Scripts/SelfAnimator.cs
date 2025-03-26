using UnityEngine;

public class SelfAnimator : MonoBehaviour
{
    private Animator animator;
    string currentKey;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    public void SetKey(string val)
    {
        currentKey = val;
    }

    public void SetTrigger(int activate)
    {
        if (!animator) return;
        if (activate > 0) animator.SetTrigger(currentKey);
        else animator.ResetTrigger(currentKey);
    }

    public void SetFloat(float value)
    {
        animator?.SetFloat(currentKey, value);
    }

    public void SetInteger(int value)
    {
        animator?.SetInteger(currentKey, value);
    }

    public void SetBool(int value)
    {
        if (animator) animator.SetBool(currentKey, value > 0);
    }
}
