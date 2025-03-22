using TMPro;
using UnityEngine;

public class MainMenuExtraSpecialPunchCard : PunchCard
{
    bool isHovering;

    [Header("Special Settings")]
    [SerializeField] private TMP_Text usernameDisplay; // Reference to TextMeshPro component
    [SerializeField] private Vector3 hoverStartPosition;
    private float hoverPositionOffset = 3;
    [SerializeField] private float hoverAmplitude = 0.5f;
    [SerializeField] private float hoverSpeed = 2f;

    Rigidbody rb;
    Coroutine bastard;
    void Start()
    {
        hoverStartPosition = transform.position;
        rb = GetComponent<Rigidbody>();
        if (usernameDisplay != null)
            usernameDisplay.text = PlayerPrefs.GetString("CurrentUsername");

        StartHoverSequence();
    }

    public override void OnPickup(Transform holder)
    {
        base.OnPickup(holder);
        rb.useGravity = true;
        if (bastard != null) StopCoroutine(bastard);
        bastard = null;
    }

    public override void OnUse(GameObject user)
    {
        if (!user.TryGetComponent<PunchClock>(out var _)) return;
        GameManager.Instance.LoadScene(1);
    }

    private void StartHoverSequence()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.AddForce(Vector3.up * 2f, ForceMode.Impulse);

        if (bastard != null) StopCoroutine(bastard);
        bastard = StartCoroutine(HoverRoutine());
    }

    private System.Collections.IEnumerator HoverRoutine()
    {
        isHovering = true;
        float timer = 0f;
        rb.useGravity = false;

        while (isHovering)
        {
            float yOffset = Mathf.Sin(timer * hoverSpeed) * hoverAmplitude;

            Vector3 newPos = (hoverStartPosition + new Vector3(0, hoverPositionOffset, 0)) +
                           Vector3.up * (yOffset + hoverAmplitude);

            transform.position = Vector3.Lerp(
                transform.position,
                newPos,
                Time.deltaTime * 5f
            );

            timer += Time.deltaTime;
            yield return null;
        }
    }
}
