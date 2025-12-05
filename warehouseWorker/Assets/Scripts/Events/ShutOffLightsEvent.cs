using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[SelectionBase]
public class ShutOffLightsEvent : Event
{
    [Header("Light Targets")]
    [SerializeField] List<Transform> lightParents = new List<Transform>();

    [Header("Transition Settings")]
    [SerializeField][Range(0, 5)] float dimFrom = 1f;
    [SerializeField][Range(0, 5)] float dimInto = 0.2f;
    [SerializeField] float transitionDuration = 2f;
    [SerializeField] AudioClip associatedSfx;

    [Header("Outline Settings")]
    [SerializeField] private RenderFeatureOutlineFader outlineFader;
    [SerializeField] private float outlineStartAlpha = 1f;
    [SerializeField] private float outlineEndAlpha = 0f;

    GameObject outlineObjectInstance;
    RenderFeatureOutlineFader outlineFaderInstance;
    private List<Light> targetLights = new List<Light>();
    private PlayerController player;
    private Coroutine activeRoutine;
    private Blank slave;

    public override void StartEvent()
    {
        base.StartEvent();

        outlineObjectInstance = Instantiate(outlineFader.gameObject);
        outlineFaderInstance = outlineObjectInstance.GetComponent<RenderFeatureOutlineFader>();

        player = FindObjectOfType<PlayerController>();
        if (player == null) return;
        slave = new GameObject("LightController").AddComponent<Blank>();
        slave.transform.SetParent(player.transform);

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
                    targetLights.Add(item); // Fuck it. Shitcode. Kill me.
            }
        }


        activeRoutine = slave.StartCoroutine(TransitionLights(dimFrom, dimInto, transitionDuration, slave.gameObject));
    }

    IEnumerator TransitionLights(float startIntensity, float endIntensity, float duration, GameObject slave)
    {
        if (!TryGetComponent<AudioSource>(out var audioSource))
        {
            audioSource = slave.AddComponent<AudioSource>();
            audioSource.outputAudioMixerGroup = ((GameManager)GameManager.Instance).sfx; // shittiest hakc
        }
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
                if (light != null)
                {
                    light.intensity = Mathf.Lerp(startIntensity, endIntensity, t);
                }
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
            if (light != null)
            {
                light.intensity = value;
            }
        }
    }

    public override void EndEvent()
    {
        base.EndEvent();

        if (activeRoutine != null)
        {
            slave.StopCoroutine(activeRoutine);
        }

        slave.StartCoroutine(TransitionLights(dimInto, dimFrom, transitionDuration, slave.gameObject));
        Destroy(slave.gameObject, associatedSfx.length + 1f);
        Destroy(outlineObjectInstance, 5f);
    }
}
