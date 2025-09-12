using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static SettingsManager;

public class NotesScript : MonoBehaviour
{
    public SettingsManager settingsManager;
    public TextMeshProUGUI pickUpHint;
    public TextMeshProUGUI useHint;
    public TextMeshProUGUI jumpHint;
    public TextMeshProUGUI placeHint;
    public TextMeshProUGUI throwHint;
    public TextMeshProUGUI movementHint;    // HARDCODED! Kill Coder to unhardcode this shit



    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(FindBindsNamesLater());
    }

    IEnumerator FindBindsNamesLater()
    {
        while (settingsManager == null)
        {
            settingsManager = SettingsManager.Instance;
            if (settingsManager == null)
                settingsManager = FindAnyObjectByType<SettingsManager>();

            if (settingsManager == null)
                yield return new WaitForSecondsRealtime(0.1f); // Short wait
        }

        pickUpHint.text = settingsManager.GetKeyDisplay("Pickup");
        useHint.text = settingsManager.GetKeyDisplay("Use");
        jumpHint.text = settingsManager.GetKeyDisplay("Jump");
        placeHint.text = settingsManager.GetKeyDisplay("Place");
        throwHint.text = settingsManager.GetKeyDisplay("Throw");
        movementHint.text = "WASD";
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
