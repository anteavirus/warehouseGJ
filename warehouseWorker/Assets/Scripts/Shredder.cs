using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(BoxCollider), typeof(AudioSource))]
public class Shredder : MonoBehaviour
{
    [SerializeField] AudioClip[] shredderDestroySFX;
    [SerializeField] ParticleSystem shredderParticle;
    AudioSource shredderSource;
    AudioSource ambientShredder;

    Coroutine speedierShredder;

    private void Start()
    {
        AudioSource[] buncha = GetComponents<AudioSource>();
        shredderSource = buncha[0];
        ambientShredder = buncha[1];

        ambientShredder.Play();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Interactable"))
        {        
            if (shredderDestroySFX.Length > 0 && shredderDestroySFX.All(i => i != null))
            {
                AudioClip clip = shredderDestroySFX[Random.Range(0, shredderDestroySFX.Length)];
                shredderSource.PlayOneShot(clip);
            }
            
            // Dirty, but serves me well for now
            if (other.TryGetComponent<Box>(out var box))
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.AddScore(box.scoreValue, false);
            }
            if (other.TryGetComponent<C4Item>(out var bomb))
            {
                bomb.HandleShredded();
            }

            if (other.TryGetComponent<PlayerController>(out var playerController))
            {
                playerController.OnShredderEnter();
                return;
            }
            Destroy(other.gameObject);

            if (speedierShredder != null) StopCoroutine(speedierShredder);
            speedierShredder = StartCoroutine(MakeItSpewOutFaster(shredderParticle));
        }
    }

    IEnumerator MakeItSpewOutFaster(ParticleSystem particleSystem)
    {
        var module = particleSystem.main;
        float startSpeed = 1f;
        float targetSpeed = 2f;
        float duration = 3f;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            module.simulationSpeed = Mathf.Lerp(
                Mathf.Max(startSpeed, module.simulationSpeed), targetSpeed, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        module.simulationSpeed = targetSpeed;

        yield return new WaitForSeconds(12f);

        elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            module.simulationSpeed = Mathf.Lerp(targetSpeed, startSpeed, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        module.simulationSpeed = startSpeed;
        speedierShredder = null;
    }
}
