using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Terminal : MonoBehaviour
{
    public Canvas[] terminalCanvasi = new Canvas[8];  // same amount as selection buttons... lets hope there won't be any more
    
    readonly ArrowControllableInt canvas_selector = new();
    public ArrowSet[] canvas_selection_buttons = new ArrowSet[8];  // lets hardcode 8 for now, should be golden. terminal won't have any more than that, right?

    public List<Camera> cameras = new(10);  // i don't know why but i'd like for this to have a limit
    
    [SerializeField] RenderTexture cameraVision;
    
    readonly ArrowControllableInt selection_cam = new();
    ArrowShift left_cam;
    ArrowShift right_cam;


    public GameObject shopProductPrefab;
    // TODO: set camera priority -100

    Coroutine GetTheFuckingShopAtSomePoint;

    void Start()
    {
        canvas_selector.OnSelectionChanged += CanvasSetter;
        selection_cam.OnSelectionChanged += CamShifter;

        foreach (var canvas in terminalCanvasi)
        {
            if (canvas == null) continue;
            canvas.gameObject.SetActive(false);
        }
        terminalCanvasi[0].gameObject.SetActive(true);

        foreach (var button in canvas_selection_buttons)
        {
            if (button == null) continue;
            button.area = canvas_selector;
        }

        selection_cam.left = left_cam; selection_cam.right = right_cam;
        left_cam = terminalCanvasi[1].transform.GetChild(1).GetComponent<ArrowShift>();
        right_cam = terminalCanvasi[1].transform.GetChild(2).GetComponent<ArrowShift>();

        GetTheFuckingShopAtSomePoint ??= StartCoroutine(nameof(GetTheShittyShopAndFillYourShittyShop));
    }

    IEnumerator GetTheShittyShopAndFillYourShittyShop()
    {
        yield return new WaitUntil(() => ShopManager.Instance != null);
        // TODO: fucking... needs a better way to know what's where. I'm currently hardcoding this shit, and this shit ain't shitting when it's outta sync.
        foreach (var item in UsefulStuffs.FindComponentsInChildren<Transform>(terminalCanvasi[0].gameObject)) // This has an error if I do it with Unity's own method...
        {
            if (item == terminalCanvasi[0].transform) continue;
            Destroy(item.gameObject);
        }

        foreach (var item in ShopManager.Instance.shopElements)
        {
            var shopButton = Instantiate(shopProductPrefab, terminalCanvasi[0].transform);
            // Ughh.. we gotta somehow find the fucking elements within the prefab...
            // TODO: find a way to create easy-to-create UI elements with easy-to-replace UI element overriders methods
            // Right now, I'm going to hardcode it. Why? Because I'm practically the only one making UI for this mess...

            var icon = shopButton.transform.Find("icon");
            icon.GetComponent<Image>().sprite = item.sprite;
            var datacard = shopButton.transform.Find("datacard");
            datacard.Find("name").GetComponent<TextMeshProUGUI>().text = item.name;
            datacard.Find("descr").GetComponent<TextMeshProUGUI>().text = item.description;
            var purchasefield = shopButton.transform.Find("purchase field");
            purchasefield.Find("cost").GetComponent<TextMeshProUGUI>().text = item.cost.ToString();

            var button = purchasefield.Find("purchase").GetComponent<GenericUIButton>();  // TODO: perhaps use this instead of "ArrowSet" and shit like that. Less messy...
            button.OnUseAction = (() => ExtraSpecialFunctionTomfuckery(item));

            yield break;
        }
    }

    // TODO: successful / failed purchase, notification, perhaps even an actual explanation
    void ExtraSpecialFunctionTomfuckery(ShopManager.PurchasableElement item)
    {
        if (GameManager.Instance.score - item.cost <= 0) return; // Can't have people going into debt

        var fuckingGameObject = Instantiate(item.prefab);
        fuckingGameObject.transform.SetPositionAndRotation(GameManager.Instance.blackHoleSpawnPosition.position, Quaternion.identity);

        if (!string.IsNullOrEmpty(item.extraSpecialFunctionThatIsHardcodedWithinTerminalToDoSomethingUnique))
        {
            switch (item.extraSpecialFunctionThatIsHardcodedWithinTerminalToDoSomethingUnique)
            {
                case "camera":
                    {
                        if (cameras.Count + 1 < cameras.Capacity)
                        {
                            if (selection_cam.selection > 0 && selection_cam.selection < selection_cam.size && cameras[selection_cam.selection] != null)
                                cameras[selection_cam.selection].targetTexture = null;  // unsetting it, for it is no longer the chosen one
                            var fuckingCameraCamera = fuckingGameObject.GetComponent<Camera>();
                            cameras.Add(fuckingCameraCamera);
                            fuckingCameraCamera.targetTexture = cameraVision;
                            fuckingCameraCamera.depth = -100; // LAST! Todo: figure out what to do when the player managed to turn themselves into Gaster. They can't just be a Cameraman from now on, can they..?
                        }
                        else
                        {
                            return; // Deny purchase, arbitrary limit on cameras reached. I don't know why I'd want it but I'd like for it to be there
                        }
                        break;
                    }
                default:
                    {
                        Debug.LogWarning("Unrecognized extraSpecialFunctionThatIsHardcodedWithinTerminalToDoSomethingUnique parameter: " + item.extraSpecialFunctionThatIsHardcodedWithinTerminalToDoSomethingUnique);  // hehehe, pointlessly big. can't fit in a line.
                        break;
                    }
            }
        }

        GameManager.Instance.score -= item.cost;    
        return; // Successful purchase
    }

    // TODO: automatically create buttons? or manually
    public void CanvasSetter(bool _) // we don't use the bool, it's basically the same class anyway but we use the "set" instead
    {
        foreach (var canvas in terminalCanvasi)
        {
            if (canvas == null) continue;
            canvas.gameObject.SetActive(false);
        }

        terminalCanvasi[canvas_selector.selection].gameObject.SetActive(true);
    }

    void CamShifter(bool left)
    {
        cameras[selection_cam.selection].targetTexture = null;  // unsetting it, for it is no longer the chosen one
        selection_cam.ShiftSelection(left);
        UpdateUI();
    }

    void UpdateUI()
    {
        cameras[selection_cam.selection].targetTexture = cameraVision;
            
    }
}
