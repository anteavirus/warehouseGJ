using UnityEngine;

public class Box : Item
{
    public enum SpecialRequirement
    {
        None = -1,
        Fragile,
        Flammable,
        Easysoaking,
        KeepRefrigerated,
        KeepItFuckingFried  // i have no other idea how to describe in a few words "keep an object in an area of extreme heat"
    }

    public GameObject containedItem;
    [SerializeField] GameObject futureBoxPrefab;
    public SpecialRequirement containmentProcedure;
    public bool containmentProcedureSuccessful = true;
    public float containmentProcedureMaxTime = 30f;
    public float containmentProcedureClock = 30f;

    private void Start()
    {
        containmentProcedureClock = containmentProcedureMaxTime;
    }

    private void Update()
    {
        if (containmentProcedure == SpecialRequirement.None) return;
        containmentProcedureClock -= Time.deltaTime;
    }

    public override void OnUse(GameObject user)
    {
        if (containedItem == null) return;
        base.OnUse(user);
        useSounds = null;
        OnDrop();

        var newbox = Instantiate(futureBoxPrefab);
        newbox.transform.position = transform.position;
        var boxscript = newbox.GetComponent<Box>();
        boxscript.OnDrop();
        boxscript.containedItem = null;
        boxscript.canUseOnID = new int[] { -1};
        boxscript.OnUse(user); // I wonder if race condition makes it sometimes play the sound, sometimes not.
        boxscript.useSounds = null;

        // should we pass on the containment procedure..? I mean... we probably should... but at the same time, like, cat's outta the box, no? Unless we want to seal something back in - IF - we want such a possibility. TODO: to be discussed! 

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
        if (containmentProcedure == SpecialRequirement.Fragile)
        {
            containmentProcedureSuccessful = false;
            // TODO: maybe borrow glass noises and play them too. to be discussed
        }
    }
}