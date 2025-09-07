using UnityEngine;
using UnityEngine.SceneManagement;

public class ExitDoor : Item
{
    private void Start()
    {
        audioSource.outputAudioMixerGroup = mixerGroup;
        isPickupable = false;
    }

    public override void OnUse(GameObject user)
    {
        base.OnUse(user);
        SceneManager.LoadScene(0);
    }
}