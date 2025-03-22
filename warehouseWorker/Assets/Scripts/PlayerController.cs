using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float airControlFactor = 0.5f;
    [SerializeField] private float groundDrag = 5f;
    [SerializeField] private float airDrag = 0.5f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float playerHeight = 2f;
    
    [Header("Camera Inversion Settings")]
    public float inversionProgress = 0f;
    public float inversionTransitionSpeed = 2f;

    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 100f;
    [SerializeField] private Transform playerCameraTransform;
    public Camera playerCamera;
    [SerializeField] private float maxLookAngle = 90f;

    [Header("Interaction Settings")]
    [SerializeField] private float pickupRange = 2f;
    public LayerMask interactableLayer;
    public Transform handTransform;
    public Transform pickUpHintUI;
    public Transform canUseOnItemHintUI;

    [Header("GPT Named me Combat, but actually i'm just Throwing")]
    [SerializeField] private float minThrowForce = 5f;
    [SerializeField] private float maxThrowForce = 25f;
    [SerializeField] private float maxChargeTime = 1.5f;

    [Header("Placement Settings")]
    [SerializeField] private Material previewMaterial;
    [SerializeField] private float placementMaxDistance = 5f;
    [SerializeField] private LayerMask placementLayer;
    [SerializeField] private Vector3 placementOffset = new Vector3(0, 0.1f, 0);
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private GameObject chargeMeterUI;
    [SerializeField] private Image chargeMeter;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioSource landingSource;
    [SerializeField] private float footstepInterval = 0.4f;
    [SerializeField] private List<SurfaceSound> surfaceSounds = new();
    [SerializeField] private AudioClip[] slipSounds;

    [Header("Hover Over Settings")]
    [SerializeField] private float checkRate = 0.2f;
    [SerializeField] private LayerMask hoverLayer;
    readonly string pickUpHint = "[E]";
    readonly string useHint = "[œ Ã]";

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
    private Vector3 lastMovementDirection;

    [Header("Pause UI Menu")]
    [SerializeField] private Animator pauseAnimator;
    [SerializeField] PauseMenuUI pauseMenu;
    [SerializeField] GameObject pauseUI;

    public bool alive = true;
    private Rigidbody heldItemRb;
    private Vector3 originalCameraLocalPosition;

    private Rigidbody rb;
    private float xRotation;
    private Vector3 moveDirection;
    private GameObject heldItem, previewObject;
    private Item currentInteractable;
    private Coroutine spinRoutine;

    private SurfaceType currentSurface;

    private bool isGrounded, isHoldingToPlace, isValidPlacement, wasGrounded, canMove = true;

    private float currentChargeTime, chargedThrowForce, currentRotationOffset, footstepTimer, lastTimeRagdoll;

    void Start()
    {
        if (pauseMenu == null) pauseMenu = pauseUI.GetComponent<PauseMenuUI>(); 
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        Cursor.lockState = CursorLockMode.Locked;

        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main;

        playerCameraTransform = playerCamera.transform;
        originalCameraLocalPosition = playerCameraTransform.localPosition;

        StartCoroutine(CheckForInteractables());
    }


    void Update()
    {
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
    }

    void FixedUpdate()
    {
        if (!alive) return;
        if (canMove) MovePlayer();
        HandleHeldItemPhysics();
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


    private void MovePlayer()
    {
        var input = new Vector2(
            (1 - 2 * inversionProgress) * Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        moveDirection = transform.forward * input.y + transform.right * input.x;

        float multiplier = isGrounded ? 1f : airControlFactor;
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
        isGrounded = Physics.Raycast(transform.position, Vector3.down,
            playerHeight * 0.5f + 0.2f, groundLayer);

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
        }
    }

    private void HandleCameraWobble()
    {
        if (isGrounded && moveDirection != Vector3.zero)
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

    private void UpdateDrag() => rb.drag = isGrounded ? groundDrag : airDrag;

    private void HandleFootsteps()
    {
        if (isGrounded)
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
        wasGrounded = isGrounded;
    }

    private void DetectSurface()
    {
        if (Physics.Raycast(transform.position, Vector3.down,
            out RaycastHit hit, playerHeight * 0.5f + 0.2f, groundLayer))
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
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);

        playerCameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 180 * inversionProgress);
        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleInteractions()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (heldItem == null) TryPickupItem();
            else if (!isHoldingToPlace) DropItem();
        }
    }

    private void HandleUseItem()
    {
        if (heldItem != null && Input.GetMouseButtonDown(1)) // Right click
        {
            heldItem.GetComponent<Item>().OnUse(gameObject);
        }
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
            if (hit.transform.TryGetComponent<Item>(out var item))
            {
                if (item.isPickupable)
                {
                    PickUpItem(item);
                }
            }
            else if (stupidShit != null)
            {
                if (stupidShit.isPickupable)
                {
                    PickUpItem(stupidShit);
                    return;
                }
            }


            if (hit.transform.TryGetComponent<StorageArea>(out var area))
            {
                var newItem = area.CreateNewItemForPickup();
                newItem.SetActive(true);
                Item itemScript = newItem.GetComponent<Item>();
                TryPickupItem(itemScript);
            }
        }
    }

    void PickUpItem(Item item)
    {
        if (item == null && item.isPickupable) return;

        heldItem = item.gameObject;
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
    }

    public void ForceDropItem()
    {
        DropItem();
    }

    private void DropItem()
    {
        if (heldItem == null) return;
        DisableSpinRoutineIfReal();

        Item item = heldItem.GetComponent<Item>();
        item.OnDrop();

        // Reset physics properties
        if (heldItemRb != null)
        {
            heldItemRb.useGravity = true;
            heldItemRb.drag = 0f;
            heldItemRb.angularDrag = 0.05f;
        }
        heldItem = null;
        heldItemRb = null;
        heldItem = null;
    }

    private void HandleThrow()
    {
        if (heldItem == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            currentChargeTime = 0f;
            DisableSpinRoutineIfReal();
            chargeMeterUI.SetActive(true);
            spinRoutine = StartCoroutine(SpinWhileCharging());
        }

        if (Input.GetMouseButton(0))
        {
            chargeMeter.fillAmount = Mathf.Clamp01((currentChargeTime += Time.deltaTime) / maxChargeTime);
            chargedThrowForce = Mathf.Lerp(minThrowForce, maxThrowForce, chargeMeter.fillAmount);
        }

        if (Input.GetMouseButtonUp(0) && spinRoutine != null)
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
        if (heldItem == null) return;

        Item item = heldItem.GetComponent<Item>();
        item.OnThrow(playerCameraTransform.forward, force);

        // Reset physics properties
        if (heldItemRb != null)
        {
            heldItemRb.useGravity = true;
            heldItemRb.drag = 0f;
            heldItemRb.angularDrag = 0.05f;
        }
        heldItem = null;
        heldItemRb = null;
        heldItem = null;
    }

    private void HandlePlacement()
    {
        HandlePlacementInput();
        HandlePlacementPreview();
    }

    private void HandlePlacementInput()
    {
        if (heldItem == null || !Application.isFocused) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            isHoldingToPlace = true;
            CreatePreview();
        }

        if (Input.GetKeyUp(KeyCode.E))
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

        if (Input.GetMouseButtonDown(0))
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

        RaycastHit? validHit = null;
        foreach (RaycastHit hit in hits)
        {
            if (!hit.collider.CompareTag("IgnoreRaycast"))
            {
                validHit = hit;
                break;
            }
        }

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

        Quaternion targetRot = Quaternion.Euler(0,
            Quaternion.LookRotation(
                Vector3.ProjectOnPlane(playerCameraTransform.forward, Vector3.up)
            ).eulerAngles.y + currentRotationOffset,
            0
        );

        previewObject.transform.SetPositionAndRotation(targetPos, targetRot);
        UpdatePreviewAppearance();
    }


    private void CreatePreview()
    {
        previewObject = Instantiate(heldItem);
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
        if (previewObject == null) return;
        Item item = heldItem.GetComponent<Item>();

        if (heldItemRb != null)
        {
            heldItemRb.velocity = Vector3.zero;
            heldItemRb.useGravity = true;
            heldItemRb.drag = 0f;
            heldItemRb.angularDrag = 0.05f;
        }

        item.OnPlace(previewObject.transform.position, previewObject.transform.rotation);
        heldItem = null;
        DisableSpinRoutineIfReal();
    }

    private float GetObjectBottomOffset(GameObject obj)
    {
        if (obj == null) return 0f;

        if (obj.TryGetComponent<Collider>(out var col)) return col.bounds.extents.y;
        // TODO: These two seem to return 0 most, if not all the time... may need to be fixed but. meh. below works as well
        if (obj.TryGetComponent<Renderer>(out var rend)) return rend.bounds.extents.y;

        return obj.transform.lossyScale.y / 2; // it's something... i guess.
    }

    private void DestroyPreview()
    {
        if (previewObject != null) Destroy(previewObject);
    }

    IEnumerator CheckForInteractables()
    {
        while (true)
        {
            bool hitItem = Physics.Raycast(
                playerCameraTransform.position,
                playerCameraTransform.forward,
                out RaycastHit hit,
                pickupRange,
                hoverLayer
            );

            Item newItem = null;
            if (hitItem) hit.collider.TryGetComponent(out newItem);

            bool showUseHint = heldItem != null &&
                              newItem != null &&
                              heldItem.GetComponent<Item>().CanBeUsedWith(newItem.GetComponent<Item>()); // fuck. Dirt.

            bool showPickupHint = heldItem == null &&
                                 newItem != null;

            if (currentInteractable != newItem)
            {
                if (currentInteractable != null)
                {
                    ShowUseHint(false);
                    ShowPickUpHint(false);
                }

                currentInteractable = newItem;

                if (currentInteractable != null)
                {
                    if (showUseHint) ShowUseHint(true);
                    else if (showPickupHint && newItem.isPickupable) ShowPickUpHint(true);
                }
            }

            yield return new WaitForSecondsRealtime(checkRate > 0 ? checkRate : 1);
        }
    }


    void ShowUseHint(bool saye)
    {
        if (saye)
        {
            if (canUseOnItemHintUI != null)
            {
                canUseOnItemHintUI.GetComponent<TextMeshProUGUI>().text = useHint;
                canUseOnItemHintUI.gameObject.SetActive(true);
            }
        }
        else
        {
            if (canUseOnItemHintUI != null) 
                canUseOnItemHintUI.gameObject.SetActive(false);
        }
    }

    void ShowPickUpHint(bool saye)
    {
        if (saye)
        {
            if (pickUpHintUI != null)
            {
                pickUpHintUI.GetComponent<TextMeshProUGUI>().text = pickUpHint;
                pickUpHintUI.gameObject.SetActive(true);
            }
        }
        else
        {
            if (pickUpHintUI != null) 
                pickUpHintUI.gameObject.SetActive(false);
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
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
        GameManager.Instance.LoadScene(0);
    }

    void Slip(float slipfactor = 1)
    {
        Ragdoll(slipfactor);
    }

    public void Ragdoll(float factor = 1)
    {
        if (lastTimeRagdoll > Time.time + slipCooldown) return;
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
    }

    public void EndingSequence()
    {
        // whatever... it works? it works.
        footstepSource.PlayOneShot((AudioClip) GetComponent<SerializableDictionaryObjectContainer>().Fetch("boom"));
        if (pauseMenu.isPaused) HandlePauseToggle();
        // i regret nothing
    }

    public void Die()
    {
        GameManager.Instance.LoadScene(0);
    }
}
