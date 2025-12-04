using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Helper script to quickly build a runtime UI for VoronoiFracture2D.
/// This is a development tool - you can use it to generate a UI template,
/// then customize it to your needs.
/// </summary>
public class VoronoiFractureUIBuilder : MonoBehaviour
{
#if UNITY_EDITOR
    [Header("Build Settings")]
    [Tooltip("The VoronoiFracture2D to control")]
    public VoronoiFracture2D targetFracture;

    [Tooltip("Include all advanced settings")]
    public bool includeAdvancedSettings = true;

    [Tooltip("Use TextMeshPro (requires TMP package)")]
    public bool useTextMeshPro = true;

    [Header("UI Style")]
    public Color panelColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
    public Color buttonColor = new Color(0.3f, 0.6f, 0.9f, 1f);
    public Color sliderColor = new Color(0.4f, 0.7f, 1f, 1f);

    [ContextMenu("Build Simple UI")]
    public void BuildSimpleUI()
    {
        BuildUI(false);
    }

    [ContextMenu("Build Full UI")]
    public void BuildFullUI()
    {
        BuildUI(true);
    }

    private void BuildUI(bool fullUI)
    {
        if (targetFracture == null)
        {
            Debug.LogError("Please assign a VoronoiFracture2D target first!");
            return;
        }

        // Create or find Canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var canvasGO = new GameObject("Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            
            // Create EventSystem if needed
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var eventSystemGO = new GameObject("EventSystem");
                eventSystemGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
            
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");
        }

        // Create main panel
        var panelGO = new GameObject("FractureControlPanel");
        panelGO.transform.SetParent(canvas.transform, false);
        Undo.RegisterCreatedObjectUndo(panelGO, "Create Fracture UI");

