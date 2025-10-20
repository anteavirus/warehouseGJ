using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// типы диалогов:
// когда подбирают стакан
// комментарий про предмет
[RequireComponent(typeof(AudioSource))]
public class TalkingDeliveryItem : MonoBehaviour
{
    [Header("Audio Settings")]
    public DeliveryAudioConfig defaultAudioConfig;
    public List<DeliveryAudioConfig> itemSpecificConfigs = new List<DeliveryAudioConfig>();

    [Header("Timing Settings")]
    public float commentCooldown = 5f;
    public float initialReminderDelay = 10f;
    public float reminderInterval = 25f;

    private AudioSource _audioSource;
    [SerializeField] private Collider _triggerZone;
    private bool _canComment = true;
    private Coroutine _reminderRoutine;
    private Dictionary<int, DeliveryAudioConfig> _audioConfigCache;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _triggerZone = GetComponent<Collider>();
        _triggerZone.isTrigger = true;
        BuildAudioCache();
    }

    private void BuildAudioCache()
    {
        _audioConfigCache = new Dictionary<int, DeliveryAudioConfig>();
        foreach (var config in itemSpecificConfigs)
        {
            _audioConfigCache[config.itemID] = config;
        }
    }

    private DeliveryAudioConfig GetAudioConfigForItem(Item item)
    {
        if (_audioConfigCache.TryGetValue(item.ID, out DeliveryAudioConfig config))
        {
            return config;
        }
        return defaultAudioConfig;
    }

    private void StartReminderSystem()
    {
        if (_reminderRoutine != null) StopCoroutine(_reminderRoutine);
        _reminderRoutine = StartCoroutine(ReminderCycle());
    }

    private IEnumerator ReminderCycle()
    {
        yield return new WaitForSeconds(initialReminderDelay);

        while (true)
        {
            if (TryGetCurrentEventItem(out Item currentItem))
            {
                var config = GetAudioConfigForItem(currentItem);
                PlayRandomClip(config.genericReminderClips);
            }
            yield return new WaitForSeconds(reminderInterval);
        }
    }

    private bool TryGetCurrentEventItem(out Item item)
    {
        item = null;
        foreach (var i in GameManager.Instance.activeEvents)
        {
            if (i is DeliveryEvent deliveryEvent)
            {
                item = deliveryEvent.MainItem;
                return true;
            }
        }
        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_canComment) return;
        foreach (var i in GameManager.Instance.activeEvents)
        {
            if (i is not DeliveryEvent) return;
        }

        Item item = other.GetComponent<Item>();
        if (item != null)
        {
            StartCoroutine(HandleItemComment(item));
        }
    }

    private IEnumerator HandleItemComment(Item item)
    {
        _canComment = false;

        DeliveryEvent deliveryEvent = null;
        foreach (var i in GameManager.Instance.activeEvents)
        {
            if (i is DeliveryEvent a)
            {
                deliveryEvent = a;
                break;
            }
        }
        if (deliveryEvent == null) yield break;

        bool isCorrect = item.ID == deliveryEvent.MainItem.ID;

        var itemConfig = GetAudioConfigForItem(item);
        var eventItemConfig = GetAudioConfigForItem(deliveryEvent.MainItem);

        if (isCorrect)
        {
            PlayRandomClip(eventItemConfig.correctDeliveryClips);
            GameManager.Instance.ProcessDelivery(0, item, true); // todo: finish creating this stupid event, maybe?  some day later.
            yield return new WaitForSeconds(commentCooldown * 2);
            if (_reminderRoutine != null) StopCoroutine(_reminderRoutine);
        }
        else
        {
            PlayRandomClip(itemConfig.wrongDeliveryClips ?? defaultAudioConfig.wrongDeliveryClips);
            yield return new WaitForSeconds(commentCooldown);
            _canComment = true;
        }
    }

    private void PlayRandomClip(AudioClip[] clips)
    {
        if (clips != null && clips.Length > 0 && _audioSource != null)
        {
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            _audioSource.PlayOneShot(clip);
        }
    }
}
