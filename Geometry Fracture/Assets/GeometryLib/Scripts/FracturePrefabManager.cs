using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.UIElements;

/// <summary>
/// Manages a collection of fracture prefabs with their individual settings.
/// </summary>
public class FracturePrefabManager : MonoBehaviour
{
    [System.Serializable]
    public class FracturePrefabData
    {
        public string name;
        public GameObject prefab;
        [Header("Fracture Settings")]
        public int siteCount = 8;
        public float siteJitter = 0.2f;
        public bool enableRuntimeFracture = true;
        public float breakImpactThreshold = 5f;
        public bool waitForCollision = false;
        public int runtimeSiteCount = 6;
        public int runtimeBreakDepth = 1;
        [Header("Overlay")]
        public bool generateOverlay = true;
        public int overlayTextureSize = 512;
        [Header("Spawn")]
        public Vector2 initialVelocity = Vector2.zero;
        public float spawnScale = 1f;
    }

    [Header("Prefab Library")]
    public List<FracturePrefabData> prefabs = new List<FracturePrefabData>();

    [Header("Current Selection")]
    public int selectedPrefabIndex = 0;

    [Header("Camera")]
    public Camera targetCamera;

    [Header("Spawn Settings")]
    public Vector2 spawnOffset = Vector2.zero;
    public bool spawnOnClick = true;

    private void Reset()
    {
        targetCamera = Camera.main;
    }

