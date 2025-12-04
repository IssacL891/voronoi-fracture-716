using UnityEngine;

/// <summary>
/// Enables runtime fracturing for fragment pieces.
/// Attached automatically to fragments during fracture if runtime fracture is enabled.
/// When this fragment receives sufficient impact, it will fracture into smaller pieces.
/// </summary>
public class FracturablePiece2D : MonoBehaviour
{
    [HideInInspector] public VoronoiFracture2D owner;
    [HideInInspector] public float breakImpactThreshold = 5f;
    [HideInInspector] public bool waitForCollision = false;
    [HideInInspector] public int siteCount = 6;
    [HideInInspector] public int remainingDepth = 1; // Maximum recursive fracture depth remaining

    /// <summary>
    /// Validate owner reference on start. If null, disable this component.
    /// This can happen after rewind operations or when the original object is destroyed.
    /// </summary>
    void Start()
    {
        if (owner == null)
        {
            Debug.LogWarning($"FracturablePiece2D: Fragment '{name}' has null owner - disabling runtime fracture capability");
            enabled = false;
        }
    }

    /// <summary>
    /// Handle collision and trigger fracture if impact exceeds threshold.
    /// </summary>
    void OnCollisionEnter2D(Collision2D collision)
    {
        // Validate preconditions
        if (owner == null)
        {
            Debug.LogWarning($"FracturablePiece2D: Cannot fracture '{name}' on collision - owner is null");
            return;
        }

        if (remainingDepth <= 0 || collision == null)
            return;

        // If waitForCollision is false, fragments were already fractured
        if (!waitForCollision)
            return;

        // Calculate impact magnitude
        var rb = GetComponent<Rigidbody2D>();
        float mass = rb != null ? rb.mass : 1f;
        float otherMass = collision.rigidbody != null ? collision.rigidbody.mass : mass;
        float impact = collision.relativeVelocity.magnitude * otherMass;

        if (impact >= breakImpactThreshold)
        {
            FractureThisFragment();
        }
    }

    /// <summary>
    /// Fracture this fragment into smaller pieces.
    /// Creates a temporary VoronoiFracture2D component with inherited settings.
    /// </summary>
    private void FractureThisFragment()
    {
        // Validate fragment has required components
        var polyCollider = GetComponent<PolygonCollider2D>();
        if (polyCollider == null || polyCollider.points.Length < 3)
        {
            Debug.LogWarning($"FracturablePiece2D: Cannot fracture '{name}' - missing or invalid PolygonCollider2D");
            return;
        }

        // Ensure fragment has visuals before fracturing
        var renderer = GetComponentInChildren<MeshRenderer>();
        if (renderer == null)
        {
            Debug.LogWarning($"FracturablePiece2D: Cannot fracture '{name}' - no MeshRenderer found");
            return;
        }

        if (owner == null)
        {
            Debug.LogWarning($"FracturablePiece2D: Cannot fracture '{name}' - owner reference is null");
            return;
        }

        // Prevent multiple triggers
        int depthBefore = remainingDepth;
        remainingDepth = 0;

        // Create temporary VoronoiFracture2D component
        var fractureComponent = gameObject.AddComponent<VoronoiFracture2D>();

        // Copy settings from owner
        fractureComponent.siteCount = Mathf.Max(3, siteCount);
        fractureComponent.siteJitter = owner.siteJitter;
        fractureComponent.randomSeed = owner.randomSeed ^ (int)Time.time; // Vary seed for different pattern
        fractureComponent.fragmentMaterial = owner.fragmentMaterial;
        fractureComponent.generateOverlay = owner.generateOverlay;
        fractureComponent.overlayTextureSize = owner.overlayTextureSize;

        // Runtime fracture settings with decremented depth
        fractureComponent.enableRuntimeFracture = owner.enableRuntimeFracture;
        fractureComponent.waitForCollision = owner.waitForCollision;
        fractureComponent.runtimeSiteCount = owner.runtimeSiteCount;
        fractureComponent.runtimeBreakDepth = Mathf.Max(0, depthBefore - 1);
        fractureComponent.breakImpactThreshold = owner.breakImpactThreshold;

        // Execute fracture
        fractureComponent.Fracture();

        // Clean up temporary component (GameObject will be deactivated by Fracture)
        Destroy(fractureComponent);
    }
}
