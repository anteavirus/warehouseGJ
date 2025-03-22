using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class C4Item : Item
{
    [HideInInspector] public C4Event parentEvent;
    [SerializeField] private float explosionTime = 15f;
    [SerializeField] AudioClip boomSfx;
    private float remainingTime;

    public bool armed = true;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        remainingTime = explosionTime;
        if (armed) audioSource?.Play();
    }

    private void Update()
    {
        if (!armed) return;

        if (audioSource.isPlaying)
            remainingTime = audioSource.clip.length - audioSource.time;
        else
            remainingTime -= Time.deltaTime;

        if (remainingTime <= 0f)
            Detonate();
    }

    public void HandleShredded()
    {
        if (armed)
        {
            Debug.Log("Bomb has been shredded! Counter-Terrorists win!");
        }
        else
        {
            GameManager.Instance.AddScore(scoreValue, resetTimer: true, immediateReset: true);
        }
    }

    private void Detonate()
    {
        StartCoroutine(nameof(explosions));
    }

    IEnumerator explosions()
    {
        audioSource.clip = boomSfx;
        audioSource.Play();
        while (audioSource.clip.length > audioSource.time)
        {
            yield return null;
        }

        // Boom animation here...
        GameManager.Instance.gameStarted = false; // technically yeah but. like these variable names suck ass.
        GameManager.Instance.ForceGameOver();
        yield break;
    }
}
