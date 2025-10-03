using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ZombieAI : MonoBehaviour
{
    [Header("Movement Settings")]
    public float wanderSpeed = 2f;
    public float chaseSpeed = 4f;
    public float wanderRadius = 10f;
    public float detectionRadius = 15f;
    public float attackRange = 2f;
    public float directionChangeCooldown = 3f;
    public float searchArriveThreshold = 1f;
    public float rotationSpeed = 3f;
    public LayerMask obstacleMask;
    public AudioClip boomClip;
    public GameObject explosionPrefab;

    [Header("Footstep Settings")]
    public float footstepInterval = 0.5f;
    public List<SurfaceSound> surfaceSounds = new List<SurfaceSound>();
    public LayerMask groundLayer;
    public float characterHeight = 2f;
    public AudioClip[] slipSounds;
    public float slipCooldown = 2f;
    public float slipRiskIncreasePerFootstep = 0.1f;
    public float maxSlipRisk = 1f;
    public float minSlipSpeed = 1f;

    private Rigidbody rb;
    private Transform player;
    private Vector3 currentDirection;
    private float directionTimer;
    public bool isDead;
    private AudioSource footstepSource;

    // Footstep system variables
    private SurfaceType currentSurface;
    private float footstepTimer;
    private float slipRisk;
    private Vector3 lastMovementDirection;
    private float lastTimeRagdoll;
    private bool canMove = true;
    private Vector3 moveDirection;
    public Animator animator; // Added animator reference

    private enum State { Wandering, Chasing, Searching }
    private State currentState;
    private Vector3 lastSeenPosition;

    void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        player = FindObjectOfType<PlayerController>().transform;    // TODO: mustn't know player from start. we are going to make this multiplayer at some point, so...
        footstepSource = gameObject.AddComponent<AudioSource>();
        footstepSource.spatialBlend = 1f;
        footstepSource.outputAudioMixerGroup = GameManager.Instance.sfx;

        SetRandomDirection();
        directionTimer = directionChangeCooldown;
        currentState = State.Wandering;
    }

    void Update()
    {
        if (isDead) return;
        HandleFootsteps();

        if (animator != null)
        {
            Vector3 speed = transform.position + wanderSpeed * currentDirection;
            float horizontalSpeed = canMove ? Mathf.Abs(speed.x) + Mathf.Abs(speed.z) : 0f;
            animator.SetFloat("Speed", horizontalSpeed);
        }
    }

    void FixedUpdate()
    {
        if (isDead || !canMove) return;

        directionTimer -= Time.fixedDeltaTime;

        switch (currentState)
        {
            case State.Wandering:
                WanderBehavior();
                CheckForPlayer();
                break;
            case State.Chasing:
                ChaseBehavior();
                break;
            case State.Searching:
                SearchBehavior();
                CheckForPlayer();
                break;
        }
    }

    void SetRandomDirection()
    {
        currentDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
        transform.rotation = Quaternion.LookRotation(currentDirection);
    }
    private bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down,
            characterHeight * 0.5f + 0.1f, groundLayer);
    }

    // TODO: it should NOT be outside of boundaries of where it can go. 
    void WanderBehavior()
    {
        if (directionTimer <= 0 || CheckForObstacle())
        {
            SetRandomDirection();
            directionTimer = directionChangeCooldown;
        }

        UpdateSearchRotation(currentDirection);
        moveDirection = currentDirection;
        Vector3 horizontalVelocity = currentDirection * wanderSpeed;
        rb.velocity = new Vector3(
            horizontalVelocity.x,
            rb.velocity.y,
            horizontalVelocity.z
        );

        if (!IsGrounded())
        {
            rb.AddForce(Vector3.down * 100f, ForceMode.Acceleration);
        }
    }

    void ChaseBehavior()
    {
        Vector3 directionToPlayer = player.position - transform.position;
        bool canSeePlayer = CheckLineOfSight();

        if (canSeePlayer)
        {
            lastSeenPosition = player.position;
            Vector3 chaseDirection = directionToPlayer.normalized;

            if (Physics.Raycast(transform.position, chaseDirection, out RaycastHit hit, detectionRadius, obstacleMask))
            {
                Vector3 avoidDirection = Vector3.Cross(hit.normal, Vector3.up).normalized;
                currentDirection = (avoidDirection + chaseDirection * 0.2f).normalized;
            }
            else
            {
                currentDirection = chaseDirection;
            }

            moveDirection = currentDirection;
            Vector3 horizontalVelocity = currentDirection * chaseSpeed;
            rb.velocity = new Vector3(horizontalVelocity.x, rb.velocity.y, horizontalVelocity.z);
            transform.rotation = Quaternion.LookRotation(currentDirection);

            if (directionToPlayer.magnitude <= attackRange)
            {
                AttackPlayer();
            }
        }
        else
        {
            currentState = State.Searching;
        }
    }

    void SearchBehavior()
    {
        Vector3 toLastSeen = lastSeenPosition - transform.position;
        if (toLastSeen.magnitude < searchArriveThreshold)
        {
            currentState = State.Wandering;
            SetRandomDirection();
            return;
        }

        Vector3 searchDirection = toLastSeen.normalized;
        UpdateSearchRotation(searchDirection);

        if (Physics.Raycast(transform.position, searchDirection, out RaycastHit hit, 2f, obstacleMask))
        {
            Vector3 avoidDirection = Vector3.Cross(hit.normal, Vector3.up).normalized;
            searchDirection = (avoidDirection + searchDirection * 0.2f).normalized;
        }

        moveDirection = searchDirection;
        Vector3 horizontalVelocity = searchDirection * chaseSpeed;
        rb.velocity = new Vector3(horizontalVelocity.x, rb.velocity.y, horizontalVelocity.z);
        transform.rotation = Quaternion.LookRotation(searchDirection);
    }

    void UpdateSearchRotation(Vector3 targetDirection)
    {
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation,
            rotationSpeed * Time.deltaTime);
    }

    void HandleFootsteps()
    {
        if (moveDirection != Vector3.zero && canMove)
        {
            footstepTimer -= Time.deltaTime;
            if (footstepTimer <= 0)
            {
                DetectSurface();
                PlayFootstepSound();
                footstepTimer = footstepInterval;

                if (currentSurface == SurfaceType.Water)
                {
                    float horizontalSpeed = new Vector3(rb.velocity.x, 0, rb.velocity.z).magnitude;
                    float speedFactor = Mathf.InverseLerp(minSlipSpeed, chaseSpeed, horizontalSpeed);

                    float directionSimilarity = Vector3.Dot(moveDirection.normalized, lastMovementDirection.normalized);
                    slipRisk += slipRiskIncreasePerFootstep * speedFactor * (0.5f + 0.5f * directionSimilarity);

                    lastMovementDirection = moveDirection;

                    if (slipRisk >= Random.Range(0f, maxSlipRisk))
                    {
                        Slip(speedFactor);
                        slipRisk = 0f;
                    }
                }
            }
        }
    }

    void DetectSurface()
    {
        if (Physics.Raycast(transform.position, Vector3.down,
            out RaycastHit hit, characterHeight * 0.5f + 0.2f, groundLayer))
        {
            if (hit.collider.TryGetComponent<GroundSurface>(out var surface))
            {
                currentSurface = surface.surfaceType;
            }
        }
    }

    void PlayFootstepSound()
    {
        var sound = surfaceSounds.Find(s => s.surface == currentSurface);
        if (sound != null && sound.footstepSounds.Length > 0)
        {
            AudioClip clip = sound.footstepSounds[Random.Range(0, sound.footstepSounds.Length)];
            if (clip)
                footstepSource.PlayOneShot(clip);
        }
    }

    void Slip(float slipFactor = 1)
    {
        Ragdoll(slipFactor);
    }

    public void Ragdoll(float factor = 1)
    {
        if (Time.time < lastTimeRagdoll + slipCooldown) return;
        StartCoroutine(RagdollAsync(factor));
    }

    IEnumerator RagdollAsync(float factor = 1)
    {
        rb.constraints = RigidbodyConstraints.None;
        canMove = false;
        lastTimeRagdoll = Time.time;

        if (slipSounds != null && slipSounds.Length > 0)
        {
            footstepSource.pitch = Mathf.Lerp(0.9f, 1.1f, factor);
            footstepSource.PlayOneShot(slipSounds[Random.Range(0, slipSounds.Length)]);
            footstepSource.pitch = 1f;
        }

        Vector3 slipDirection = Vector3.Lerp(
            Random.onUnitSphere,
            lastMovementDirection.normalized,
            factor
        ).normalized;

        float slipForceMultiplier = Mathf.Lerp(10f, 25f, factor);
        float torqueMultiplier = Mathf.Lerp(0.5f, 2f, factor);

        rb.AddForce(slipDirection * slipForceMultiplier, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * torqueMultiplier, ForceMode.Impulse);

        float maxWaitTime = 3f;
        float waitStartTime = Time.time;
        while ((rb.velocity.magnitude > 0.1f || rb.angularVelocity.magnitude > 0.1f) && Time.time - waitStartTime < maxWaitTime)
        {
            yield return null;
        }

        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
        float rotationTime = 1f;
        float elapsedTime = 0f;

        while (elapsedTime < rotationTime)
        {
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, elapsedTime / rotationTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        rb.constraints = RigidbodyConstraints.FreezeRotation;
        canMove = true;
    }

    bool CheckLineOfSight()
    {
        Vector3 directionToPlayer = player.position - transform.position;
        if (directionToPlayer.magnitude > detectionRadius) return false;

        if (Physics.Raycast(transform.position, directionToPlayer, out RaycastHit hit, detectionRadius, obstacleMask))
        {
            return hit.collider.transform == player;
        }
        return true;
    }

    void CheckForPlayer()
    {
        if (CheckLineOfSight())
        {
            lastSeenPosition = player.position;
            currentState = State.Chasing;
        }
    }

    bool CheckForObstacle()
    {
        return Physics.Raycast(transform.position, currentDirection, 1f, obstacleMask);
    }

    void AttackPlayer()
    {
        if (player.TryGetComponent<PlayerController>(out var controller))
        {
            controller.Ragdoll();
            currentState = State.Wandering;
            SetRandomDirection();
        }
    }

    public void Die()
    {
        isDead = true;
        canMove = false;
        rb.velocity = Vector3.zero;

        GameObject boom = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        AudioSource audioSource = boom.AddComponent<AudioSource>();
        audioSource.outputAudioMixerGroup = GameManager.Instance.sfx;
        audioSource.PlayOneShot(boomClip);
        Destroy(boom, 2f);
        Destroy(gameObject);
    }
}
