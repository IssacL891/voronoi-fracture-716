using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Camera))]
public class LowResCamera : MonoBehaviour
{
    [Tooltip("Integer downscale factor: 1 = native, 2 = half res, 4 = quarter, etc.")]
    public int pixelScale = 4;

    [Tooltip("Optional target pixel height. If >0 this will override pixelScale and compute width to preserve aspect ratio.")]
    public int targetPixelHeight = 0;

    [Tooltip("Optional RawImage to display the low-res buffer. If null the script will create a Canvas+RawImage.")]
    public RawImage outputRawImage;

    [Tooltip("Optional material to apply to the RawImage (e.g. PixelToon). If null, uses default UI material.")]
    public Material outputMaterial;

    Camera cam;
    RenderTexture rt;
    int lastW = 0, lastH = 0;

    void Start()
    {
        cam = GetComponent<Camera>();
        EnsureOutputRawImage();
        CreateOrUpdateRT();
    }

    void Update()
    {
        int w, h;
        if (targetPixelHeight > 0)
        {
            h = Mathf.Max(1, targetPixelHeight);
            w = Mathf.Max(1, Mathf.RoundToInt(h * (Screen.width / (float)Screen.height)));
        }
        else
        {
            int scale = Mathf.Max(1, pixelScale);
            w = Mathf.Max(1, Screen.width / scale);
            h = Mathf.Max(1, Screen.height / scale);
        }

        if (w != lastW || h != lastH)
        {
            CreateOrUpdateRT(w, h);
        }
    }

    void EnsureOutputRawImage()
    {
        if (outputRawImage != null) return;
        // try find in scene
        var existing = GameObject.Find("LowResRawImage");
        if (existing != null)
        {
            outputRawImage = existing.GetComponent<RawImage>();
            if (outputRawImage != null) return;
        }

        // create Canvas + RawImage
        var canvasGO = new GameObject("LowResCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var rawGO = new GameObject("LowResRawImage");
        rawGO.transform.SetParent(canvasGO.transform, false);
        var raw = rawGO.AddComponent<RawImage>();
        var rect = raw.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        outputRawImage = raw;
    }

    void CreateOrUpdateRT()
    {
        int w, h;
        if (targetPixelHeight > 0)
        {
            h = Mathf.Max(1, targetPixelHeight);
            w = Mathf.Max(1, Mathf.RoundToInt(h * (Screen.width / (float)Screen.height)));
        }
        else
        {
            int scale = Mathf.Max(1, pixelScale);
            w = Mathf.Max(1, Screen.width / scale);
            h = Mathf.Max(1, Screen.height / scale);
        }
        CreateOrUpdateRT(w, h);
    }

    void CreateOrUpdateRT(int w, int h)
    {
        // clean up old
        if (rt != null)
        {
            cam.targetTexture = null;
            rt.Release();
            Destroy(rt);
            rt = null;
        }

        rt = new RenderTexture(w, h, 24)
        {
            filterMode = FilterMode.Point,
            antiAliasing = 1,
            useMipMap = false,
            autoGenerateMips = false
        };
        rt.Create();

        cam.targetTexture = rt;
        lastW = w; lastH = h;

        if (outputRawImage != null)
        {
            outputRawImage.texture = rt;
            if (outputMaterial != null)
            {
                // assign a copy of the material so multiple cameras can have different params
                var mat = new Material(outputMaterial);
                outputRawImage.material = mat;
                // ensure the texture is assigned to the material as well
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", rt);
            }
            else
            {
                // clear material so RawImage uses default but ensure texture is set
                outputRawImage.material = null;
            }
        }
    }

    void OnDisable()
    {
        if (cam != null) cam.targetTexture = null;
        if (rt != null)
        {
            rt.Release();
            Destroy(rt);
            rt = null;
        }
    }

    void OnDestroy()
    {
        OnDisable();
    }
}
