using UnityEngine;

[SelectionBase]
public class UpsideDownCameraEvent : Event
{
    [Header("Smooth Transition Settings")]
    [SerializeField] private float targetInversion = 1f;
    [SerializeField] private float transitionDuration = 2f;
    
    private PlayerController player;
    private Coroutine inversionRoutine;
    Blank slave;

    public override void StartEvent()
    {
        base.StartEvent();
        player = FindObjectOfType<PlayerController>();
        slave = new GameObject("slave").AddComponent<Blank>();
        inversionRoutine = slave.StartCoroutine(SmoothInversion());
    }

    private System.Collections.IEnumerator SmoothInversion()
    {
        float startValue = player.inversionProgress;
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            player.inversionProgress = Mathf.Lerp(
                startValue, 
                targetInversion, 
                elapsed / transitionDuration
            );
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        player.inversionProgress = targetInversion;
    }

    public override void EndEvent()
    {
        base.EndEvent();
        if(inversionRoutine != null) StopCoroutine(inversionRoutine);
        
        slave.StartCoroutine(RevertInversion());
    }

    private System.Collections.IEnumerator RevertInversion()
    {
        float startValue = player.inversionProgress;
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            player.inversionProgress = Mathf.Lerp(
                startValue, 
                0f, 
                elapsed / transitionDuration
            );
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        player.inversionProgress = 0f;
        Destroy(slave.gameObject);
    }
}
