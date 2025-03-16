using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(BoxCollider), typeof(AudioSource))]
public class Shredder : MonoBehaviour
{
    [SerializeField] AudioClip[] shredderDestroySFX;
    AudioSource shredderSource;
    AudioSource ambientShredder;

    private void Start()
    {
        AudioSource[] buncha = GetComponents<AudioSource>();
        shredderSource = buncha[0];
        ambientShredder = buncha[1];

        // TODO: set up ambient shredder.
        ambientShredder.Play();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (shredderDestroySFX.Length > 0 && shredderDestroySFX.All(i => i != null))
        {
            AudioClip clip = shredderDestroySFX[Random.Range(0, shredderDestroySFX.Length)];
            shredderSource.PlayOneShot(clip);
        }

        if (other.gameObject.layer == LayerMask.NameToLayer("Interactable"))
        {
            if (other.TryGetComponent<Box>(out var _))
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.AddScore(15, false);
            }
            Destroy(other.gameObject);
        }
    }
}
