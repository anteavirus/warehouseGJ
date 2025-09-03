using UnityEngine;

public class DeliveryArea : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<Item>(out var item))
        {
            bool fromShelf = item.fromShelf; // why did i do a double check??
            GameManager.Instance.ProcessDelivery(item, fromShelf);
            Destroy(other.gameObject);
        }
    }
}
