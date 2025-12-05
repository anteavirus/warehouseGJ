using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopManager : GenericManager<ShopManager>
{
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
    public bool finishedGeneratingShopSprites = false;
    Coroutine stupidMakeSureShitWorks = null;

    public override void Initialize()
    {
        base.Initialize();

        stupidMakeSureShitWorks ??= StartCoroutine(nameof(AtSomePointGenerateIcons));
    }

    IEnumerator AtSomePointGenerateIcons()
    {
        yield return new WaitUntil(() => IconManager.Instance != null);
        int w = 128, h = 128;
        for (int index = 0; index < shopElements.Length; index++)
        {
            PurchasableElement item = shopElements[index];
            if (item.sprite != null) continue;  // skip, we forced a sprite upon it, certainly must be with reason!  it's a horse.png isn't it
            Sprite newSprite = Sprite.Create(IconManager.Instance.RenderCopyToTexture(item.prefab, w, h), new Rect(0,0,w,h), UsefulStuffs.Vect2OneHalved);
            item.sprite = newSprite;
            generatedSprites.Add(newSprite);
        }
        finishedGeneratingShopSprites = true;
        yield break;
    }

    private void OnDestroy()
    {
        generatedSprites.Clear();  // apparently, sprites sitting unused & disconnected from the world isn't enough for the built-in garbo collector to notice.
    }
}
