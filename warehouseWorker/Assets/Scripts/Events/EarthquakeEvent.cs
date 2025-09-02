using System.Collections;
using UnityEngine;

public class EarthquakeEvent : Event
{
    [Header("Earthquake Settings")]
    public float shakeIntensity = 0.5f;
    public float objectForce = 10f;
    public float playerMovementPenalty = 0.5f;
    public AnimationCurve intensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Vector3 originalCameraPosition;
    private Transform mainCamera;
    public AudioClip sfx;
    private Blank slave;
    private float elapsedTime;

    public override void StartEvent()
    {
        base.StartEvent();
        slave = new GameObject("EarthController").AddComponent<Blank>();

        mainCamera = Camera.main.transform;
        originalCameraPosition = mainCamera.localPosition;

        StartCoroutine(ShakeCamera());

        if (!TryGetComponent<AudioSource>(out var audioSource))
        {
            audioSource = slave.gameObject.AddComponent<AudioSource>();
            audioSource.outputAudioMixerGroup = GameManager.Instance.sfx;
        }
        audioSource.PlayOneShot(sfx);

        elapsedTime = 0f;
    }

    public override void UpdateEvent()
    {
        base.UpdateEvent();

        float progress = Mathf.Clamp01(elapsedTime / duration);
        float currentIntensity = intensityCurve.Evaluate(progress);

        Collider[] colliders = Physics.OverlapSphere(transform.position, 220f);
        foreach (Collider col in colliders)
        {
            Rigidbody rb = col.GetComponent<Rigidbody>();
            if (rb != null && !rb.CompareTag("Player") && Random.value < 0.1f)
            {
                Vector3 randomForce = new Vector3(
                    Random.Range(-objectForce, objectForce),
                    0,
                    Random.Range(-objectForce, objectForce)
                ) * currentIntensity;

                rb.AddForce(randomForce, ForceMode.Impulse);
            }
        }

        elapsedTime += Time.deltaTime;
    }

    IEnumerator ShakeCamera()
    {
        while (elapsedTime < duration)
        {
            float progress = Mathf.Clamp01(elapsedTime / duration);
            float currentIntensity = intensityCurve.Evaluate(progress);

            mainCamera.localPosition = originalCameraPosition +
                Random.insideUnitSphere * shakeIntensity * currentIntensity;

            yield return null;
        }
        mainCamera.localPosition = originalCameraPosition;
    }

    public override void EndEvent()
    {
        base.EndEvent();
        StopAllCoroutines();
        mainCamera.localPosition = originalCameraPosition;
        Destroy(slave);
    }
}
