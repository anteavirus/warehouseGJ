using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DeliveryArea : NetworkBehaviour
{
    OrdersManager orderManager;
    Vector3 originalPosition;
    public GameObject[] selectionGameObjects;
    [SyncVar(hook = nameof(OnDoorStateChanged))]
    private bool doorClosed = true;
    Item itemInsideMe;
    bool processingDelivery;
    bool doorsMoving;
    [SerializeField] GameObject door;
    private bool playerAtDoor;
    private Coroutine autoCloseCoroutine;

    private Vector3 closedLocalPosition;
    private Vector3 openLocalPosition;
    private Coroutine currentMoveCoroutine;

    private bool isInitialized = false;
    
    // EDITED: Added table index to identify which delivery area this is
    [SerializeField] private int tableIndex = 0;

    private void Start()
    {
        StartCoroutine(Initialize());
    }

    IEnumerator Initialize()
    {
        // Wait for OrderManager to be available
        yield return StartCoroutine(FindOrderManagerSomeDay());

        closedLocalPosition = door.transform.localPosition;
        openLocalPosition = closedLocalPosition + Vector3.up * 3f;

        //// Now initialize everything that depends on orderManager
        //selectionGameObjects = new GameObject[orderManager.queue.GetLength(0)];

        //// EDITED: Find which table index this delivery area corresponds to
        //if (orderManager.doors != null && orderManager.doors.Length > 0)
        //{
        //    for (int i = 0; i < orderManager.doors.Length; i++)
        //    {
        //        if (orderManager.doors[i] == this)
        //        {
        //            tableIndex = i;
        //            break;
        //        }
        //    }
        //}


        // Only set deliveryArea if it's not already set (first one found)
        if (orderManager.deliveryArea == null)
        {
            orderManager.deliveryArea = this;
        }

        originalPosition = door.transform.position;

        isInitialized = true;
        StartCoroutine(MonitorForDelivery());
    }

    private void OnDoorStateChanged(bool _, bool newState)
    {
        // Start the appropriate coroutine on all clients
        if (newState) StartCoroutine(CloseDoor());
        else StartCoroutine(OpenDoor());
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
            yield return new WaitUntil(() => itemInsideMe != null && !processingDelivery);
            yield return StartCoroutine(ProcessDeliverySequence());
        }
    }

    // EDITED: ProcessDeliverySequence now uses correct table index and handles network properly
    IEnumerator ProcessDeliverySequence()
    {
        processingDelivery = true;

        // Attempt to process delivery
        if (itemInsideMe != null && GameManager.Instance != null && OrdersManager.Instance != null)
        {
            // EDITED: Use tableIndex instead of hardcoded 0
            bool deliverySuccessful = GameManager.Instance.ProcessDelivery(tableIndex, itemInsideMe, itemInsideMe.fromShelf);

            if (deliverySuccessful)
            {
                // Successful delivery - destroy item on network
                if (Mirror.NetworkServer.active && itemInsideMe.GetComponent<Mirror.NetworkIdentity>() != null)
                {
                    Mirror.NetworkServer.Destroy(itemInsideMe.gameObject);
                }
                else
                {
                    Destroy(itemInsideMe.gameObject);
                }
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
        if (!isServer) return;
        if (doorsMoving) return;
        doorClosed = shouldClose;   // triggers the hook on all clients
    }

    IEnumerator OpenDoor()
    {
        if (doorsMoving) yield break;
        if (currentMoveCoroutine != null) StopCoroutine(currentMoveCoroutine);
        currentMoveCoroutine = StartCoroutine(MoveDoorToLocalPosition(openLocalPosition, 1f));
    }

    IEnumerator CloseDoor()
    {
        if (doorsMoving) yield break;
        if (currentMoveCoroutine != null) StopCoroutine(currentMoveCoroutine);
        currentMoveCoroutine = StartCoroutine(MoveDoorToLocalPosition(closedLocalPosition, 1f));
    }

    IEnumerator MoveDoorToLocalPosition(Vector3 targetLocal, float duration)
    {
        doorsMoving = true;
        Vector3 startLocal = door.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            door.transform.localPosition = Vector3.Lerp(startLocal, targetLocal, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        door.transform.localPosition = targetLocal;
        doorsMoving = false;
        currentMoveCoroutine = null;
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

    //internal void UpdateYoShit()
    //{
    //    if (!isInitialized || selectionGameObjects == null) return;

    //    foreach (var item in selectionGameObjects)
    //    {
    //        if (item == null) continue;
    //        item.SetActive(false);
    //    }
    //}
}