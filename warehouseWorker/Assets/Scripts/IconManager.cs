using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IconManager : MonoBehaviour
{
    public static IconManager Instance;
    public static string IconNamePrefix(string text) => $"[{text}]";
    public List<Item> itemTemplates = new();

    // Previews
    [SerializeField] Vector2 previewSize = new Vector2(128, 128);
    public List<Texture2D> previews = new();
    public Texture2D defaultPreview;
    public List<Sprite> previewSprites;

    // Start is called before the first frame update
    void Start()
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

        DontDestroyOnLoad(gameObject);
        GeneratePreviews();
    }

    void GeneratePreviews()
    {
        previews.Clear();
        previewSprites.Clear();

        for (int i = 0; i < itemTemplates.Count; i++)
        {
            Item prefab = itemTemplates[i];
            if (prefab == null)
            {
                previews.Add(null);
                continue;
            }

            Texture2D preview = RenderCopyToTexture(prefab.gameObject, (int)previewSize.x, (int)previewSize.y, i);
            previews.Add(preview);
        }
        previews.Add(defaultPreview);

        foreach (var preview in previews)
        {
            if (preview == null) continue;

            Rect rect = new(0, 0, preview.width, preview.height);
            Sprite sprite = Sprite.Create(preview, rect, new Vector2(0.5f, 0.5f));
            sprite.name = preview.name;
            previewSprites.Add(sprite);
        }
    }

    public Texture2D RenderCopyToTexture(GameObject prefab, int width, int height, int item = -1)
    {
        int previewLayer = 31;

        GameObject instance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        instance.SetActive(true);
        SetLayer(instance, previewLayer);

        Bounds bounds = GetBounds(instance);
        Vector3 center = bounds.center;
        float maxExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);

        GameObject lightObj = new GameObject("PreviewLight");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.color = Color.white;
        light.cullingMask = 1 << previewLayer;

        Vector3 viewDir = new Vector3(1, 1, -1).normalized;
        lightObj.transform.rotation = Quaternion.LookRotation(-viewDir);

        Camera cam = new GameObject("PreviewCam").AddComponent<Camera>();
        cam.enabled = false;
        cam.clearFlags = CameraClearFlags.Color;
        cam.backgroundColor = Color.clear;
        cam.cullingMask = 1 << previewLayer;
        cam.orthographic = true;
        cam.orthographicSize = maxExtent * 1.2f;

        float distance = maxExtent * 3f;
        Vector3 camPos = center + viewDir * distance;
        cam.transform.position = camPos;
        cam.transform.LookAt(center);

        RenderTexture rt = new RenderTexture(width, height, 16);
        cam.targetTexture = rt;

        cam.Render();

        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        tex.name = $"{IconNamePrefix(itemTemplates[item].ID.ToString())} - {prefab.name}";

        RenderTexture.active = null;

        DestroyImmediate(rt);
        DestroyImmediate(cam.gameObject);
        DestroyImmediate(lightObj);
        DestroyImmediate(instance);

        return tex;
    }

    void SetLayer(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.transform)
            SetLayer(t.gameObject, layer);
    }

    Bounds GetBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }
}
