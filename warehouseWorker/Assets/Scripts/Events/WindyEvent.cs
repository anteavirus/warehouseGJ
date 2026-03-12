using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class WindyEvent : Event
{
    public List<ParticleSystem> leavesParticleSystems = new List<ParticleSystem>();
    public string leavesParentName = "LeavesParent";
    public Vector3 windDirection = Vector3.forward;
    public float windStrength = 5f;
    public float swayFrequency = 1f;
    public float swayAmplitude = 0.5f;

    public AudioClip sfx;
    private Blank slave;

    public override void StartEvent()
    {
        base.StartEvent();
        slave = new GameObject("WindController").AddComponent<Blank>();

        PlayAllLeafParticleSystems();

        if (!TryGetComponent<AudioSource>(out var audioSource))
        {
            audioSource = slave.gameObject.AddComponent<AudioSource>();
            audioSource.outputAudioMixerGroup = ((GameManager)GameManager.Instance).sfx;
        }
        audioSource.clip = sfx;
        audioSource.loop = true;
        audioSource.maxDistance = 30;
        audioSource.Play();
    }

    private void PlayAllLeafParticleSystems()
    {
        if (leavesParticleSystems.Count == 0 || leavesParticleSystems[0] == null)
        {
            FindAndPopulateLeafParticleSystems();
        }

        foreach (var ps in leavesParticleSystems)
        {
            if (ps != null && ShouldPlayParticleSystem(ps))
            {
                ps.startDelay = Random.Range(0, 3);
                ps.Play();
            }
        }
    }

    private bool ShouldPlayParticleSystem(ParticleSystem ps)
    {
        // Check if the particle system has an Item component
        if (!ps.TryGetComponent<Item>(out var itemComponent))
        {
            // If no Item component, check parent objects
            itemComponent = ps.GetComponentInParent<Item>();
        }

        // If there's an Item component, check if it's in GameManager.itemTemplates
        if (itemComponent != null)
        {
            return ((GameManager)GameManager.Instance).items.Any(i => i.TryGetComponent<Item>(out var item) && item.ID == itemComponent.ID);
        }

        // If no Item component found, allow playing by default
        return true;
    }

    private void FindAndPopulateLeafParticleSystems()
    {
        leavesParticleSystems.Clear();

        GameObject leavesParent = GameObject.Find(leavesParentName);
        if (leavesParent != null)
        {
            // Get all particle systems in children of the parent
            ParticleSystem[] childParticleSystems = leavesParent.GetComponentsInChildren<ParticleSystem>();
            leavesParticleSystems.AddRange(childParticleSystems);

            if (leavesParticleSystems.Count > 0)
            {
                Debug.Log($"Found {leavesParticleSystems.Count} leaf particle systems under {leavesParentName}");
                return;
            }
        }

        ParticleSystem[] allParticleSystems = FindObjectsOfType<ParticleSystem>();
        foreach (var ps in allParticleSystems)
        {
            leavesParticleSystems.Add(ps);
        }

        if (leavesParticleSystems.Count > 0)
        {
            Debug.Log($"Found {leavesParticleSystems.Count} leaf particle systems by name search");
        }
        else
        {
            Debug.LogWarning("No leaf particle systems found! Wind effect will be missing leaves.");
        }
    }

    public override void UpdateEvent()
    {
        if (!isServer) return;
        Collider[] colliders = Physics.OverlapSphere(transform.position, 50f);
        foreach (Collider col in colliders)
        {
            Rigidbody rb = col.GetComponent<Rigidbody>();
            if (rb != null && !rb.CompareTag("Player"))
            {
                rb.AddForce(windDirection * windStrength +
                    new Vector3(
                        Mathf.Sin(Time.time * swayFrequency) * swayAmplitude,
                        0,
                        Mathf.Cos(Time.time * swayFrequency) * swayAmplitude
                    ),
                    ForceMode.Force);
            }
        }
    }

    public override void EndEvent()
    {
        base.EndEvent();

        // Stop all particle systems in the list (regardless of Item component check)
        foreach (var ps in leavesParticleSystems)
        {
            if (ps != null)
            {
                ps.Stop();
            }
        }

        if (!TryGetComponent<AudioSource>(out var audioSource))
            audioSource = slave.gameObject.AddComponent<AudioSource>();
        audioSource.Stop();
        Destroy(slave.gameObject);
    }
}