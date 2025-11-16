using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance;
    [System.Serializable]
    public class PurchasableElement
    {
        public string name;
        public string description;
        public int cost;
        public GameObject prefab;
        [Tooltip("If set to custom, will show custom sprite. Else, generates one of itself thanks to IconManager. Or tries to, at least.")]
        public Sprite sprite;
        public string extraSpecialFunctionThatIsHardcodedWithinTerminalToDoSomethingUnique;
    }

    public List<Sprite> generatedSprites;   
    public PurchasableElement[] shopElements;
    
    Coroutine stupidMakeSureShitWorks = null;

    private void Start()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        stupidMakeSureShitWorks ??= StartCoroutine(nameof(AtSomePointGenerateIcons));
    }

    IEnumerator AtSomePointGenerateIcons()
    {
        yield return new WaitUntil(() => IconManager.Instance != null);
        for (int index = 0; index < shopElements.Length; index++)
        {
            PurchasableElement item = shopElements[index];
            if (item.sprite != null) continue;  // skip, we forced a sprite upon it, certainly must be with reason!  it's a horse.png isn't it
            Sprite newSprite = Sprite.Create(IconManager.Instance.RenderCopyToTexture(item.prefab, 128, 128), new Rect(), UsefulStuffs.Vect2OneHalved);
            item.sprite = newSprite;
            generatedSprites.Add(newSprite);
        }
        yield break;
    }

    private void OnDestroy()
    {
        generatedSprites.Clear();  // apparently, sprites sitting unused & disconnected from the world isn't enough for the built-in garbo collector to notice.
    }
}
