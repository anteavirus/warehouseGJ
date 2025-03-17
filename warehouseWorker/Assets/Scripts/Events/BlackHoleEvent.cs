using UnityEngine;
using UnityEngine.AI;

public class BlackHoleEvent : Event
{
    [Header("Black Hole Settings")]
    [SerializeField] GameObject blackHolePrefab;
    [SerializeField] float blackHoleForce = 10f;
    [SerializeField] float rangeOfForce = 10f;

    private GameObject blackHoleInstance;
    private Transform spawnPoint;

    public override void StartEvent()
    {
        base.StartEvent();
        spawnPoint = GameManager.Instance.spawnPosition;

        Invoke(nameof(SpawnBlackHole), 2f);
    }

    void SpawnBlackHole()
    {
        blackHoleInstance = Instantiate(blackHolePrefab, spawnPoint.position, Quaternion.identity);
    }

    public override void UpdateEvent()
    {
        if (!isActive || blackHoleInstance == null) return;

        ApplyBlackHoleForce();
    }

    void ApplyBlackHoleForce()
    {
        Collider[] colliders = Physics.OverlapSphere(blackHoleInstance.transform.position, rangeOfForce);
        foreach (Collider col in colliders)
        {
            Rigidbody rb = col.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                Vector3 direction = (blackHoleInstance.transform.position - col.transform.position).normalized;
                rb.AddForce(direction * blackHoleForce, ForceMode.Force);
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
        base.EndEvent();
        if (blackHoleInstance != null)
        {
            Destroy(blackHoleInstance);
        }
    }
}
