using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

// Okay i've decided this shit is outdated, fuck this incomprehensive piece of shit
public class StupidHitboxToMakeGameManagerRestartTimer : MonoBehaviour
{
    //public Light enlighten;
    //public AudioSource source;
    //private void OnTriggerEnter(Collider other)
    //{
    //    var gm = FindObjectOfType<GameManager>();
    //    if (gm != null && gm.GameStarted && gm.setdownItem && other.GetComponent<PlayerController>() != null)
    //    {
    //        if (gm.timer is EndlessGamemodeTimer)
    //            gm.timer.ResetTimer();
    //        source.Play();
    //        StartCoroutine(nameof(blink));
    //    }
    //}

    //IEnumerator blink()
    //{
    //    var ass = FindObjectOfType<ShutOffLightsEvent>();
    //    var ass1 = FindObjectOfType<BoogeymanAndLightsTurnOffEvent>();
    //    if ((ass != null && ass.isActiveAndEnabled) || (ass1 != null && ass.isActiveAndEnabled))
    //        yield break;

    //    float duration = 0.67f;
    //    float maxIntensity = 1.5f;
    //    float minIntensity = 0f;

    //    for (float t = 0; t < duration; t += Time.deltaTime)
    //    {
    //        float normalized = t / duration;
    //        enlighten.intensity = Mathf.SmoothStep(minIntensity, maxIntensity, normalized);
    //        yield return null;
    //    }
    //    enlighten.intensity = maxIntensity;

    //    for (float t = 0; t < duration; t += Time.deltaTime)
    //    {
    //        float normalized = t / duration;
    //        enlighten.intensity = Mathf.SmoothStep(maxIntensity, minIntensity, normalized);
    //        yield return null;
    //    }
    //    enlighten.intensity = minIntensity;
    //}
}
