using UnityEngine;

public class Box : Item
{
    public GameObject containedItem;

    public override void OnUse(GameObject user)
    {
        if (containedItem == null) return;
        base.OnUse(user);
        useSounds = null;
        OnDrop();
        user.GetComponent<PlayerController>().ForceDropItem();

        var item = Instantiate(containedItem);
        item.transform.position = transform.position;

        containedItem = null;
    }

    private void ToggleColliders(bool state)
    {
        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            col.enabled = state;
        }
    }

    // keep outdated just in case
    private void TogglePhysics(bool state)
    {
        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = !state;
            rb.detectCollisions = state;
            if (state)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            col.enabled = state;
        }
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