using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Geometry;
using VoronoiFracture;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Enables 2D Voronoi fracture on a GameObject with a PolygonCollider2D.
/// Generates fragments by computing Voronoi cells and clipping them to the polygon boundary.
/// </summary>
[RequireComponent(typeof(PolygonCollider2D))]
public class VoronoiFracture2D : MonoBehaviour
{
    [Header("Fracture Settings")]
    [Tooltip("Number of Voronoi sites to generate for fracture")]
    public int siteCount = 8;

    [Tooltip("Random jitter applied to site positions")]
    public float siteJitter = 0.2f;

    [Tooltip("Random seed for deterministic fracture patterns")]
    public int randomSeed = 12345;

    [Tooltip("Material applied to fragment meshes")]
    public Material fragmentMaterial;

    [Header("Runtime Fracture")]
    [Tooltip("Enable fragments to break on impact during gameplay")]
    public bool enableRuntimeFracture = true;

    [Tooltip("Wait for collision before fracturing (otherwise fractures immediately)")]
    public bool waitForCollision = false;

    [Tooltip("Minimum impact force to trigger fracture")]
    public float breakImpactThreshold = 5f;

    [Tooltip("Number of sites for runtime fractures")]
    public int runtimeSiteCount = 6;

    [Tooltip("Maximum recursive fracture depth")]
    public int runtimeBreakDepth = 1;

    [Header("Performance")]
    [Tooltip("Spread fracture computation across multiple frames")]
    public bool spreadFractureOverFrames = false;

    [Tooltip("Fragments to process per frame when spreading")]
    public int fragmentsPerFrame = 6;

    [Tooltip("Use time budgeting instead of fixed fragment count")]
    public bool useTimeBudgeting = false;

    [Tooltip("Maximum milliseconds per frame for fracture computation")]
    public int timeBudgetMs = 8;

    [Header("Overlay")]
    [Tooltip("Generate colored texture overlays for fragments")]
    public bool generateOverlay = true;

    [Tooltip("Resolution of overlay textures")]
    public int overlayTextureSize = 512;

    [Header("Debug Visualization")]
    [Tooltip("Draw Voronoi sites in Scene view")]
    public bool drawSites = true;

    [Tooltip("Draw Voronoi cells in Scene view")]
    public bool drawCells = false;

    // Internal state
    private PolygonCollider2D polyCollider;
    private Transform fragmentParent;
    private bool hasFracturedAtRuntime = false;
    private GameObject preFractureVisual;

    void Awake()
    {
        polyCollider = GetComponent<PolygonCollider2D>();
    }

    void Start()
    {
        // If runtime fracture is enabled and we don't wait for collision, fracture immediately
        if (enableRuntimeFracture && !waitForCollision && !hasFracturedAtRuntime)
        {
            // Ensure pre-fracture visual exists before fracturing
            // Otherwise we'll create empty holding objects with no graphics
            EnsurePreFractureVisual();
            
            // Give the visual one frame to initialize before fracturing
            StartCoroutine(FractureAfterFrame());
        }
        else
        {
            // Ensure we can see the original object before fracture
            EnsurePreFractureVisual();
        }
    }

    /// <summary>
    /// Coroutine to fracture after one frame delay.
    /// This ensures the pre-fracture visual is fully initialized.
    /// </summary>
    private IEnumerator FractureAfterFrame()
    {
        yield return null; // Wait one frame
        Fracture();
    }

    /// <summary>
    /// Trigger the fracture process. Can be called from editor or runtime.
    /// </summary>
    [ContextMenu("Fracture Now")]
    public void Fracture()
    {
        if (!ValidateCanFracture())
            return;

        // Get world-space polygon from collider
        var worldPolygon = GetWorldSpacePolygon();

        // Ensure counter-clockwise winding
        var polyArray = worldPolygon.ToArray();
        PolygonUtility.EnsureCCW(ref polyArray);
        worldPolygon = new List<Vector2>(polyArray);

        // Generate Voronoi sites
        var siteGenerator = new VoronoiSiteGenerator(randomSeed, siteJitter);
        var sites = siteGenerator.GenerateSites(worldPolygon, polyCollider.bounds, siteCount);

        if (sites.Count < 3)
        {
            Debug.LogWarning("VoronoiFracture2D: Not enough sites generated for fracture.");
            return;
        }

        // Create parent object for fragments
        CreateFragmentParent();

        // Start fracture coroutine
        StartCoroutine(FractureCoroutine(sites, worldPolygon));
    }

