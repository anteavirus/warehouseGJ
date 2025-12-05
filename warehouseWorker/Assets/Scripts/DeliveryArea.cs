using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DeliveryArea : MonoBehaviour
{
    OrdersManager orderManager;
    Vector3 originalPosition;
    public GameObject[] selectionGameObjects;
    bool doorClosed = true;
    [SerializeField] DoorMover up, down;    // TODO: kick our 3d modeler to model the building proper now, this is shit and I want to rework the fuck out of this.
    public ArrowControllableInt arrowedInt = new();
    public ArrowShift left, right;
    Item itemInsideMe;
    bool processingDelivery;
    bool doorsMoving;
    [SerializeField] GameObject door;
    [SerializeField] TextMeshProUGUI selectedRequestee;

    private bool isInitialized = false;

    private void Start()
    {
        arrowedInt.left = left; arrowedInt.right = right;   
        StartCoroutine(Initialize());
    }

    IEnumerator Initialize()
    {
        // Wait for OrderManager to be available
        yield return StartCoroutine(FindOrderManagerSomeDay());

        // Now initialize everything that depends on orderManager
        selectionGameObjects = new GameObject[orderManager.queue.GetLength(0)];
        orderManager.deliveryArea = this;

        if (up != null) up.area = this;
        if (down != null) down.area = this;

        // Initialize ArrowControlledInt
        if (arrowedInt != null)
        {
            arrowedInt.size = orderManager.queue.GetLength(0);
            arrowedInt.OnSelectionChanged += HandleShiftSelection;
            arrowedInt.left.area = arrowedInt.right.area = arrowedInt;
        }

        originalPosition = door.transform.position;

        isInitialized = true;
        StartCoroutine(MonitorForDelivery());
    }

    private void OnDestroy()
    {
        // Clean up event subscription
        if (arrowedInt != null)
        {
            arrowedInt.OnSelectionChanged -= HandleShiftSelection;
        }
    }

    IEnumerator FindOrderManagerSomeDay()
    {
        while (orderManager == null)
        {
            yield return new WaitUntil(() => OrdersManager.Instance != null);
            orderManager = OrdersManager.Instance;
        }
    }

    IEnumerator MonitorForDelivery()
    {
        while (true)
        {
            //Debug.Log("Waiting for item.");
            yield return new WaitUntil(() => doorClosed && itemInsideMe != null && !processingDelivery);
            //Debug.Log("Item received, door closed");
            yield return StartCoroutine(ProcessDeliverySequence());
            //Debug.Log("waiting to stop processing the delivery");
            yield return new WaitWhile(() => processingDelivery);
        }
    }

    IEnumerator ProcessDeliverySequence()
    {
        processingDelivery = true;

        AttemptProcessDelivery();

        yield return new WaitForSeconds(0.5f);

        processingDelivery = false;
    }

    public void MoveDoors(bool shouldClose)
    {
        if (doorsMoving) return;

        if (shouldClose)
        {
            StartCoroutine(CloseDoor());
        }
        else
        {
            StartCoroutine(OpenDoors());
        }
    }

    IEnumerator OpenDoors()
    {
        if (doorsMoving) yield break;
        doorClosed = false;
        doorsMoving = true;

        yield return StartCoroutine(MoveDoorToPosition(door.transform, originalPosition + Vector3.up * 2f, 1f));

        doorClosed = false;
        doorsMoving = false;
    }

    IEnumerator CloseDoor()
    {
        if (doorsMoving) yield break;

        doorsMoving = true;

        yield return StartCoroutine(MoveDoorToPosition(door.transform, originalPosition, 1f));

        doorClosed = true;
        doorsMoving = false;
    }

    IEnumerator MoveDoorToPosition(Transform door, Vector3 targetPosition, float duration)
    {
        Vector3 startPosition = door.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            door.position = Vector3.Lerp(startPosition, targetPosition, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        door.position = targetPosition;
    }

    // This method handles the selection shift from ArrowControlledInt
    private void HandleShiftSelection(bool shiftLeft)
    {
        if (!isInitialized || orderManager == null || orderManager.queue == null) return;

        // Deactivate current selection
        if (arrowedInt.selection < selectionGameObjects.Length)
            selectionGameObjects[arrowedInt.selection]?.SetActive(false);

        // Selection is already updated in ArrowControlledInt, just update visuals
        if (arrowedInt.selection < selectionGameObjects.Length)
            selectionGameObjects[arrowedInt.selection]?.SetActive(true);

        if (selectedRequestee != null)
            selectedRequestee.text = (arrowedInt.selection + 1).ToString();
    }

    public void AttemptProcessDelivery()
    {
        if (!isInitialized) return;

        if (itemInsideMe != null && GameManager.Instance != null)
        {
            if (GameManager.Instance.ProcessDelivery(arrowedInt.selection, itemInsideMe, itemInsideMe.fromShelf))
            {
                Destroy(itemInsideMe.gameObject);
                itemInsideMe = null;
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<Item>(out var item))
        {
            itemInsideMe = item;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<Item>(out var item) && item == itemInsideMe)
        {
            itemInsideMe = null;
        }
    }

    internal void UpdateYoShit()
    {
        if (!isInitialized || selectionGameObjects == null) return;

        foreach (var item in selectionGameObjects)
        {
            if (item == null) continue;
            item.SetActive(false);
        }

        // Use arrowedInt.selection instead of local selection variable
        if (arrowedInt != null && arrowedInt.selection < selectionGameObjects.Length)
        {
            selectionGameObjects[arrowedInt.selection]?.SetActive(true);
        }
    }
}