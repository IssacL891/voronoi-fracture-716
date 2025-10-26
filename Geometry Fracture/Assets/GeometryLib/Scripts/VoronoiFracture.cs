using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Geometry; // uses your DelaunayTriangulation and VoronoiGenerator

// Simple Voronoi-based fracture generator.
// Assumptions:
// - Your Geometry assembly (DelaunayTriangulation, VoronoiGenerator, Point) is available in the Unity project.
// - This script clips Voronoi cells to a rectangular shape (Rect) using Sutherland–Hodgman clipping.
// - Polygons are triangulated with a simple ear-clipping algorithm and extruded to give thickness.
// - Intended as a starting point; for production you may want to replace polygon clipping/triangulation with a robust library.
public class VoronoiFracture : MonoBehaviour
{
    public enum FracturePlane { XY = 0, XZ = 1, YZ = 2 }

    [Tooltip("Which local plane to perform the fracture on. XY uses X/Y coordinates and extrudes along Z; XZ extrudes along Y; YZ extrudes along X.")]
    public FracturePlane fracturePlane = FracturePlane.XY;

    [Header("Sites")]
    public bool useRandomSites = true;
    public int siteCount = 8;
    public int randomSeed = 12345;
    public List<Vector2> sites = new List<Vector2>();

    [Header("Target shape")]
    // The shape to fracture — we support a simple axis-aligned rect relative to this transform.
    public Vector2 rectSize = new Vector2(2f, 2f);
    public Vector2 rectCenter = Vector2.zero;

    [Header("Piece settings")]
    public float thickness = 0.2f;
    public Material pieceMaterial;
    [Tooltip("Scale multiplier applied to the 2D polygon before extrusion. Use >1 to enlarge pieces, <1 to shrink (applies to in-plane dimensions).")]
    public float fragmentScale = 1.0f;
    [Header("Explosion / Fragment impulse")]
    [Tooltip("Multiplier applied to impact to form an explosion force for spawned fragments")]
    public float fragmentExplosionForce = 0.5f;
    [Tooltip("Radius used by AddExplosionForce when applying impulse to spawned fragments")]
    public float fragmentExplosionRadius = 1.0f;
    [Tooltip("Up modifier used by AddExplosionForce for spawned fragments")]
    public float fragmentExplosionUpModifier = 0.0f;
    [Tooltip("Random variation applied to the explosion force (0..1) for spawned fragments")]
    public float fragmentExplosionRandomness = 0.25f;
    [Header("Fragment splitting")]
    [Tooltip("Impact threshold required for fragments/whole pieces to split on collision")]
    public float fragmentSplitThreshold = 1.0f;
    [Tooltip("Maximum recursion depth for fragment splitting (0 = no recursion)")]
    [Range(0, 10)]
    public int maxRecursion = 1;
    [Tooltip("When true, log more verbose messages from generation and pieces (useful for debugging). Turn off for performance.")]
    public bool verboseLogs = false;
    [Tooltip("Automatically add a CollisionLogger component to spawned pieces and the whole (useful to capture events)")]
    public bool attachCollisionLogger = true;
    [Tooltip("Attach a lightweight CollisionProbe to the whole piece (useful to detect collisions) ")]
    public bool attachCollisionProbeToWhole = true;
    [Tooltip("If collision probe is attached, auto-split when it detects a contact (debug only)")]
    public bool probeAutoSplit = false;
    [Tooltip("Attach a trigger-based helper to the whole piece (uses OnTriggerEnter) to detect overlaps")]
    public bool attachTriggerHelperToWhole = true;

    [Header("Generation")]
    public bool generateOnStart = true;
    [Tooltip("When true, spawn all Voronoi pieces immediately. When false, create a single whole piece that will split on collisions.")]
    public bool spawnPiecesImmediately = false;
    // Host component state tracking so we can disable the original mesh/collider when we spawn pieces
    MeshRenderer hostMeshRenderer;
    Collider hostCollider;
    Rigidbody hostRigidbody;
    bool prevRendererEnabled;
    bool prevColliderEnabled;
    bool prevRbKinematic;
    bool prevRbDetectCollisions;
    bool hostStateSaved = false;
    // Saved originals so we can restore host on Clear
    Mesh originalHostMesh = null;
    Material originalHostMaterial = null;
    Collider originalHostCollider = null;
    Mesh originalHostColliderMesh = null;
    Rigidbody originalHostRigidbody = null;
    bool addedMeshCollider = false;
    bool addedRigidbody = false;
    bool hostConvertedToWhole = false;

