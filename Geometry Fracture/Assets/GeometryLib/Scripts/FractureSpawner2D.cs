using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Simple spawner: on mouse click, instantiate the configured prefab (e.g. your circle that has VoronoiFracture2D)
public class FractureSpawner2D : MonoBehaviour
{
    [Tooltip("Prefab to spawn. The prefab should be your circle GameObject (with VoronoiFracture2D attached).")]
    public GameObject prefabToSpawn;

    [Tooltip("Camera used to convert screen->world. If null, Camera.main will be used.")]
    public Camera targetCamera;

    [Tooltip("Spawn on left mouse button click")]
    public bool spawnOnLeftClick = true;
    [Tooltip("Spawn on right mouse button click")]
    public bool spawnOnRightClick = false;

    [Tooltip("Optional initial linear velocity applied to the spawned object's Rigidbody2D")]
    public Vector2 initialVelocity = Vector2.zero;

    [Tooltip("Optional spawn offset (world units) applied to spawn position")]
    public Vector2 spawnOffset = Vector2.zero;

    void Reset()
    {
        if (targetCamera == null) targetCamera = Camera.main;
    }

    void Update()
    {
        // Support both the new Input System and the legacy Input manager.
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (spawnOnLeftClick && mouse.leftButton.wasPressedThisFrame)
                SpawnAtMouse();
            if (spawnOnRightClick && mouse.rightButton.wasPressedThisFrame)
                SpawnAtMouse();
        }
#else
        if (spawnOnLeftClick && Input.GetMouseButtonDown(0))
            SpawnAtMouse();
        if (spawnOnRightClick && Input.GetMouseButtonDown(1))
            SpawnAtMouse();
#endif
    }

    void SpawnAtMouse()
    {
        if (prefabToSpawn == null)
        {
            Debug.LogWarning("FractureSpawner2D: prefabToSpawn is not assigned.");
            return;
        }

        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("FractureSpawner2D: No Camera available to convert screen to world.");
            return;
        }

        Vector3 mousePos;
#if ENABLE_INPUT_SYSTEM
    var mouse = Mouse.current;
    if (mouse != null)
        mousePos = (Vector3)mouse.position.ReadValue();
    else
        mousePos = Input.mousePosition; // fallback
#else
        mousePos = Input.mousePosition;
#endif
        Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, cam.nearClipPlane));
        worldPos.z = 0f; // spawn on z = 0 plane for 2D
        worldPos += (Vector3)spawnOffset;

        var go = Instantiate(prefabToSpawn, worldPos, Quaternion.identity);
        go.SetActive(true);

        // If it has a Rigidbody2D and an initial velocity is specified, apply it
        if (initialVelocity != Vector2.zero)
        {
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = go.AddComponent<Rigidbody2D>();
            }
            rb.linearVelocity = initialVelocity;
        }

        // Optional: if the prefab had a VoronoiFracture2D on it, ensure it's enabled for runtime fractures
        var vf = go.GetComponent<VoronoiFracture2D>();
        if (vf != null)
        {
            // ensure runtime fracture enabled so collisions will break it
            vf.enableRuntimeFracture = true;
        }
    }
}
