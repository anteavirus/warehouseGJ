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

        if (collision.gameObject.TryGetComponent<ZombieAI>(out var zombieAI))
        {
            zombieAI.Die();
            Destroy(gameObject);
        }
    }
}
