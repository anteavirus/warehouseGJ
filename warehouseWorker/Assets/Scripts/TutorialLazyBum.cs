using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TutorialLazyBum: MonoBehaviour
{
    public TextMeshProUGUI text;
    public C4Item c4;
    public ZombieAI zombie;

    string FormatBool(bool uh)
    {
        return uh ? "<u>X</u>" : "_";
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // TODO: unlazify this shit, maybe? make it interesting? i dunno
        text.text = $"Деактивировать С4 - {FormatBool(c4 == null && c4.armed)}\nУбить зомби - {FormatBool(zombie == null || zombie.isDead)}\nВыйти из комнаты - _";
    }
}
