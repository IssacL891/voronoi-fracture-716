using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Runtime UI controller for VoronoiFracture2D.
/// Provides sliders, buttons, and toggles to control fracture parameters during gameplay.
/// Works with both Unity UI and TextMeshPro.
/// </summary>
public class VoronoiFractureUI : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The VoronoiFracture2D component to control")]
    public VoronoiFracture2D targetFracture;

    [Header("UI References - Fracture Settings")]
    public Slider siteCountSlider;
    public TextMeshProUGUI siteCountText; // or Text if not using TMP

    public Slider siteJitterSlider;
    public TextMeshProUGUI siteJitterText;

    public Slider randomSeedSlider;
    public TextMeshProUGUI randomSeedText;

    [Header("UI References - Runtime Fracture")]
    public Toggle enableRuntimeFractureToggle;

    public Slider breakImpactThresholdSlider;
    public TextMeshProUGUI breakImpactThresholdText;

    public Slider runtimeSiteCountSlider;
    public TextMeshProUGUI runtimeSiteCountText;

    public Slider runtimeBreakDepthSlider;
    public TextMeshProUGUI runtimeBreakDepthText;

    [Header("UI References - Overlay")]
    public Toggle generateOverlayToggle;

    public Slider overlayTextureSizeSlider;
    public TextMeshProUGUI overlayTextureSizeText;

    [Header("UI References - Performance")]
    public Toggle spreadFractureOverFramesToggle;

    public Slider fragmentsPerFrameSlider;
    public TextMeshProUGUI fragmentsPerFrameText;

    public Toggle useTimeBudgetingToggle;

    public Slider timeBudgetMsSlider;
    public TextMeshProUGUI timeBudgetMsText;

    [Header("UI References - Actions")]
    public Button fractureButton;
    public Button clearFragmentsButton;
    public Button resetToDefaultButton;

    [Header("Default Values")]
    public int defaultSiteCount = 8;
    public float defaultSiteJitter = 0.2f;
    public int defaultRandomSeed = 12345;
    public bool defaultEnableRuntimeFracture = true;
    public float defaultBreakImpactThreshold = 5f;
    public int defaultRuntimeSiteCount = 6;
    public int defaultRuntimeBreakDepth = 1;
    public bool defaultGenerateOverlay = true;
    public int defaultOverlayTextureSize = 512;
    public bool defaultSpreadFractureOverFrames = false;
    public int defaultFragmentsPerFrame = 6;
    public bool defaultUseTimeBudgeting = false;
    public int defaultTimeBudgetMs = 8;

    void Start()
    {
        if (targetFracture == null)
        {
            targetFracture = FindFirstObjectByType<VoronoiFracture2D>();
            if (targetFracture == null)
            {
                Debug.LogWarning("VoronoiFractureUI: No VoronoiFracture2D component found.");
                return;
            }
        }

        SetupUI();
        SyncUIFromTarget();
    }

    /// <summary>
    /// Initialize UI element listeners and ranges.
    /// </summary>
    void SetupUI()
    {
        // Site Count
        if (siteCountSlider != null)
        {
            siteCountSlider.minValue = 3;
            siteCountSlider.maxValue = 50;
            siteCountSlider.wholeNumbers = true;
            siteCountSlider.onValueChanged.AddListener(OnSiteCountChanged);
        }

        // Site Jitter
        if (siteJitterSlider != null)
        {
            siteJitterSlider.minValue = 0f;
            siteJitterSlider.maxValue = 1f;
            siteJitterSlider.onValueChanged.AddListener(OnSiteJitterChanged);
        }

        // Random Seed
        if (randomSeedSlider != null)
        {
            randomSeedSlider.minValue = 0;
            randomSeedSlider.maxValue = 99999;
            randomSeedSlider.wholeNumbers = true;
            randomSeedSlider.onValueChanged.AddListener(OnRandomSeedChanged);
        }

        // Enable Runtime Fracture
        if (enableRuntimeFractureToggle != null)
        {
            enableRuntimeFractureToggle.onValueChanged.AddListener(OnEnableRuntimeFractureChanged);
        }

        // Break Impact Threshold
        if (breakImpactThresholdSlider != null)
        {
            breakImpactThresholdSlider.minValue = 1f;
            breakImpactThresholdSlider.maxValue = 20f;
            breakImpactThresholdSlider.onValueChanged.AddListener(OnBreakImpactThresholdChanged);
        }

        // Runtime Site Count
        if (runtimeSiteCountSlider != null)
        {
            runtimeSiteCountSlider.minValue = 3;
            runtimeSiteCountSlider.maxValue = 30;
            runtimeSiteCountSlider.wholeNumbers = true;
            runtimeSiteCountSlider.onValueChanged.AddListener(OnRuntimeSiteCountChanged);
        }

        // Runtime Break Depth
        if (runtimeBreakDepthSlider != null)
        {
            runtimeBreakDepthSlider.minValue = 0;
            runtimeBreakDepthSlider.maxValue = 5;
            runtimeBreakDepthSlider.wholeNumbers = true;
            runtimeBreakDepthSlider.onValueChanged.AddListener(OnRuntimeBreakDepthChanged);
        }

        // Generate Overlay
        if (generateOverlayToggle != null)
        {
            generateOverlayToggle.onValueChanged.AddListener(OnGenerateOverlayChanged);
        }

        // Overlay Texture Size
        if (overlayTextureSizeSlider != null)
        {
            overlayTextureSizeSlider.minValue = 64;
            overlayTextureSizeSlider.maxValue = 2048;
            overlayTextureSizeSlider.wholeNumbers = true;
            overlayTextureSizeSlider.onValueChanged.AddListener(OnOverlayTextureSizeChanged);
        }

        // Spread Fracture Over Frames
        if (spreadFractureOverFramesToggle != null)
        {
            spreadFractureOverFramesToggle.onValueChanged.AddListener(OnSpreadFractureOverFramesChanged);
        }

        // Fragments Per Frame
        if (fragmentsPerFrameSlider != null)
        {
            fragmentsPerFrameSlider.minValue = 1;
            fragmentsPerFrameSlider.maxValue = 20;
            fragmentsPerFrameSlider.wholeNumbers = true;
            fragmentsPerFrameSlider.onValueChanged.AddListener(OnFragmentsPerFrameChanged);
        }

        // Use Time Budgeting
        if (useTimeBudgetingToggle != null)
        {
            useTimeBudgetingToggle.onValueChanged.AddListener(OnUseTimeBudgetingChanged);
        }

        // Time Budget Ms
        if (timeBudgetMsSlider != null)
        {
            timeBudgetMsSlider.minValue = 1;
            timeBudgetMsSlider.maxValue = 50;
            timeBudgetMsSlider.wholeNumbers = true;
            timeBudgetMsSlider.onValueChanged.AddListener(OnTimeBudgetMsChanged);
        }

        // Buttons
        if (fractureButton != null)
        {
            fractureButton.onClick.AddListener(OnFractureButtonClicked);
        }

        if (clearFragmentsButton != null)
        {
            clearFragmentsButton.onClick.AddListener(OnClearFragmentsButtonClicked);
        }

        if (resetToDefaultButton != null)
        {
            resetToDefaultButton.onClick.AddListener(OnResetToDefaultButtonClicked);
        }
    }

    /// <summary>
    /// Update UI to reflect current target fracture settings.
    /// </summary>
    public void SyncUIFromTarget()
    {
        if (targetFracture == null) return;

        // Remove listeners temporarily to avoid triggering callbacks
        RemoveListeners();

        // Update UI values
        if (siteCountSlider != null) siteCountSlider.value = targetFracture.siteCount;
        if (siteJitterSlider != null) siteJitterSlider.value = targetFracture.siteJitter;
        if (randomSeedSlider != null) randomSeedSlider.value = targetFracture.randomSeed;
        if (enableRuntimeFractureToggle != null) enableRuntimeFractureToggle.isOn = targetFracture.enableRuntimeFracture;
        if (breakImpactThresholdSlider != null) breakImpactThresholdSlider.value = targetFracture.breakImpactThreshold;
        if (runtimeSiteCountSlider != null) runtimeSiteCountSlider.value = targetFracture.runtimeSiteCount;
        if (runtimeBreakDepthSlider != null) runtimeBreakDepthSlider.value = targetFracture.runtimeBreakDepth;
        if (generateOverlayToggle != null) generateOverlayToggle.isOn = targetFracture.generateOverlay;
        if (overlayTextureSizeSlider != null) overlayTextureSizeSlider.value = targetFracture.overlayTextureSize;
        if (spreadFractureOverFramesToggle != null) spreadFractureOverFramesToggle.isOn = targetFracture.spreadFractureOverFrames;
        if (fragmentsPerFrameSlider != null) fragmentsPerFrameSlider.value = targetFracture.fragmentsPerFrame;
        if (useTimeBudgetingToggle != null) useTimeBudgetingToggle.isOn = targetFracture.useTimeBudgeting;
        if (timeBudgetMsSlider != null) timeBudgetMsSlider.value = targetFracture.timeBudgetMs;

        // Update text labels
        UpdateAllLabels();

        // Re-add listeners
        SetupUI();
    }

    void UpdateAllLabels()
    {
        if (siteCountText != null) siteCountText.text = $"Site Count: {targetFracture.siteCount}";
        if (siteJitterText != null) siteJitterText.text = $"Site Jitter: {targetFracture.siteJitter:F2}";
        if (randomSeedText != null) randomSeedText.text = $"Random Seed: {targetFracture.randomSeed}";
        if (breakImpactThresholdText != null) breakImpactThresholdText.text = $"Impact Threshold: {targetFracture.breakImpactThreshold:F1}";
        if (runtimeSiteCountText != null) runtimeSiteCountText.text = $"Runtime Sites: {targetFracture.runtimeSiteCount}";
        if (runtimeBreakDepthText != null) runtimeBreakDepthText.text = $"Break Depth: {targetFracture.runtimeBreakDepth}";
        if (overlayTextureSizeText != null) overlayTextureSizeText.text = $"Texture Size: {targetFracture.overlayTextureSize}";
        if (fragmentsPerFrameText != null) fragmentsPerFrameText.text = $"Fragments/Frame: {targetFracture.fragmentsPerFrame}";
        if (timeBudgetMsText != null) timeBudgetMsText.text = $"Time Budget: {targetFracture.timeBudgetMs}ms";
    }

    void RemoveListeners()
    {
        if (siteCountSlider != null) siteCountSlider.onValueChanged.RemoveAllListeners();
        if (siteJitterSlider != null) siteJitterSlider.onValueChanged.RemoveAllListeners();
        if (randomSeedSlider != null) randomSeedSlider.onValueChanged.RemoveAllListeners();
        if (enableRuntimeFractureToggle != null) enableRuntimeFractureToggle.onValueChanged.RemoveAllListeners();
        if (breakImpactThresholdSlider != null) breakImpactThresholdSlider.onValueChanged.RemoveAllListeners();
        if (runtimeSiteCountSlider != null) runtimeSiteCountSlider.onValueChanged.RemoveAllListeners();
        if (runtimeBreakDepthSlider != null) runtimeBreakDepthSlider.onValueChanged.RemoveAllListeners();
        if (generateOverlayToggle != null) generateOverlayToggle.onValueChanged.RemoveAllListeners();
        if (overlayTextureSizeSlider != null) overlayTextureSizeSlider.onValueChanged.RemoveAllListeners();
        if (spreadFractureOverFramesToggle != null) spreadFractureOverFramesToggle.onValueChanged.RemoveAllListeners();
        if (fragmentsPerFrameSlider != null) fragmentsPerFrameSlider.onValueChanged.RemoveAllListeners();
        if (useTimeBudgetingToggle != null) useTimeBudgetingToggle.onValueChanged.RemoveAllListeners();
        if (timeBudgetMsSlider != null) timeBudgetMsSlider.onValueChanged.RemoveAllListeners();
    }

    // Slider/Toggle Callbacks
    void OnSiteCountChanged(float value)
    {
        targetFracture.siteCount = (int)value;
        if (siteCountText != null) siteCountText.text = $"Site Count: {(int)value}";
    }

    void OnSiteJitterChanged(float value)
    {
        targetFracture.siteJitter = value;
        if (siteJitterText != null) siteJitterText.text = $"Site Jitter: {value:F2}";
    }

    void OnRandomSeedChanged(float value)
    {
        targetFracture.randomSeed = (int)value;
        if (randomSeedText != null) randomSeedText.text = $"Random Seed: {(int)value}";
    }

    void OnEnableRuntimeFractureChanged(bool value)
    {
        targetFracture.enableRuntimeFracture = value;
    }

    void OnBreakImpactThresholdChanged(float value)
    {
        targetFracture.breakImpactThreshold = value;
        if (breakImpactThresholdText != null) breakImpactThresholdText.text = $"Impact Threshold: {value:F1}";
    }

    void OnRuntimeSiteCountChanged(float value)
    {
        targetFracture.runtimeSiteCount = (int)value;
        if (runtimeSiteCountText != null) runtimeSiteCountText.text = $"Runtime Sites: {(int)value}";
    }

    void OnRuntimeBreakDepthChanged(float value)
    {
        targetFracture.runtimeBreakDepth = (int)value;
        if (runtimeBreakDepthText != null) runtimeBreakDepthText.text = $"Break Depth: {(int)value}";
    }

    void OnGenerateOverlayChanged(bool value)
    {
        targetFracture.generateOverlay = value;
    }

    void OnOverlayTextureSizeChanged(float value)
    {
        targetFracture.overlayTextureSize = (int)value;
        if (overlayTextureSizeText != null) overlayTextureSizeText.text = $"Texture Size: {(int)value}";
    }

    void OnSpreadFractureOverFramesChanged(bool value)
    {
        targetFracture.spreadFractureOverFrames = value;
    }

    void OnFragmentsPerFrameChanged(float value)
    {
        targetFracture.fragmentsPerFrame = (int)value;
        if (fragmentsPerFrameText != null) fragmentsPerFrameText.text = $"Fragments/Frame: {(int)value}";
    }

    void OnUseTimeBudgetingChanged(bool value)
    {
        targetFracture.useTimeBudgeting = value;
    }

    void OnTimeBudgetMsChanged(float value)
    {
        targetFracture.timeBudgetMs = (int)value;
        if (timeBudgetMsText != null) timeBudgetMsText.text = $"Time Budget: {(int)value}ms";
    }

    // Button Callbacks
    void OnFractureButtonClicked()
    {
        if (targetFracture != null)
        {
            targetFracture.Fracture();
        }
    }

    void OnClearFragmentsButtonClicked()
    {
        ClearFragments();
    }

    void OnResetToDefaultButtonClicked()
    {
        ResetToDefaults();
    }

    /// <summary>
    /// Clear all fragments and restore the original object.
    /// </summary>
    public void ClearFragments()
    {
        if (targetFracture == null) return;

        // Find and destroy fragment parent
        var fragmentName = $"Fragments_{targetFracture.gameObject.name}_{targetFracture.GetInstanceID()}";
        var existing = GameObject.Find(fragmentName);
        if (existing != null)
        {
            Destroy(existing);
        }

        // Restore original object
        targetFracture.gameObject.SetActive(true);
    }

    /// <summary>
    /// Reset all parameters to default values.
    /// </summary>
    public void ResetToDefaults()
    {
        if (targetFracture == null) return;

        targetFracture.siteCount = defaultSiteCount;
        targetFracture.siteJitter = defaultSiteJitter;
        targetFracture.randomSeed = defaultRandomSeed;
        targetFracture.enableRuntimeFracture = defaultEnableRuntimeFracture;
        targetFracture.breakImpactThreshold = defaultBreakImpactThreshold;
        targetFracture.runtimeSiteCount = defaultRuntimeSiteCount;
        targetFracture.runtimeBreakDepth = defaultRuntimeBreakDepth;
        targetFracture.generateOverlay = defaultGenerateOverlay;
        targetFracture.overlayTextureSize = defaultOverlayTextureSize;
        targetFracture.spreadFractureOverFrames = defaultSpreadFractureOverFrames;
        targetFracture.fragmentsPerFrame = defaultFragmentsPerFrame;
        targetFracture.useTimeBudgeting = defaultUseTimeBudgeting;
        targetFracture.timeBudgetMs = defaultTimeBudgetMs;

        SyncUIFromTarget();
    }

    /// <summary>
    /// Randomize the random seed value.
    /// </summary>
    public void RandomizeSeed()
    {
        targetFracture.randomSeed = Random.Range(0, 100000);
        if (randomSeedSlider != null) randomSeedSlider.value = targetFracture.randomSeed;
        if (randomSeedText != null) randomSeedText.text = $"Random Seed: {targetFracture.randomSeed}";
    }
}
