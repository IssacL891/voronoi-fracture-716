using UnityEngine;
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

    private float _lastRefreshTime;
    private int _lastSelectedIndex = -1;
    private bool _isPanelVisible = true;

    private void Awake()
    {
        // Ensure only one active instance to avoid overlapping duplicate UIs
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("FractureUIController: Another instance already exists. Disabling this duplicate to prevent overlapping UI.");
            gameObject.SetActive(false);
            return;
        }
        _instance = this;

        // Warn if legacy Canvas-based UI is present which may overlap/interfere
        var legacyUIs = FindObjectsByType<VoronoiFractureUI>(FindObjectsSortMode.None);
        if (legacyUIs != null && legacyUIs.Length > 0)
        {
            Debug.LogWarning("FractureUIController: Detected legacy Canvas UI (VoronoiFractureUI). Consider disabling/removing it to avoid overlapping controls.");
        }
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
        
        // Update slider range based on available rewind time
        if (_rewindSlider != null && RewindManager.Instance != null)
        {
            float available = RewindManager.Instance.HowManySecondsAvailableForRewind;
            float maxTime = Mathf.Min(available, 12f); // Cap at 12 seconds
            
            // Only update if different to avoid constant resets
            if (Mathf.Abs(_rewindSlider.highValue - maxTime) > 0.01f)
            {
                _rewindSlider.highValue = maxTime;
            }
            
            // Update label to show available time
            if (_rewindTimeLabel != null)
            {
                _rewindTimeLabel.text = $"Available: {available:F1}s";
            }
        }
        
        // Continuously update rewind position while slider is being dragged
        if (_isSliderBeingDragged && _rewindSlider != null && RewindManager.Instance != null && !_isTimePaused)
        {
            float rewindAmount = _rewindSlider.value;
            RewindManager.Instance.SetTimeSecondsInRewind(rewindAmount);
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
            
            // Register value change callback
            _rewindSlider.RegisterValueChangedCallback(evt =>
            {
                if (RewindManager.Instance == null) return;
                
                float newValue = evt.newValue;
                float oldValue = evt.previousValue;
                
                Debug.Log($"Slider value changed from {oldValue:F2} to {newValue:F2}");
                
                // If slider moved away from 0, start rewind mode and pause time
                if (oldValue == 0f && newValue > 0f)
                {
                    _isSliderBeingDragged = true;
                    
                    // Pause time and tracking to prevent conflicts (only if not already paused)
                    if (!_isTimePaused)
                    {
                        _isTimePaused = true;
                        RewindManager.Instance.TrackingEnabled = false;
                        Time.timeScale = 0f;
                        
                        // Update pause button text
                        if (_pauseTimeButton != null)
                        {
                            _pauseTimeButton.text = "Resume Time";
                        }
                        
                        Debug.Log("Starting rewind mode - Time paused");
                    }
                    else
                    {
                        Debug.Log("Starting rewind mode - Time already paused");
                    }
                    
                    RewindManager.Instance.StartRewindTimeBySeconds(0);
                }
                
                // Update rewind position if actively dragging
                if (_isSliderBeingDragged && newValue > 0f)
                {
                    Debug.Log($"Rewinding to {newValue}s");
                    RewindManager.Instance.SetTimeSecondsInRewind(newValue);
                }
                
                // If slider returned to 0, stop rewind mode (keep time paused)
                if (newValue == 0f && _isSliderBeingDragged)
                {
                    _isSliderBeingDragged = false;
                    Debug.Log("Stopping rewind mode - Time remains paused");
                    RewindManager.Instance.StopRewindTimeBySeconds();
                    
                    // Time stays paused so user can see the result
                    // They can manually resume using the Resume Time button
                }
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
