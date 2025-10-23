using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class StorageArea : MonoBehaviour
{
    [SerializeField] private List<int> _allowedItemIDs = new();
    public List<int> allowedItemIDs => _allowedItemIDs;

    BoxCollider boxCollider;
    [SerializeField] Vector3 limitVelocity = new Vector3(0.3f, 0.3f, 0.3f);

    public Vector3 scaleOffset;

    void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            boxCollider.isTrigger = true;
        }
    }

    // This looks depressingly empty now. Anyway, now I need to make this to check if whatever with order comes in and has deposit type mission, and flip completed to ture
    private void OnTriggerStay(Collider other)
    {   
        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) 
            return;

        if (!other.TryGetComponent<Box>(out var item)) 
            return;   

        Vector3 velocity = rb.velocity;
        bool isVelocityUnderLimit =
            Mathf.Abs(velocity.x) < limitVelocity.x &&
            Mathf.Abs(velocity.y) < limitVelocity.y &&
            Mathf.Abs(velocity.z) < limitVelocity.z;

        if (isVelocityUnderLimit)
        {
            if (item.isActiveAndEnabled)  // TODO: fuckin. make boxes. shelves dont assign some item. we save boxes and whatever they contain.
            {
                if (GameManager.Instance != null && !item.fromShelf && item.order.orderType == OrdersManager.OrderType.Deposit)
                {
                    item.order.orderFulfilled = true;
                    GameManager.Instance.setdownItem = true; // is this necessary anymore? whatever it'll stay until we get rid of this
                }
            }
        }
    }
}