    private void Update()
    {
        if (!spawnOnClick) return;

        if (IsPointerOverUI())
            return;

        if (IsPointerOverObject())
            return;

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            SpawnSelected();
        }
    }

    /// <summary>
    /// Check if the mouse pointer is currently over any UI element.
    /// </summary>
    private bool IsPointerOverUI()
    {
        var mouse = Mouse.current;
        if (mouse == null) return false;
        
        Vector2 mousePos = mouse.position.ReadValue();

        var uiDocuments = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
        foreach (var uiDoc in uiDocuments)
        {
            if (uiDoc.rootVisualElement != null)
            {
                var pickedElement = uiDoc.rootVisualElement.panel?.Pick(mousePos);
                if (pickedElement != null && pickedElement != uiDoc.rootVisualElement.panel.visualTree)
                {
                    if (pickedElement.pickingMode == PickingMode.Position)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Check if the mouse pointer is currently over any 2D object.
    /// </summary>
    private bool IsPointerOverObject()
    {
        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return false;

        var mouse = Mouse.current;
        if (mouse == null) return false;

        Vector2 mousePos = mouse.position.ReadValue();
        Vector2 worldPos = cam.ScreenToWorldPoint(mousePos);
        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);

        return hit.collider != null;
    }

    /// <summary>
    /// Get the currently selected prefab data.
    /// </summary>
    public FracturePrefabData GetSelectedPrefab()
    {
        if (prefabs.Count == 0) return null;
        selectedPrefabIndex = Mathf.Clamp(selectedPrefabIndex, 0, prefabs.Count - 1);
        return prefabs[selectedPrefabIndex];
    }

    /// <summary>
    /// Spawn the currently selected prefab at mouse position.
    /// </summary>
    public void SpawnSelected()
    {
        SpawnPrefabAtMouse(selectedPrefabIndex);
    }

    /// <summary>
    /// Spawn a specific prefab by index at mouse position.
    /// </summary>
    public void SpawnPrefabAtMouse(int prefabIndex)
    {
        if (prefabs.Count == 0 || prefabIndex < 0 || prefabIndex >= prefabs.Count)
        {
            Debug.LogWarning("FracturePrefabManager: Invalid prefab index.");
            return;
        }

        Vector3 worldPos = GetMouseWorldPosition();
        SpawnPrefab(prefabIndex, worldPos);
    }

    /// <summary>
    /// Spawn a prefab at a specific world position.
    /// </summary>
    public GameObject SpawnPrefab(int prefabIndex, Vector3 worldPosition)
    {
        if (prefabs.Count == 0 || prefabIndex < 0 || prefabIndex >= prefabs.Count)
        {
            Debug.LogWarning("FracturePrefabManager: Invalid prefab index.");
            return null;
        }

        var prefabData = prefabs[prefabIndex];
        if (prefabData.prefab == null)
        {
            Debug.LogWarning($"FracturePrefabManager: Prefab at index {prefabIndex} is null.");
            return null;
        }

        worldPosition += (Vector3)spawnOffset;
        worldPosition.z = 0f;

        var instance = Instantiate(prefabData.prefab, worldPosition, Quaternion.identity);
        instance.SetActive(false);
        instance.name = $"{prefabData.name}";
        instance.tag = "Fracture";

        if (prefabData.spawnScale != 1f)
        {
            instance.transform.localScale = Vector3.one * prefabData.spawnScale;
        }

        var fractureComponent = instance.GetComponent<VoronoiFracture2D>();
        if (fractureComponent != null)
        {
            ApplyFractureSettings(instance, prefabData);
        }

        if (prefabData.initialVelocity != Vector2.zero)
        {
            var rb = instance.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = prefabData.initialVelocity;
            }
        }

        instance.SetActive(true);
        return instance;
    }

    /// <summary>
    /// Apply prefab data settings to a VoronoiFracture2D component.
    /// </summary>
    private void ApplyFractureSettings(GameObject spawned, FracturePrefabData pref)
    {
        var fracture = spawned.GetComponent<VoronoiFracture2D>();
        if (fracture != null)
        {
            fracture.siteCount = pref.siteCount;
            fracture.siteJitter = pref.siteJitter;
            fracture.enableRuntimeFracture = pref.enableRuntimeFracture;
            fracture.waitForCollision = pref.waitForCollision;
            fracture.breakImpactThreshold = pref.breakImpactThreshold;
            fracture.runtimeSiteCount = pref.runtimeSiteCount;
            fracture.runtimeBreakDepth = pref.runtimeBreakDepth;
            fracture.generateOverlay = pref.generateOverlay;
            fracture.overlayTextureSize = pref.overlayTextureSize;
            fracture.randomSeed = Random.Range(0, 100000);
        }
        
        var genericRewind = spawned.GetComponent<GenericRewind>();
        if (genericRewind != null && RewindManager.Instance != null)
        {
            RewindManager.Instance.AddObjectForTracking(genericRewind, RewindManager.OutOfBoundsBehaviour.DisableDestroy);
        }
    }

    /// <summary>
    /// Select a prefab by index.
    /// </summary>
    public void SelectPrefab(int index)
    {
        if (index >= 0 && index < prefabs.Count)
        {
            selectedPrefabIndex = index;
        }
    }

    /// <summary>
    /// Select next prefab in list.
    /// </summary>
    public void SelectNextPrefab()
    {
        if (prefabs.Count == 0) return;
        selectedPrefabIndex = (selectedPrefabIndex + 1) % prefabs.Count;
    }

    /// <summary>
    /// Select previous prefab in list.
    /// </summary>
    public void SelectPreviousPrefab()
    {
        if (prefabs.Count == 0) return;
        selectedPrefabIndex--;
        if (selectedPrefabIndex < 0) selectedPrefabIndex = prefabs.Count - 1;
    }

    /// <summary>
    /// Get mouse world position.
    /// </summary>
    private Vector3 GetMouseWorldPosition()
    {
        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("FracturePrefabManager: No camera available.");
            return Vector3.zero;
        }

        var mouse = Mouse.current;
        if (mouse == null) return Vector3.zero;

        Vector3 mouseScreenPos = (Vector3)mouse.position.ReadValue();
        return cam.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, cam.nearClipPlane));
    }

    /// <summary>
    /// Update current prefab settings.
    /// </summary>
    public void UpdateCurrentPrefabSettings(
        int? siteCount = null,
        float? siteJitter = null,
        bool? enableRuntimeFracture = null,
        float? breakImpactThreshold = null,
        bool? waitForCollision = null,
        int? runtimeSiteCount = null,
        int? runtimeBreakDepth = null,
        bool? generateOverlay = null,
        int? overlayTextureSize = null)
    {
        var current = GetSelectedPrefab();
        if (current == null) return;

        if (siteCount.HasValue) current.siteCount = siteCount.Value;
        if (siteJitter.HasValue) current.siteJitter = siteJitter.Value;
        if (enableRuntimeFracture.HasValue) current.enableRuntimeFracture = enableRuntimeFracture.Value;
        if (breakImpactThreshold.HasValue) current.breakImpactThreshold = breakImpactThreshold.Value;
        if (waitForCollision.HasValue) current.waitForCollision = waitForCollision.Value;
        if (runtimeSiteCount.HasValue) current.runtimeSiteCount = runtimeSiteCount.Value;
        if (runtimeBreakDepth.HasValue) current.runtimeBreakDepth = runtimeBreakDepth.Value;
        if (generateOverlay.HasValue) current.generateOverlay = generateOverlay.Value;
        if (overlayTextureSize.HasValue) current.overlayTextureSize = overlayTextureSize.Value;
    }
}
