using System.Collections;
using System.Collections.Generic;
using TMPro;
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
    [SerializeField] bool reverseMovement;

    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 100f;
    [SerializeField] private Transform playerCameraTransform;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float maxLookAngle = 90f;

    [Header("Interaction Settings")]
    [SerializeField] private float pickupRange = 2f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] public Transform handTransform;
    [SerializeField] public Transform interactionHintUI;

    [Header("Combat Settings")]
    [SerializeField] private float parryWindow = 0.5f;
    [SerializeField] private GameObject parryMiniGameUI;
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

    [Header("Hover Over Settings")]
    [SerializeField] private float checkRate = 0.2f;
    [SerializeField] private LayerMask hoverLayer;
    readonly string pickUpHint = "[E]";

    [Header("Held Item Physics")]
    [SerializeField] private float followForce = 100f;
    [SerializeField] private float maxHoldDistance = 1.5f;
    [SerializeField] private float damping = 5f;
    [SerializeField] private float angularDamping = 5f;

    [Header("Camera Wobble")]
    [SerializeField] private float wobbleAmount = 0.05f;
    [SerializeField] private float wobbleFrequency = 4f;
    [SerializeField] private float wobbleResetSpeed = 5f;

    private Rigidbody heldItemRb;
    private Vector3 originalCameraLocalPosition;

    private Rigidbody rb;
    private float xRotation;
    private Vector3 moveDirection;
    private GameObject heldItem, previewObject;
    private Item currentInteractable;
    private Coroutine spinRoutine;

    private SurfaceType currentSurface;

    private bool isGrounded, isParrying, canParry, isHoldingToPlace, isValidPlacement, wasGrounded;

    private float currentChargeTime, chargedThrowForce, currentRotationOffset, footstepTimer;

    void Start()
    {
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
        if (!Application.isFocused) return;

        HandleLook();
        HandleJump();
        UpdateDrag();
        HandlePlacement();
        HandleUseItem();
        HandleThrow();
        HandleParry();
        HandleInteractions();
        HandleFootsteps();
        HandleCameraWobble();
    }

    void FixedUpdate()
    {
        MovePlayer();
        HandleHeldItemPhysics(); // Add physics handling for held item
    }

    private void MovePlayer()
    {
        var input = new Vector2((reverseMovement ? -1 : 1) * Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
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

        Vector3 targetPosition = handTransform.position;
        Quaternion targetRotation = handTransform.rotation;

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
                    PlayFootstepSound();
                    footstepTimer = footstepInterval;
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

        playerCameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, reverseMovement ? 180f : 0);
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

    private void TryPickupItem()
    {
        if (Physics.Raycast(playerCameraTransform.position, playerCameraTransform.forward,
            out RaycastHit hit, pickupRange, interactableLayer) && hit.collider.CompareTag("Item"))
        {
            if (hit.transform.TryGetComponent<Item>(out var item))
            {
                if (item.isPickupable)
                {
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
            }
        }
    }

    public void ForceDropItem()
    {
        DropItem();
    }

    private void DropItem()
    {
        if (heldItem == null) return;

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
    }

    private void HandleParry()
    {
        if (Input.GetKeyDown(KeyCode.F) && canParry)
            StartCoroutine(ParryAction());
    }

    private IEnumerator ParryAction()
    {
        isParrying = true;
        parryMiniGameUI.SetActive(true);
        yield return new WaitForSeconds(parryWindow);
        isParrying = false;
        parryMiniGameUI.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        Item item = other.GetComponent<Item>();
        if (item != null && rb != null && rb.velocity.magnitude >= 0.5f)
        {
            StartCoroutine(EnableParryWindow());
        }
    }

    private IEnumerator EnableParryWindow()
    {
        canParry = true;
        yield return new WaitForSeconds(1f);
        canParry = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isParrying)
        {
            Item item = collision.gameObject.GetComponent<Item>();
            if (item != null)
            {
                Rigidbody rb = collision.gameObject.GetComponent<Rigidbody>();
                if (rb != null && rb.velocity.magnitude >= 0.5f)
                {
                    ParryProjectile(collision.gameObject);
                }
            }
        }
    }

    private void ParryProjectile(GameObject projectile)
    {
        if (projectile.TryGetComponent<Item>(out var item))
        {
            item.OnParry();
        }

        if (projectile.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.velocity *= -3;
        }
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
        isValidPlacement = Physics.Raycast(playerCameraTransform.position, playerCameraTransform.forward,
            out RaycastHit hit, placementMaxDistance, placementLayer);

        // TODO: issue if placing upwards. i'm thinking of just disallowing that lol
        float offest = GetObjectBottomOffset(previewObject);
        Vector3 targetPos = isValidPlacement ?
            hit.point + placementOffset + Vector3.up * offest :
            playerCameraTransform.position + playerCameraTransform.forward * placementMaxDistance;

        Quaternion targetRot = Quaternion.Euler(0,
            Quaternion.LookRotation(Vector3.ProjectOnPlane(playerCameraTransform.forward, Vector3.up)).eulerAngles.y
            + currentRotationOffset, 0);

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
        heldItemRb.velocity = Vector3.zero;
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
            bool hitInteractable = Physics.Raycast(playerCameraTransform.position, playerCameraTransform.forward,
                out RaycastHit hit, pickupRange, hoverLayer);

            Item newInteractable = null;
            if (hitInteractable) hit.collider.TryGetComponent(out newInteractable);

            if (newInteractable != currentInteractable)
            {
                if (currentInteractable != null)
                {
                    HideHint();
                }

                currentInteractable = newInteractable;

                if (currentInteractable != null)
                {
                    ShowHint(pickUpHint);
                }
            }

            yield return new WaitForSecondsRealtime(checkRate);
        }
    }

    void ShowHint(string text)
    {
        if (interactionHintUI != null)
        {
            interactionHintUI.GetComponent<TextMeshProUGUI>().text = text;
            interactionHintUI.gameObject.SetActive(true);
        }
    }

    void HideHint() {
        if (interactionHintUI != null) interactionHintUI.gameObject.SetActive(false);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
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

            isParrying = false;
            parryMiniGameUI.SetActive(false);
            canParry = false;
        }
    }
}
