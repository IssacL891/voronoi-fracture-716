using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple spawner for fracture objects.
/// Instantiates a prefab at mouse click position.
/// </summary>
public class FractureSpawner2D : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("Prefab to spawn (should have VoronoiFracture2D component)")]
    public GameObject prefabToSpawn;

    [Tooltip("Camera for screen-to-world conversion (uses Camera.main if null)")]
    public Camera targetCamera;

    [Header("Input")]
    [Tooltip("Spawn on left mouse button")]
    public bool spawnOnLeftClick = true;

    [Tooltip("Spawn on right mouse button")]
    public bool spawnOnRightClick = false;

    [Header("Physics")]
    [Tooltip("Initial velocity applied to spawned object")]
    public Vector2 initialVelocity = Vector2.zero;

    [Tooltip("Position offset from mouse click")]
    public Vector2 spawnOffset = Vector2.zero;

    void Reset()
    {
        if (targetCamera == null) targetCamera = Camera.main;
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (spawnOnLeftClick && mouse.leftButton.wasPressedThisFrame)
            SpawnAtMousePosition();
        if (spawnOnRightClick && mouse.rightButton.wasPressedThisFrame)
            SpawnAtMousePosition();
    }

    /// <summary>
    /// Spawn the prefab at the current mouse position in world space.
    /// </summary>
    void SpawnAtMousePosition()
    {
        if (prefabToSpawn == null)
        {
            Debug.LogWarning("FractureSpawner2D: No prefab assigned.");
            return;
        }

        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("FractureSpawner2D: No camera available.");
            return;
        }

        // Get mouse position and convert to world space
        Vector3 mouseScreenPos = (Vector3)Mouse.current.position.ReadValue();
        Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, cam.nearClipPlane));
        worldPos.z = 0f;
        worldPos += (Vector3)spawnOffset;

        // Instantiate prefab
        var spawnedObject = Instantiate(prefabToSpawn, worldPos, Quaternion.identity);
        spawnedObject.SetActive(true);

        // Apply initial velocity if specified
        ApplyInitialVelocity(spawnedObject);

        // Ensure runtime fracture is enabled
        EnableRuntimeFracture(spawnedObject);
    }

    /// <summary>
    /// Apply initial velocity to spawned object if it has a Rigidbody2D.
    /// </summary>
    private void ApplyInitialVelocity(GameObject spawnedObject)
    {
        if (initialVelocity == Vector2.zero)
            return;

        var rb = spawnedObject.GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = spawnedObject.AddComponent<Rigidbody2D>();

        rb.linearVelocity = initialVelocity;
    }

    /// <summary>
    /// Ensure VoronoiFracture2D component has runtime fracture enabled.
    /// </summary>
    private void EnableRuntimeFracture(GameObject spawnedObject)
    {
        var fractureComponent = spawnedObject.GetComponent<VoronoiFracture2D>();
        if (fractureComponent != null)
        {
            fractureComponent.enableRuntimeFracture = true;
        }
    }
}
