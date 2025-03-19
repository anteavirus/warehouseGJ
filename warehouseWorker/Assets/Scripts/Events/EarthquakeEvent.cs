using System.Collections;
using UnityEngine;

public class EarthquakeEvent : Event
{
    public float shakeIntensity = 0.5f;
    public float objectForce = 10f;
    public float playerMovementPenalty = 0.5f;

    private Vector3 originalCameraPosition;
    private Transform mainCamera;
    public AudioClip sfx;
    private Blank slave;

    public override void StartEvent()
    {
        base.StartEvent();
        slave = new GameObject("LightController").AddComponent<Blank>();

        mainCamera = Camera.main.transform;
        originalCameraPosition = mainCamera.localPosition;

        StartCoroutine(ShakeCamera());

        if (!TryGetComponent<AudioSource>(out var audioSource)) audioSource = slave.gameObject.AddComponent<AudioSource>();
        audioSource.PlayOneShot(sfx);
    }

    public override void UpdateEvent()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, 20f);
        foreach (Collider col in colliders)
        {
            Rigidbody rb = col.GetComponent<Rigidbody>();
            if (rb != null && !rb.CompareTag("Player") && Random.value < 0.1f)
            {
                Vector3 randomForce = new Vector3(
                    Random.Range(-objectForce, objectForce),
                    0,
                    Random.Range(-objectForce, objectForce)
                );
                rb.AddForce(randomForce, ForceMode.Impulse);
            }
        }
    }

    IEnumerator ShakeCamera()
    {
        while (isActive)
        {
            mainCamera.localPosition = originalCameraPosition +
                Random.insideUnitSphere * shakeIntensity;

            yield return null;
        }
        mainCamera.localPosition = originalCameraPosition;
    }

    public override void EndEvent()
    {
        base.EndEvent();

        StopAllCoroutines();
        mainCamera.localPosition = originalCameraPosition;
    }
}
