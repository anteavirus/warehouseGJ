using UnityEngine;

public class WindyEvent : Event
{
    public ParticleSystem leavesParticleSystem;
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

        if (leavesParticleSystem != null)
            leavesParticleSystem.Play();
        else
        {
            //hardcoding solutions doodlee-doo. i'm going to die doodle-doo
            leavesParticleSystem = GameObject.Find("leaves").GetComponent<ParticleSystem>();
            leavesParticleSystem.Play();
        }

        if (!TryGetComponent<AudioSource>(out var audioSource)) 
            audioSource = slave.gameObject.AddComponent<AudioSource>();
        audioSource.clip = sfx;
        audioSource.loop = true;
        audioSource.maxDistance = 30;
        audioSource.Play();
    }

    public override void UpdateEvent()
    {
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

        if (leavesParticleSystem != null)
            leavesParticleSystem.Stop();

        if (!TryGetComponent<AudioSource>(out var audioSource))
            audioSource = slave.gameObject.AddComponent<AudioSource>();
        audioSource.Stop();
        Destroy(slave.gameObject);
    }
}