    // Public entry
    public void Start()
    {
        // Ensure the host collider forwards collisions even before Generate() is called so the first impact can trigger generation.
        try
        {
            var hostCol = gameObject.GetComponent<Collider>();
            if (hostCol != null && gameObject.GetComponent<CollisionForwarder>() == null)
                gameObject.AddComponent<CollisionForwarder>();
        }
        catch { }

        if (generateOnStart)
            Generate();
    }

    // Call to create pieces
    public void Generate()
    {
        Debug.Log("VoronoiFracture.Generate: starting generation");
        ClearExistingChildren();
        // grab host components for possible disabling (we'll save states)
        hostMeshRenderer = gameObject.GetComponent<MeshRenderer>();
        hostCollider = gameObject.GetComponent<Collider>();
        hostRigidbody = gameObject.GetComponent<Rigidbody>();
        // Extract host polygon early so both branches can use it
        List<Vector2> hostPoly = GetHostPolygon();
        // If configured to spawn pieces immediately, generate Voronoi cells now.
        if (spawnPiecesImmediately)
        {
            // Determine sites
            var pts = new List<Point>();
            var rnd = new System.Random(randomSeed);
            if (useRandomSites || sites == null || sites.Count == 0)
            {
                for (int i = 0; i < siteCount; i++)
                {
                    var x = (float)rnd.NextDouble() * rectSize.x + (rectCenter.x - rectSize.x * 0.5f);
                    var y = (float)rnd.NextDouble() * rectSize.y + (rectCenter.y - rectSize.y * 0.5f);
                    pts.Add(new Point(x, y));
                }
            }
            else
            {
                foreach (var s in sites)
                    pts.Add(new Point(s.x + rectCenter.x - rectSize.x * 0.5f, s.y + rectCenter.y - rectSize.y * 0.5f));
            }

            Debug.Log($"VoronoiFracture.Generate: pts count={pts.Count} (siteCount={siteCount} useRandom={useRandomSites})");
            for (int i = 0; i < Mathf.Min(5, pts.Count); i++) Debug.Log($"  pt[{i}]={pts[i]}");

            // Build Delaunay and then Voronoi
            var triangles = DelaunayTriangulation.Triangulate(pts);
            Debug.Log($"VoronoiFracture.Generate: triangles count={triangles?.Count ?? 0}");
            var vor = VoronoiGenerator.FromDelaunay(triangles);
            Debug.Log($"VoronoiFracture.Generate: voronoi cells count={vor?.Count ?? 0}");

            // Build Rect for clipping (in local space)
            // Prefer the host mesh outline if available; otherwise fall back to the configured rect
            var rect = new Rect(rectCenter - rectSize * 0.5f, rectSize);

            int idx = 0;
            foreach (object pair in vor)
            {
                // pair is (Point site, List<Point> cell) in your generator (Program.cs showed this pattern)
                Point sitePoint;
                List<Point> cellPoints;

                // Support tuple or KeyValuePair style enumerables by deconstructing carefully
                if (pair is System.ValueTuple<Point, List<Point>> vt)
                {
                    sitePoint = vt.Item1;
                    cellPoints = vt.Item2;
                }
                else if (pair is KeyValuePair<Point, List<Point>> kv)
                {
                    sitePoint = kv.Key;
                    cellPoints = kv.Value;
                }
                else
                {
                    // Try reflection-based handling (avoids 'dynamic' and the C# runtime binder requirement)
                    var t = pair.GetType();

                    // Check for ValueTuple-like fields Item1/Item2
                    var f1 = t.GetField("Item1");
                    var f2 = t.GetField("Item2");
                    if (f1 != null && f2 != null)
                    {
                        sitePoint = (Point)f1.GetValue(pair);
                        cellPoints = (List<Point>)f2.GetValue(pair);
                    }
                    else
                    {
                        // Fallback: Key/Value properties (e.g., KeyValuePair<,>)
                        var pKey = t.GetProperty("Key");
                        var pValue = t.GetProperty("Value");
                        if (pKey != null && pValue != null)
                        {
                            sitePoint = (Point)pKey.GetValue(pair);
                            cellPoints = (List<Point>)pValue.GetValue(pair);
                        }
                        else
                        {
                            // Unable to interpret this element — skip
                            continue;
                        }
                    }
                }

                // Convert to Vector2 polygon
                var poly = cellPoints.Select(p => new Vector2((float)p.X, (float)p.Y)).ToList();
                Debug.Log($"Voronoi cell for site {sitePoint}: raw poly count={poly.Count}");
                if (poly.Count < 3) { Debug.Log("  skipping: poly < 3"); continue; }

                // Clip polygon to host polygon (if available) or rect
                List<Vector2> clipped;
                if (hostPoly != null && hostPoly.Count >= 3)
                    clipped = ClipPolygonToPolygon(poly, hostPoly);
                else
                    clipped = ClipPolygonToRect(poly, rect);
                // Apply optional in-plane scaling around polygon centroid
                if (clipped != null && clipped.Count >= 3 && !Mathf.Approximately(fragmentScale, 1f))
                {
                    clipped = ScalePolygon(clipped, fragmentScale);
                }
                Debug.Log($"  after clip count={(clipped == null ? 0 : clipped.Count)}");
                if (clipped == null || clipped.Count < 3) { Debug.Log("  skipping: clipped < 3"); continue; }

                // Create extruded mesh
                var mesh = CreateExtrudedMesh(clipped, thickness, fracturePlane);

                // Spawn GameObject
                var go = new GameObject($"VorPiece_{idx}");
                go.transform.SetParent(transform, false);
                // Add visual and physics components first
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                if (pieceMaterial != null) mr.sharedMaterial = pieceMaterial;

                // Add collider and rigidbody
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = true;
                mc.isTrigger = false;

                var rb = go.AddComponent<Rigidbody>();
                rb.mass = Mathf.Max(0.1f, MeshArea(clipped));
                // use continuous dynamic collision detection for small fast fragments
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.isKinematic = false;
                rb.detectCollisions = true;
                rb.WakeUp();

                Debug.Log($"VoronoiFracture: spawned fragment {go.name} meshVerts={mesh.vertexCount} colliderConvex={mc.convex} rbMass={rb.mass} collisionMode={rb.collisionDetectionMode}");
                if (attachCollisionLogger)
                {
                    var logger = go.AddComponent<CollisionLogger>();
                    logger.logCollisions = true;
                    logger.logTriggers = true;
                    logger.verboseStay = false;
                }

                // Add splitting behavior so pieces can fracture on collision (after Rigidbody exists)
                var pf = go.AddComponent<PieceFracture>();
                pf.fracturePlane = fracturePlane;
                pf.pieceMaterial = pieceMaterial;
                pf.thickness = thickness;
                pf.splitSiteCount = Mathf.Max(2, siteCount / 2);
                pf.splitThreshold = fragmentSplitThreshold; // use generator-level tuning
                // propagate generator-level fragment impulse tuning to pieces
                pf.explosionForce = fragmentExplosionForce;
                pf.explosionRadius = fragmentExplosionRadius;
                pf.explosionUpModifier = fragmentExplosionUpModifier;
                pf.explosionRandomness = fragmentExplosionRandomness;
                // propagate scale so child pieces can be stretched
                pf.fragmentScale = fragmentScale;


                idx++;
            }
            Debug.Log($"VoronoiFracture.Generate: created {idx} pieces");
            // Disable host visuals/collision so generated pieces are the active colliders
            if (!hostStateSaved)
            {
                if (hostMeshRenderer != null) { prevRendererEnabled = hostMeshRenderer.enabled; }
                if (hostCollider != null) { prevColliderEnabled = hostCollider.enabled; }
                if (hostRigidbody != null) { prevRbKinematic = hostRigidbody.isKinematic; prevRbDetectCollisions = hostRigidbody.detectCollisions; }
                hostStateSaved = true;
            }
            if (hostMeshRenderer != null) hostMeshRenderer.enabled = false;
            if (hostCollider != null) hostCollider.enabled = false;
            if (hostRigidbody != null) { hostRigidbody.isKinematic = true; hostRigidbody.detectCollisions = false; }
            return;
        }

        // Otherwise, create a single whole piece that will split on collision
        // Use host polygon if available, otherwise fall back to rectPoly built from rectSize/rectCenter
        List<Vector2> wholePoly = hostPoly != null && hostPoly.Count >= 3 ? hostPoly : new List<Vector2>
        {
            new Vector2(rectCenter.x - rectSize.x * 0.5f, rectCenter.y - rectSize.y * 0.5f),
            new Vector2(rectCenter.x + rectSize.x * 0.5f, rectCenter.y - rectSize.y * 0.5f),
            new Vector2(rectCenter.x + rectSize.x * 0.5f, rectCenter.y + rectSize.y * 0.5f),
            new Vector2(rectCenter.x - rectSize.x * 0.5f, rectCenter.y + rectSize.y * 0.5f)
        };

        // Configure the original host object to act as the "whole" piece so it fractures on collision.
        // Optionally scale the whole polygon before extrusion so pieces fill more of the host volume
        var wholePolyScaled = wholePoly;
        if (!Mathf.Approximately(fragmentScale, 1f)) wholePolyScaled = ScalePolygon(wholePoly, fragmentScale);
        var wholeMesh = CreateExtrudedMesh(wholePolyScaled, thickness, fracturePlane);

        // Save original host state so Clear can restore it
        var mfHost = gameObject.GetComponent<MeshFilter>();
        if (mfHost != null) originalHostMesh = mfHost.sharedMesh;
        var mrHostCheck = gameObject.GetComponent<MeshRenderer>();
        if (mrHostCheck != null) originalHostMaterial = mrHostCheck.sharedMaterial;
        originalHostCollider = gameObject.GetComponent<MeshCollider>();
        if (originalHostCollider is MeshCollider mh) originalHostColliderMesh = mh.sharedMesh;
        originalHostRigidbody = gameObject.GetComponent<Rigidbody>();

        // Replace host mesh with the extruded whole mesh
        if (mfHost == null) mfHost = gameObject.AddComponent<MeshFilter>();
        mfHost.sharedMesh = wholeMesh;

        var mrHost = gameObject.GetComponent<MeshRenderer>();
        if (mrHost == null) mrHost = gameObject.AddComponent<MeshRenderer>();
        if (pieceMaterial != null) mrHost.sharedMaterial = pieceMaterial;

        // Ensure a collider exists and uses the new mesh so collisions hit this object
        var mcHost = gameObject.GetComponent<MeshCollider>();
        if (mcHost == null)
        {
            mcHost = gameObject.AddComponent<MeshCollider>();
            addedMeshCollider = true;
        }
        else addedMeshCollider = false;
        mcHost.sharedMesh = wholeMesh;
        mcHost.convex = true;
        mcHost.isTrigger = false;

        // Ensure a Rigidbody exists and is configured to detect impacts
        var rbHost = gameObject.GetComponent<Rigidbody>();
        if (rbHost == null)
        {
            rbHost = gameObject.AddComponent<Rigidbody>();
            addedRigidbody = true;
        }
        else addedRigidbody = false;
        rbHost.mass = Mathf.Max(0.1f, MeshArea(wholePoly));
        rbHost.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rbHost.isKinematic = false;
        rbHost.detectCollisions = true;
        rbHost.WakeUp();

        // Attach PieceFracture to the host so it will split on collision
        var pfHost = gameObject.GetComponent<PieceFracture>();
        if (pfHost == null) pfHost = gameObject.AddComponent<PieceFracture>();
        pfHost.pieceMaterial = pieceMaterial;
        pfHost.thickness = thickness;
        pfHost.splitSiteCount = Mathf.Max(2, siteCount);
        pfHost.fracturePlane = fracturePlane;
        pfHost.splitThreshold = fragmentSplitThreshold;
        pfHost.explosionForce = fragmentExplosionForce;
        pfHost.explosionRadius = fragmentExplosionRadius;
        pfHost.explosionUpModifier = fragmentExplosionUpModifier;
        pfHost.explosionRandomness = fragmentExplosionRandomness;
        pfHost.fragmentScale = fragmentScale;
        // Propagate recursion limit and mark host converted
        pfHost.recursionLimit = maxRecursion;
        pfHost.recursionDepth = 0;
        hostConvertedToWhole = true;

        if (attachCollisionLogger)
        {
            var logger = gameObject.GetComponent<CollisionLogger>() ?? gameObject.AddComponent<CollisionLogger>();
            logger.logCollisions = true;
            logger.logTriggers = true;
            logger.verboseStay = false;
        }

        // Ensure any collider in the host hierarchy forwards events to the host PieceFracture
        try
        {
            var colliders = gameObject.GetComponentsInChildren<Collider>(true);
            foreach (var c in colliders)
            {
                if (c.gameObject.GetComponent<CollisionForwarder>() == null)
                    c.gameObject.AddComponent<CollisionForwarder>();
            }
        }
        catch { /* ignore in edit mode if physics not available */ }

        // Quick overlap check for diagnostics
        try
        {
            var worldCenter = transform.TransformPoint(wholeMesh.bounds.center);
            float probeR = Mathf.Max(0.01f, Mathf.Max(wholeMesh.bounds.extents.x, Mathf.Max(wholeMesh.bounds.extents.y, wholeMesh.bounds.extents.z)) * 0.5f);
            var overlapped = Physics.OverlapSphere(worldCenter, probeR);
            if (overlapped != null && overlapped.Length > 0)
            {
                string s = "VoronoiFracture: OverlapSphere found: ";
                foreach (var c in overlapped) s += c.gameObject.name + ",";
                Debug.Log(s);
            }
            else Debug.Log("VoronoiFracture: OverlapSphere found no colliders near host piece center");
        }
        catch { /* may be called in edit mode where physics isn't available; ignore */ }
    }

