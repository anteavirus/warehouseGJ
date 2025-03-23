using UnityEngine;

[CreateAssetMenu(menuName = "Audio/Delivery Audio Config")]
public class DeliveryAudioConfig : ScriptableObject
{
    [Header("Item Identification")]
    public int itemID;
    public string itemName;

    [Header("Audio Clips")]
    public AudioClip[] genericReminderClips;
    public AudioClip[] correctDeliveryClips;
    public AudioClip[] wrongDeliveryClips;
}
