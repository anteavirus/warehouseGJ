using System.Collections;
using UnityEngine;

public class PunchClock : MonoBehaviour
{
    [SerializeField] GameObject thingsToCloseSoThatPlayerWouldntBeSoftlockedForSomeTimePreferrablyHoursLikelyTwoSeconds;

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PunchCard>(out var card))
        {
            if (GameManager.Instance != null && !GameManager.Instance.gameStarted)
            {
                card.OnUse(gameObject);
                GameManager.Instance.StartGame();
                StartCoroutine(MoveObjectUp(thingsToCloseSoThatPlayerWouldntBeSoftlockedForSomeTimePreferrablyHoursLikelyTwoSeconds.transform, 150, 1));
            }
        }
    }

    IEnumerator MoveObjectUp(Transform objectToMove, float distance, float speed)
    {
        Vector3 startPos = objectToMove.position;
        float distanceMoved = 0f;

        while (distanceMoved < distance)
        {
            float step = speed * Time.deltaTime;
            objectToMove.position += Vector3.up * step;
            distanceMoved += step;
            yield return null;
        }

        objectToMove.position = startPos + Vector3.up * distance;
    }

}