    public void ClearExistingChildren()
    {
        var children = new List<GameObject>();
        foreach (Transform t in transform) children.Add(t.gameObject);
        foreach (var c in children) DestroyImmediate(c);
        // If we converted the host into the whole piece, restore its original components/state
        if (hostConvertedToWhole)
        {
            // Remove PieceFracture from host if present
            var pf = gameObject.GetComponent<PieceFracture>();
            if (pf != null) DestroyImmediate(pf);

            // Remove CollisionForwarders added to children
            var forwards = gameObject.GetComponentsInChildren<CollisionForwarder>(true);
            foreach (var f in forwards) DestroyImmediate(f);

            // Restore MeshFilter mesh
            var mf = gameObject.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = originalHostMesh;

            // Restore material
            var mr = gameObject.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = originalHostMaterial;

            // Restore or remove MeshCollider
            var mc = gameObject.GetComponent<MeshCollider>();
            if (addedMeshCollider)
            {
                if (mc != null) DestroyImmediate(mc);
                // If there was an original MeshCollider, restore it
                if (originalHostCollider != null)
                {
                    var orig = gameObject.GetComponent<MeshCollider>();
                    if (orig == null) orig = gameObject.AddComponent<MeshCollider>();
                    if (originalHostColliderMesh != null) orig.sharedMesh = originalHostColliderMesh;
                }
            }
            else
            {
                // If we didn't add the mesh collider but changed its mesh, try to restore original mesh
                if (mc != null && originalHostColliderMesh != null) mc.sharedMesh = originalHostColliderMesh;
            }

            // Restore or remove Rigidbody
            var rb = gameObject.GetComponent<Rigidbody>();
            if (addedRigidbody)
            {
                if (rb != null) DestroyImmediate(rb);
            }

            // Clear conversion flag
            hostConvertedToWhole = false;
        }
        // Restore host visuals/collision if we disabled them during generation
        if (hostStateSaved)
        {
            // Prefer stored references if available, otherwise try to find components on the host now
            if (hostMeshRenderer != null)
                hostMeshRenderer.enabled = prevRendererEnabled;
            else
            {
                var mr = gameObject.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = prevRendererEnabled;
            }

            if (hostCollider != null)
                hostCollider.enabled = prevColliderEnabled;
            else
            {
                var col = gameObject.GetComponent<Collider>();
                if (col != null) col.enabled = prevColliderEnabled;
            }

            if (hostRigidbody != null)
            {
                hostRigidbody.isKinematic = prevRbKinematic;
                hostRigidbody.detectCollisions = prevRbDetectCollisions;
            }
            else
            {
                var rb = gameObject.GetComponent<Rigidbody>();
                if (rb != null) { rb.isKinematic = prevRbKinematic; rb.detectCollisions = prevRbDetectCollisions; }
            }

            hostStateSaved = false;
        }
        else
        {
            // If state wasn't saved (Generate may not have been called), be conservative and re-enable host renderer if it exists and is currently disabled.
            var mr = gameObject.GetComponent<MeshRenderer>();
            if (mr != null && !mr.enabled) mr.enabled = true;
        }
    }

