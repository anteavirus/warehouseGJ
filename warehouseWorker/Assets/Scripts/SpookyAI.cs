using UnityEngine;

[RequireComponent(typeof(Renderer), typeof(AudioSource), typeof(Collider))]
public class BoogeymanAI : MonoBehaviour
{
    [Header("Spawning Settings")]
    public float vanishCooldown = 5f;
    public LayerMask obstacleMask;
    public LayerMask groundMask;
    public float spawnRadius = 5f;
    public float minSpawnDistance = 3f;
    public float groundCheckDistance = 10f;

    [Header("Combat Settings")]
    public float pushForce = 10f;
    public float pushAfterNotSeenTime = 5f;

    [Header("Movement Settings")]
    public float rotationSpeed = 5f;
    public float trackingStartDelay = 2f; // New field to control tracking delay

    [Header("Cooldowns")]
    public float vanishAfterSpawnCooldown = 2f;

    [Header("Audio")]
    public AudioClip[] respawnSFX;

    [Header("Visibility Settings")]
    [Range(0f, 0.3f)] public float viewportMargin = 0.05f;
    public float visibilityCheckInterval = 0.1f;

    [Header("Position Settings")]
    public float followDistance = 5f;
    public float heightOffset = 1f;

    private float _lastVisibilityCheck;
    private bool _needsImmediateVanish;
    private bool _trackingEnabled;

    private Transform player;
    private Camera playerCamera;
    private Renderer render;
    private AudioSource source;
    private Collider col;

    private bool isVisible;
    private bool isVanished;
    private float vanishTimer;
    private float timeNotSeen;
    private Vector3 positionOffset;
    private float attackTimer;
    private bool attackReady;
    private float vanishAvailableTimer;

    void Start()
    {
        render = GetComponent<Renderer>();
        source = GetComponent<AudioSource>();
        col = GetComponent<Collider>();
        FindPlayer();
        FindCamera();
        Respawn();
        vanishAvailableTimer = vanishAfterSpawnCooldown;
    }

    void Update()
    {
        EnsurePlayerAndCameraReferences();
        if (player == null || playerCamera == null) return;

        vanishAvailableTimer -= Time.deltaTime;
        _lastVisibilityCheck += Time.deltaTime;

        if (_needsImmediateVanish)
        {
            Vanish();
            _needsImmediateVanish = false;
            return;
        }

        if (_lastVisibilityCheck >= visibilityCheckInterval)
        {
            CheckVisibility();
            _lastVisibilityCheck = 0f;
        }

        if (_trackingEnabled) HandlePositionTracking();
        FacePlayer();
        HandleAttackTimer();
        HandleVanishedState();
        HandleNotSeenPush();

        if (playerCamera != null && IsPlayerLookingDirectly())
        {
            if (isVisible) _needsImmediateVanish = true;
        }
    }
        
    void HandleAttackTimer()
    {
        if (isVanished || !render.enabled) return;

        if (isVisible)
        {
            attackTimer = 0f;
            attackReady = false;
        }
        else
        {
            attackTimer += Time.deltaTime;

            if (!attackReady && attackTimer >= pushAfterNotSeenTime)
            {
                attackReady = true;
                TryTriggerRagdoll();
                Vanish();
            }
        }
    }

    void CheckVisibility()
    {
        if (isVanished || vanishAvailableTimer > 0) return;

        bool wasVisible = isVisible;
        isVisible = IsVisibleToPlayer();

        if (isVisible && !wasVisible)
        {
            _needsImmediateVanish = true;
        }

        if (isVisible != wasVisible)
            OnVisibilityChanged();
    }

    void HandleNotSeenPush()
    {
        if (isVanished) return;

        if (isVisible)
        {
            timeNotSeen = 0f;
        }
        else
        {
            timeNotSeen += Time.deltaTime;
            if (timeNotSeen >= pushAfterNotSeenTime)
            {
                PushPlayer();
                TryTriggerRagdoll();
                Vanish();
                timeNotSeen = 0f;
            }
        }
    }

    void Respawn()
    {
        Vector3 spawnPos = FindValidSpawnPosition();
        transform.position = SnapToGround(spawnPos);

        render.enabled = true;
        col.enabled = true;
        attackReady = false;
        attackTimer = 0f;
        vanishAvailableTimer = vanishAfterSpawnCooldown;
        _trackingEnabled = false;

        if (respawnSFX.Length > 0)
        {
            AudioClip clip = respawnSFX[Random.Range(0, respawnSFX.Length)];
            source.PlayOneShot(clip);
        }

        Invoke(nameof(EnableTracking), trackingStartDelay);
    }

    void EnableTracking()
    {
        _trackingEnabled = true;
        positionOffset = GetDynamicOffset();
    }

    Vector3 GetDynamicOffset()
    {
        return -playerCamera.transform.forward * followDistance + Vector3.up * heightOffset;
    }

    Vector3 SnapToGround(Vector3 position)
    {
        if (Physics.SphereCast(position + Vector3.up * 5f, 0.5f, Vector3.down,
                             out RaycastHit hit, 10f, groundMask))
        {
            return hit.point + Vector3.up * 0.25f;
        }
        return position;
    }

    bool IsPlayerLookingDirectly()
    {
        Vector3 screenPoint = playerCamera.WorldToViewportPoint(transform.position);
        return screenPoint.z > 0 &&
               screenPoint.x > 0.4f && screenPoint.x < 0.6f &&
               screenPoint.y > 0.4f && screenPoint.y < 0.6f;
    }

