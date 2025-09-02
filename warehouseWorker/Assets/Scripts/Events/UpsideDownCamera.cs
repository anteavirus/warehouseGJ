using Unity.VisualScripting;
using UnityEngine;

[SelectionBase]
public class UpsideDownCameraEvent : Event
{
    [Header("Smooth Transition Settings")]
    [SerializeField] private float targetInversion = 1f;
    [SerializeField] private float transitionDuration = 2f;
    [SerializeField] AudioClip rotato;
    private PlayerController player;
    private Coroutine inversionRoutine;
    Blank slave;

    public override void StartEvent()
    {
        base.StartEvent();
        player = FindObjectOfType<PlayerController>();
        slave = new GameObject("upsideDownSlave").AddComponent<Blank>();
        slave.transform.SetParent(player.transform);
        inversionRoutine = slave.StartCoroutine(SmoothInversion(player.inversionProgress, targetInversion));
    }

    private System.Collections.IEnumerator SmoothInversion(float start, float end, bool Kaboom = false)
    {
        if (player == null || slave == null || rotato == null)
            yield break;

        if (!slave.TryGetComponent<AudioSource>(out var audioSource))
        {
            audioSource = slave.gameObject.AddComponent<AudioSource>();
            audioSource.outputAudioMixerGroup = GameManager.Instance.sfx; // shittiest hakc
        }
        audioSource.PlayOneShot(rotato);

        float elapsed = 0f;
        float duration = Mathf.Max(transitionDuration, 0.001f);

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float smoothedT = 0.5f * (1f - Mathf.Cos(t * Mathf.PI));

            player.inversionProgress = Mathf.Lerp(start, end, smoothedT);
            elapsed += Time.deltaTime;
            yield return null;
        }

        player.inversionProgress = end;

        if (Kaboom)
            Destroy(slave.gameObject, rotato.length);
    }

    public override void EndEvent()
    {
        base.EndEvent();
        if(inversionRoutine != null) StopCoroutine(inversionRoutine);
        
        slave.StartCoroutine(SmoothInversion(player.inversionProgress, 0, true));
    }
}
