using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PunchCard : Item
{
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
    }
}