using UnityEngine;

public class MainMenuPlayerController : MonoBehaviour
{
    public bool STOPWORKINGIMINUI = true;
    [Header("Camera Settings")]
    [SerializeField] private Transform cameraPivot;

    [Header("Item Interaction")]
    [SerializeField] private float pickupRange = 5f;
    [SerializeField] private float throwForce = 10f;
    [SerializeField] private float scrollSensitivity = 2f;
    [SerializeField] private float followForce = 50f;
    [SerializeField] private float damping = 10f;
    [SerializeField] private float positionOffset = 0.1f;
    [SerializeField] private float repickCooldown = 0.5f;
    [SerializeField] private LayerMask interactableLayer;

    private Camera mainCamera;
    private Rigidbody heldItem;
    private float currentHoldDistance;
    private Vector3 targetItemPosition;
    private int originalItemLayer;
    private int ignoreRaycastLayer;

    // Cooldown system
    private Rigidbody lastReleasedItem;
    private float lastReleaseTime;

    void Start()
    {
        mainCamera = Camera.main;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
    }

    void Update()
    {
        if (STOPWORKINGIMINUI) return;

        HandleItemInteraction();
        HandleScrollWheel();
    }

    void FixedUpdate()
    {
        if (heldItem != null)
        {
            UpdateHeldItemPosition();
        }
    }

    void HandleItemInteraction()
    {
        if (Input.GetMouseButton(0))
        {
            if (heldItem == null) TryPickupItem();
            else UpdateTargetPosition();
        }
        else if (heldItem != null)
        {
            DropItem();
        }

        if (Input.GetMouseButtonDown(1) && heldItem != null)
        {
            ThrowItem();
        }
    }

    void TryPickupItem()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, interactableLayer))
        {
            var rb = hit.rigidbody;
            if (rb != null && !rb.isKinematic)
            {
                // Prevent immediate repick of thrown item
                if (IsItemOnCooldown(rb)) return;

                GrabItem(rb, hit.point);
            }
        }
    }

    void GrabItem(Rigidbody item, Vector3 hitPoint)
    {
        heldItem = item;
        originalItemLayer = item.gameObject.layer;
        item.gameObject.layer = ignoreRaycastLayer;
        item.useGravity = false;
        currentHoldDistance = Vector3.Distance(mainCamera.transform.position, hitPoint);
        lastReleasedItem = null; // Reset cooldown when grabbing new item
    }

    void UpdateTargetPosition()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Vector3 idealPosition = ray.GetPoint(currentHoldDistance);

        // Obstacle avoidance with sphere cast
        if (Physics.SphereCast(mainCamera.transform.position, 0.3f, ray.direction,
            out RaycastHit hit, currentHoldDistance, ~interactableLayer))
        {
            idealPosition = hit.point - ray.direction * positionOffset;
        }

        targetItemPosition = idealPosition;
    }

    void UpdateHeldItemPosition()
    {
        Vector3 positionDelta = targetItemPosition - heldItem.position;
        Vector3 force = positionDelta * followForce;
        force -= heldItem.velocity * damping;
        heldItem.AddForce(force);

        // Smooth rotation towards camera view
        heldItem.MoveRotation(Quaternion.Slerp(
            heldItem.rotation,
            Quaternion.LookRotation(mainCamera.transform.forward, Vector3.up),
            Time.fixedDeltaTime * 5f
        ));
    }

    void HandleScrollWheel()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        currentHoldDistance = Mathf.Clamp(
            currentHoldDistance + scroll * scrollSensitivity,
            1f,
            pickupRange
        );
    }

    void DropItem()
    {
        if (heldItem == null) return;

        ReleaseItem(heldItem);
        heldItem = null;
    }

    void ThrowItem()
    {
        if (heldItem == null) return;

        Vector3 throwDirection = mainCamera.transform.forward.normalized;
        ReleaseItem(heldItem);
        heldItem.AddForce(throwDirection * throwForce, ForceMode.Impulse);
        heldItem = null;
    }

    void ReleaseItem(Rigidbody item)
    {
        item.gameObject.layer = originalItemLayer;
        item.useGravity = true;
        lastReleasedItem = item;
        lastReleaseTime = Time.time;
    }

    bool IsItemOnCooldown(Rigidbody item)
    {
        return item == lastReleasedItem &&
               Time.time < lastReleaseTime + repickCooldown;
    }

    public void StopWorking(bool yeah)
    {
        STOPWORKINGIMINUI = yeah;
    }
}
