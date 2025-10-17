using UnityEngine;

public class Box : Item
{
    public GameObject containedItem;
    [SerializeField] GameObject openBox;

    public override void OnUse(GameObject user)
    {
        if (containedItem == null) return;
        base.OnUse(user);
        useSounds = null;
        OnDrop();

        var newbox = Instantiate(openBox);
        newbox.transform.position = transform.position;
        var boxscript = newbox.GetComponent<Box>();
        boxscript.OnDrop();
        boxscript.containedItem = null;
        boxscript.canUseOnID = new int[] { -1};
        boxscript.OnUse(user); // I wonder if race condition makes it sometimes play the sound, sometimes not.
        boxscript.useSounds = null;

        var player = user.GetComponent<PlayerController>();
        player.ForceDropItem();

        var item = Instantiate(containedItem);
        item.name = containedItem.name;
        item.transform.position = transform.position + new Vector3(0,newbox.transform.localScale.y,0); // TODO: maybe it should spawn at the black part and not. somewhere
        item.SetActive(true);
        Item itemscript = item.GetComponent<Item>();
        itemscript.fromShelf = false;
        item.GetComponent<Rigidbody>().collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        containedItem = null;
        player.ForcePickupItem(itemscript);
        Destroy(this.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collisionSounds != null && collisionSounds.Length > 0)
        {
            float impactVolume = Mathf.Clamp01(collision.relativeVelocity.magnitude * 0.1f);
            AudioClip clip = collisionSounds[Random.Range(0, collisionSounds.Length)];
            audioSource.PlayOneShot(clip, impactVolume);
        }
    }
}