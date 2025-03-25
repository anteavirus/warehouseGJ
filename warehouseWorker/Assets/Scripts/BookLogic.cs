using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

// this is for help. for stats we could use an endless cheque
public class BookLogic : MonoBehaviour
{
    [Header("Page Settings")]
    [SerializeField] List<GameObject> pageObjects = new List<GameObject>();
    [SerializeField] int visiblePageCount = 2;
    [SerializeField] float pageSpacing = 100f;

    [Header("Animation")]
    [SerializeField] float pageTurnTime = 0.5f;
    [SerializeField] AnimationCurve turnCurve;

    [Header("Navigation")]
    [SerializeField] Button prevButton;
    [SerializeField] Button nextButton;
    [SerializeField] Button backgroundButton;

    int currentPageIndex = 0;
    bool isAnimating;
    Coroutine currentAnimation;

    void Start()
    {
        prevButton.onClick.AddListener(PreviousPage);
        nextButton.onClick.AddListener(NextPage);
        backgroundButton.onClick.AddListener(CloseBook);
        UpdatePagePositions();
    }

    void UpdatePagePositions()
    {
        for (int i = 0; i < pageObjects.Count; i++)
        {
            var page = pageObjects[i];
            float xPos = (i - currentPageIndex) * pageSpacing;

            // Automatic activation based on visible range
            bool inRange = Mathf.Abs(i - currentPageIndex) < visiblePageCount;
            page.SetActive(inRange);

            if (inRange)
            {
                if (currentAnimation == null)
                {
                    page.transform.localPosition = new Vector3(xPos, 0, 0);
                }
                else
                {
                    // If animating, let animation handle position
                    StartCoroutine(AnimatePagePosition(page.transform, xPos));
                }
            }
        }
    }

    IEnumerator AnimatePagePosition(Transform page, float targetX)
    {
        Vector3 startPos = page.localPosition;
        Vector3 endPos = new Vector3(targetX, 0, 0);
        float elapsed = 0;

        while (elapsed < pageTurnTime)
        {
            elapsed += Time.deltaTime;
            float t = turnCurve.Evaluate(elapsed / pageTurnTime);
            page.localPosition = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        page.localPosition = endPos;
    }

    public void NextPage()
    {
        if (!isAnimating && currentPageIndex < pageObjects.Count - 1)
        {
            currentPageIndex++;
            StartCoroutine(PageTurnRoutine(1));
        }
    }

    public void PreviousPage()
    {
        if (!isAnimating && currentPageIndex > 0)
        {
            currentPageIndex--;
            StartCoroutine(PageTurnRoutine(-1));
        }
    }

    IEnumerator PageTurnRoutine(int direction)
    {
        isAnimating = true;
        UpdatePagePositions();

        // Play animation sound/effects here
        float elapsed = 0;

        while (elapsed < pageTurnTime)
        {
            elapsed += Time.deltaTime;
            // Add any additional animation logic here
            yield return null;
        }

        isAnimating = false;
    }

    void CloseBook()
    {
        if (!isAnimating)
        {
            // Handle book close logic
            gameObject.SetActive(false);
        }
    }

    // Visual debugging
    void OnDrawGizmos()
    {
        for (int i = 0; i < pageObjects.Count; i++)
        {
            if (pageObjects[i] == null) continue;

            float xPos = (i - currentPageIndex) * pageSpacing;
            Gizmos.DrawWireCube(
                transform.position + new Vector3(xPos, 0, 0),
                new Vector3(1, 1.5f, 0.1f)
            );
        }
    }
}
