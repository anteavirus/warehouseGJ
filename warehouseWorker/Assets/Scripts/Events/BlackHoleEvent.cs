using UnityEngine;

public class BlackHoleEvent : Event
{
    [Header("Black Hole Settings")]
    [SerializeField] GameObject blackHolePrefab;
    [SerializeField] float blackHoleForce = 10f;
    [SerializeField] float rangeOfForce = 10f;
    [SerializeField] float forceMultiplierToPlayer = 5f;
    [SerializeField] AudioClip suckingSFX;

    private GameObject blackHoleInstance;
    private GameObject weakerBlackHoleInstance;

    private Transform weakerSpawnPoint;
    private Transform spawnPoint;

    public override void StartEvent()
    {
        base.StartEvent();
        spawnPoint = GameManager.Instance.blackHoleSpawnPosition;
        weakerSpawnPoint = GameManager.Instance.spawnPosition;
        Invoke(nameof(SpawnBlackHole), 2f);
    }

    void SpawnBlackHole()
    {
        blackHoleInstance = Instantiate(blackHolePrefab, spawnPoint.position, Quaternion.identity);
        weakerBlackHoleInstance = Instantiate(blackHolePrefab, weakerSpawnPoint.position, Quaternion.identity);
        var audio = weakerBlackHoleInstance.AddComponent<AudioSource>();
        audio.maxDistance = 50;
        audio.spatialBlend = 1;
        audio.rolloffMode = AudioRolloffMode.Linear;
        audio.clip = suckingSFX;
        audio.Play();
    }

    public override void UpdateEvent()
    {
        if (!isActive || blackHoleInstance == null || weakerBlackHoleInstance == null) return;

        ApplyBlackHoleForce();
    }

    void ApplyBlackHoleForce()
    {
        if (blackHoleInstance != null)
        {
            GraviPull(blackHoleInstance.transform);
        }
        if (weakerBlackHoleInstance != null)
        {
            GraviPull(weakerBlackHoleInstance.transform, 2.5f);
        }
    }

    void GraviPull(Transform position, float forceWeakener = 1)
    {
        Collider[] colliders = Physics.OverlapSphere(position.position, rangeOfForce);
        foreach (Collider col in colliders)
        {
            Rigidbody rb = col.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic && rb.gameObject.layer != LayerMask.NameToLayer("Light"))
            {
                Vector3 direction = (position.position - col.transform.position).normalized;
                float forceMultiplier = rb.GetComponent<PlayerController>() != null ? forceMultiplierToPlayer : 1;
                Vector3 force = (blackHoleForce / forceWeakener) * forceMultiplier * Time.deltaTime * direction;
                rb.AddForce(force, ForceMode.Force);
            }
        }
    }


    /*
    void ApplyBlackHoleForce()
    {
        Collider[] colliders = Physics.OverlapSphere(blackHoleInstance.transform.position, rangeOfForce);
        foreach (Collider col in colliders)
        {
            Rigidbody rb = col.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                Vector3 directionToBlackHole = blackHoleInstance.transform.position - col.transform.position;
                float distance = directionToBlackHole.magnitude;
                Vector3 direction = directionToBlackHole.normalized;

                float forceMagnitude = Mathf.Lerp(blackHoleForce, 0, distance / rangeOfForce);

                rb.AddForce(direction * forceMagnitude, ForceMode.Force);
            }
        }
    }
     */

    public override void EndEvent()
    {
        // handled by GM
        if (blackHoleInstance != null)
        {
            Destroy(blackHoleInstance);
        }
        if (weakerBlackHoleInstance != null)
        {
            Destroy(weakerBlackHoleInstance);
        }
        base.EndEvent();
    }
}