        var rectTransform = panelGO.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.02f, 0.02f);
        rectTransform.anchorMax = new Vector2(0.35f, 0.98f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        var panelImage = panelGO.AddComponent<Image>();
        panelImage.color = panelColor;

        var verticalLayout = panelGO.AddComponent<VerticalLayoutGroup>();
        verticalLayout.padding = new RectOffset(10, 10, 10, 10);
        verticalLayout.spacing = 5;
        verticalLayout.childForceExpandHeight = false;
        verticalLayout.childControlHeight = true;

        // Add UI controller component
        var uiController = panelGO.AddComponent<VoronoiFractureUI>();
        uiController.targetFracture = targetFracture;

        // Build UI elements
        CreateTitle(panelGO, "Fracture Controls");

        if (fullUI)
        {
            CreateSection(panelGO, "Fracture Settings");
            uiController.siteCountSlider = CreateSlider(panelGO, "Site Count", 3, 50, targetFracture.siteCount, true, out uiController.siteCountText);
            uiController.siteJitterSlider = CreateSlider(panelGO, "Site Jitter", 0, 1, targetFracture.siteJitter, false, out uiController.siteJitterText);
            uiController.randomSeedSlider = CreateSlider(panelGO, "Random Seed", 0, 99999, targetFracture.randomSeed, true, out uiController.randomSeedText);

            CreateSection(panelGO, "Runtime Fracture");
            uiController.enableRuntimeFractureToggle = CreateToggle(panelGO, "Enable Runtime Fracture", targetFracture.enableRuntimeFracture);
            uiController.breakImpactThresholdSlider = CreateSlider(panelGO, "Impact Threshold", 1, 20, targetFracture.breakImpactThreshold, false, out uiController.breakImpactThresholdText);
            
            if (includeAdvancedSettings)
            {
                uiController.runtimeSiteCountSlider = CreateSlider(panelGO, "Runtime Sites", 3, 30, targetFracture.runtimeSiteCount, true, out uiController.runtimeSiteCountText);
                uiController.runtimeBreakDepthSlider = CreateSlider(panelGO, "Break Depth", 0, 5, targetFracture.runtimeBreakDepth, true, out uiController.runtimeBreakDepthText);
            }

            CreateSection(panelGO, "Overlay");
            uiController.generateOverlayToggle = CreateToggle(panelGO, "Generate Overlay", targetFracture.generateOverlay);
            uiController.overlayTextureSizeSlider = CreateSlider(panelGO, "Texture Size", 64, 2048, targetFracture.overlayTextureSize, true, out uiController.overlayTextureSizeText);
        }

        CreateSection(panelGO, "Actions");
        uiController.fractureButton = CreateButton(panelGO, "Fracture!");
        uiController.clearFragmentsButton = CreateButton(panelGO, "Clear Fragments");
        if (fullUI)
        {
            uiController.resetToDefaultButton = CreateButton(panelGO, "Reset to Defaults");
        }

        Debug.Log("Fracture UI created successfully!");
    }

    private void CreateTitle(GameObject parent, string text)
    {
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(parent.transform, false);

        var rectTransform = titleGO.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(0, 40);

        if (useTextMeshPro)
        {
            var tmp = titleGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 24;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
        }
        else
        {
            var textComp = titleGO.AddComponent<Text>();
            textComp.text = text;
            textComp.fontSize = 20;
            textComp.fontStyle = FontStyle.Bold;
            textComp.alignment = TextAnchor.MiddleCenter;
            textComp.color = Color.white;
        }

        var layoutElement = titleGO.AddComponent<LayoutElement>();
        layoutElement.minHeight = 40;
    }

    private void CreateSection(GameObject parent, string text)
    {
        var sectionGO = new GameObject($"Section_{text}");
        sectionGO.transform.SetParent(parent.transform, false);

        var rectTransform = sectionGO.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(0, 25);

        if (useTextMeshPro)
        {
            var tmp = sectionGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 16;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.8f, 0.9f, 1f);
        }
        else
        {
            var textComp = sectionGO.AddComponent<Text>();
            textComp.text = text;
            textComp.fontSize = 14;
            textComp.fontStyle = FontStyle.Bold;
            textComp.color = new Color(0.8f, 0.9f, 1f);
        }

        var layoutElement = sectionGO.AddComponent<LayoutElement>();
        layoutElement.minHeight = 25;
    }

    private Slider CreateSlider(GameObject parent, string label, float min, float max, float value, bool wholeNumbers, out TextMeshProUGUI textComponent)
    {
        var sliderContainerGO = new GameObject($"Slider_{label}");
        sliderContainerGO.transform.SetParent(parent.transform, false);

        var containerRect = sliderContainerGO.AddComponent<RectTransform>();
        containerRect.sizeDelta = new Vector2(0, 50);

        var vertLayout = sliderContainerGO.AddComponent<VerticalLayoutGroup>();
        vertLayout.spacing = 2;
        vertLayout.childForceExpandHeight = false;

        // Create label
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(sliderContainerGO.transform, false);
        
        if (useTextMeshPro)
        {
            textComponent = labelGO.AddComponent<TextMeshProUGUI>();
            textComponent.text = $"{label}: {value}";
            textComponent.fontSize = 14;
        }
        else
        {
            var textComp = labelGO.AddComponent<Text>();
            textComp.text = $"{label}: {value}";
            textComp.fontSize = 12;
            textComp.color = Color.white;
            textComponent = null; // Can't assign Text to TextMeshProUGUI
        }

        // Create slider
        var sliderGO = new GameObject("Slider");
        sliderGO.transform.SetParent(sliderContainerGO.transform, false);

        var slider = sliderGO.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;
        slider.wholeNumbers = wholeNumbers;

        // Slider visuals
        var sliderRect = sliderGO.GetComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(0, 20);

        // Background
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(sliderGO.transform, false);
        var bgImage = bgGO.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // Fill Area
        var fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        var fillAreaRect = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.sizeDelta = new Vector2(-20, 0);
        fillAreaRect.anchoredPosition = Vector2.zero;

        // Fill
        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        var fillImage = fillGO.AddComponent<Image>();
        fillImage.color = sliderColor;
        var fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;

        slider.fillRect = fillRect;

        // Handle Slide Area
        var handleAreaGO = new GameObject("Handle Slide Area");
        handleAreaGO.transform.SetParent(sliderGO.transform, false);
        var handleAreaRect = handleAreaGO.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.sizeDelta = new Vector2(-20, 0);

        // Handle
        var handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(handleAreaGO.transform, false);
        var handleImage = handleGO.AddComponent<Image>();
        handleImage.color = Color.white;
        var handleRect = handleGO.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20, 20);

        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;

        var layoutElement = sliderContainerGO.AddComponent<LayoutElement>();
        layoutElement.minHeight = 50;

        return slider;
    }

    private Toggle CreateToggle(GameObject parent, string label, bool value)
    {
        var toggleGO = new GameObject($"Toggle_{label}");
        toggleGO.transform.SetParent(parent.transform, false);

        var toggleRect = toggleGO.AddComponent<RectTransform>();
        toggleRect.sizeDelta = new Vector2(0, 30);

        var horizontalLayout = toggleGO.AddComponent<HorizontalLayoutGroup>();
        horizontalLayout.childForceExpandWidth = false;
        horizontalLayout.childControlWidth = false;
        horizontalLayout.spacing = 10;

        var toggle = toggleGO.AddComponent<Toggle>();
        toggle.isOn = value;

        // Background
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(toggleGO.transform, false);
        var bgImage = bgGO.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(20, 20);

        toggle.targetGraphic = bgImage;

        // Checkmark
        var checkGO = new GameObject("Checkmark");
        checkGO.transform.SetParent(bgGO.transform, false);
        var checkImage = checkGO.AddComponent<Image>();
        checkImage.color = sliderColor;
        var checkRect = checkGO.GetComponent<RectTransform>();
        checkRect.anchorMin = Vector2.zero;
        checkRect.anchorMax = Vector2.one;
        checkRect.sizeDelta = new Vector2(-6, -6);

        toggle.graphic = checkImage;

        // Label
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(toggleGO.transform, false);
        
        if (useTextMeshPro)
        {
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 14;
        }
        else
        {
            var textComp = labelGO.AddComponent<Text>();
            textComp.text = label;
            textComp.fontSize = 12;
            textComp.color = Color.white;
        }

        var layoutElement = toggleGO.AddComponent<LayoutElement>();
        layoutElement.minHeight = 30;

        return toggle;
    }

    private Button CreateButton(GameObject parent, string label)
    {
        var buttonGO = new GameObject($"Button_{label}");
        buttonGO.transform.SetParent(parent.transform, false);

        var buttonRect = buttonGO.AddComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(0, 40);

        var button = buttonGO.AddComponent<Button>();
        var buttonImage = buttonGO.AddComponent<Image>();
        buttonImage.color = buttonColor;
        button.targetGraphic = buttonImage;

        var labelGO = new GameObject("Text");
        labelGO.transform.SetParent(buttonGO.transform, false);

        if (useTextMeshPro)
        {
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 16;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
        }
        else
        {
            var textComp = labelGO.AddComponent<Text>();
            textComp.text = label;
            textComp.fontSize = 14;
            textComp.alignment = TextAnchor.MiddleCenter;
            textComp.color = Color.white;
        }

        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;

        var layoutElement = buttonGO.AddComponent<LayoutElement>();
        layoutElement.minHeight = 40;

        return button;
    }
#endif
}
