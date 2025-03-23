using UnityEngine;

public class ThrowableWeapon : Item
{
    public float throwForce = 20f;
    public GameObject projectilePrefab;

    public override void OnUse(GameObject user)
    {
        base.OnUse(user);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collisionSounds != null && collisionSounds.Length > 0)
        {
            float impactVolume = Mathf.Clamp01(collision.relativeVelocity.magnitude * 0.1f);
            AudioClip clip = collisionSounds[Random.Range(0, collisionSounds.Length)];
            audioSource.PlayOneShot(clip, impactVolume);
        }

        var rb = GetComponent<Rigidbody>();
        if (collision.gameObject.TryGetComponent<ZombieAI>(out var zombieAI) && 
            rb.velocity.magnitude > 0.4f) // its no longer a landmine now xd
        {
            zombieAI.Die();
            Destroy(gameObject);
        }
    }
}
