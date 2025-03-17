using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class StorageArea : MonoBehaviour
{
    public int ID;
    BoxCollider boxCollider;
    [SerializeField] Vector3 limitVelocity = new Vector3(0.3f, 0.3f, 0.3f);

    void Start()
    {
        boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;
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
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.AddScore(item.scoreValue);
                    GameManager.Instance.setdownItem = true;
                }
                item.enabled = false;
                Destroy(other.gameObject);
            }
        }
    }
}