    /// <summary>
    /// Clear all generated fragments and restore the original object.
    /// Can be called from runtime UI or editor.
    /// </summary>
    [ContextMenu("Clear Fragments")]
    public void ClearFragments()
    {
        // Find fragment parent GameObject
        var fragmentName = $"Fragments_{gameObject.name}_{GetInstanceID()}";
        var existing = GameObject.Find(fragmentName);

        if (existing != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // Editor mode: use DestroyImmediate with Undo
                UnityEditor.Undo.DestroyObjectImmediate(existing);
            }
            else
            {
                // Play mode: use DestroyImmediate for immediate cleanup
                // (Destroy() is deferred and causes issues with rapid clear/spawn cycles)
                DestroyImmediate(existing);
            }
#else
            // Runtime build: use DestroyImmediate for immediate cleanup
            DestroyImmediate(existing);
#endif
        }

        // Restore original object
        gameObject.SetActive(true);
        hasFracturedAtRuntime = false;

        // Ensure pre-fracture visual is restored so object is visible
        if (preFractureVisual != null)
        {
            preFractureVisual.SetActive(true);
        }
        else
        {
            // Rebuild visual if it doesn't exist
            EnsurePreFractureVisual();
        }
    }

    /// <summary>
    /// Static helper method to find and clear ALL fragment parent objects in the scene.
    /// </summary>
    public static int ClearAllFragmentParentsInScene()
    {
        int clearedCount = 0;

        // Find all GameObjects in scene (including inactive ones)
        var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        
        foreach (var obj in allObjects)
        {
            // Skip prefabs and assets - only process scene objects
            if (obj.scene.IsValid() && obj.name.StartsWith("Fragments_"))
            {
                // Check if this is a root-level fragment parent (not a child fragment)
                if (obj.transform.parent == null || !obj.transform.parent.name.StartsWith("Fragments_"))
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        UnityEditor.Undo.DestroyObjectImmediate(obj);
                    }
                    else
#endif
                    {
                        Object.Destroy(obj);
                    }
                    clearedCount++;
                }
            }
        }

        return clearedCount;
    }

    /// <summary>
    /// Validate that fracture can proceed.
    /// </summary>
    private bool ValidateCanFracture()
    {
        hasFracturedAtRuntime = true;

        if (polyCollider == null || polyCollider.points.Length < 3)
        {
            Debug.LogWarning("VoronoiFracture2D: No valid polygon to fracture.");
            return false;
        }

#if UNITY_EDITOR
        // Check if fragments already exist for this instance
        var fragmentName = $"Fragments_{gameObject.name}_{GetInstanceID()}";
        if (GameObject.Find(fragmentName) != null)
        {
            Debug.LogWarning($"VoronoiFracture2D: Fragments already exist for '{gameObject.name}'. Clear them first.");
            return false;
        }
#endif

        return true;
    }

    /// <summary>
    /// Get polygon vertices in world space.
    /// </summary>
    private List<Vector2> GetWorldSpacePolygon()
    {
        var worldPolygon = new List<Vector2>();
        foreach (var point in polyCollider.points)
            worldPolygon.Add(transform.TransformPoint(point));
        return worldPolygon;
    }

    /// <summary>
    /// Create parent GameObject to organize fragments.
    /// </summary>
    private void CreateFragmentParent()
    {
        var parentName = $"Fragments_{gameObject.name}_{GetInstanceID()}";
        var parentGO = new GameObject(parentName);
        
        // Tag the fragment parent so it can be found and cleared
        parentGO.tag = "Fracture";

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(parentGO, "Voronoi Fracture Parent");
#endif

        parentGO.transform.SetParent(transform.parent, true);
        fragmentParent = parentGO.transform;

        // Hide pre-fracture visual while fragments exist
        if (preFractureVisual != null)
            preFractureVisual.SetActive(false);
    }

    /// <summary>
    /// Main fracture coroutine that processes sites and creates fragments.
    /// Uses Geometry-Lib's Delaunay triangulation and Voronoi generation.
    /// </summary>
    private IEnumerator FractureCoroutine(List<Point> sites, List<Vector2> worldPolygon)
    {
        var clipper = new VoronoiCellClipper();
        var factory = new FragmentFactory(
            this, fragmentParent, fragmentMaterial,
            generateOverlay, overlayTextureSize, gameObject.layer);

        factory.SetRuntimeFractureSettings(
            enableRuntimeFracture, waitForCollision, breakImpactThreshold,
            runtimeSiteCount, runtimeBreakDepth);

        int processedCount = 0;
        float frameStartTime = Time.realtimeSinceStartup;

        var voronoiCells = clipper.GenerateVoronoiCells(sites, worldPolygon, polyCollider.bounds);

        foreach (var (site, clippedCell) in voronoiCells)
        {
            if (clippedCell != null && clippedCell.Count >= 3)
            {
                // Generate color for this fragment
                var color = FragmentTextureGenerator.ColorFromSite(site, randomSeed);

                // Create the fragment
                factory.CreateFragment(clippedCell, color);
                processedCount++;
            }

            // Check if we should yield to next frame
            if (ShouldYieldToNextFrame(processedCount, frameStartTime))
            {
                processedCount = 0;
                yield return null;
                frameStartTime = Time.realtimeSinceStartup;
            }
        }

        // Hide original object after fracture completes
        gameObject.SetActive(false);
        fragmentParent = null;
    }

    /// <summary>
    /// Ensure the original object has a visible mesh before fracture.
    /// Builds a mesh from the PolygonCollider2D if no visible renderer exists.
    /// This prevents "invisible" objects when runtime fracture is enabled.
    /// </summary>
    private void EnsurePreFractureVisual()
    {
        if (polyCollider == null || polyCollider.points == null || polyCollider.points.Length < 3)
        {
            Debug.LogWarning($"VoronoiFracture2D: Cannot create pre-fracture visual for '{name}' - invalid PolygonCollider2D");
            return;
        }

        // If there is already any enabled Renderer on this object or its children (excluding our own), do nothing
        var existingRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in existingRenderers)
        {
            if (preFractureVisual != null && r.gameObject == preFractureVisual) 
                continue;
            
            if (r.enabled)
            {
                // Already has a visible renderer, no need to create pre-fracture visual
                return;
            }
        }

        // Create or reuse a dedicated pre-fracture visual child
        if (preFractureVisual == null)
        {
            preFractureVisual = new GameObject("PreFractureVisual");
            preFractureVisual.transform.SetParent(transform, false);
        }
        preFractureVisual.SetActive(true);

        // Generate a sprite from the polygon shape
        var sprite = CreateSpriteFromPolygon(polyCollider.points);
        
        // Use SpriteRenderer for proper sprite display
        var sr = preFractureVisual.GetComponent<SpriteRenderer>();
        if (sr == null) sr = preFractureVisual.AddComponent<SpriteRenderer>();
        
        sr.sprite = sprite;
        
        // Use fragment material if provided, else use default sprite material
        if (fragmentMaterial != null)
        {
            sr.sharedMaterial = fragmentMaterial;
        }
        
        // Match the layer and sorting
        sr.sortingLayerID = GetComponent<Renderer>()?.sortingLayerID ?? 0;
        sr.sortingOrder = GetComponent<Renderer>()?.sortingOrder ?? 0;
    }

    /// <summary>
    /// Create a sprite from polygon vertices.
    /// Generates a texture that covers the polygon shape.
    /// </summary>
    private Sprite CreateSpriteFromPolygon(Vector2[] polygonPoints)
    {
        // Calculate bounds
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        
        foreach (var point in polygonPoints)
        {
            if (point.x < minX) minX = point.x;
            if (point.y < minY) minY = point.y;
            if (point.x > maxX) maxX = point.x;
            if (point.y > maxY) maxY = point.y;
        }

        float width = maxX - minX;
        float height = maxY - minY;

        // Create texture (use reasonable size, minimum 32x32)
        int texWidth = Mathf.Max(32, Mathf.CeilToInt(width * 100));
        int texHeight = Mathf.Max(32, Mathf.CeilToInt(height * 100));
        
        // Clamp to reasonable max size
        texWidth = Mathf.Min(texWidth, 512);
        texHeight = Mathf.Min(texHeight, 512);

        Texture2D texture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;

        // Fill with white (or use a color from fragment material)
        Color fillColor = Color.white;
        Color[] pixels = new Color[texWidth * texHeight];
        
        // Draw polygon fill using simple scanline algorithm
        for (int y = 0; y < texHeight; y++)
        {
            for (int x = 0; x < texWidth; x++)
            {
                // Convert pixel coords back to polygon space
                float worldX = minX + (x / (float)texWidth) * width;
                float worldY = minY + (y / (float)texHeight) * height;
                
                // Check if point is inside polygon
                if (IsPointInPolygon(new Vector2(worldX, worldY), polygonPoints))
                {
                    pixels[y * texWidth + x] = fillColor;
                }
                else
                {
                    pixels[y * texWidth + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        // Create sprite from texture
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, texWidth, texHeight),
            new Vector2(0.5f - (minX + width * 0.5f) / width, 0.5f - (minY + height * 0.5f) / height),
            100f // pixels per unit
        );

        return sprite;
    }

    /// <summary>
    /// Check if a point is inside a polygon using ray casting algorithm.
    /// </summary>
    private bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
    {
        int intersections = 0;
        
        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 v1 = polygon[i];
            Vector2 v2 = polygon[(i + 1) % polygon.Length];
            
            // Check if ray from point crosses edge
            if ((v1.y > point.y) != (v2.y > point.y))
            {
                float xIntersection = v1.x + (point.y - v1.y) * (v2.x - v1.x) / (v2.y - v1.y);
                if (point.x < xIntersection)
                {
                    intersections++;
                }
            }
        }
        
        return (intersections % 2) == 1;
    }

    /// <summary>
    /// Determine if coroutine should yield based on performance settings.
    /// </summary>
    private bool ShouldYieldToNextFrame(int processedCount, float frameStartTime)
    {
        if (useTimeBudgeting && timeBudgetMs > 0)
        {
            float elapsedMs = (Time.realtimeSinceStartup - frameStartTime) * 1000f;
            return elapsedMs >= timeBudgetMs;
        }
        else if (spreadFractureOverFrames && fragmentsPerFrame > 0)
        {
            return processedCount >= fragmentsPerFrame;
        }

        return false;
    }

    /// <summary>
    /// Handle collision-based runtime fracturing.
    /// </summary>
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!enableRuntimeFracture || hasFracturedAtRuntime || collision == null)
            return;

        // If waitForCollision is false, object fractures immediately on spawn
        // If true, only fracture on collision
        if (!waitForCollision)
            return; // Already fractured on spawn

        // Calculate impact magnitude
        float otherMass = collision.rigidbody != null ? collision.rigidbody.mass : 1f;
        float impact = collision.relativeVelocity.magnitude * otherMass;

        if (impact >= breakImpactThreshold)
        {
            Fracture();
        }
    }

    /// <summary>
    /// Draw debug visualization in Scene view.
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (!drawSites && !drawCells)
            return;

        if (polyCollider == null)
            polyCollider = GetComponent<PolygonCollider2D>();

        if (polyCollider == null || polyCollider.points.Length < 3)
            return;

        var worldPolygon = GetWorldSpacePolygon();

        // Draw polygon boundary
        Gizmos.color = Color.yellow;
        for (int i = 0; i < worldPolygon.Count; i++)
            Gizmos.DrawLine(worldPolygon[i], worldPolygon[(i + 1) % worldPolygon.Count]);

        if (!drawSites && !drawCells)
            return;

        // Generate preview sites
        var siteGenerator = new VoronoiSiteGenerator(randomSeed, siteJitter);
        var sites = siteGenerator.GenerateSites(worldPolygon, polyCollider.bounds, siteCount);

        if (drawSites)
        {
            Gizmos.color = Color.red;
            foreach (var site in sites)
                Gizmos.DrawSphere(new Vector3((float)site.X, (float)site.Y, 0f), 0.05f);
        }

        if (drawCells && sites.Count >= 3)
        {
            var clipper = new VoronoiCellClipper();
            Gizmos.color = Color.cyan;

            var voronoiCells = clipper.GenerateVoronoiCells(sites, worldPolygon, polyCollider.bounds);

            foreach (var (site, clippedCell) in voronoiCells)
            {
                if (clippedCell == null || clippedCell.Count < 3)
                    continue;

                for (int i = 0; i < clippedCell.Count; i++)
                {
                    var a = new Vector3((float)clippedCell[i].X, (float)clippedCell[i].Y, 0f);
                    var b = new Vector3((float)clippedCell[(i + 1) % clippedCell.Count].X,
                                       (float)clippedCell[(i + 1) % clippedCell.Count].Y, 0f);
                    Gizmos.DrawLine(a, b);
                }
            }
        }
    }
}
