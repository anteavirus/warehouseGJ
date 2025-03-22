using UnityEngine;

public class ZombieAI : MonoBehaviour
{
    [Header("Movement Settings")]
    public float wanderSpeed = 2f;
    public float chaseSpeed = 4f;
    public float wanderRadius = 10f;
    public float detectionRadius = 7f;
    public float attackRange = 2f;
    public float directionChangeCooldown = 3f;
    public LayerMask obstacleMask;
    public AudioClip boomClip;
    public GameObject explosionPrefab;

    private Rigidbody rb;
    private Transform player;
    private Vector3 currentDirection;
    private bool isChasing;
    private float directionTimer;
    private bool isDead;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        player = FindAnyObjectByType<PlayerController>().transform;
        SetRandomDirection();
        directionTimer = directionChangeCooldown;
    }

    void FixedUpdate()
    {
        if (isDead) return;

        directionTimer -= Time.fixedDeltaTime;

        if (isChasing)
        {
            ChaseBehavior();
        }
        else
        {
            WanderBehavior();
            CheckForPlayer();
        }
    }

    void SetRandomDirection()
    {
        currentDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
        transform.rotation = Quaternion.LookRotation(currentDirection);
    }

    void WanderBehavior()
    {
        if (directionTimer <= 0 || CheckForObstacle())
        {
            SetRandomDirection();
            directionTimer = directionChangeCooldown;
        }

        rb.MovePosition(transform.position + currentDirection * wanderSpeed * Time.fixedDeltaTime);
    }

    void ChaseBehavior()
    {
        Vector3 chaseDirection = (player.position - transform.position).normalized;

        if (Physics.Raycast(transform.position, chaseDirection, out RaycastHit hit, detectionRadius, obstacleMask))
        {
            Vector3 avoidDirection = Vector3.Cross(hit.normal, Vector3.up).normalized;
            currentDirection = (avoidDirection + chaseDirection * 0.2f).normalized;
        }
        else
        {
            currentDirection = chaseDirection;
        }

        rb.MovePosition(transform.position + currentDirection * chaseSpeed * Time.fixedDeltaTime);
        transform.rotation = Quaternion.LookRotation(currentDirection);

        if (Vector3.Distance(transform.position, player.position) <= attackRange)
        {
            AttackPlayer();
        }
        else if (Vector3.Distance(transform.position, player.position) > detectionRadius * 1.5f)
        {
            isChasing = false;
            SetRandomDirection();
        }
    }

    bool CheckForObstacle()
    {
        return Physics.Raycast(transform.position, currentDirection, 1f, obstacleMask);
    }

    void CheckForPlayer()
    {
        // yeah he knows about the player all the time. yeah that's a bad idea. yeah we're a singleplayer. so no, it's fine. for now.
        if (Vector3.Distance(transform.position, player.position) <= detectionRadius)
        {
            isChasing = true;
        }
    }

    void AttackPlayer()
    {
        if (player.TryGetComponent<PlayerController>(out var controller))
        {
            controller.Ragdoll();

            isChasing = false;
            SetRandomDirection();
        }
    }

    public void Die()
    {
        isDead = true;
        rb.velocity = Vector3.zero;

        var boom = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        boom.AddComponent<AudioSource>().PlayOneShot(boomClip);
        Destroy(gameObject);
    }
}