    void HandlePositionTracking()
    {
        if (isVanished || isVisible) return;

        Vector3 targetPosition = player.position + positionOffset;
        Vector3 adjustedPosition = FindValidPositionNear(targetPosition);

        transform.position = Vector3.Lerp(transform.position,
                                         SnapToGround(adjustedPosition),
                                         Time.deltaTime * 5f);
    }

    bool IsVisibleToPlayer()
    {
        Vector3 viewportPos = playerCamera.WorldToViewportPoint(transform.position);
        bool inViewFrustum = viewportPos.z > 0.1f &&
                           viewportPos.x >= 0f &&
                           viewportPos.x <= 1f &&
                           viewportPos.y >= 0f &&
                           viewportPos.y <= 1f;

        if (!inViewFrustum) return false;

        float radius = GetComponent<Collider>().bounds.extents.magnitude;
        Vector3 direction = transform.position - playerCamera.transform.position;
        float distance = direction.magnitude;

        return !Physics.SphereCast(playerCamera.transform.position, radius,
                                  direction.normalized, out RaycastHit hit,
                                  distance, obstacleMask);
    }

    void Vanish()
    {
        if (isVanished) return;
        isVanished = true;
        render.enabled = false;
        col.enabled = false;
        vanishTimer = 0f;

        if (respawnSFX.Length > 0)
        {
            AudioClip clip = respawnSFX[Random.Range(0, respawnSFX.Length)];
            source.PlayOneShot(clip);
        }
    }

    void HandleVanishedState()
    {
        if (!isVanished) return;

        vanishTimer += Time.deltaTime;
        if (vanishTimer >= vanishCooldown)
        {
            Respawn();
            isVanished = false;
        }
    }

    Vector3 FindValidSpawnPosition()
    {
        Vector3 spawnPos = Vector3.zero;
        int attempts = 0;
        bool validPosition = false;

        while (!validPosition && attempts < 50)
        {
            Vector3 cameraForward = playerCamera.transform.forward;
            cameraForward.y = 0;
            cameraForward.Normalize();

            // Add random angle spread (-60 to +60 degrees)
            float spreadAngle = Random.Range(-60f, 60f);
            Quaternion spreadRotation = Quaternion.Euler(0, spreadAngle, 0);
            Vector3 behindDirection = spreadRotation * (-cameraForward);

            // Add random lateral offset
            Vector3 right = Vector3.Cross(behindDirection, Vector3.up).normalized;
            Vector3 lateralOffset = right * Random.Range(-2f, 2f);

            float distance = Random.Range(minSpawnDistance, spawnRadius);
            spawnPos = player.position + behindDirection * distance + lateralOffset;

            spawnPos = SnapToGround(spawnPos);

            // Check vertical distance from player
            if (Mathf.Abs(spawnPos.y - player.position.y) > 3f) continue;

            validPosition = !IsPositionVisible(spawnPos) &&
                          !Physics.CheckSphere(spawnPos, 1f, obstacleMask);
            attempts++;
        }

        if (!validPosition)
        {
            // Fallback position
            Vector3 cameraForward = playerCamera.transform.forward;
            cameraForward.y = 0;
            cameraForward.Normalize();
            Vector3 behindDirection = -cameraForward;
            spawnPos = player.position + behindDirection * minSpawnDistance;
            spawnPos = SnapToGround(spawnPos);
        }

        return spawnPos;
    }

    bool IsPositionVisible(Vector3 position)
    {
        Vector3 viewportPos = playerCamera.WorldToViewportPoint(position);
        bool inView = viewportPos.z > 0 &&
                    viewportPos.x >= 0.1f && viewportPos.x <= 0.9f &&
                    viewportPos.y >= 0.1f && viewportPos.y <= 0.9f;
        if (!inView) return false;

        Vector3 dirToPos = (position - playerCamera.transform.position).normalized;
        float distance = Vector3.Distance(playerCamera.transform.position, position);
        return !Physics.Raycast(playerCamera.transform.position, dirToPos, distance, obstacleMask);
    }

    void PushPlayer()
    {
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 pushDir = (player.position - transform.position).normalized;
            rb.AddForce(pushDir * pushForce, ForceMode.Impulse);
        }
    }

    void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
    }

    Vector3 FindValidPositionNear(Vector3 preferredPosition)
    {
        Vector3 finalPosition = preferredPosition;
        if (Physics.CheckSphere(preferredPosition, 1f, obstacleMask))
        {
            Vector3 dirToPlayer = (player.position - preferredPosition).normalized;
            finalPosition = preferredPosition + dirToPlayer * 2f;
        }
        return finalPosition;
    }

    void FindCamera()
    {
        playerCamera = Camera.main;
    }

    void EnsurePlayerAndCameraReferences()
    {
        if (player == null)
            FindPlayer();
        if (playerCamera == null)
            FindCamera();
    }

    void FacePlayer()
    {
        if (player == null || isVanished) return;
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(direction), rotationSpeed * Time.deltaTime);
    }

    void OnVisibilityChanged()
    {
        // Reserved for visibility change logic
    }

    void TryTriggerRagdoll()
    {
        if (player != null && player.TryGetComponent<PlayerController>(out var controller))
        {
            controller.Ragdoll();
        }
    }
}
