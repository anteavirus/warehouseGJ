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
        armed = false;
        // the boom is forced by gameover now. idk why.
        GameManager.Instance.ForceGameOver();
        yield break;
    }
}
