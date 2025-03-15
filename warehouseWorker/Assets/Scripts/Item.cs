using UnityEngine;
using System.Linq;

public class Item : MonoBehaviour
{
    [Header("Base Item Settings")]
    public bool isPickupable = true;

    [Header("Audio Settings")]
    public AudioClip[] pickupSounds;
    public AudioClip[] useSounds;
    public AudioClip[] collisionSounds;

    [HideInInspector] public PlayerController controller;
    protected AudioSource audioSource;

    protected virtual void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();
    }

    public virtual void OnPickup(Transform holder)
    {
        if (pickupSounds != null && pickupSounds.Length > 0)
            audioSource.PlayOneShot(pickupSounds[Random.Range(0, pickupSounds.Length)]);

        transform.SetParent(holder);
        transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        TogglePhysics(false);
    }

    public virtual void OnDrop()
    {
        transform.SetParent(null);
        TogglePhysics(true);
    }

    private void TogglePhysics(bool state)
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
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


    public virtual void OnUse(GameObject user)
    {
        if (useSounds.All(i=>i!=null)) audioSource.PlayOneShot(useSounds[Random.Range(0,useSounds.Length)]);
        Debug.Log($"{gameObject.name} used by {user.name}");
    }

    public virtual void OnThrow(Vector3 direction, float force)
    {
        OnDrop();
        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.AddForce(direction * force, ForceMode.Impulse);
        }
    }

    public virtual void OnPlace(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
        OnDrop();
    }
}