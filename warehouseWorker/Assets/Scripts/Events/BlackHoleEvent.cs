using Mirror;
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
    private Transform spawnPoint;

    public override void StartEvent()
    {
        base.StartEvent();
        spawnPoint = ((GameManager)GameManager.Instance).blackHoleSpawnPosition;
        Invoke(nameof(SpawnBlackHole), 2f);
    }

    [Server]
    void SpawnBlackHole()
    {
        if (!isServer) return;
        blackHoleInstance = Instantiate(blackHolePrefab, spawnPoint.position, Quaternion.identity);   // Isn't quite necessary; who coded this?
        NetworkServer.Spawn(blackHoleInstance);
        var audio = blackHoleInstance.AddComponent<AudioSource>();
        audio.outputAudioMixerGroup = ((GameManager)GameManager.Instance).sfx; // shittiest hakc
        audio.maxDistance = 50;
        audio.spatialBlend = 1;
        audio.rolloffMode = AudioRolloffMode.Linear;
        audio.clip = suckingSFX;
        audio.Play();
    }

    public override void UpdateEvent()
    {
        if (!isActive || blackHoleInstance == null) return;

        ApplyBlackHoleForce();
    }

    void ApplyBlackHoleForce()
    {
        if (blackHoleInstance != null)
        {
            GraviPull(blackHoleInstance.transform);
        }
    }

    void GraviPull(Transform position, float forceWeakener = 1)
    {
        if (!isServer) return;  // fuck you server
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
        if (!isServer) return;
        if (blackHoleInstance != null)
        {
            NetworkServer.Destroy(blackHoleInstance);
        }
        base.EndEvent();
    }
}