    // Sutherland–Hodgman polygon clipping against axis-aligned rect
    List<Vector2> ClipPolygonToRect(List<Vector2> subject, Rect clipRect)
    {
        List<Vector2> output = new List<Vector2>(subject);
        // Clip rectangle edges in CCW order (bottom, right, top, left).
        // Sutherland–Hodgman keeps the "left" side of each directed edge, so edges must be CCW.
        var bl = new Vector2(clipRect.xMin, clipRect.yMin);
        var br = new Vector2(clipRect.xMax, clipRect.yMin);
        var tr = new Vector2(clipRect.xMax, clipRect.yMax);
        var tl = new Vector2(clipRect.xMin, clipRect.yMax);

        output = ClipAgainstEdge(output, bl, br); // bottom edge (keep points above)
        if (output.Count == 0) return output;
        output = ClipAgainstEdge(output, br, tr); // right edge (keep points to left)
        if (output.Count == 0) return output;
        output = ClipAgainstEdge(output, tr, tl); // top edge (keep points below)
        if (output.Count == 0) return output;
        output = ClipAgainstEdge(output, tl, bl); // left edge (keep points to right)

        return output;
    }

    // Clip polygon against a single directed edge (edge from A to B). Keep points on the left side of AB.
    List<Vector2> ClipAgainstEdge(List<Vector2> subject, Vector2 A, Vector2 B)
    {
        var result = new List<Vector2>();
        if (subject.Count == 0) return result;

        Vector2 prev = subject[subject.Count - 1];
        bool prevInside = IsLeft(A, B, prev) >= 0f;
        for (int i = 0; i < subject.Count; i++)
        {
            Vector2 cur = subject[i];
            bool curInside = IsLeft(A, B, cur) >= 0f;
            if (curInside)
            {
                if (!prevInside)
                {
                    // entering — compute intersection
                    if (LineIntersect(prev, cur, A, B, out Vector2 ip)) result.Add(ip);
                }
                result.Add(cur);
            }
            else if (prevInside)
            {
                // leaving — compute intersection
                if (LineIntersect(prev, cur, A, B, out Vector2 ip)) result.Add(ip);
            }
            prev = cur;
            prevInside = curInside;
        }
        return result;
    }

