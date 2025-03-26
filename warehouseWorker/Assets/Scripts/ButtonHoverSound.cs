using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;

[RequireComponent(typeof(AudioSource))]
public class ButtonHoverSound : MonoBehaviour, IPointerEnterHandler
{
    public AudioClip sound;
    AudioSource source;
    public AudioMixerGroup mixer;

    private void Start()
    {
        source = GetComponent<AudioSource>();
        source.outputAudioMixerGroup = mixer;
        source.maxDistance = 10000;
        source.rolloffMode = AudioRolloffMode.Linear;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        source.PlayOneShot(sound);
    }
}
