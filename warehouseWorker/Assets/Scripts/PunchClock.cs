using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class PunchClock : Item
{
    [SerializeField] GameObject thingsToCloseSoThatPlayerWouldntBeSoftlockedForSomeTimePreferrablyHoursLikelyTwoSeconds;
    [SerializeField] AudioClip startGoingUp, continueGettingUp, finishedGoingUp;
    readonly float maxDistance = 10;

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<MainMenuExtraSpecialPunchCard>(out var specilCard))
        {
            specilCard.OnUse(gameObject);
            // Do we even need the return?
        }
        if (other.TryGetComponent<PunchCard>(out var _) || specilCard)
        {
            OnUse(other.gameObject);
        }
    }

    public override void OnUse(GameObject user)
    {
        if (!user.TryGetComponent<PunchCard>(out var _)) return;
        if (GameManager.Instance != null && !GameManager.Instance.gameStarted)
        {
            base.OnUse(gameObject);
            GameManager.Instance.StartGame();
            for (int i = 0; i < thingsToCloseSoThatPlayerWouldntBeSoftlockedForSomeTimePreferrablyHoursLikelyTwoSeconds.transform.childCount; i++)
            {
                StartCoroutine(MoveObjectUp(thingsToCloseSoThatPlayerWouldntBeSoftlockedForSomeTimePreferrablyHoursLikelyTwoSeconds.transform.GetChild(i), 15, 1));
            }
        }
    }

    IEnumerator MoveObjectUp(Transform objectToMove, float distance, float speed)
    {
        if (!objectToMove.gameObject.activeSelf) yield break;

        if (!objectToMove.gameObject.TryGetComponent<AudioSource>(out var soundsrc)) 
            soundsrc = objectToMove.AddComponent<AudioSource>();
        soundsrc.maxDistance = 30;
        soundsrc.rolloffMode = AudioRolloffMode.Linear;
        soundsrc.spatialBlend = 1;

        Vector3 startPos = objectToMove.position;
        float distanceMoved = 0f;

        soundsrc.loop = false;
        soundsrc.clip = startGoingUp;
        soundsrc.Play();

        float timer = 0f;
        bool continueClipStarted = false;

        while (distanceMoved < distance && objectToMove.position.y <= startPos.y + maxDistance)
        {
            float step = speed * Time.deltaTime;
            objectToMove.position += Vector3.up * step;
            distanceMoved += step;

            timer += Time.deltaTime;

            if (!continueClipStarted && timer >= startGoingUp.length)
            {
                soundsrc.Stop();
                soundsrc.loop = true;
                soundsrc.clip = continueGettingUp;
                soundsrc.Play();
                continueClipStarted = true;
            }

            yield return null;
        }

        objectToMove.position = startPos + Vector3.up * Mathf.Min(distance, maxDistance);

        soundsrc.Stop();
        soundsrc.loop = false;
        soundsrc.clip = finishedGoingUp;
        soundsrc.Play();
    }
}
