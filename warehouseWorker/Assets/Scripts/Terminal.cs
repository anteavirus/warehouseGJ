using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Terminal : MonoBehaviour
{
    public Canvas[] terminalCanvasi = new Canvas[8];  // same amount as selection buttons... lets hope there won't be any more
    
    readonly ArrowControllableInt canvas_selector = new();
    public ArrowSet[] canvas_selection_buttons = new ArrowSet[8];  // lets hardcode 8 for now, should be golden. terminal won't have any more than that, right?

    public Camera[] cameras;
    
    [SerializeField] RenderTexture cameraVision;
    
    readonly ArrowControllableInt selection_cam = new();
    ArrowShift left_cam;
    ArrowShift right_cam;


    public GameObject shopProductPrefab;
    public GameObject cameraProductPrefab; // TODO: set up shop class, add there the prefabs and fucking shit akin to that, pull shit from here and make shopProducts of that!

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
