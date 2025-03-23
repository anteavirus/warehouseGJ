using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Renderer), typeof(AudioSource))]
public class BoogeymanAI : MonoBehaviour
{
    public float respawnCooldown = 1f;
    public float chaseDelay = 2f;
    public float chaseSpeed = 5f;
    public float centerViewThreshold = 5f;
    public float attackRange = 2f;
    public float rotationSpeed = 5f;
    public LayerMask obstacleMask;
    public AudioClip[] respawnSFX;

    private Transform player;
    private Camera playerCamera;
    private Renderer render;
    private BoxCollider spawnArea;

    private bool isVisible;
    private float respawnTimer;
    private float chaseTimer;
    private float lastVisibilityChangeTime;
    private bool isChasing;
    private AudioSource source;

    public void SetSpawnArea(BoxCollider area) => spawnArea = area;

    void Start()
    {
        render = GetComponent<Renderer>();
        source = GetComponent<AudioSource>();
        FindPlayer();
        FindCamera();
    }

    void Update()
    {
        EnsurePlayerAndCameraReferences();

        CheckVisibility();
        HandleStates();
        HandleFrequentVisibilityChanges();
        FacePlayer();
    }

    void EnsurePlayerAndCameraReferences()
    {
        if (player == null)
            FindPlayer();
        if (playerCamera == null)
            FindCamera();
    }

    void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
    }

    void FindCamera()
    {
        playerCamera = Camera.main;
    }

    void CheckVisibility()
    {
        bool wasVisible = isVisible;
        isVisible = IsVisibleToPlayer();

        if (isVisible)
        {
            if (IsInCenterOfView()) Vanish();
        }

        if (isVisible != wasVisible)
            OnVisibilityChanged(isVisible);
    }

    bool IsVisibleToPlayer()
    {
        if (playerCamera == null) return false;

        Vector3 viewportPos = playerCamera.WorldToViewportPoint(transform.position);
        bool inView = viewportPos.z > 0
            && viewportPos.x >= 0 && viewportPos.x <= 1
            && viewportPos.y >= 0 && viewportPos.y <= 1;

        if (!inView) return false;

        Vector3 dirToBoogey = transform.position - playerCamera.transform.position;
        return !Physics.Raycast(playerCamera.transform.position, dirToBoogey.normalized,
                              dirToBoogey.magnitude, obstacleMask);
    }

    bool IsInCenterOfView()
    {
        if (playerCamera == null) return false;

        Vector3 screenPos = playerCamera.WorldToScreenPoint(transform.position);
        if (screenPos.z <= 0) return false; // Behind camera

        Vector3 screenCenter = new Vector3(Screen.width / 2, Screen.height / 2, 0);
        return Vector3.Distance(screenPos, screenCenter) < centerViewThreshold;
    }

    void OnVisibilityChanged(bool visible)
    {
        lastVisibilityChangeTime = Time.time;
        if (visible) OnBecameVisible();
        else OnBecameInvisible();
    }

    void HandleStates()
    {
        if (!isVisible)
        {
            respawnTimer += Time.deltaTime;
            if (respawnTimer >= respawnCooldown) Respawn();
        }
        else if (!isChasing)
        {
            chaseTimer += Time.deltaTime;
            if (chaseTimer >= chaseDelay && player != null) StartChasing();
        }

        if (isChasing && player != null) ChasePlayer();
    }

    void HandleFrequentVisibilityChanges()
    {
        if (Time.time - lastVisibilityChangeTime < 0.3f && isVisible)
            StartChasing();
    }

    void OnBecameVisible()
    {
        chaseTimer = 0f;
        respawnTimer = 0f;
    }

    void OnBecameInvisible()
    {
        isChasing = false;
        respawnTimer = 0f;
    }

    void FacePlayer()
    {
        if (player == null) return;
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation,
            rotationSpeed * Time.deltaTime);
    }

    void StartChasing()
    {
        isChasing = true;
        chaseTimer = 0f;
    }

    void ChasePlayer()
    {
        if (player == null) return;

        Vector3 direction = (player.position - transform.position).normalized;
        transform.position += chaseSpeed * Time.deltaTime * direction;

        if (Vector3.Distance(transform.position, player.position) <= attackRange)
        {
            TryTriggerRagdoll();
            Vanish();
        }
    }

    void TryTriggerRagdoll()
    {
        if (player != null && player.TryGetComponent<PlayerController>(out var controller))
        {
            controller.Ragdoll();
        }
    }

    void Vanish()
    {
        Respawn();

        if (respawnSFX != null && respawnSFX.Length > 0)
        {
            AudioClip clip = respawnSFX[Random.Range(0, respawnSFX.Length)];
            source.PlayOneShot(clip);
        }
    }

    void Respawn()
    {
        if (spawnArea == null) return;

        Vector3 spawnPos = new Vector3(
            Random.Range(spawnArea.bounds.min.x, spawnArea.bounds.max.x),
            spawnArea.bounds.center.y,
            Random.Range(spawnArea.bounds.min.z, spawnArea.bounds.max.z)
        );

        transform.position = spawnPos;
        isChasing = false;
        respawnTimer = 0f;
        chaseTimer = 0f;
    }
}
