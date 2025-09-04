using UnityEngine;
using System.Linq;
using UnityEngine.Audio;

[RequireComponent(typeof(Rigidbody))]
public class Item : MonoBehaviour
{
    [SerializeField] private DeliveryAudioConfig _audioConfig;
    public DeliveryAudioConfig AudioConfig => _audioConfig;

    public int ID;
    public int scoreValue;
    public bool fromShelf;
    public int[] canUseOnID;

    [Header("Base Item Settings")]
    public bool isPickupable = true;
    public bool createdOnShelf = false;

    [Header("Audio Settings")]
    public AudioClip[] pickupSounds;
    public AudioClip[] useSounds;
    public AudioClip[] collisionSounds;
    public AudioMixerGroup mixerGroup;

    [Header("Settings that exist")]
    [SerializeField] float throwMultiplier = 0.1f;

    [HideInInspector] public PlayerController controller;
    protected AudioSource audioSource;

    protected virtual void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (!audioSource)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.spatialBlend = 1;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.maxDistance = 30;
    }

    private void Start()
    {
        audioSource.outputAudioMixerGroup = mixerGroup;
    }

    public virtual void OnPickup(Transform holder)
    {
        if (pickupSounds != null && pickupSounds.Length > 0)
            audioSource.PlayOneShot(pickupSounds[Random.Range(0, pickupSounds.Length)]);

        ToggleColliders(false);
    }

    public virtual void OnDrop()
    {
        ToggleColliders(true);
    }

    private void ToggleColliders(bool state)
    {
        TogglePhysics(state);

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
            if (state)
            {
                rb.useGravity = state;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }


    public virtual void OnUse(GameObject user)
    {
        if (user.TryGetComponent<PlayerController>(out var plr))
        {
            controller = plr;
        }

        if (useSounds.Length > 0 && useSounds.All(i=>i!=null))
        {
            AudioClip clip = useSounds[Random.Range(0, useSounds.Length)];
            audioSource.PlayOneShot(clip);
        }

        Debug.Log($"{gameObject.name} used by {user.name}");
    }

    public virtual void OnThrow(Vector3 direction, float force)
    {
        OnDrop();
        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.AddForce(direction * force, ForceMode.Impulse);

            Vector3 torque = force * throwMultiplier * Vector3.Cross(direction, Vector3.up);

            Vector3 randomSpin = new Vector3(
                Random.Range(-0.5f, 0.5f),
                Random.Range(-0.5f, 0.5f),
                Random.Range(-0.5f, 0.5f)
            );

            rb.AddTorque((torque + randomSpin * force) / rb.mass, ForceMode.Impulse);
        }
    }

    public virtual void OnPlace(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
        OnDrop();
    }

    public bool CanBeUsedWith(int ID)
    {
        foreach (int i in canUseOnID)
        {
            if (ID == i)
                return true;
        }
        return false;
    }

    public bool CanBeUsedWith(Item item)
    {
        return CanBeUsedWith(item.ID);
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