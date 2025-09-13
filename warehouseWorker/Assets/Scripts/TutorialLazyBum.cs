using TMPro;
using UnityEngine;

public class TutorialLazyBum : MonoBehaviour
{
    public TextMeshProUGUI text;
    public C4Item c4;
    public ZombieAI zombie;

    // Add these serialized fields for localized text components
    public LocalizedText c4TaskText;
    public LocalizedText zombieTaskText;
    public LocalizedText exitTaskText;

    string FormatBool(bool uh)
    {
        return uh ? "<u>X</u>" : "_";
    }

    void Start()
    {
        // Initialize localized text components if needed
    }

    void Update()
    {
        // Build the text using localized strings
        text.text = $"{c4TaskText.text} - {FormatBool(c4 == null && c4.armed)}\n" +
                   $"{zombieTaskText.text} - {FormatBool(zombie == null || zombie.isDead)}\n" +
                   $"{exitTaskText.text} - _";
    }
}