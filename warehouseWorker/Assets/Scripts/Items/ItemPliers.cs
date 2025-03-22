using Unity.VisualScripting;
using UnityEngine;

public class ItemPliers : Item
{
    [HideInInspector] public C4Event parentEvent;
    [SerializeField] AudioClip defusion;
    public float interactionRange = 3f;

    public Blank slave;
    public override void OnUse(GameObject user)
    {
        base.OnUse(user);

        if (!user.TryGetComponent<PlayerController>(out var player)) return;

        if (slave == null)
        {
            slave = new GameObject("Bomb Slave").AddComponent<Blank>();
            if (!slave.TryGetComponent<Blank>(out var _))
                slave.AddComponent<Blank>();
        }

        if (!slave.TryGetComponent<AudioSource>(out var _))
        {
            slave.gameObject.AddComponent<AudioSource>();
        }

        slave.transform.SetParent(player.transform, true);

        if (Physics.Raycast(player.playerCamera.transform.position,
                          player.playerCamera.transform.forward,
                          out RaycastHit hit,
                          Mathf.Clamp(interactionRange, 0.1f, 50f),
                          player.interactableLayer))
        {
            if (hit.collider.TryGetComponent<C4Item>(out var c4Target) && c4Target.armed)
            {
                CombineWithC4(c4Target, player.transform.position);
            }
        }
    }

    private void CombineWithC4(C4Item c4, Vector3 combinePosition)
    {
        if (parentEvent == null) parentEvent = FindAnyObjectByType<C4Event>();
        var disarmedC4 = Instantiate(parentEvent.disarmedC4Prefab,
                                    combinePosition,
                                    Quaternion.identity);
        disarmedC4.armed = false;
        disarmedC4.parentEvent = parentEvent;
        parentEvent.activeC4 = disarmedC4;

        // tbh, slave was used for this only so that it'd play the sounds. rn it's kinda useless.
        // as we don't destroy the defuse kit.
        if (defusion != null && slave.TryGetComponent<AudioSource>(out var slaveSource))
        {
            slaveSource.PlayOneShot(defusion);
            Destroy(slave.gameObject, defusion.length);
        }
        else
        {
            Destroy(slave.gameObject);
        }

        controller?.ForceDropItem();
        Destroy(c4.gameObject);
    }
}