    // Cross product (B-A) x (P-A) z component
    float IsLeft(Vector2 A, Vector2 B, Vector2 P) => (B.x - A.x) * (P.y - A.y) - (B.y - A.y) * (P.x - A.x);

    // Line segment intersection (p1-p2) with (p3-p4). Returns true if intersects and sets intersection point.
    bool LineIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out Vector2 ip)
    {
        ip = Vector2.zero;
        var s1 = p2 - p1;
        var s2 = p4 - p3;
        float denom = (-s2.x * s1.y + s1.x * s2.y);
        if (Mathf.Approximately(denom, 0f)) return false;
        float s = (-s1.y * (p1.x - p3.x) + s1.x * (p1.y - p3.y)) / denom;
        float t = (s2.x * (p1.y - p3.y) - s2.y * (p1.x - p3.x)) / denom;
        if (s >= 0 && s <= 1 && t >= 0 && t <= 1)
        {
            ip = p1 + (t * s1);
            return true;
        }
        return false;
    }

    // Clip polygon A against polygon B using Sutherland-Hodgman (clip A to inside of B)
    List<Vector2> ClipPolygonToPolygon(List<Vector2> subject, List<Vector2> clipPoly)
    {
        var output = new List<Vector2>(subject);
        for (int i = 0; i < clipPoly.Count; i++)
        {
            var A = clipPoly[i];
            var B = clipPoly[(i + 1) % clipPoly.Count];
            output = ClipAgainstEdge(output, A, B);
            if (output.Count == 0) break;
        }
        return output;
    }

    // Compute convex hull (Monotone chain) of 2D points. Returns vertices in CCW order.
    List<Vector2> ConvexHull(List<Vector2> points)
    {
        if (points == null || points.Count <= 1) return new List<Vector2>(points ?? new List<Vector2>());
        var pts = points.OrderBy(p => p.x).ThenBy(p => p.y).ToList();

        List<Vector2> lower = new List<Vector2>();
        foreach (var p in pts)
        {
            while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], p) <= 0) lower.RemoveAt(lower.Count - 1);
            lower.Add(p);
        }

        List<Vector2> upper = new List<Vector2>();
        for (int i = pts.Count - 1; i >= 0; i--)
        {
            var p = pts[i];
            while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], p) <= 0) upper.RemoveAt(upper.Count - 1);
            upper.Add(p);
        }

        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        var hull = new List<Vector2>(lower);
        hull.AddRange(upper);
        return hull;
    }

    float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

    // Try to extract a 2D polygon outline from the host MeshFilter by projecting vertices to the chosen plane and taking convex hull.
    List<Vector2> GetHostPolygon()
    {
        var mf = gameObject.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return null;
        var verts = mf.sharedMesh.vertices;
        if (verts == null || verts.Length == 0) return null;
        var pts = verts.Select(v => ProjectToPlane(v, fracturePlane)).ToList();
        var hull = ConvexHull(pts);
        if (hull == null || hull.Count < 3) return null;
        return hull;
    }

    // Project a 3D local point to 2D according to the fracture plane
    Vector2 ProjectToPlane(Vector3 v, FracturePlane plane)
    {
        switch (plane)
        {
            case FracturePlane.XZ: return new Vector2(v.x, v.z);
            case FracturePlane.YZ: return new Vector2(v.y, v.z);
            case FracturePlane.XY:
            default: return new Vector2(v.x, v.y);
        }
    }

    // Convert a 2D polygon point back into a 3D local position on the given plane, with optional offset along the normal (depth)
    Vector3 UnprojectFromPlane(Vector2 p, FracturePlane plane, float normalOffset = 0f)
    {
        switch (plane)
        {
            case FracturePlane.XZ: return new Vector3(p.x, normalOffset, p.y);
            case FracturePlane.YZ: return new Vector3(normalOffset, p.x, p.y);
            case FracturePlane.XY:
            default: return new Vector3(p.x, p.y, normalOffset);
        }
    }

    // Overload for centroid/unproject where depth is zero
    Vector3 UnprojectFromPlane(Vector2 p, FracturePlane plane) => UnprojectFromPlane(p, plane, 0f);

    // Simple ear clipping triangulation for a simple polygon (CCW assumed). Returns list of triangle indices into the input vertices.
    List<int> Triangulate(List<Vector2> poly)
    {
        var indices = new List<int>();
        int n = poly.Count;
        if (n < 3) return indices;

        List<int> V = Enumerable.Range(0, n).ToList();

        int guard = 0;
        while (V.Count > 3 && guard++ < 1000)
        {
            bool earFound = false;
            for (int i = 0; i < V.Count; i++)
            {
                int prev = V[(i + V.Count - 1) % V.Count];
                int curr = V[i];
                int next = V[(i + 1) % V.Count];

                var A = poly[prev];
                var B = poly[curr];
                var C = poly[next];

                if (IsLeft(A, B, C) <= 0) continue; // not convex (assuming CCW)

                bool anyInside = false;
                for (int j = 0; j < V.Count; j++)
                {
                    int vi = V[j];
                    if (vi == prev || vi == curr || vi == next) continue;
                    if (PointInTriangle(poly[vi], A, B, C)) { anyInside = true; break; }
                }
                if (anyInside) continue;

                // Ear found
                indices.Add(prev);
                indices.Add(curr);
                indices.Add(next);
                V.RemoveAt(i);
                earFound = true;
                break;
            }
            if (!earFound) break; // probably degenerate
        }

        if (V.Count == 3) { indices.Add(V[0]); indices.Add(V[1]); indices.Add(V[2]); }
        return indices;
    }

    bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float a1 = IsLeft(p, a, b);
        float a2 = IsLeft(p, b, c);
        float a3 = IsLeft(p, c, a);
        bool hasNeg = (a1 < 0) || (a2 < 0) || (a3 < 0);
        bool hasPos = (a1 > 0) || (a2 > 0) || (a3 > 0);
        return !(hasNeg && hasPos);
    }

    // Create an extruded prism mesh from a 2D polygon. Returns mesh centered at local origin.
    Mesh CreateExtrudedMesh(List<Vector2> poly, float depth, FracturePlane plane)
    {
        var mesh = new Mesh();
        mesh.name = "VoronoiPiece";

        // Triangulate top face
        var triIndices = Triangulate(poly);

        int n = poly.Count;
        var verts = new List<Vector3>(n * 2);
        // Top
        for (int i = 0; i < n; i++) verts.Add(UnprojectFromPlane(poly[i], plane, depth * 0.5f));
        // Bottom
        for (int i = 0; i < n; i++) verts.Add(UnprojectFromPlane(poly[i], plane, -depth * 0.5f));

        var tris = new List<int>();
        // Top face
        for (int i = 0; i < triIndices.Count; i += 3)
        {
            tris.Add(triIndices[i]);
            tris.Add(triIndices[i + 1]);
            tris.Add(triIndices[i + 2]);
        }
        // Bottom face (reverse order)
        for (int i = 0; i < triIndices.Count; i += 3)
        {
            tris.Add(n + triIndices[i + 2]);
            tris.Add(n + triIndices[i + 1]);
            tris.Add(n + triIndices[i]);
        }

        // Side faces
        for (int i = 0; i < n; i++)
        {
            int ni = (i + 1) % n;
            int topA = i;
            int topB = ni;
            int botA = n + i;
            int botB = n + ni;

            // two triangles per quad
            tris.Add(topA); tris.Add(topB); tris.Add(botB);
            tris.Add(topA); tris.Add(botB); tris.Add(botA);
        }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Approximate polygon area
    float MeshArea(List<Vector2> poly)
    {
        float a = 0f;
        for (int i = 0; i < poly.Count; i++)
        {
            var p1 = poly[i];
            var p2 = poly[(i + 1) % poly.Count];
            a += p1.x * p2.y - p2.x * p1.y;
        }
        return Mathf.Abs(a) * 0.5f;
    }

    // Simple centroid (arithmetic mean) used for scaling the polygon about its center
    List<Vector2> ScalePolygon(List<Vector2> poly, float scale)
    {
        if (poly == null || poly.Count == 0) return poly;
        if (Mathf.Approximately(scale, 1f)) return poly;
        float cx = 0f, cy = 0f;
        foreach (var p in poly) { cx += p.x; cy += p.y; }
        cx /= poly.Count; cy /= poly.Count;
        var result = new List<Vector2>(poly.Count);
        for (int i = 0; i < poly.Count; i++)
        {
            var p = poly[i];
            var v = new Vector2((p.x - cx) * scale + cx, (p.y - cy) * scale + cy);
            result.Add(v);
        }
        return result;
    }
}
