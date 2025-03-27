using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class StorageArea : MonoBehaviour
{
    public int ID;
    Item containingObject;
    BoxCollider boxCollider;
    [SerializeField] Vector3 limitVelocity = new Vector3(0.3f, 0.3f, 0.3f);

    void Start()
    {
        boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;

        containingObject = GameManager.Instance.ReturnItemById(ID);
        containingObject.fromShelf = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;

        Vector3 velocity = rb.velocity;
        bool isVelocityUnderLimit =
            Mathf.Abs(velocity.x) < limitVelocity.x &&
            Mathf.Abs(velocity.y) < limitVelocity.y &&
            Mathf.Abs(velocity.z) < limitVelocity.z;

        if (isVelocityUnderLimit)
        {
            Item item = other.GetComponent<Item>();
            if (item != null && item.ID == ID)
            {
                if (GameManager.Instance != null && !item.fromShelf)
                {
                    GameManager.Instance.AddScore(item.scoreValue);
                    GameManager.Instance.setdownItem = true;
                }
                item.enabled = false;
                Destroy(other.gameObject);
            }
        }
    }

    public GameObject CreateNewItemForPickup()
    {
        var obj = containingObject.gameObject;
        var created = Instantiate(obj);
        created.SetActive(true);
        // do whatever else is needed to the created.
        return created;
    }
}
