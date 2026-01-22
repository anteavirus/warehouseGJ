using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuExtraSpecialPunchCard : PunchCard
{
    bool isHovering;

    [Header("Special Settings")]
    [SerializeField] private TMP_Text usernameDisplay; // Reference to TextMeshPro component
    [SerializeField] private Vector3 hoverStartPosition;
    [SerializeField] private float hoverAmplitude = 0.5f;
    [SerializeField] private float hoverSpeed = 2f;
    public event System.Action OnDestroyed; 

    [Header("Hover Settings")]
    [SerializeField] private float riseHeight = 2f;
    [SerializeField] private float riseDuration = 1f;
    [SerializeField] private float rotationSpeed = 30f;
    private Vector3 targetHoverPosition;
    private Vector3 targetEulerRotation;

    Rigidbody rb;
    Coroutine bastard;
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (usernameDisplay != null)
            usernameDisplay.text = PlayerPrefs.GetString("CurrentUsername");

        InitializeHoverPosition(hoverStartPosition);
    }

    public override void OnPickup(Transform holder)
    {
        if (bastard != null) StopCoroutine(bastard);
        base.OnPickup(holder);
        rb.useGravity = true;
        bastard = null;
        transform.position = hoverStartPosition;
    }

    public override void OnUse(GameObject user)
    {
        if (!user.TryGetComponent<PunchClock>(out var _)) return;  //TODO fix
        if (SceneManager.GetActiveScene().name == "Main Menu") ((GameManager)GameManager.Instance).LoadSceneStr("GameplayScene");
    }

    private void StartHoverSequence()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.AddForce(Vector3.up * 2f, ForceMode.Impulse);

        if (bastard != null) StopCoroutine(bastard);
        bastard = StartCoroutine(HoverRoutine());
    }

    public void InitializeHoverPosition(Vector3 groundPosition)
    {
        hoverStartPosition = groundPosition;
        targetHoverPosition = groundPosition + Vector3.up * riseHeight;
        StartCoroutine(RiseFromGround());
    }

    private IEnumerator RiseFromGround()
    {
        float elapsed = 0f;
        Vector3 startPos = transform.position;

        while (elapsed < riseDuration)
        {
            transform.position = Vector3.Lerp(
                startPos,
                targetHoverPosition,
                elapsed / riseDuration
            );
            elapsed += Time.deltaTime;
            yield return null;
        }

        StartHoverSequence();
    }

    private IEnumerator HoverRoutine()
    {
        isHovering = true;
        float timer = 0f;
        rb.useGravity = false;
        targetEulerRotation = transform.eulerAngles;

        while (isHovering)
        {
            rb.velocity = Vector3.zero;
            float yOffset = Mathf.Sin(timer * hoverSpeed) * hoverAmplitude;
            Vector3 newPos = targetHoverPosition + Vector3.up * yOffset;

            transform.position = Vector3.Lerp(
                transform.position,
                newPos,
                Time.deltaTime * 5f
            );

            targetEulerRotation.y += rotationSpeed * Time.deltaTime;
            transform.rotation = Quaternion.Euler(targetEulerRotation);

            timer += Time.deltaTime;
            yield return null;
        }
    }

    private void OnDestroy()
    {
        OnDestroyed?.Invoke();
    }
}
