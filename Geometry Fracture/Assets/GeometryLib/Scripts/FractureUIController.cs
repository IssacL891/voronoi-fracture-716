using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// UI Controller for FracturePrefabManager using UI Toolkit.
/// Displays prefab selection and parameter controls.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class FractureUIController : MonoBehaviour
{
    private static FractureUIController _instance;
    [Header("References")]
    public FracturePrefabManager prefabManager;

    [Header("UI Settings")]
    public bool autoRefreshOnChange = true;
    public float refreshInterval = 0.1f;

    private UIDocument _uiDocument;
    private VisualElement _root;

    // UI Elements
    private Button _toggleButton;
    private VisualElement _panel;
    private DropdownField _prefabDropdown;
    private Label _selectedPrefabLabel;
    private SliderInt _siteCountSlider;
    private Slider _siteJitterSlider;
    private Toggle _enableRuntimeFractureToggle;
    private Slider _breakImpactThresholdSlider;
    private Toggle _waitForCollisionToggle;
    private SliderInt _runtimeSiteCountSlider;
    private SliderInt _runtimeBreakDepthSlider;
    private Button _spawnButton;
    private Button _clearButton;
    private Button _prevButton;
    private Button _nextButton;
    
    // Time Rewinder UI Elements
    private Label _rewindTimeLabel;
    private Slider _rewindSlider;
    private Button _pauseTimeButton;
    private Button _resetTimeButton;
    private bool _isTimePaused = false;
    private bool _isSliderBeingDragged = false;
    private bool _wasTimePausedBeforeRewind = false;

    private float _lastRefreshTime;
    private int _lastSelectedIndex = -1;
    private bool _isPanelVisible = true;

    private void Awake()
    {
        // Ensure only one active instance to avoid overlapping duplicate UIs
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("FractureUIController: Another instance already exists. Disabling this duplicate.");
            gameObject.SetActive(false);
            return;
        }
        _instance = this;
    }

    private void OnEnable()
    {
        _uiDocument = GetComponent<UIDocument>();
        if (_uiDocument == null)
        {
            Debug.LogError("FractureUIController: UIDocument component not found!");
            return;
        }

        _root = _uiDocument.rootVisualElement;
        BindUIElements();
        SetupCallbacks();
        RefreshUI();
        UpdatePanelVisibility();
        
        // CRITICAL: Check if RewindManager exists
        if (RewindManager.Instance == null)
        {
            Debug.LogError("FractureUIController: RewindManager.Instance is NULL! Time rewinder will NOT work. Make sure RewindManager GameObject exists in scene!");
        }
        else
        {
            Debug.Log($"FractureUIController: RewindManager found! HowManySecondsToTrack = {RewindManager.Instance.HowManySecondsToTrack}");
        }
    }

    private void OnDisable()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void Update()
    {
        if (!autoRefreshOnChange) return;
        if (prefabManager == null) return;

        // Check if selection changed
        if (_lastSelectedIndex != prefabManager.selectedPrefabIndex)
        {
            RefreshUI();
            _lastSelectedIndex = prefabManager.selectedPrefabIndex;
        }
        
        // Update label to show available rewind time
        if (_rewindTimeLabel != null && RewindManager.Instance != null)
        {
            float available = RewindManager.Instance.HowManySecondsAvailableForRewind;
            _rewindTimeLabel.text = $"Available: {available:F1}s";
        }
        
        // CRITICAL: Detect mouse release while slider is being dragged
        // UI Toolkit's PointerCaptureOutEvent doesn't fire reliably, so we use Input System directly
        if (_isSliderBeingDragged && Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            Debug.Log("Mouse button released - stopping rewind via Update()");
            StopSliderRewind();
        }
    }

    private void BindUIElements()
    {
        // Bind UI elements
        _toggleButton = _root.Q<Button>("ToggleButton");
        _panel = _root.Q<VisualElement>("Panel");
        _prefabDropdown = _root.Q<DropdownField>("PrefabDropdown");
        _selectedPrefabLabel = _root.Q<Label>("SelectedPrefabLabel");
        _siteCountSlider = _root.Q<SliderInt>("SiteCountSlider");
        _siteJitterSlider = _root.Q<Slider>("SiteJitterSlider");
        _enableRuntimeFractureToggle = _root.Q<Toggle>("EnableRuntimeFractureToggle");
        _breakImpactThresholdSlider = _root.Q<Slider>("BreakImpactThresholdSlider");
        _waitForCollisionToggle = _root.Q<Toggle>("WaitForCollisionToggle");
        _runtimeSiteCountSlider = _root.Q<SliderInt>("RuntimeSiteCountSlider");
        _runtimeBreakDepthSlider = _root.Q<SliderInt>("RuntimeBreakDepthSlider");
        _spawnButton = _root.Q<Button>("SpawnButton");
        _clearButton = _root.Q<Button>("ClearButton");
        _prevButton = _root.Q<Button>("PrevButton");
        _nextButton = _root.Q<Button>("NextButton");
        
        // Time Rewinder bindings
        _rewindTimeLabel = _root.Q<Label>("RewindTimeLabel");
        _rewindSlider = _root.Q<Slider>("RewindSlider");
        _pauseTimeButton = _root.Q<Button>("PauseTimeButton");
        _resetTimeButton = _root.Q<Button>("ResetTimeButton");
    }
    
    /// <summary>
    /// Stops the slider-based rewind and commits the final state.
    /// Called when user releases the mouse button.
    /// </summary>
    private void StopSliderRewind()
    {
        if (!_isSliderBeingDragged) return;
        
        bool wasDragging = _isSliderBeingDragged;
        _isSliderBeingDragged = false;
        
        if (RewindManager.Instance == null) return;
        
        // Stop the rewind if we're actually rewinding
        if (RewindManager.Instance.IsBeingRewinded)
        {
            RewindManager.Instance.StopRewindTimeBySeconds();
            Debug.Log("Rewind stopped and committed");
        }
        
        // Re-enable tracking
        RewindManager.Instance.TrackingEnabled = true;
        
        // Restore time paused state if it was paused before
        if (_wasTimePausedBeforeRewind && wasDragging)
        {
            Time.timeScale = 0f;
            RewindManager.Instance.TrackingEnabled = false;
            Debug.Log("Restored paused state");
        }
        
        // Reset flags
        _wasTimePausedBeforeRewind = false;
        
        // Reset slider to 0 after committing
        if (_rewindSlider != null)
        {
            _rewindSlider.SetValueWithoutNotify(0f);
        }
        
        Debug.Log("Slider state fully reset, ready for next rewind");
    }

    private void SetupCallbacks()
    {
        if (_toggleButton != null)
        {
            _toggleButton.clicked += () =>
            {
                _isPanelVisible = !_isPanelVisible;
                UpdatePanelVisibility();
            };
        }

        if (_prefabDropdown != null)
        {
            _prefabDropdown.RegisterValueChangedCallback(evt =>
            {
                int index = _prefabDropdown.choices.IndexOf(evt.newValue);
                if (index >= 0 && prefabManager != null)
                {
                    prefabManager.SelectPrefab(index);
                    RefreshUI();
                }
            });
        }

        if (_siteCountSlider != null)
        {
            _siteCountSlider.RegisterValueChangedCallback(evt =>
            {
                prefabManager?.UpdateCurrentPrefabSettings(siteCount: evt.newValue);
            });
        }

        if (_siteJitterSlider != null)
        {
            _siteJitterSlider.RegisterValueChangedCallback(evt =>
            {
                prefabManager?.UpdateCurrentPrefabSettings(siteJitter: evt.newValue);
            });
        }

        if (_enableRuntimeFractureToggle != null)
        {
            _enableRuntimeFractureToggle.RegisterValueChangedCallback(evt =>
            {
                prefabManager?.UpdateCurrentPrefabSettings(enableRuntimeFracture: evt.newValue);
            });
        }

        if (_breakImpactThresholdSlider != null)
        {
            _breakImpactThresholdSlider.RegisterValueChangedCallback(evt =>
            {
                prefabManager?.UpdateCurrentPrefabSettings(breakImpactThreshold: evt.newValue);
            });
        }

        if (_waitForCollisionToggle != null)
        {
            _waitForCollisionToggle.RegisterValueChangedCallback(evt =>
            {
                prefabManager?.UpdateCurrentPrefabSettings(waitForCollision: evt.newValue);
            });
        }

        if (_runtimeSiteCountSlider != null)
        {
            _runtimeSiteCountSlider.RegisterValueChangedCallback(evt =>
            {
                prefabManager?.UpdateCurrentPrefabSettings(runtimeSiteCount: evt.newValue);
            });
        }

        if (_runtimeBreakDepthSlider != null)
        {
            _runtimeBreakDepthSlider.RegisterValueChangedCallback(evt =>
            {
                prefabManager?.UpdateCurrentPrefabSettings(runtimeBreakDepth: evt.newValue);
            });
        }

        if (_spawnButton != null)
        {
            _spawnButton.clicked += () =>
            {
                prefabManager?.SpawnSelected();
            };
        }

        if (_clearButton != null)
        {
            _clearButton.clicked += () =>
            {
                ClearAllFragments();
            };
        }

        // Time Rewinder callbacks - Slider based
        if (_rewindSlider != null)
        {
            // Set slider range from 0 to 12 seconds
            _rewindSlider.lowValue = 0f;
            _rewindSlider.highValue = 12f;
            _rewindSlider.value = 0f;
            
            // Use value change to detect drag start and update preview
            _rewindSlider.RegisterValueChangedCallback(evt =>
            {
                if (RewindManager.Instance == null) return;
                
                float newValue = evt.newValue;
                float available = RewindManager.Instance.HowManySecondsAvailableForRewind;
                float clampedValue = Mathf.Min(newValue, available);
                
                Debug.Log($"Slider value changed: {newValue}, available: {available}, dragging: {_isSliderBeingDragged}, isRewinding: {RewindManager.Instance.IsBeingRewinded}");
                
                // If not dragging yet and value moved from 0, start rewind
                if (!_isSliderBeingDragged && newValue > 0.01f)
                {
                    // Safety reset: if IsBeingRewinded is true but we're not dragging, force stop first
                    if (RewindManager.Instance.IsBeingRewinded)
                    {
                        Debug.Log("Safety reset: stopping previous stuck rewind");
                        RewindManager.Instance.StopRewindTimeBySeconds();
                        RewindManager.Instance.TrackingEnabled = true;
                    }
                    
                    _isSliderBeingDragged = true;
                    _wasTimePausedBeforeRewind = _isTimePaused;
                    
                    // CRITICAL: Time.timeScale MUST be > 0 for rewind preview to work!
                    if (Time.timeScale == 0f)
                    {
                        Time.timeScale = 1f;
                        Debug.Log("Set timeScale to 1 for rewind preview");
                    }
                    
                    // Pause tracking
                    RewindManager.Instance.TrackingEnabled = false;
                    
                    Debug.Log($"Starting rewind at {clampedValue} seconds");
                    RewindManager.Instance.StartRewindTimeBySeconds(clampedValue);
                }
                // If already dragging, just update the position
                else if (_isSliderBeingDragged && RewindManager.Instance.IsBeingRewinded)
                {
                    Debug.Log($"Updating rewind to {clampedValue} seconds");
                    RewindManager.Instance.SetTimeSecondsInRewind(clampedValue);
                }
            });
            
            // Fallback: Detect when slider loses pointer capture
            // Note: This doesn't fire reliably, so we also check Input.GetMouseButtonUp in Update()
            _rewindSlider.RegisterCallback<PointerCaptureOutEvent>(evt =>
            {
                Debug.Log($"PointerCaptureOut detected (fallback), dragging: {_isSliderBeingDragged}");
                StopSliderRewind();
            });
        }

        if (_pauseTimeButton != null)
        {
            _pauseTimeButton.clicked += () =>
            {
                if (RewindManager.Instance == null) return;
                
                _isTimePaused = !_isTimePaused;
                
                if (_isTimePaused)
                {
                    RewindManager.Instance.TrackingEnabled = false;
                    Time.timeScale = 0f;
                    _pauseTimeButton.text = "Resume Time";
                    Debug.Log("Time paused");
                }
                else
                {
                    RewindManager.Instance.TrackingEnabled = true;
                    Time.timeScale = 1f;
                    _pauseTimeButton.text = "Pause Time";
                    Debug.Log("Time resumed");
                }
            };
        }

        if (_resetTimeButton != null)
        {
            _resetTimeButton.clicked += () =>
            {
                if (RewindManager.Instance != null)
                {
                    Debug.Log("Resetting time tracker");
                    RewindManager.Instance.RestartTracking();
                }
            };
        }

        if (_prevButton != null)
        {
            _prevButton.clicked += () =>
            {
                prefabManager?.SelectPreviousPrefab();
                RefreshUI();
            };
        }

        if (_nextButton != null)
        {
            _nextButton.clicked += () =>
            {
                prefabManager?.SelectNextPrefab();
                RefreshUI();
            };
        }
    }

    public void RefreshUI()
    {
        if (prefabManager == null) return;

        var selectedPrefab = prefabManager.GetSelectedPrefab();
        if (selectedPrefab == null) return;

        // Update dropdown choices
        if (_prefabDropdown != null)
        {
            var choices = new System.Collections.Generic.List<string>();
            foreach (var p in prefabManager.prefabs)
            {
                choices.Add(p.name);
            }
            _prefabDropdown.choices = choices;
            if (prefabManager.selectedPrefabIndex < choices.Count)
            {
                _prefabDropdown.SetValueWithoutNotify(choices[prefabManager.selectedPrefabIndex]);
            }
        }

        // Update selected prefab label
        if (_selectedPrefabLabel != null)
        {
            _selectedPrefabLabel.text = $"Selected: {selectedPrefab.name}";
        }

        // Update sliders/toggles without triggering callbacks
        _siteCountSlider?.SetValueWithoutNotify(selectedPrefab.siteCount);
        _siteJitterSlider?.SetValueWithoutNotify(selectedPrefab.siteJitter);
        _enableRuntimeFractureToggle?.SetValueWithoutNotify(selectedPrefab.enableRuntimeFracture);
        _breakImpactThresholdSlider?.SetValueWithoutNotify(selectedPrefab.breakImpactThreshold);
        _waitForCollisionToggle?.SetValueWithoutNotify(selectedPrefab.waitForCollision);
        _runtimeSiteCountSlider?.SetValueWithoutNotify(selectedPrefab.runtimeSiteCount);
        _runtimeBreakDepthSlider?.SetValueWithoutNotify(selectedPrefab.runtimeBreakDepth);

        _lastRefreshTime = Time.time;
    }

    public void SetPrefabManager(FracturePrefabManager manager)
    {
        prefabManager = manager;
        RefreshUI();
    }

    private void UpdatePanelVisibility()
    {
        if (_panel == null) return;

        _panel.RemoveFromClassList("panel-visible");
        _panel.RemoveFromClassList("panel-hidden");
        _panel.AddToClassList(_isPanelVisible ? "panel-visible" : "panel-hidden");

        // Also toggle picking so hidden panel never captures clicks
        _panel.pickingMode = _isPanelVisible ? PickingMode.Position : PickingMode.Ignore;
        // Keep the toggle button always clickable
        _toggleButton?.BringToFront();
    }   

    private void ClearAllFragments()
    {
        // Find all GameObjects with the "fracture" tag
        var allFracturedObjects = GameObject.FindGameObjectsWithTag("Fracture");
        int objectsDestroyed = 0;

        foreach (var obj in allFracturedObjects)
        {
            if (obj != null)
            {
                DestroyImmediate(obj);
                objectsDestroyed++;
            }
        }

        Debug.Log($"Clear All: Removed {objectsDestroyed} fractured object(s) with 'fracture' tag.");
    }
}
