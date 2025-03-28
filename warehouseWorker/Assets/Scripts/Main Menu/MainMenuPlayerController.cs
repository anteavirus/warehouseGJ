using System.Collections;
using UnityEngine;
using TMPro;
using static SettingsManager;

public class MainMenuPlayerController : MonoBehaviour
{
    public bool STOPWORKINGIMINUI = true;

    [Header("Settings Reference")]
    [SerializeField] private SettingsManager settingsManager;

    [Header("Held Item Physics")]
    [SerializeField] private float followForce = 100f;
    [SerializeField] private float damping = 5f;
    [SerializeField] private float angularDamping = 5f;
    [SerializeField] private float pickupRange = 5f;
    [SerializeField] private float throwForce = 5f;
    [SerializeField] private float maxHoldDistance = 1.5f;
    [SerializeField] private Transform handTransform;
    [SerializeField] private LayerMask interactableLayer;

    [Header("UI References")]
    [SerializeField] private Transform pickUpHintUI;
    [SerializeField] private Transform throwHintUI;

    private Camera mainCamera;
    private Rigidbody heldItemRb;
    private int originalItemLayer;
    private int ignoreRaycastLayer;
    private string pickUpHint = "E";
    private string throwHint = "œ Ã";
    private Item currentInteractable;

    void Start()
    {
        mainCamera = Camera.main;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");

        if (settingsManager == null)
            settingsManager = FindObjectOfType<SettingsManager>();

        StartCoroutine(CheckLater());
        StartCoroutine(CheckForInteractables());
    }

    IEnumerator CheckLater()
    {
        yield return new WaitForSecondsRealtime(0.1f);
        settingsManager.LoadSettings();
        KeyBind pickupBind = settingsManager.keyBinds.Find(b => b.actionName == "Pickup");
        KeyBind throwBind = settingsManager.keyBinds.Find(b => b.actionName == "Throw");

        pickUpHint = (pickupBind.currentKey != KeyCode.None ?
            pickupBind.currentKey : pickupBind.defaultKey).ToString();
        throwHint = (throwBind.currentKey != KeyCode.None ?
            throwBind.currentKey : throwBind.defaultKey).ToString();
    }

    void Update()
    {
        if (STOPWORKINGIMINUI) return;

        if (settingsManager.GetActionDown("Pickup"))
        {
            if (heldItemRb == null) TryPickupItem();
            else DropItem();
        }

        if (settingsManager.GetActionDown("Throw") && heldItemRb != null)
        {
            ThrowItem();
        }
    }

    void FixedUpdate()
    {
        if (heldItemRb != null)
        {
            HandleHeldItemPhysics();
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
                GrabItem(rb);
            }
        }
    }

    void GrabItem(Rigidbody item)
    {
        item.GetComponent<Item>().OnPickup(handTransform);
        heldItemRb = item;
        originalItemLayer = item.gameObject.layer;
        item.gameObject.layer = ignoreRaycastLayer;

        item.useGravity = false;
        item.drag = 5f;
        item.angularDrag = 5f;

        ClearAllHints();
    }

    void HandleHeldItemPhysics()
    {
        handTransform.GetPositionAndRotation(out Vector3 targetPos, out Quaternion targetRot);

        Vector3 positionDelta = targetPos - heldItemRb.position;
        Vector3 force = positionDelta.normalized * (followForce * positionDelta.magnitude);
        force -= heldItemRb.velocity * damping;
        heldItemRb.AddForce(force);

        Quaternion rotDelta = targetRot * Quaternion.Inverse(heldItemRb.rotation);
        rotDelta.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;

        Vector3 torque = (0.5f * angle * axis) - (heldItemRb.angularVelocity * angularDamping);
        heldItemRb.AddTorque(torque, ForceMode.Acceleration);

        if (positionDelta.magnitude > maxHoldDistance)
        {
            heldItemRb.MovePosition(targetPos);
            heldItemRb.velocity = Vector3.zero;
            heldItemRb.angularVelocity = Vector3.zero;
        }
    }

    void ThrowItem()
    {
        Vector3 throwDir = mainCamera.transform.forward.normalized;
        heldItemRb.GetComponent<Item>().OnThrow(throwDir, throwForce);
        ReleaseItem(heldItemRb);
        heldItemRb = null;
    }

    void DropItem()
    {
        heldItemRb.GetComponent<Item>().OnDrop();
        ReleaseItem(heldItemRb);
        heldItemRb = null;
    }

    void ReleaseItem(Rigidbody item)
    {
        item.gameObject.layer = originalItemLayer;
        item.useGravity = true;
        item.drag = 0f;
        item.angularDrag = 0.05f;
        ClearAllHints();
    }

    IEnumerator CheckForInteractables()
    {
        var wait = new WaitForSecondsRealtime(0.1f);
        while (true)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            bool hitInteractable = Physics.Raycast(
                ray,
                out RaycastHit hit,
                pickupRange,
                interactableLayer
            );

            Item detectedItem = null;
            if (hitInteractable) hit.collider.TryGetComponent(out detectedItem);

            if (currentInteractable != detectedItem)
            {
                currentInteractable = detectedItem;
                ClearAllHints();
            }

            if (hitInteractable)
            {
                if (heldItemRb == null && detectedItem != null && detectedItem.isPickupable)
                {
                    ShowPickUpHint(true);
                }
                else if (heldItemRb != null)
                {
                    ShowThrowHint(true);
                }
            }

            yield return wait;
        }
    }

    void ClearAllHints()
    {
        ShowPickUpHint(false);
        ShowThrowHint(false);
    }

    void ShowPickUpHint(bool show)
    {
        if (pickUpHintUI == null) return;
        pickUpHintUI.GetComponent<TextMeshProUGUI>().text = $"{pickUpHint}";
        pickUpHintUI.gameObject.SetActive(show);
    }

    void ShowThrowHint(bool show)
    {
        if (throwHintUI == null) return;
        throwHintUI.GetComponent<TextMeshProUGUI>().text = $"{throwHint}";
        throwHintUI.gameObject.SetActive(show);
    }

    // Rest of your existing methods...

    public void MoveToPosition(Transform target, System.Action onComplete = null)
    {
        try
        {
            StartCoroutine(SmoothMoveTo(target, onComplete));
        }
        catch
        {
            Debug.Log("Movement error handled");
        }
    }

    private IEnumerator SmoothMoveTo(Transform target, System.Action onComplete)
    {
        float duration = 0.5f;
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        while (elapsed < duration)
        {
            transform.SetPositionAndRotation(
                Vector3.Lerp(startPos, target.position, elapsed / duration),
                Quaternion.Slerp(startRot, target.rotation, elapsed / duration)
            );
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.SetPositionAndRotation(target.position, target.rotation);
        onComplete?.Invoke();
    }

    public void StopWorking(bool yeah)
    {
        STOPWORKINGIMINUI = yeah;
    }
}
