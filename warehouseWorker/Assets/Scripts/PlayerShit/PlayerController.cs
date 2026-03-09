using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class PlayerController : NetworkBehaviour
{
    public static PlayerController LocalPlayer;
    // === CAN BE CHANGED ===
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float airControlFactor = 0.5f;
    [SerializeField] private float groundDrag = 5f;
    [SerializeField] private float airDrag = 0.5f;
    public LayerMask groundLayer;

    [Header("Camera Inversion Settings")]
    public float inversionProgress = 0f;
    public float inversionTransitionSpeed = 2f;

    [Header("Look Settings")]
    [SerializeField] private float defaultMouseSensitivity = 100f;
    [SerializeField] private Transform playerCameraTransform;
    public Camera playerCamera;
    [SerializeField] private float maxLookAngle = 90f;

    [Header("Interaction Settings")]
    public float pickupRange = 2f;
    public LayerMask interactableLayer;
    public Transform handTransform;
    public Transform pickUpHintUI;
    public Transform canUseOnItemHintUI;

    [Header("Throwing")]
    [SerializeField] private float minThrowForce = 5f;
    [SerializeField] private float maxThrowForce = 25f;
    [SerializeField] private float maxChargeTime = 1.5f;
    [SerializeField] private GameObject chargeMeterUI;
    [SerializeField] private Image chargeMeter;

    [Header("Placement Settings")]
    [SerializeField] private Material previewMaterial;
    [SerializeField] private float placementMaxDistance = 5f;
    [SerializeField] private LayerMask placementLayer;
    [SerializeField] private Vector3 placementOffset = new Vector3(0, 0.1f, 0);
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioSource landingSource;
    [SerializeField] private float footstepInterval = 0.4f;
    [SerializeField] private List<SurfaceSound> surfaceSounds = new();
    [SerializeField] private AudioClip[] slipSounds;

    [Header("Held Item Physics")]
    [SerializeField] private float followForce = 100f;
    [SerializeField] private float maxHoldDistance = 1.5f;
    [SerializeField] private float damping = 5f;
    [SerializeField] private float angularDamping = 5f;

    [Header("Camera Wobble")]
    [SerializeField] private float wobbleAmount = 0.05f;
    [SerializeField] private float wobbleFrequency = 4f;
    [SerializeField] private float wobbleResetSpeed = 5f;

    [Header("Slipping Mechanics")]
    [SerializeField] private float slipRisk = 0f;
    [SerializeField] private float slipRiskIncreasePerFootstep = 0.15f;
    [SerializeField] private float slipRiskDecayRate = 0.1f;
    [SerializeField] private float maxSlipRisk = 1f;
    [SerializeField] private float slipCooldown = 3f;
    [SerializeField] private float minSlipSpeed = 3f;

    [Space]
    // === MUST BE CHANGED ===
    [Header("Settings Reference")]
    [SerializeField] private SettingsManager settingsManager;

    private float currentMouseSensitivity = 100;

    private Vector3 lastMovementDirection;

    [Header("Pause UI Menu")]
    [SerializeField] private Animator pauseAnimator;
    [SerializeField] private PauseMenuUI pauseMenu;
    public GameObject pauseUI;
    public GameObject UI;
    public AudioSource musicSource;

    public bool alive = true;
    private Rigidbody heldItemRb;
    private Vector3 originalCameraLocalPosition;

    [SerializeField] private Image timeWatch;
    private bool checkingTime;
    private Vector2 timeWatchStartPosition;
    [SerializeField] private Vector2 timeWatchMoveToPosition;

    private Rigidbody rb;
    private float xRotation;
    private Vector3 moveDirection;
    public GameObject heldItem, dragObject, previewObject;
    private Coroutine spinRoutine;
    private SurfaceType currentSurface;

    private bool isHoldingToPlace, isValidPlacement, wasGrounded, canMove = true;

    private float currentChargeTime, chargedThrowForce, currentRotationOffset, footstepTimer, lastTimeRagdoll, rbMass;

    PlayerFeetScript feet;
    PlayerGrabbyScript grabby;

    [SyncVar(hook = nameof(OnHeldItemChanged))]
    private uint heldItemNetId = 0;

    private bool isDragging = false;
    private NetworkIdentity dragNetIdentity;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rbMass = rb.mass;

        if (feet == null)
            feet = transform.Find("feet")?.GetComponent<PlayerFeetScript>();

        if (grabby == null)
        {
            grabby = transform.Find("Main Camera").Find("grabbyHitbox")?.GetComponent<PlayerGrabbyScript>();
            grabby.controller = this;
        }

        // Only setup camera and input for local player
        if (isLocalPlayer)
        {
            LocalPlayer = this;

            if (pauseMenu == null) 
                pauseMenu = pauseUI.GetComponent<PauseMenuUI>();

            if (settingsManager == null)
                settingsManager = FindObjectOfType<SettingsManager>();

            if (settingsManager != null)
            {
                settingsManager.InitializeThyself();
                settingsManager.LoadSettings();
            }

            Cursor.lockState = CursorLockMode.Locked;

            if (playerCamera == null && Camera.main != null)
                playerCamera = Camera.main;

            playerCameraTransform = playerCamera.transform;
            originalCameraLocalPosition = playerCameraTransform.localPosition;
            timeWatchStartPosition = timeWatch.transform.position;

            // Disable other players' cameras
            DisableOtherPlayerCameras();
        }
        else
        {
            // Disable camera and input for remote players
            if (playerCamera != null)
                playerCamera.gameObject.SetActive(false);
            
            // Disable UI for remote players
            if (pauseUI != null)
                pauseUI.SetActive(false);

            if (UI != null)
                UI.SetActive(false);
        }
    }

    private void DisableOtherPlayerCameras()
    {
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (PlayerController player in players)
        {
            if (player != this && player.playerCamera != null)
            {
                player.playerCamera.gameObject.SetActive(false);
            }
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!isLocalPlayer)
        {
            // Disable camera for remote players
            if (playerCamera != null)
                playerCamera.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        if (moveDirection.magnitude < 0.1f || rb.velocity.magnitude < minSlipSpeed)
        {
            slipRisk = Mathf.MoveTowards(slipRisk, 0f, slipRiskDecayRate * Time.deltaTime);
        }

        if (!alive) return;
        HandlePauseToggle();

        if (!Application.isFocused || pauseMenu.isPaused) return;

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.I)) Ragdoll();
#endif
        HandleLook();
        HandleJump();
        UpdateDrag();
        HandlePlacement();
        HandleUseItem();
        HandleThrow();
        HandleInteractions();
        HandleFootsteps();
        HandleCameraWobble();
        HandleCheckTime();
    }

    void FixedUpdate()
    {
        if (!alive) return;

        HandleHeldItemPhysics();
        HandleDragItemPhysics();
        if (isLocalPlayer)
        {
            if (canMove) MovePlayer();
        }
    }

    void HandlePauseToggle()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            pauseMenu.TogglePause();

            Cursor.lockState = pauseMenu.isPaused ?
                CursorLockMode.None :
                CursorLockMode.Locked;

            Cursor.visible = pauseMenu.isPaused;
        }
    }

    void HandleCheckTime()
    {
        if (settingsManager.GetActionDown("CheckTime"))
        {
            checkingTime = !checkingTime;
            StopCoroutine(MoveImage());
            StartCoroutine(MoveImage());

        }
    }

    IEnumerator MoveImage()
    {
        Vector2 targetPos = checkingTime ? timeWatchMoveToPosition : timeWatchStartPosition;
        RectTransform rectTransform = timeWatch.GetComponent<RectTransform>();
        Vector2 startPos = rectTransform.anchoredPosition;

        for (float t = 0; t < 1; t += Time.deltaTime / 0.3f)
        {
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, Mathf.SmoothStep(0, 1, t));
            yield return null;
        }
    }

    private void MovePlayer()
    {
        var input = new Vector2(
            (1 - 2 * inversionProgress) * Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        moveDirection = transform.forward * input.y + transform.right * input.x;

        float multiplier = feet.isGrounded ? 1f : airControlFactor;
        rb.AddForce(10f * multiplier * moveSpeed * moveDirection.normalized, ForceMode.Force);

        Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        if (flatVel.magnitude > moveSpeed)
        {
            Vector3 limitedVel = flatVel.normalized * moveSpeed;
            rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
        }
    }

    private void HandleHeldItemPhysics()
    {
        if (heldItem == null || heldItemRb == null) return;

        handTransform.GetPositionAndRotation(out Vector3 targetPosition, out Quaternion targetRotation);

        // Position handling
        Vector3 positionDelta = targetPosition - heldItemRb.position;
        float distance = positionDelta.magnitude;

        Vector3 force = positionDelta.normalized * (followForce * distance);
        force -= heldItemRb.velocity * damping;
        heldItemRb.AddForce(force);

        // Rotation handling
        Quaternion rotationDelta = targetRotation * Quaternion.Inverse(heldItemRb.rotation);
        rotationDelta.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;

        Vector3 torque = (0.5f * angle * axis) - (heldItemRb.angularVelocity * angularDamping);
        heldItemRb.AddTorque(torque, ForceMode.Acceleration);

        // Prevent object from getting too far
        if (distance > maxHoldDistance)
        {
            heldItemRb.MovePosition(targetPosition);
            heldItemRb.velocity = Vector3.zero;
            heldItemRb.angularVelocity = Vector3.zero;
        }
    }

    private void HandleJump()
    {
        if (settingsManager.GetActionDown("Jump") && feet.isGrounded)
        {
            rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
            slipRisk += 5; // nope not gonna cap it at 100 i'll see the players annoyed
        }
    }

    private void HandleCameraWobble()
    {
        if (feet.isGrounded && moveDirection != Vector3.zero)
        {
            float wobble = Mathf.Sin(Time.time * wobbleFrequency) * wobbleAmount;
            Vector3 targetWobble = originalCameraLocalPosition + new Vector3(0, Mathf.Abs(wobble), 0);
            playerCameraTransform.localPosition = Vector3.Lerp(
                playerCameraTransform.localPosition,
                targetWobble,
                Time.deltaTime * wobbleResetSpeed
            );
        }
        else
        {
            playerCameraTransform.localPosition = Vector3.Lerp(
                playerCameraTransform.localPosition,
                originalCameraLocalPosition,
                Time.deltaTime * wobbleResetSpeed
            );
        }
    }
    // TODO: clients can't drag, but at least the server can see how the clients pick up the items. noone can place down items, not sure about dropping them. items' position is not synched properly. clients don't get shelves generated, i'm assuming something was a null.
    private void UpdateDrag() => rb.drag = feet.isGrounded ? groundDrag : airDrag;

    private void HandleFootsteps()
    {
        if (feet.isGrounded)
        {
            if (!wasGrounded)
            {
                DetectSurface();
                PlayLandingSound();
            }

            if (moveDirection != Vector3.zero)
            {
                footstepTimer -= Time.deltaTime;
                if (footstepTimer <= 0)
                {
                    DetectSurface();
                    PlayFootstepSound();
                    footstepTimer = footstepInterval;
                    if (currentSurface == SurfaceType.Water && canMove)
                    {
                        float horizontalSpeed = new Vector3(rb.velocity.x, 0, rb.velocity.z).magnitude;
                        float speedFactor = Mathf.InverseLerp(minSlipSpeed, moveSpeed, horizontalSpeed);

                        float directionSimilarity = Vector3.Dot(moveDirection.normalized, lastMovementDirection.normalized);
                        slipRisk += slipRiskIncreasePerFootstep * speedFactor * (0.5f + 0.5f * directionSimilarity);
                        
                        lastMovementDirection = moveDirection;

                        // Check for slip with weighted probability
                        if (slipRisk >= Random.Range(0f, maxSlipRisk))
                        {
                            Slip(speedFactor);
                            slipRisk = 0f;
                        }
                    }
                }
            }
        }
        wasGrounded = feet.isGrounded;
    }

    private void DetectSurface()
    {
        if (Physics.Raycast(transform.position, Vector3.down,
            out RaycastHit hit, transform.localScale.y * .66f, groundLayer))
        {
            if (hit.collider.TryGetComponent<GroundSurface>(out var surface))
            {
                currentSurface = surface.surfaceType;
            }
        }
    }

    private void PlayFootstepSound()
    {
        var sound = surfaceSounds.Find(s => s.surface == currentSurface);
        if (sound != null && sound.footstepSounds.Length > 0)
        {
            AudioClip clip = sound.footstepSounds[Random.Range(0, sound.footstepSounds.Length)];
            footstepSource.PlayOneShot(clip);
        }
    }

    private void PlayLandingSound()
    {
        var sound = surfaceSounds.Find(s => s.surface == currentSurface);
        if (sound != null && sound.landingSounds.Length > 0)
        {
            landingSource.PlayOneShot(sound.landingSounds[Random.Range(0, sound.landingSounds.Length)]);
        }
    }

    private void HandleLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * currentMouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * currentMouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);

        playerCameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 180 * inversionProgress);
        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleInteractions()
    {
        if (settingsManager.GetActionDown("Pickup"))
        {
            if (heldItem == null) TryPickupItem(grabby.Pop());
            else if (!isHoldingToPlace) DropItem();
        }

        if (settingsManager.GetActionDown("Drag"))
        {
            TryDragItem(grabby.focusedTarget);
        }
    }


    private void HandleUseItem()
    {
        if (settingsManager.GetActionDown("Use"))
        {
            if (heldItem != null) 
            {
                heldItem.GetComponent<Item>().OnUse(gameObject);
                return;
            }
            if (Physics.Raycast(playerCameraTransform.position, playerCameraTransform.forward,
                out RaycastHit hit, pickupRange, interactableLayer))
            {
                hit.transform.GetComponent<Item>().OnUse(gameObject);
                return;
            }
        }
    }

    private void TryDragItem(object item)
    {
        if (item == null)
        {
            StopDragging();
            return;
        }

        if (item is not GameObject gameObj) return;
        if (gameObj.layer != LayerMask.NameToLayer("Draggable")) return;

        var netId = gameObj.GetComponent<NetworkIdentity>();
        if (netId == null)
        {
            Debug.LogWarning("Draggable object has no NetworkIdentity!");
            return;
        }

        // If we're already dragging this object, stop
        if (dragObject == gameObj)
        {
            StopDragging();
            return;
        }

        // If we're dragging something else, stop it first
        if (dragObject != null)
            StopDragging();

        // Request authority from the server
        CmdRequestDragAuthority(netId.netId);
    }

    [Command]
    private void CmdRequestDragAuthority(uint netId)
    {
        if (!NetworkServer.spawned.TryGetValue(netId, out var netIdentity))
            return;

        // Grant authority if not already owned
        if (netIdentity.connectionToClient == null)
            netIdentity.AssignClientAuthority(connectionToClient);

        TargetDragAuthorityGranted(connectionToClient, netId);
    }

    [TargetRpc]
    private void TargetDragAuthorityGranted(NetworkConnection target, uint netId)
    {
        if (!NetworkClient.spawned.TryGetValue(netId, out var netIdentity))
            return;

        dragObject = netIdentity.gameObject;
        dragNetIdentity = netIdentity;
        isDragging = true;

        // Configure Rigidbody for dragging
        var dragRB = dragObject.GetComponent<Rigidbody>();
        if (dragRB == null) dragRB = dragObject.AddComponent<Rigidbody>();
        dragRB.drag = 1f;
        dragRB.angularDrag = 1f;
        dragRB.useGravity = true;

        Debug.Log($"Started dragging: {dragObject.name}");
    }

    private void StopDragging()
    {
        if (dragObject != null && dragNetIdentity != null)
        {
            CmdReleaseDragAuthority(dragNetIdentity.netId);
        }

        isDragging = false;
        dragObject = null;
        dragNetIdentity = null;
    }

    [Command]
    private void CmdReleaseDragAuthority(uint netId)
    {
        if (!NetworkServer.spawned.TryGetValue(netId, out var netIdentity))
            return;
        if (netIdentity.connectionToClient == connectionToClient)
            netIdentity.RemoveClientAuthority();
    }

    [SerializeField] private float dragFollowForce = 100f;
    [SerializeField] private float dragDamping = 5f;

    private void HandleDragItemPhysics()
    {
        if (!isDragging || dragObject == null || !dragObject.TryGetComponent<Rigidbody>(out var dragRB))
        {
            if (rb != null) rb.mass = rbMass;
            return;
        }   

        Vector3 direction = handTransform.position - dragObject.transform.position;
        float distance = direction.magnitude;

        if (distance > 0.1f)
        {
            Vector3 force = direction.normalized * (dragFollowForce * distance);
            force -= dragRB.velocity * dragDamping;
            dragRB.AddForce(force);
            rb.AddForce(-force);
        }

        rb.mass = rbMass + dragRB.mass;
    }
    
    public void ForcePickupItem(Item stupidBullshit)
    {
        TryPickupItem(stupidBullshit);
    }

    private void TryPickupItem(Item stupidShit = null)
    {
        if (Physics.Raycast(playerCameraTransform.position, playerCameraTransform.forward,
            out RaycastHit hit, pickupRange, interactableLayer))
        {
            if (hit.collider.TryGetComponent<Item>(out var item))
            {
                if (item.isPickupable)
                {
                    PickUpItem(item);
                    return;
                }
            }
            // May lord not see this mess, for his punishment'd be death by choking hanging.
            if (stupidShit != null)
            {
                if (stupidShit.isPickupable)
                {
                    PickUpItem(stupidShit);
                    return;
                }
            }

        }

        // May lord not see this mess, for his punishment'd be death by choking hanging.
        if (stupidShit != null)
        {
            if (stupidShit.isPickupable)
            {
                PickUpItem(stupidShit);
                return;
            }
        }
    }

    void PickUpItem(Item item)
    {
        if (item == null || !item.isPickupable) return;

        if (!isLocalPlayer) return;

        // Get NetworkIdentity of item
        if (!item.TryGetComponent<NetworkIdentity>(out var itemNetId))
        {
            itemNetId = item.gameObject.AddComponent<NetworkIdentity>();
            Debug.LogWarning("wait, I can't add network identity to items. how did this get through?");
        }

        CmdPickupItem(itemNetId.netId);
    }

    [Command]
    private void CmdPickupItem(uint itemNetId)
    {
        if (!NetworkServer.spawned.ContainsKey(itemNetId)) return;
        
        GameObject itemObj = NetworkServer.spawned[itemNetId].gameObject;
        if (itemObj == null) return;

        Item item = itemObj.GetComponent<Item>();
        if (item == null || !item.isPickupable) return;

        NetworkIdentity itemNetIdentity = itemObj.GetComponent<NetworkIdentity>();
        if (itemNetIdentity == null) return;

        // Check if item is already held by someone else
        if (itemNetIdentity.connectionToClient != null && itemNetIdentity.connectionToClient != connectionToClient)
            return;

        // Assign authority to this player
        if (itemNetIdentity.connectionToClient == null)
        {
            itemNetIdentity.AssignClientAuthority(connectionToClient);
        }
        
        heldItemNetId = itemNetId;
        
        RpcPickupItem(itemNetId);
    }

    [ClientRpc]
    private void RpcPickupItem(uint itemNetId)
    {
        if (!NetworkClient.spawned.ContainsKey(itemNetId)) return;
        
        GameObject itemObj = NetworkClient.spawned[itemNetId].gameObject;
        Item item = itemObj.GetComponent<Item>();
        if (item == null) return;

        heldItem = itemObj;
        item.OnPickup(handTransform);

        heldItem.transform.parent = null;

        heldItemRb = heldItem.GetComponent<Rigidbody>();
        if (heldItemRb != null)
        {
            heldItemRb.isKinematic = false;
            heldItemRb.useGravity = false;
            heldItemRb.drag = 5f;
            heldItemRb.angularDrag = 5f;
        }

        try
        {
            feet.objects.Remove(item.GetComponent<Collider>());
            feet.CleanUp();
        }
        catch { Debug.Log("Failed to remove feet or something. Whatever this is fine"); }
    }

    private void OnHeldItemChanged(uint oldId, uint newId)
    {
        if (newId == 0)
        {
            heldItem = null;
            heldItemRb = null;
            return;
        }

        if (NetworkClient.spawned.ContainsKey(newId))
        {
            heldItem = NetworkClient.spawned[newId].gameObject;
            heldItemRb = heldItem.GetComponent<Rigidbody>();
        }
    }

    public void ForceDropItem()
    {
        DropItem();
    }

    private void DropItem()
    {
        if (heldItem == null || !isLocalPlayer) return;
        
        NetworkIdentity itemNetId = heldItem.GetComponent<NetworkIdentity>();
        if (itemNetId != null)
        {
            CmdDropItem(itemNetId.netId);
        }
    }

    [Command]
    private void CmdDropItem(uint itemNetId)
    {
        if (heldItemNetId != itemNetId) return;

        GameObject itemObj = NetworkServer.spawned[itemNetId].gameObject;
        Item item = itemObj.GetComponent<Item>();
        if (item == null) return;

        // Remove authority
        itemObj.GetComponent<NetworkIdentity>().RemoveClientAuthority();
        heldItemNetId = 0;

        RpcDropItem(itemNetId);
    }

    [ClientRpc]
    private void RpcDropItem(uint itemNetId)
    {
        if (!NetworkClient.spawned.ContainsKey(itemNetId)) return;

        GameObject itemObj = NetworkClient.spawned[itemNetId].gameObject;
        DisableSpinRoutineIfReal();

        Item item = itemObj.GetComponent<Item>();
        item.OnDrop();

        // Reset physics properties
        Rigidbody itemRb = itemObj.GetComponent<Rigidbody>();
        if (itemRb != null)
        {
            itemRb.velocity = Vector3.zero;
            itemRb.useGravity = true;
            itemRb.drag = 0f;
            itemRb.angularDrag = 0.05f;
        }
        
        if (heldItem == itemObj)
        {
            heldItem = null;
            heldItemRb = null;
        }
    }

    private void HandleThrow()
    {
        if (heldItem == null) return;

        if (settingsManager.GetActionDown("Throw"))
        {
            currentChargeTime = 0f;
            DisableSpinRoutineIfReal();
            chargeMeterUI.SetActive(true);
            spinRoutine = StartCoroutine(SpinWhileCharging());
        }

        if (settingsManager.GetAction("Throw"))
        {
            chargeMeter.fillAmount = Mathf.Clamp01((currentChargeTime += Time.deltaTime) / maxChargeTime);
            chargedThrowForce = Mathf.Lerp(minThrowForce, maxThrowForce, chargeMeter.fillAmount);
        }

        if (settingsManager.GetActionUp("Throw") && spinRoutine != null)
        {
            DestroyPreview();
            ThrowItem(chargedThrowForce);
            currentChargeTime = 0f;

            DisableSpinRoutineIfReal();
        }
    }

    private void DisableSpinRoutineIfReal()
    {
        if (spinRoutine == null) return;
        StopCoroutine(spinRoutine);
        spinRoutine = null;
        chargeMeterUI.SetActive(false);
    }

    private IEnumerator SpinWhileCharging()
    {
        while (true)
        {
            if (heldItem != null)
            {
                float spinSpeed = chargeMeter.fillAmount * 360f;
                heldItem.transform.Rotate(Vector3.right, spinSpeed * Time.deltaTime);
            }
            yield return null;
        }
    }

    private void ThrowItem(float force)
    {
        if (heldItem == null || !isLocalPlayer) return;

        NetworkIdentity itemNetId = heldItem.GetComponent<NetworkIdentity>();
        if (itemNetId != null)
        {
            Vector3 throwDirection = playerCameraTransform.forward;
            CmdThrowItem(itemNetId.netId, throwDirection, force);
        }
    }

    [Command]
    private void CmdThrowItem(uint itemNetId, Vector3 direction, float force)
    {
        if (heldItemNetId != itemNetId) return;

        if (!NetworkServer.spawned.TryGetValue(itemNetId, out var netIdentity))
            return;

        GameObject itemObj = netIdentity.gameObject;
        Item item = itemObj.GetComponent<Item>();
        if (item == null) return;

        netIdentity.RemoveClientAuthority();
        heldItemNetId = 0;

        // SERVER throws the item
        item.OnThrow(direction, force);

        // (Optional) ClientRpc for effects (sound, particles) – do not apply physics again
        RpcThrowItem(itemNetId, direction, force);
    }

    [ClientRpc]
    private void RpcThrowItem(uint itemNetId, Vector3 direction, float force)
    {
        // Only for client‑side effects (sound, animation)
        if (!NetworkClient.spawned.TryGetValue(itemNetId, out var netIdentity))
            return;
        // Optionally play throw sound, etc.
    }

    private void HandlePlacement()
    {
        HandlePlacementInput();
        HandlePlacementPreview();
    }

    // TODO: fucking fix this
    private void HandlePlacementInput()
    {
        if (heldItem == null || !Application.isFocused) return;

        if (settingsManager.GetActionDown("Place"))
        {
            isHoldingToPlace = true;
            CreatePreview();
        }

        if (settingsManager.GetActionUp("Place"))
        {
            if (isHoldingToPlace)
            {
                if (isValidPlacement)
                {
                    PlaceItem();
                }
                else
                {
                    heldItem.SetActive(true);
                }
                DestroyPreview();
                isHoldingToPlace = false;
            }
        }

        if (settingsManager.GetActionDown("Throw"))
        {
            DestroyPreview();
            isHoldingToPlace = false;
        }
    }

    private void HandlePlacementPreview()
    {
        if (!isHoldingToPlace || previewObject == null) return;

        currentRotationOffset += Input.mouseScrollDelta.y * rotationSpeed;

        RaycastHit[] hits = Physics.RaycastAll(
            playerCameraTransform.position,
            playerCameraTransform.forward,
            placementMaxDistance,
            placementLayer
        );

        RaycastHit? validHit = GetPriorityHit(hits);

        isValidPlacement = validHit.HasValue;

        bool isVerticalSurface = false;
        bool isThisActuallyNecessaryForTheGameplayLoop = false;

        if (isValidPlacement && isThisActuallyNecessaryForTheGameplayLoop)
        {
            isVerticalSurface = Mathf.Abs(validHit.Value.normal.y) < 0.7f;
            isValidPlacement = !isVerticalSurface;
        }

        Vector3 targetPos;
        if (isValidPlacement || isVerticalSurface)
        {
            float offset = GetObjectBottomOffset(previewObject);
            targetPos = validHit.Value.point
                       + placementOffset
                       + Vector3.up * offset;
        }
        else
        {
            targetPos = playerCameraTransform.position
                       + playerCameraTransform.forward * placementMaxDistance;
        }

        // Shitty hack to stop the console spam of "waah the rotation is zero!!!" like no bitch, shit
        Vector3 cameraForward = playerCameraTransform.forward;
        Vector3 projectedForward = Vector3.ProjectOnPlane(cameraForward, Vector3.up);

        Quaternion targetRot = Quaternion.identity;
        if (cameraForward != Vector3.zero && projectedForward != Vector3.zero)
        {
            targetRot = Quaternion.Euler(0,
                Quaternion.LookRotation(projectedForward).eulerAngles.y + currentRotationOffset,
                0
            );
        }

        previewObject.transform.SetPositionAndRotation(targetPos, targetRot);
        UpdatePreviewAppearance();
    }

    [Command]
    public void CmdRequestTeleport(Vector3 position, Quaternion rotation)
    {
        // Server validates and teleports
        transform.SetPositionAndRotation(position, rotation);

        // Tell all clients about the teleport
        RpcTeleport(position, rotation);
    }

    [ClientRpc]
    void RpcTeleport(Vector3 position, Quaternion rotation)
    {
        if (!isLocalPlayer) return; // Only move our own player

        transform.SetPositionAndRotation(position, rotation);
    }

    public void DisableYoShit(GameObject player)
    {
        DisableYoShitRPC(player);
        DisableYoShitServer(player);
    }
    public void ReenableYoShit(GameObject player)
    {
        ReenableYoShitRPC(player);
        ReenableYoShitServer(player);
    }

    // Garbage code, yippee
    [ClientRpc]
    void DisableYoShitRPC(GameObject player)
    {
        player.SetActive(false);
        player.GetComponent<Rigidbody>().isKinematic = true;
    }
    [Server]
    void DisableYoShitServer(GameObject player)
    {
        player.SetActive(false);
        player.GetComponent<Rigidbody>().isKinematic = true;
    }


    [ClientRpc]
    void ReenableYoShitRPC(GameObject player)
    {
        player.SetActive(true);
        player.GetComponent<Rigidbody>().isKinematic = false;
    }
    [Server]
    void ReenableYoShitServer(GameObject player)
    {
        player.SetActive(true);
        player.GetComponent<Rigidbody>().isKinematic = false;
    }


    private RaycastHit? GetPriorityHit(RaycastHit[] hits)
    {
        RaycastHit? interactableHit = null;
        RaycastHit? grassHit = null;
        RaycastHit? fallbackHit = null;

        foreach (RaycastHit hit in hits)
        {
            // Skip ignored objects
            if (hit.collider.CompareTag("IgnoreRaycast") || hit.collider.CompareTag("Player"))
                continue;

            // Check layer priority
            if (UsefulStuffs.IsOnLayer(hit.collider.gameObject, "Interactable"))
            {
                if (!interactableHit.HasValue || hit.distance < interactableHit.Value.distance)
                    interactableHit = hit;
            }
            else if (UsefulStuffs.IsOnLayer(hit.collider.gameObject, "Grass"))
            {
                if (!grassHit.HasValue || hit.distance < grassHit.Value.distance)
                    grassHit = hit;
            }
            else
            {
                if (!fallbackHit.HasValue || hit.distance < fallbackHit.Value.distance)
                    fallbackHit = hit;
            }
        }

        // Return in priority order: Interactable > Grass > Other
        return interactableHit ?? grassHit ?? fallbackHit;
    }

    private void CreatePreview()
    {
        previewObject = Instantiate(heldItem);
        previewObject.tag = "IgnoreRaycast";
        
        if (previewObject.TryGetComponent<Item>(out var comp))  // that thing does not need item.
            Destroy(comp);
        
        previewObject.GetComponent<Rigidbody>().isKinematic = true;
        foreach (Collider col in previewObject.GetComponentsInChildren<Collider>())
            col.enabled = false;

        foreach (Renderer rend in previewObject.GetComponentsInChildren<Renderer>())
            rend.materials = System.Array.ConvertAll(rend.materials, _ => previewMaterial);
    }

    private void UpdatePreviewAppearance()
    {
        Color previewColor = isValidPlacement ? Color.green : Color.red;
        previewColor.a = 0.5f;
        foreach (Renderer rend in previewObject.GetComponentsInChildren<Renderer>())
            foreach (Material mat in rend.materials)
                mat.color = previewColor;
    }

    private void PlaceItem()
    {
        if (previewObject == null || !isLocalPlayer) return;
        
        NetworkIdentity itemNetId = heldItem?.GetComponent<NetworkIdentity>();
        if (itemNetId != null)
        {
            Vector3 position = previewObject.transform.position;
            Quaternion rotation = previewObject.transform.rotation;
            CmdPlaceItem(itemNetId.netId, position, rotation);
        }
    }

    [Command]
    private void CmdPlaceItem(uint itemNetId, Vector3 position, Quaternion rotation)
    {
        if (heldItemNetId != itemNetId) return;

        if (!NetworkServer.spawned.TryGetValue(itemNetId, out var netIdentity))
            return;

        GameObject itemObj = netIdentity.gameObject;
        Item item = itemObj.GetComponent<Item>();
        if (item == null) return;

        // Remove authority from this client
        netIdentity.RemoveClientAuthority();
        heldItemNetId = 0;

        // SERVER sets the position (authoritative)
        itemObj.transform.SetPositionAndRotation(position, rotation);
        item.OnPlace(position, rotation);   // calls OnDrop() internally

        // Tell all clients to also update the position (smoothness)
        RpcPlaceItem(itemNetId, position, rotation);
    }

    [ClientRpc]
    private void RpcPlaceItem(uint itemNetId, Vector3 position, Quaternion rotation)
    {
        if (!NetworkClient.spawned.ContainsKey(itemNetId)) return;

        GameObject itemObj = NetworkClient.spawned[itemNetId].gameObject;
        Item item = itemObj.GetComponent<Item>();
        
        Rigidbody itemRb = itemObj.GetComponent<Rigidbody>();
        if (itemRb != null)
        {
            itemRb.velocity = Vector3.zero;
            itemRb.useGravity = true;
            itemRb.drag = 0f;
            itemRb.angularDrag = 0.05f;
        }

        item.OnPlace(position, rotation);
        
        if (heldItem == itemObj)
        {
            heldItem = null;
            heldItemRb = null;
        }
        
        DisableSpinRoutineIfReal();
    }

    private float GetObjectBottomOffset(GameObject obj)
    {
        if (obj == null) return 0f;

        Bounds? bounds = null;
        if (obj.TryGetComponent<Collider>(out var col))
        {
            bounds = col.bounds;
        }
        else if (obj.TryGetComponent<Renderer>(out var rend) && rend.bounds.size.sqrMagnitude > 0)
        {
            bounds = rend.bounds;
        }

        if (bounds.HasValue)
        {
            float pivotY = obj.transform.position.y;
            float bottomY = bounds.Value.min.y;
            return pivotY - bottomY;
        }

        return obj.transform.lossyScale.y / 2;
    }


    private void DestroyPreview()
    {
        if (previewObject != null) Destroy(previewObject);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!isActiveAndEnabled) return;

        if (hasFocus)
        {
            if (pauseMenu != null)
            {
                Cursor.lockState = pauseMenu.isPaused ?
                    CursorLockMode.None :
                    CursorLockMode.Locked;

                Cursor.visible = pauseMenu.isPaused;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        else
        {
            if (isHoldingToPlace)
            {
                DestroyPreview();
                isHoldingToPlace = false;
                if (heldItem != null) heldItem.SetActive(true);
            }

            currentChargeTime = 0f;
            chargeMeterUI.SetActive(false);
            if (spinRoutine != null)
            {
                StopCoroutine(spinRoutine);
                spinRoutine = null;
                if (heldItem != null) heldItem.transform.rotation = Quaternion.identity;
            }
        }
    }

    private void OnDisable()
    {
        // Player somehow disabled. Let's throw him back into the lobby.
        //GameManager.Instance.LoadSceneStr("Main Menu");
    }

    private void OnSettingsChanged()
    {
        PlayerPrefs.Save(); // TODO: ... fucking do your pause shit. and more! that wind event tho gotta be purged or revamped.
        // Add any real-time settings updates here if needed
    }

    void Slip(float slipfactor = 1)
    {
        Ragdoll(slipfactor);
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

        // TODO: probably could use random?
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
        Quaternion targetRotation = Quaternion.identity;
        float rotationTime = 1f;
        float elapsedTime = 0f;

        while (elapsedTime < rotationTime)
        {
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, elapsedTime / rotationTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;

        canMove = true;
        yield break;
    }

    public void EndingSequence()
    {
        // whatever... it works? it works.
        footstepSource.PlayOneShot((AudioClip) GetComponent<SerializableDictionaryObjectContainer>().Fetch("boom"));
        if (pauseMenu.isPaused) HandlePauseToggle();
        // i regret nothing
    }

    public void Disconnect()
    {
        NetworkGameManager.singleton.StopHost();
    }

    public void Die()
    {
        if (NetworkGameManager.Instance) NetworkGameManager.singleton.StopHost();
        ((GameManager)GameManager.Instance).LoadSceneStr("Main Menu");
    }

    [Server]
    public void ServerDie()
    {
        if (!alive) return;
        alive = false;
        GameManager.Instance.ForceGameOver();   // your existing method
    }

    [Client]
    public void PlayerDie()
    {
        if (!alive) return;
        alive = false;
        GameManager.Instance.ForceGameOver();
    }

    public void OnShredderEnter()
    {
        if (isServer)
            ServerDie();
        else
            PlayerDie();
    }
}
// todo. players cant drag stuff that wasn't *spawned*. so i need to spawn those crates in as to allow them to be dragged, OR figure out a hacky way to allow that instead
// same with preview items . okay so simpler fix to two of these issues: make something that spawns these items. urgh this stinks
// also, teleport on the client side doesn't exactly work, as the server sometimes falls through and keeps falling. and that's despite the fact that server is not moving anywhere on their side of the game