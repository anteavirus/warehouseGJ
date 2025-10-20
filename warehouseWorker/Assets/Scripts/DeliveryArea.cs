using System.Collections;
using TMPro;
using UnityEngine;

public class DeliveryArea : MonoBehaviour
{
    OrdersManager orderManager;
    Vector3 originalPosition;
    int selection;
    bool doorClosed = true;
    [SerializeField] DoorMover up, down;
    [SerializeField] ArrowShift left, right;
    Item itemInsideMe;
    bool processingDelivery;
    bool doorsMoving;
    [SerializeField] GameObject door;
    [SerializeField] TextMeshProUGUI selectedRequestee;

    private void Start()
    {
        orderManager = OrdersManager.Instance;
        if (orderManager == null)
        {
            StartCoroutine(FindOrderManagerSomeDay());
        }

        // Assign this area to the door controllers
        if (up != null) up.area = this;
        if (down != null) down.area = this;
        if (left != null) left.area = this;
        if (right != null) right.area = this;

        originalPosition = door.transform.position;
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
            Debug.Log("Waiting for item.");
            yield return new WaitUntil(() => doorClosed && itemInsideMe != null && !processingDelivery);
            Debug.Log("Item received, door closed");
            yield return StartCoroutine(ProcessDeliverySequence());
            Debug.Log("waiting to stop processing the delivery");
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

    public void ShiftSelection(bool shiftLeft)
    {
        if (orderManager == null || orderManager.queue == null) return;

        int queueLength = orderManager.queue.GetLength(0);
        selection = (selection + (shiftLeft ? -1 : 1) + queueLength) % queueLength;

        if (selectedRequestee != null)
            selectedRequestee.text = (selection + 1).ToString();
    }

    public void AttemptProcessDelivery()
    {
        if (itemInsideMe != null && GameManager.Instance != null)
        {
            if (GameManager.Instance.ProcessDelivery(selection, itemInsideMe, itemInsideMe.fromShelf))
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
}