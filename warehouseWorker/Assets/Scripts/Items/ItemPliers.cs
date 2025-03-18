using UnityEngine;

public class ItemPliers : Item
{
    [HideInInspector] public C4Event parentEvent;
    public float interactionRange = 3f;

    public override void OnUse(GameObject user)
    {
        base.OnUse(user);

        PlayerController player = user.GetComponent<PlayerController>();
        if (!player) return;

        if (Physics.Raycast(player.playerCamera.transform.position,
                         player.playerCamera.transform.forward,
                         out RaycastHit hit,
                         interactionRange, 
                         player.interactableLayer))
        {
            C4Item c4Target = hit.collider.GetComponent<C4Item>();
            if (c4Target != null && c4Target.armed)
            {
                CombineWithC4(c4Target, user.transform.position);
            }
        }
    }

    private void CombineWithC4(C4Item c4, Vector3 combinePosition)
    {
        C4Item disarmedC4 = Instantiate(parentEvent.DefusedC4Prefab,
                                        combinePosition,
                                        Quaternion.identity);

        disarmedC4.armed = false;
        disarmedC4.parentEvent = parentEvent;
        parentEvent.activeC4 = disarmedC4;

        PlayerController player = controller;

        player.ForceDropItem();
        Destroy(c4.gameObject);
        Destroy(gameObject);
    }
}
