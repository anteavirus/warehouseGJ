using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerGrabbyScript : MonoBehaviour
{
    [Header("References")]
    public PlayerController controller;

    [Header("Indicator Settings")]
    public Sprite indicatorSprite;
    public float rotationSpeed = 90f;
    public float spriteScale = 1f;

    [Header("Refresh Settings")]
    public float raycastRefreshInterval = 0.1f;

    [Header("Grabbing Interaction")]
    [SerializeField] Image grabbyIndicator;
    [SerializeField] Image itemToGrabIndicator;
    [SerializeField] Sprite noItem, someItem, grabbedItem;

    private List<object> nearbyObjects = new();
    private RotatingIndicator currentIndicator;
    private float lastRaycastTime;

    public object focusedTarget => GetFocusedTarget();
    public Item focusedItem => focusedTarget as Item;

    private void Update()
    {
        if (Time.time - lastRaycastTime >= raycastRefreshInterval)
        {
            RefreshFocus();
            lastRaycastTime = Time.time;
        }
    }

    //TODO: start ignoring the storageareas, they're only there to count the boxes stored now

    private object GetFocusedTarget()
    {
        // Raycast takes priority
        var rayList = Physics.RaycastAll(controller.playerCamera.transform.position,
            controller.playerCamera.transform.forward,
            controller.pickupRange, controller.interactableLayer);
        foreach (var rayHit in rayList)
        {
            if (rayHit.collider.TryGetComponent<Item>(out var item))
            {
                if (item.isActiveAndEnabled && item.isPickupable)
                    return item;
            }
        }

        if (Physics.Raycast(controller.playerCamera.transform.position,
            controller.playerCamera.transform.forward, out RaycastHit hit,
            controller.pickupRange, controller.interactableLayer))
        {
            // Used to contain item condition; unnecessary, I assume, seein' above

            if (hit.collider.TryGetComponent<StorageArea>(out var area))
            {
                if (area.isActiveAndEnabled)
                    return area;
            }

            var collidedGameObj = hit.collider.gameObject;
            if (collidedGameObj.layer == LayerMask.NameToLayer("Draggable"))
            {
                if (collidedGameObj.activeInHierarchy)
                    return collidedGameObj;
            }
        }

        CleanupNullObjects();
        if (nearbyObjects.Count > 0)
        {
            return GetNearestObject();
        }

        return null;
    }

    private object GetNearestObject()
    {
        if (nearbyObjects.Count == 0) return null;
        if (nearbyObjects.Count == 1)
        {
            var single = nearbyObjects[^1];
            if (single is Item i && !i.isPickupable) return null;
            return single;
        }

        var cameraPos = controller.playerCamera.transform.position;
        object nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (var obj in nearbyObjects)
        {
            if (obj is Item item && !item.isPickupable)
                continue;

            var transform = GetTransform(obj);
            if (transform == null) continue;

            float distance = Vector3.Distance(cameraPos, transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = obj;
            }
        }

        return nearest;
    }


    private void RefreshFocus()
    {
        UpdateIndicator();
        
        if (controller.heldItem != null)
        {
            grabbyIndicator.sprite = grabbedItem;
            itemToGrabIndicator.gameObject.SetActive(true);

            if (IconManager.Instance != null)
            {
                var heldItemSprite = IconManager.Instance.previewSprites
                    .Find(i => i != null && i.name.StartsWith(IconManager.IconNamePrefix(controller.heldItem.GetComponent<Item>().ID.ToString())));
                // Fallback if no match found
                itemToGrabIndicator.sprite = heldItemSprite ?? IconManager.Instance.previewSprites[^1];
            }
            // Place the preview below the main indicator (adjust as desired)
            var heldOffset = new Vector2(0, -16f);
            itemToGrabIndicator.rectTransform.anchoredPosition = grabbyIndicator.rectTransform.anchoredPosition + heldOffset;
            grabbyIndicator.transform.SetAsLastSibling();

            return;
        }

        var target = focusedTarget;
        if (target != null)
        {
            grabbyIndicator.sprite = someItem;
            if (IconManager.Instance != null)
            {
                if (target is Item item)
                {
                    itemToGrabIndicator.gameObject.SetActive(true);
                    var previewSprite = IconManager.Instance.previewSprites
                        .Find(i => i != null && i.name.StartsWith(IconManager.IconNamePrefix(item.ID.ToString())));
                    itemToGrabIndicator.sprite = previewSprite ?? IconManager.Instance.previewSprites[^1];
                }
                else
                {
                    itemToGrabIndicator.gameObject.SetActive(false);
                }
            }
            var heldOffset = new Vector2(0, -grabbyIndicator.rectTransform.rect.height / 2f);

            itemToGrabIndicator.rectTransform.anchoredPosition = grabbyIndicator.rectTransform.anchoredPosition;
            itemToGrabIndicator.transform.SetAsLastSibling();

            return;
        }

        grabbyIndicator.sprite = noItem;
        itemToGrabIndicator.gameObject.SetActive(false);
    }


    private void OnTriggerEnter(Collider other)
    {
        CleanupNullObjects();
        var obj = GetInteractableObject(other);
        if (obj != null && !nearbyObjects.Contains(obj))
        {
            if (obj is Item item && !item.isPickupable) return;
            nearbyObjects.Add(obj);
            RefreshFocus();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        CleanupNullObjects();
        var obj = GetInteractableObject(other);
        if (obj != null)
        {
            nearbyObjects.Remove(obj);
            RefreshFocus();
        }
    }

    private object GetInteractableObject(Collider collider)
    {
        if (collider.TryGetComponent<Item>(out var item))
        {
            if (item.isActiveAndEnabled && item.isPickupable)
                return item;
        }
        if (collider.TryGetComponent<StorageArea>(out var area))
        {
            if (area.isActiveAndEnabled)
                return area;
        }

        var collidedGameObj = collider.gameObject;
        if (collidedGameObj.layer == LayerMask.NameToLayer("Draggable"))
        {
            if (collidedGameObj.activeInHierarchy)
                return collidedGameObj;
        }

        return null;
    }

    public Item Pop()
    {
        DisableIndicator();

        var target = focusedTarget;
        if (target == null) return null;

        if (target is Item item)
        {
            nearbyObjects.Remove(item);
            RefreshFocus();
            return item;
        }

        return null;
    }

    private void UpdateIndicator()
    {
        var target = focusedTarget;
        if (target == null)
        {
            DisableIndicator();
            return;
        }

        var targetTransform = GetTransform(target);
        if (targetTransform == null)
        {
            DisableIndicator();
            return;
        }

        if (currentIndicator == null || !currentIndicator.IsValid() ||
            currentIndicator.targetTransform != targetTransform)
        {
            DisableIndicator();
            CreateIndicator(targetTransform);
        }
    }

    private void CreateIndicator(Transform target)
    {
        if (indicatorSprite == null) return;

        var indicatorObj = new GameObject("RotatingIndicator");
        currentIndicator = indicatorObj.AddComponent<RotatingIndicator>();
        currentIndicator.Initialize(target, indicatorSprite, rotationSpeed, spriteScale);
    }

    private void DisableIndicator()
    {
        if (currentIndicator != null)
        {
            currentIndicator.Disable();
            currentIndicator = null;
        }
    }

    Transform GetTransform(object target)
    {
        if (target == null) return null;

        if (target is IEnumerable<object> list && target is not string) // we probably hit the nearbyObjects list.
        {
            foreach (var boba in list)
            {
                if (boba == null) continue;
                if (boba is GameObject ass)
                {
                    if (ass.activeSelf)
                        return ass.transform;
                    continue;
                }
                if (boba is Component blast)
                {
                    return blast.transform;
                }
            }
            return null;
        }

        if (target is GameObject go) return go.transform;
        if (target is Component comp) return comp.transform;

        return null;
    }

    private void CleanupNullObjects()
    {
        nearbyObjects.RemoveAll(obj =>
            obj == null ||      // removes pure nulls
            (obj is UnityEngine.Object unityObj && unityObj == null) // removes destroyed Unity objects
        );

    }

    private void OnDestroy()
    {
        DisableIndicator();
    }
}
