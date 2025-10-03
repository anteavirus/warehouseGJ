using UnityEngine;

public class DeliveryArea : MonoBehaviour
{
    // TODO: move GameManager's Order system here. Or somewhere else, definitely. I mean, it is related to the game, but it's not the core...

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
