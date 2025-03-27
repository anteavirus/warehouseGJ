using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BoogeymanAndLightsTurnOffEvent : Event
{
    [Header("Boogeyman Settings")]
    public GameObject boogeymanPrefab;
    public BoxCollider spawnArea;

    [Header("Light Settings")]
    [SerializeField] List<Transform> lightParents = new List<Transform>();
    [SerializeField][Range(0, 5)] float dimFrom = 1f;
    [SerializeField][Range(0, 5)] float dimInto = 0.2f;
    [SerializeField] float transitionDuration = 2f;
    [SerializeField] AudioClip associatedSfx;

    [Header("Optional Outline")]
    [SerializeField] private RenderFeatureOutlineFader outlineFader;
    [SerializeField] private float outlineStartAlpha = 1f;
    [SerializeField] private float outlineEndAlpha = 0f;

    private GameObject realBoogey;
    private List<Light> targetLights = new List<Light>();
    private PlayerController player;
    private Coroutine activeRoutine;
    private Blank slave;
    private GameObject outlineObjectInstance;
    private RenderFeatureOutlineFader outlineFaderInstance;

    public override void StartEvent()
    {
        base.StartEvent();
        SpawnBoogey();
        InitializeLightControl();

        player = FindObjectOfType<PlayerController>();
        player.musicSource.Pause();
    }

    void SpawnBoogey()
    {
        if (spawnArea == null)
            spawnArea = GameObject.Find("SpookySpawn").GetComponent<BoxCollider>();

        Vector3 spawnPos = GetRandomPositionInBox(spawnArea.bounds);
        realBoogey = Instantiate(boogeymanPrefab, spawnPos, Quaternion.identity);
    }

    void InitializeLightControl()
    {
        if (outlineFader != null)
        {
            outlineObjectInstance = Instantiate(outlineFader.gameObject);
            outlineFaderInstance = outlineObjectInstance.GetComponent<RenderFeatureOutlineFader>();
        }

        if (player == null) return;

        slave = new GameObject("LightController").AddComponent<Blank>();
        slave.transform.SetParent(player.transform);

        FindTargetLights();
        activeRoutine = slave.StartCoroutine(TransitionLights(dimFrom, dimInto, transitionDuration));
    }

    void FindTargetLights()
    {
        targetLights.Clear();
        foreach (var parent in lightParents)
        {
            if (parent != null)
            {
                targetLights.AddRange(parent.GetComponentsInChildren<Light>(true));
            }
        }

        if (targetLights.Count == 0)
        {
            foreach (var item in FindObjectsOfType<Light>())
            {
                if (!item.CompareTag("IgnoreComponent"))
                    targetLights.Add(item);
            }
        }
    }

    IEnumerator TransitionLights(float startIntensity, float endIntensity, float duration)
    {
        if (!TryGetComponent<AudioSource>(out var audioSource))
            audioSource = slave.gameObject.AddComponent<AudioSource>();

        audioSource.PlayOneShot(associatedSfx);

        if (outlineFaderInstance != null)
            outlineFaderInstance.FadeOutline(startIntensity > endIntensity ? outlineEndAlpha : outlineStartAlpha);

        SetLightIntensity(startIntensity);

        float elapsed = 0;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            foreach (var light in targetLights)
            {
                if (light != null) light.intensity = Mathf.Lerp(startIntensity, endIntensity, t);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetLightIntensity(endIntensity);
    }

    void SetLightIntensity(float value)
    {
        foreach (var light in targetLights)
        {
            if (light != null) light.intensity = value;
        }
    }

    public override void EndEvent()
    {
        base.EndEvent();

        player.musicSource.UnPause();

        if (realBoogey != null) Destroy(realBoogey);

        if (activeRoutine != null && slave != null)
        {
            slave.StopCoroutine(activeRoutine);
            slave.StartCoroutine(TransitionLights(dimInto, dimFrom, transitionDuration));
        }

        if (slave != null) Destroy(slave.gameObject, associatedSfx.length + 1f);
        if (outlineObjectInstance != null) Destroy(outlineObjectInstance, 5f);
    }

    Vector3 GetRandomPositionInBox(Bounds bounds)
    {
        return new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            bounds.center.y,
            Random.Range(bounds.min.z, bounds.max.z)
        );
    }
}
