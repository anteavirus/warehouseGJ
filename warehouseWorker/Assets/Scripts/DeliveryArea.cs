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
    Item itemInsideMe;
    bool processingDelivery;
    bool doorsMoving;
    [SerializeField] GameObject door;
    [SerializeField] TextMeshProUGUI selectedRequestee;
    private bool playerAtDoor;
    private Coroutine autoCloseCoroutine;

    private bool isInitialized = false;

    private void Start()
    {
        StartCoroutine(Initialize());
    }

    IEnumerator Initialize()
    {
        // Wait for OrderManager to be available
        yield return StartCoroutine(FindOrderManagerSomeDay());

        // Now initialize everything that depends on orderManager
        selectionGameObjects = new GameObject[orderManager.queue.GetLength(0)];
        orderManager.deliveryArea = this;

        originalPosition = door.transform.position;

        isInitialized = true;
        StartCoroutine(MonitorForDelivery());
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
            yield return new WaitUntil(() => doorClosed && itemInsideMe != null && !processingDelivery);
            yield return StartCoroutine(ProcessDeliverySequence());
            yield return new WaitWhile(() => processingDelivery);
        }
    }

    IEnumerator ProcessDeliverySequence()
    {
        processingDelivery = true;

        // Attempt to process delivery
        if (itemInsideMe != null && GameManager.Instance != null)
        {
            bool deliverySuccessful = GameManager.Instance.ProcessDelivery(0, itemInsideMe, itemInsideMe.fromShelf);

            if (deliverySuccessful)
            {
                // Successful delivery - destroy item
                Destroy(itemInsideMe.gameObject);
                itemInsideMe = null;

                // Close door after successful delivery
                yield return StartCoroutine(CloseDoor());
            }
            else
            {
                // Failed delivery - just clear the item but don't close door
                // (player punishment - they need to manually clear the area)
                yield return StartCoroutine(CloseDoor());
            }
        }

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
            StartCoroutine(OpenDoor());
        }
    }

    IEnumerator OpenDoor()
    {
        if (doorsMoving) yield break;
        doorsMoving = true;

        yield return StartCoroutine(MoveDoorToPosition(door.transform, originalPosition + Vector3.up * 3f, 1f));

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

    IEnumerator MoveDoorToPosition(Transform doorTransform, Vector3 targetPosition, float duration)
    {
        Vector3 startPosition = doorTransform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            doorTransform.position = Vector3.Lerp(startPosition, targetPosition, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        doorTransform.position = targetPosition;
    }

    // Automatically close door after a delay when player leaves
    IEnumerator AutoCloseDoor(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Only close if player is still not at door and there's no item inside
        if (!playerAtDoor && itemInsideMe == null)
        {
            yield return StartCoroutine(CloseDoor());
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<Item>(out var item))
        {
            itemInsideMe = item;

            if (doorClosed && !doorsMoving)
            {
                StartCoroutine(OpenDoor());
            }
        }
        else if (other.CompareTag("Player"))
        {
            playerAtDoor = true;

            if (autoCloseCoroutine != null)
            {
                StopCoroutine(autoCloseCoroutine);
                autoCloseCoroutine = null;
            }

            if (doorClosed && !doorsMoving)
            {
                StartCoroutine(OpenDoor());
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<Item>(out var item) && item == itemInsideMe)
        {
            itemInsideMe = null;

            // Start auto-close timer when item leaves (unless player is still there)
            if (!playerAtDoor && !doorClosed && !doorsMoving)
            {
                autoCloseCoroutine = StartCoroutine(AutoCloseDoor(2f));
            }
        }
        else if (other.CompareTag("Player"))
        {
            playerAtDoor = false;

            // Start auto-close timer when player leaves (unless item is still there)
            if (itemInsideMe == null && !doorClosed && !doorsMoving)
            {
                autoCloseCoroutine = StartCoroutine(AutoCloseDoor(1f));
            }
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
    }
}