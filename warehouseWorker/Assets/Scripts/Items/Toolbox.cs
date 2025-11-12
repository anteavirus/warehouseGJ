using System.Collections;
using UnityEngine;

public class Toolbox : Item
{
    // TODO: finalize? this looks kinda boilerplatey
    int uses = 3;
    [SerializeField] AudioClip patchUpSFX;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.TryGetComponent<ElectronicItem>(out var item) && item.isBroken && uses > 0)
        {
            --uses;
            item.isBroken = false;
            audioSource.PlayOneShot(patchUpSFX);
        }
    }

    public override void OnUse(GameObject user)
    {
        base.OnUse(user);
        
        // Throw raycast probably, and basically the above.
        // user.GetComponent<PlayerController>()
    }
}
