using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PunchCard : Item
{
    float interactionRange = 3;
    public override void OnUse(GameObject user)
    {
        base.OnUse(user);
        if (!user.TryGetComponent<PlayerController>(out var player)) return;

        if (Physics.Raycast(player.playerCamera.transform.position,
                  player.playerCamera.transform.forward,
                  out RaycastHit hit,
                  Mathf.Clamp(interactionRange, 0.1f, 50f),
                  player.interactableLayer))
        {
            if (hit.transform.TryGetComponent<PunchClock>(out var clock))
            {
                clock.OnUse(gameObject);
            }
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