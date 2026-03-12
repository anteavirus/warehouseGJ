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
    internal int materialIndex;

    private void Start()
    {
        containmentProcedureClock = containmentProcedureMaxTime;
    }

    private void Update()
    {
        if (containmentProcedure == SpecialRequirement.None) return;
        containmentProcedureClock -= Time.deltaTime;
    }

    // EDITED: OnUse now properly handles network spawning for box and item
    public override void OnUse(GameObject user)
    {
        if (containedItem == null) return;
        
        // Only process on server
        if (!Mirror.NetworkServer.active)
        {
            // Client requests server to open box
            CmdOpenBox(user.GetComponent<Mirror.NetworkIdentity>()?.netId ?? 0);
            return;
        }
        
        OpenBoxServer(user);
    }
    
    [Mirror.Command(requiresAuthority = false)]
    private void CmdOpenBox(uint playerNetId)
    {
        if (!Mirror.NetworkServer.spawned.ContainsKey(playerNetId)) return;
        GameObject playerObj = Mirror.NetworkServer.spawned[playerNetId].gameObject;
        OpenBoxServer(playerObj);
    }
    
    [Mirror.Server]
    private void OpenBoxServer(GameObject user)
    {
        if (containedItem == null) return;
        
        base.OnUse(user);
        useSounds = null;
        OnDrop();

        var newbox = Instantiate(futureBoxPrefab);
        newbox.transform.position = transform.position;
        
        // EDITED: Ensure NetworkIdentity for new box
        if (newbox.GetComponent<Mirror.NetworkIdentity>() == null)
        {
            newbox.AddComponent<Mirror.NetworkIdentity>();
        }
        Mirror.NetworkServer.Spawn(newbox);
        
        var boxscript = newbox.GetComponent<Box>();
        boxscript.OnDrop();
        boxscript.containedItem = null;
        boxscript.canUseOnID = new int[] { -1};
        boxscript.useSounds = null;

        // should we pass on the containment procedure..? I mean... we probably should... but at the same time, like, cat's outta the box, no? Unless we want to seal something back in - IF - we want such a possibility. TODO: to be discussed! 

        var player = user.GetComponent<PlayerController>();
        if (player != null)
        {
            player.ForceDropItem();
        }

        var item = Instantiate(containedItem);
        item.name = containedItem.name;
        item.transform.position = transform.position + new Vector3(0,newbox.transform.localScale.y,0); // TODO: maybe it should spawn at the black part and not. somewhere
        
        // EDITED: Ensure NetworkIdentity for item
        if (item.GetComponent<Mirror.NetworkIdentity>() == null)
        {
            item.AddComponent<Mirror.NetworkIdentity>();
        }
        item.SetActive(true);
        Mirror.NetworkServer.Spawn(item);
        
        Item itemscript = item.GetComponent<Item>();
        itemscript.fromShelf = false;
        var itemRb = item.GetComponent<Rigidbody>();
        if (itemRb != null)
        {
            itemRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        containedItem = null;
        
        // EDITED: Only force pickup on server, let network sync handle it
        if (player != null && player.isServer)
        {
            player.ForcePickupItem(itemscript);
        }
        
        Mirror.NetworkServer.Destroy(this.gameObject);
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