using UnityEngine;

public class DeliveryArea : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<Item>(out var item))
        {
            bool fromShelf = false;
            if (item.TryGetComponent<Item>(out var itemScript))
            {
                fromShelf = itemScript.fromShelf;
            }

            GameManager.Instance.ProcessDelivery(item, fromShelf);
            Destroy(other.gameObject);
        }
    }
}
