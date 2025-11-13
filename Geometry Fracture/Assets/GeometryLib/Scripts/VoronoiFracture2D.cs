using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
// System.Diagnostics omitted to avoid ambiguity with UnityEngine.Debug; fully-qualify Stopwatch usages below.
using UnityEngine;
using Geometry;
using Unity.Collections;
using UnityEngine.U2D;
using Clipper2Lib; // Added Clipper2Lib using directive
#if UNITY_EDITOR
using UnityEditor;
#endif

// Attach to a GameObject to enable 2D Voronoi fracture on demand.
[RequireComponent(typeof(PolygonCollider2D))]
public class VoronoiFracture2D : MonoBehaviour
{
    [Header("Fracture Settings")]
    public int siteCount = 8;
    public float siteJitter = 0.2f;
    public int randomSeed = 12345;
    public Material fragmentMaterial;
    [Header("Runtime Break")]
    public bool enableRuntimeFracture = true;
    public float breakImpactThreshold = 5f; // impulse threshold to trigger break
    public int runtimeSiteCount = 6; // sites used when a fragment breaks at runtime
    public int runtimeBreakDepth = 1; // how many recursive runtime breaks allowed
    [Header("Performance")]
    public bool spreadFractureOverFrames = false;
    public int fragmentsPerFrame = 6;
    public bool useTimeBudgeting = false;
    public int timeBudgetMs = 8; // approximate milliseconds per frame to spend on fragment creation
    [Header("Overlay")]
    public bool generateOverlay = true;
    public int overlayTextureSize = 512;

    [Header("Debug")]
    public bool drawSites = true;
    public bool drawCells = false;
    // Debugging: show raw/clipped Voronoi cells computed during fracture
    public bool debugDrawRawCells = true;
    public bool debugDrawClippedCells = true;
    public int debugPreviewCount = 3; // how many cells to log/draw

    // runtime debug storage
    private List<List<Vector2>> debugRawCells = new List<List<Vector2>>();
    private List<List<Vector2>> debugClippedCells = new List<List<Vector2>>();
    // fragment creation debug
    public int debugFragmentDump = 8;
    private int fragmentCreatedCount = 0;

    // Profiling / timing
    [Header("Profiling")]
    public bool enableProfiling = false;
    public int profilingDumpCount = 8; // how many fragments to log timings for
    public float profiling_siteGenMs = 0f;
    public float profiling_clippingMs = 0f;
    public float profiling_clipperMs = 0f;
    public float profiling_fragmentCreateMs = 0f;
    public float profiling_textureMs = 0f;
    public float profiling_meshMs = 0f;
    public float profiling_totalMs = 0f;
    public int profiling_fragmentCount = 0;

    private PolygonCollider2D polyCollider;
    // shared overlay material used to render fragment textures without allocating per-fragment materials
    static Material s_overlayMaterial;
    // parent transform used to group created fragments during a single fracture operation
    private Transform currentFragmentParent;
    // runtime guard to avoid fracturing multiple times from repeated collisions
    private bool hasFracturedAtRuntime = false;

    void Awake()
    {
        polyCollider = GetComponent<PolygonCollider2D>();
    }

    [ContextMenu("Fracture Now")]
    public void Fracture()
    {
        // mark that fracture was triggered (could be editor or runtime)
        hasFracturedAtRuntime = true;
        if (polyCollider == null || polyCollider.points.Length < 3)
        {
            Debug.LogWarning("VoronoiFracture2D: No valid polygon to fracture.");
            return;
        }
        // Get world-space polygon
        var worldPoly = new List<Vector2>();
        foreach (var pt in polyCollider.points)
            worldPoly.Add(transform.TransformPoint(pt));

        // Ensure worldPoly has CCW winding for clipping routines
        if (SignedArea(worldPoly.ToArray()) < 0f)
            worldPoly.Reverse();

        // Generate random sites inside polygon bounds
        var bounds = polyCollider.bounds;
        var sites = new List<Point>();
        var rnd = new System.Random(randomSeed);
        int placed = 0;
        int maxTries = Math.Max(200, siteCount * 100);
        float dedupeEps = Mathf.Max(1e-4f, Mathf.Min(bounds.size.x, bounds.size.y) * 1e-4f);
        // profiling: site generation
        System.Diagnostics.Stopwatch swSite = null;
        if (enableProfiling) swSite = System.Diagnostics.Stopwatch.StartNew();
        while (placed < siteCount && maxTries-- > 0)
        {
            float x = (float)(bounds.min.x + rnd.NextDouble() * bounds.size.x);
            float y = (float)(bounds.min.y + rnd.NextDouble() * bounds.size.y);
            var v2 = new Vector2(x, y);
            if (!PointInPolygon(v2, worldPoly)) continue;

            // deterministic jitter using System.Random. Apply jitter but keep site inside polygon.
            if (siteJitter != 0f)
            {
                float jx = (float)(rnd.NextDouble() * 2.0 - 1.0);
                float jy = (float)(rnd.NextDouble() * 2.0 - 1.0);
                var jittered = v2 + new Vector2(jx, jy) * siteJitter;
                if (PointInPolygon(jittered, worldPoly))
                {
                    v2 = jittered;
                }
                else
                {
                    // clamp the jittered point to the nearest point on the polygon (edge projection)
                    v2 = NearestPointOnPolygon(jittered, worldPoly);
                }
            }

            // skip near-duplicates (dedupe) to avoid identical sites which produce degenerate cells
            bool tooClose = false;
            for (int si = 0; si < sites.Count; si++)
            {
                var s = sites[si];
                if (Vector2.SqrMagnitude(new Vector2((float)s.X, (float)s.Y) - v2) <= dedupeEps * dedupeEps)
                {
                    tooClose = true; break;
                }
            }
            if (tooClose) continue;

            sites.Add(new Point(v2.x, v2.y));
            placed++;
        }
        if (enableProfiling && swSite != null)
        {
            swSite.Stop();
            profiling_siteGenMs = (float)swSite.Elapsed.TotalMilliseconds;
        }
        if (sites.Count < 3)
        {
            Debug.LogWarning("VoronoiFracture2D: Not enough sites placed.");
            return;
        }
        // Compute Voronoi-like cells by half-plane intersection (same approach as the console sample)
        // Build a clipping rectangle that comfortably encloses all sites
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity, maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        foreach (var s in sites)
        {
            if (s.X < minX) minX = s.X;
            if (s.Y < minY) minY = s.Y;
            if (s.X > maxX) maxX = s.X;
            if (s.Y > maxY) maxY = s.Y;
        }
        if (minX == double.PositiveInfinity)
        {
            Debug.LogWarning("VoronoiFracture2D: no sites to process");
            return;
        }
        double dataW = Math.Max(1e-6, maxX - minX);
        double dataH = Math.Max(1e-6, maxY - minY);
        double margin = Math.Max(dataW, dataH) * 2.0 + 1.0;
        double clipMinX = minX - margin;
        double clipMinY = minY - margin;
        double clipMaxX = maxX + margin;
        double clipMaxY = maxY + margin;

        // prepare debug storage
        // (RectPolygon and ClipWithHalfPlane moved to class-level to support coroutine usage.)

        // prepare debug storage
        debugRawCells.Clear();
        debugClippedCells.Clear();

#if UNITY_EDITOR
    // If fragments already exist for this specific instance, do not create another set.
    // Use instance id to avoid collisions between multiple clones/prefab instances with the same name.
    var fragParentName = $"Fragments_{gameObject.name}_{GetInstanceID()}";
    var existingObj = GameObject.Find(fragParentName);
    if (existingObj != null)
    {
        Debug.LogWarning($"VoronoiFracture2D: Fragments already exist for instance '{gameObject.name}' (id={GetInstanceID()}). Clear them before fracturing again.");
        return;
    }
#endif

        // Create a parent object to hold all fragments for this fracture operation (helps undo)
        GameObject fragParent = null;
#if UNITY_EDITOR
    fragParent = new GameObject(fragParentName);
    Undo.RegisterCreatedObjectUndo(fragParent, "Voronoi Fracture Parent");
    fragParent.transform.SetParent(transform.parent, true);
    currentFragmentParent = fragParent.transform;
#else
        fragParent = new GameObject(fragParentName);
        fragParent.transform.SetParent(transform.parent, true);
        currentFragmentParent = fragParent.transform;
#endif

        // For each site, build its clipped Voronoi cell. We can optionally spread work across frames.
        StartCoroutine(FractureCoroutine(sites, worldPoly, clipMinX, clipMinY, clipMaxX, clipMaxY));
        return;
    }

    void CreateFragment(List<Point> poly, Color color)
    {
        fragmentCreatedCount++;
        System.Diagnostics.Stopwatch swFragTotal = null;
        if (enableProfiling) swFragTotal = System.Diagnostics.Stopwatch.StartNew();
        // Create fragment gameobject and place it under the same parent as the source object
#if UNITY_EDITOR
    var go = new GameObject("Fragment");
    // parent under the temporary fragment parent if available to make undo/cleanup simple
    go.transform.SetParent(currentFragmentParent != null ? currentFragmentParent : transform.parent, true);
    Undo.RegisterCreatedObjectUndo(go, "Voronoi Fragment");
#else
        var go = new GameObject("Fragment");
        go.transform.SetParent(currentFragmentParent != null ? currentFragmentParent : transform.parent, true);
#endif
#if UNITY_EDITOR
    Undo.RegisterCreatedObjectUndo(go, "Voronoi Fragment");
#endif
        // compute centroid of polygon to position the fragment GameObject
        Vector2 centroid = Vector2.zero;
        for (int i = 0; i < poly.Count; i++) centroid += new Vector2((float)poly[i].X, (float)poly[i].Y);
        centroid /= poly.Count;

        // Preserve the polygon vertex order produced by the clipping algorithm (important for concave polygons)
        // Remove consecutive near-duplicate points and nearly-collinear points to avoid triangulation artifacts
        var cleaned = new List<Point>();
        const float dupEps = 1e-4f;
        const float collinearEps = 1e-4f;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            // skip exact/nearly duplicate consecutive points
            if (Mathf.Abs((float)a.X - (float)b.X) < dupEps && Mathf.Abs((float)a.Y - (float)b.Y) < dupEps)
                continue;
            cleaned.Add(a);
        }
        // remove nearly-collinear consecutive triplets
        var finalPts = new List<Point>();
        for (int i = 0; i < cleaned.Count; i++)
        {
            var prev = cleaned[(i - 1 + cleaned.Count) % cleaned.Count];
            var cur = cleaned[i];
            var next = cleaned[(i + 1) % cleaned.Count];
            // compute cross of (cur-prev) x (next-cur)
            float ux = (float)(cur.X - prev.X);
            float uy = (float)(cur.Y - prev.Y);
            float vx = (float)(next.X - cur.X);
            float vy = (float)(next.Y - cur.Y);
            float cross = Mathf.Abs(ux * vy - uy * vx);
            if (cross <= collinearEps)
            {
                // skip cur as it's nearly collinear
                continue;
            }
            finalPts.Add(cur);
        }
        if (finalPts.Count < 3) return; // nothing to create
        poly = finalPts;
        go.transform.position = new Vector3(centroid.x, centroid.y, 0f);
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        if (fragmentMaterial != null) mr.sharedMaterial = fragmentMaterial;
        var pc2d = go.AddComponent<PolygonCollider2D>();
        var rb2d = go.AddComponent<Rigidbody2D>();
        rb2d.gravityScale = 1f;

        // Convert world-space polygon to local-space points for this fragment (preserve order)
        var ptsWorld = new Vector2[poly.Count];
        for (int i = 0; i < poly.Count; i++)
            ptsWorld[i] = new Vector2((float)poly[i].X, (float)poly[i].Y);

        // Debug: check polygon simplicity before further processing
        if (fragmentCreatedCount < debugFragmentDump)
        {
            var pw = ptsWorld.ToList();
            if (!IsSimple(pw))
            {
                Debug.LogWarning($"CreateFragment: polygon for Fragment[{fragmentCreatedCount + 1}] is not simple (self-intersects). Dumping points:");
                for (int i = 0; i < pw.Count; i++)
                    Debug.Log($"  poly[{i}] = ({pw[i].x:0.###},{pw[i].y:0.###})");
            }
        }

        var ptsLocal = new Vector2[poly.Count];
        for (int i = 0; i < poly.Count; i++)
        {
            // local point relative to centroid (go.transform.position)
            ptsLocal[i] = ptsWorld[i] - centroid;
        }

        // Ensure consistent CCW winding for collider and mesh
        if (SignedArea(ptsLocal) < 0f) System.Array.Reverse(ptsLocal);

        // Assign collider points in local space
        pc2d.points = ptsLocal;

        // Build mesh using local-space vertices so it renders at the correct world position
        System.Diagnostics.Stopwatch swMesh = null;
        if (enableProfiling) swMesh = System.Diagnostics.Stopwatch.StartNew();
        mf.sharedMesh = PolygonToMesh(ptsLocal);
        if (enableProfiling && swMesh != null)
        {
            swMesh.Stop();
            profiling_meshMs += (float)swMesh.Elapsed.TotalMilliseconds;
        }
        // create overlay as a textured mesh that exactly matches the fragment geometry (avoids sprite pivot/ppus mismatches)
        if (generateOverlay)
        {
            System.Diagnostics.Stopwatch swTex = null;
            if (enableProfiling) swTex = System.Diagnostics.Stopwatch.StartNew();
            var tex = GenerateFragmentTexture(poly, color, Mathf.Clamp(overlayTextureSize, 64, 2048));
            if (enableProfiling && swTex != null)
            {
                swTex.Stop();
                profiling_textureMs += (float)swTex.Elapsed.TotalMilliseconds;
            }
            if (tex != null)
            {
                // reuse the mesh created for the fragment so overlay triangles match exactly
                var baseMesh = mf.sharedMesh;
                if (baseMesh != null)
                {
                    var overlayGo = new GameObject("OverlayMesh");
                    overlayGo.transform.SetParent(go.transform, false);
#if UNITY_EDITOR
                    Undo.RegisterCreatedObjectUndo(overlayGo, "Voronoi Overlay");
#endif
                    overlayGo.transform.localPosition = Vector3.zero;

                    var of = overlayGo.AddComponent<MeshFilter>();
                    var or = overlayGo.AddComponent<MeshRenderer>();

                    var overlayMesh = new Mesh();
                    overlayMesh.vertices = baseMesh.vertices;
                    overlayMesh.triangles = baseMesh.triangles;

                    // compute UVs from world-space polygon bounds so texture maps correctly
                    float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                    for (int i = 0; i < poly.Count; i++)
                    {
                        var p = poly[i];
                        if ((float)p.X < minX) minX = (float)p.X;
                        if ((float)p.Y < minY) minY = (float)p.Y;
                        if ((float)p.X > maxX) maxX = (float)p.X;
                        if ((float)p.Y > maxY) maxY = (float)p.Y;
                    }
                    float dataW = Mathf.Max(1e-6f, maxX - minX);
                    float dataH = Mathf.Max(1e-6f, maxY - minY);

                    var verts3 = baseMesh.vertices;
                    var uvs = new Vector2[verts3.Length];
                    for (int i = 0; i < verts3.Length; i++)
                    {
                        // verts are local-space relative to centroid; convert back to world by adding centroid
                        var worldV = new Vector2(verts3[i].x + centroid.x, verts3[i].y + centroid.y);
                        uvs[i] = new Vector2((worldV.x - minX) / dataW, (worldV.y - minY) / dataH);
                    }
                    overlayMesh.uv = uvs;
                    overlayMesh.RecalculateBounds();

                    of.sharedMesh = overlayMesh;

                    // use a simple sprite-oriented shader so transparency works as expected and draw order is predictable
                    if (s_overlayMaterial == null)
                    {
                        // Prefer the custom pixel-toon shader if available, otherwise fall back to Sprites/Default
                        var sh = Shader.Find("Custom/PixelToon");
                        if (sh != null)
                            s_overlayMaterial = new Material(sh);
                        else
                            s_overlayMaterial = new Material(Shader.Find("Sprites/Default"));
                        s_overlayMaterial.hideFlags = HideFlags.DontSave;
                        // sensible defaults for the pixel/toon look
                        if (s_overlayMaterial.HasProperty("_PosterizeLevels")) s_overlayMaterial.SetFloat("_PosterizeLevels", 4f);
                        if (s_overlayMaterial.HasProperty("_PixelSize")) s_overlayMaterial.SetFloat("_PixelSize", 32f);
                    }
                    or.sharedMaterial = s_overlayMaterial;
                    // ensure overlay draws after fragment by moving it to a higher render queue
                    or.sharedMaterial.renderQueue = (fragmentMaterial != null ? fragmentMaterial.renderQueue : 3000) + 1;
                    // set the per-instance texture without allocating a new material
                    var mpb = new MaterialPropertyBlock();
                    mpb.SetTexture("_MainTex", tex);
                    or.SetPropertyBlock(mpb);
                }
            }
        }
        // Ensure fragment uses same layer as source
        go.layer = gameObject.layer;

        // Add runtime fracturing behaviour: fragments can break further when impacted
        if (enableRuntimeFracture)
        {
            var fragComp = go.AddComponent<FracturablePiece2D>();
            fragComp.owner = this;
            fragComp.breakImpactThreshold = breakImpactThreshold;
            fragComp.siteCount = Mathf.Max(3, runtimeSiteCount);
            fragComp.remainingDepth = Mathf.Max(0, runtimeBreakDepth);
        }

        if (enableProfiling && swFragTotal != null)
        {
            swFragTotal.Stop();
            profiling_fragmentCreateMs += (float)swFragTotal.Elapsed.TotalMilliseconds;
            profiling_fragmentCount = fragmentCreatedCount;
        }

        // Debug dump for first few fragments: log mesh & collider details
        if (fragmentCreatedCount <= debugFragmentDump)
        {
            var mesh = mf.sharedMesh;
            Debug.Log($"Fragment[{fragmentCreatedCount}] centroid={centroid} localVerts={ptsLocal.Length} meshVerts={(mesh == null ? 0 : mesh.vertexCount)} tris={(mesh == null ? 0 : mesh.triangles.Length)}");
            if (mesh != null)
            {
                var verts = mesh.vertices;
                var tris = mesh.triangles;
                for (int i = 0; i < verts.Length; i++)
                    Debug.Log($"  mesh.v[{i}] = {verts[i]}");
                for (int i = 0; i < tris.Length; i += 3)
                    Debug.Log($"  tri[{i / 3}] = ({tris[i]},{tris[i + 1]},{tris[i + 2]})");
            }
            if (pc2d != null)
            {
                for (int i = 0; i < pc2d.points.Length; i++)
                    Debug.Log($"  collider.p[{i}] = {pc2d.points[i]}");
                Debug.Log($"  collider.bounds = {pc2d.bounds}");
            }
            // Sanity check: warn if any mesh vertex is far outside the fragment collider bounds
            if (mesh != null && pc2d != null)
            {
                var verts = mesh.vertices;
                var b = pc2d.bounds;
                float maxAllowed = Math.Max(b.extents.magnitude * 4f, 1f);
                for (int i = 0; i < verts.Length; i++)
                {
                    var v = verts[i];
                    if (v.magnitude > maxAllowed)
                    {
                        Debug.LogWarning($"Fragment[{fragmentCreatedCount}] mesh.v[{i}] magnitude {v.magnitude:0.###} exceeds allowed {maxAllowed:0.###}; centroid={centroid}");
                    }
                }
            }
        }
    }

    // --- Robust polygon intersection using Unity's Clipper2 (if available) ---
    // Returns a list of resulting polygons (each a list of Vector2). Returns null if clipper is not available or an error occurs.
    List<List<Vector2>> ClipPolygonsWithClipper(List<Vector2> subj, List<Vector2> clip)
    {
        if (subj == null || clip == null) return null;
        if (subj.Count < 3 || clip.Count < 3) return null;
        // Clipper2 is integer-based. Use a modest scale to preserve fractional geometry.
        const double scale = 1000.0;
        try
        {
            // Build Paths64 for subject and clip
            var subjPaths = new Paths64();
            var clipPaths = new Paths64();

            var subjPath = new Path64();
            foreach (var v in subj)
                subjPath.Add(new Point64((long)Math.Round(v.x * scale), (long)Math.Round(v.y * scale)));
            subjPaths.Add(subjPath);

            var clipPath = new Path64();
            foreach (var v in clip)
                clipPath.Add(new Point64((long)Math.Round(v.x * scale), (long)Math.Round(v.y * scale)));
            clipPaths.Add(clipPath);

            // compute intersection (try EvenOdd first, then NonZero)
            Paths64 solution = null;
            try
            {
                solution = Clipper.Intersect(subjPaths, clipPaths, FillRule.EvenOdd);
            }
            catch { solution = null; }
            if (solution == null || solution.Count == 0)
            {
                try { solution = Clipper.Intersect(subjPaths, clipPaths, FillRule.NonZero); } catch { solution = null; }
            }
            if (solution == null || solution.Count == 0) return null;

            var results = new List<List<Vector2>>();
            int pathIndex = 0;
            foreach (var path in solution)
            {
                pathIndex++;
                if (path == null || path.Count < 3) continue;
                var rawPoly = new List<Vector2>(path.Count);
                foreach (var ip in path)
                {
                    rawPoly.Add(new Vector2((float)(ip.X / scale), (float)(ip.Y / scale)));
                }
                Debug.Log($"Clipper: path[{pathIndex}] rawPts={rawPoly.Count}");

                // Clean: remove consecutive near-duplicates and nearly-collinear points
                var cleaned = new List<Vector2>();
                const float dupEps = 1e-4f;
                for (int i = 0; i < rawPoly.Count; i++)
                {
                    var a = rawPoly[i];
                    var b = rawPoly[(i + 1) % rawPoly.Count];
                    if (Mathf.Abs(a.x - b.x) < dupEps && Mathf.Abs(a.y - b.y) < dupEps) continue;
                    cleaned.Add(a);
                }
                if (cleaned.Count < 3) continue;
                var finalPts = new List<Vector2>();
                const float colEps = 1e-4f;
                for (int i = 0; i < cleaned.Count; i++)
                {
                    var prev = cleaned[(i - 1 + cleaned.Count) % cleaned.Count];
                    var cur = cleaned[i];
                    var next = cleaned[(i + 1) % cleaned.Count];
                    var ux = cur.x - prev.x; var uy = cur.y - prev.y;
                    var vx = next.x - cur.x; var vy = next.y - cur.y;
                    var cross = Mathf.Abs(ux * vy - uy * vx);
                    if (cross <= colEps) continue;
                    finalPts.Add(cur);
                }
                if (finalPts.Count < 3) continue;

                // Ensure CCW winding
                var tmpArr = finalPts.ToArray();
                if (SignedArea(tmpArr) < 0f) Array.Reverse(tmpArr);

                // If polygon is self-intersecting, try to decompose using Clipper.Union on the original integer path
                if (!IsSimple(new List<Vector2>(tmpArr)))
                {
                    Debug.Log($"Clipper: path[{pathIndex}] is not simple; attempting union repair");
                    try
                    {
                        var tempPaths = new Paths64();
                        tempPaths.Add(path);
                        var repaired = Clipper.Union(tempPaths, FillRule.NonZero);
                        if (repaired != null && repaired.Count > 0)
                        {
                            int subIdx = 0;
                            foreach (var rp in repaired)
                            {
                                subIdx++;
                                if (rp == null || rp.Count < 3) continue;
                                var poly2 = new List<Vector2>(rp.Count);
                                foreach (var ip in rp)
                                    poly2.Add(new Vector2((float)(ip.X / scale), (float)(ip.Y / scale)));
                                // clean and ensure CCW
                                var cleaned2 = CleanPolygon(poly2);
                                if (cleaned2.Count < 3) continue;
                                var arr2 = cleaned2.ToArray();
                                if (SignedArea(arr2) < 0f) Array.Reverse(arr2);
                                float area2 = Mathf.Abs(SignedArea(arr2));
                                if (area2 < 1e-6f) continue;
                                results.Add(new List<Vector2>(arr2));
                            }
                            continue; // move to next path
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Clipper: union repair failed: {ex.Message}");
                    }
                }

                // Filter tiny area polygons
                float area = Mathf.Abs(SignedArea(tmpArr));
                if (area < 1e-6f) { Debug.Log($"Clipper: path[{pathIndex}] skipped (tiny area={area})"); continue; }

                results.Add(new List<Vector2>(tmpArr));
            }
            Debug.Log($"Clipper: produced {results.Count} cleaned polygon(s)");
            return results.Count > 0 ? results : null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Clipper2.Intersect failed: {ex.Message}");
            return null;
        }
    }

    // Clip subject polygon with half-plane defined by (pt - mid) dot n <= 0
    List<Point> ClipWithHalfPlane(List<Point> subject, double midX, double midY, double nx, double ny)
    {
        var output = new List<Point>();
        if (subject == null || subject.Count == 0) return output;
        for (int i = 0; i < subject.Count; i++)
        {
            var A = subject[i];
            var B = subject[(i + 1) % subject.Count];
            double va = (A.X - midX) * nx + (A.Y - midY) * ny;
            double vb = (B.X - midX) * nx + (B.Y - midY) * ny;
            bool ina = va <= 0;
            bool inb = vb <= 0;
            if (ina && inb)
            {
                output.Add(B);
            }
            else if (ina && !inb)
            {
                double t = va / (va - vb);
                var ix = A.X + (B.X - A.X) * t;
                var iy = A.Y + (B.Y - A.Y) * t;
                output.Add(new Point(ix, iy));
            }
            else if (!ina && inb)
            {
                double t = va / (va - vb);
                var ix = A.X + (B.X - A.X) * t;
                var iy = A.Y + (B.Y - A.Y) * t;
                output.Add(new Point(ix, iy));
                output.Add(B);
            }
        }
        return output;
    }

    // Quick polygon cleaning helper used after Clipper conversions
    List<Vector2> CleanPolygon(List<Vector2> rawPoly)
    {
        var cleaned = new List<Vector2>();
        const float dupEps = 1e-4f;
        for (int i = 0; i < rawPoly.Count; i++)
        {
            var a = rawPoly[i];
            var b = rawPoly[(i + 1) % rawPoly.Count];
            if (Mathf.Abs(a.x - b.x) < dupEps && Mathf.Abs(a.y - b.y) < dupEps) continue;
            cleaned.Add(a);
        }
        if (cleaned.Count < 3) return new List<Vector2>();
        var finalPts = new List<Vector2>();
        const float colEps = 1e-4f;
        for (int i = 0; i < cleaned.Count; i++)
        {
            var prev = cleaned[(i - 1 + cleaned.Count) % cleaned.Count];
            var cur = cleaned[i];
            var next = cleaned[(i + 1) % cleaned.Count];
            var ux = cur.x - prev.x; var uy = cur.y - prev.y;
            var vx = next.x - cur.x; var vy = next.y - cur.y;
            var cross = Mathf.Abs(ux * vy - uy * vx);
            if (cross <= colEps) continue;
            finalPts.Add(cur);
        }
        return finalPts;
    }

    bool IsSimple(List<Vector2> poly)
    {
        if (poly == null || poly.Count < 4) return true; // triangles are simple
        int n = poly.Count;
        for (int i = 0; i < n; i++)
        {
            var a1 = poly[i];
            var a2 = poly[(i + 1) % n];
            for (int j = i + 1; j < n; j++)
            {
                // skip adjacent edges
                if (j == i || j == (i + 1) % n) continue;
                if ((i == 0 && j == n - 1)) continue;
                var b1 = poly[j];
                var b2 = poly[(j + 1) % n];
                if (SegmentsIntersect(a1, a2, b1, b2)) return false;
            }
        }
        return true;
    }

    // Standard segment-segment intersection (proper or improper)
    bool SegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        float d1 = Cross(a2 - a1, b1 - a1);
        float d2 = Cross(a2 - a1, b2 - a1);
        float d3 = Cross(b2 - b1, a1 - b1);
        float d4 = Cross(b2 - b1, a2 - b1);
        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
            return true;
        // check collinear/endpoint overlap
        if (Mathf.Abs(d1) < 1e-8f && OnSegment(a1, a2, b1)) return true;
        if (Mathf.Abs(d2) < 1e-8f && OnSegment(a1, a2, b2)) return true;
        if (Mathf.Abs(d3) < 1e-8f && OnSegment(b1, b2, a1)) return true;
        if (Mathf.Abs(d4) < 1e-8f && OnSegment(b1, b2, a2)) return true;
        return false;
    }

    float Cross(Vector2 v, Vector2 w) => v.x * w.y - v.y * w.x;

    bool OnSegment(Vector2 a, Vector2 b, Vector2 p)
    {
        return p.x <= Mathf.Max(a.x, b.x) + 1e-8f && p.x + 1e-8f >= Mathf.Min(a.x, b.x)
               && p.y <= Mathf.Max(a.y, b.y) + 1e-8f && p.y + 1e-8f >= Mathf.Min(a.y, b.y);
    }

    // --- Utility: Sutherland-Hodgman polygon clipping (convex only) ---
    List<Point> SutherlandHodgmanClip(List<Point> subject, List<Vector2> clip)
    {
        var output = new List<Point>(subject);
        for (int i = 0; i < clip.Count; i++)
        {
            var input = new List<Point>(output);
            output.Clear();
            Vector2 A = clip[i];
            Vector2 B = clip[(i + 1) % clip.Count];
            for (int j = 0; j < input.Count; j++)
            {
                var P = input[j];
                var Q = input[(j + 1) % input.Count];
                if (IsInside(P, A, B))
                {
                    if (!IsInside(Q, A, B))
                        output.Add(Intersect(P, Q, A, B));
                    output.Add(Q);
                }
                else if (IsInside(Q, A, B))
                {
                    output.Add(Intersect(P, Q, A, B));
                }
            }
        }
        return output;
    }
    bool IsInside(Point p, Vector2 a, Vector2 b)
    {
        return ((b.x - a.x) * ((float)p.Y - a.y) - (b.y - a.y) * ((float)p.X - a.x)) >= 0;
    }
    Point Intersect(Point p, Point q, Vector2 a, Vector2 b)
    {
        float A1 = (float)(q.Y - p.Y);
        float B1 = (float)(p.X - q.X);
        float C1 = A1 * (float)p.X + B1 * (float)p.Y;
        float A2 = b.y - a.y;
        float B2 = a.x - b.x;
        float C2 = A2 * a.x + B2 * a.y;
        float det = A1 * B2 - A2 * B1;
        if (Mathf.Abs(det) < 1e-5f) return p;
        float x = (B2 * C1 - B1 * C2) / det;
        float y = (A1 * C2 - A2 * C1) / det;
        return new Point(x, y);
    }
    bool PointInPolygon(Vector2 pt, List<Vector2> poly)
    {
        int n = poly.Count, j = n - 1;
        bool inside = false;
        for (int i = 0; i < n; j = i++)
        {
            if (((poly[i].y > pt.y) != (poly[j].y > pt.y)) &&
                (pt.x < (poly[j].x - poly[i].x) * (pt.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                inside = !inside;
        }
        return inside;
    }

    // Find nearest point on polygon edges (including vertices) to p
    Vector2 NearestPointOnPolygon(Vector2 p, List<Vector2> poly)
    {
        Vector2 best = poly[0];
        float bestDist2 = float.MaxValue;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            var q = NearestPointOnSegment(p, a, b);
            float d2 = (q - p).sqrMagnitude;
            if (d2 < bestDist2)
            {
                bestDist2 = d2;
                best = q;
            }
        }
        return best;
    }

    Vector2 NearestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float ab2 = Vector2.Dot(ab, ab);
        if (ab2 == 0f) return a;
        float t = Vector2.Dot(p - a, ab) / ab2;
        t = Mathf.Clamp01(t);
        return a + ab * t;
    }
    // --- Utility: Convert polygon to Unity Mesh ---
    Mesh PolygonToMesh(Vector2[] poly)
    {
        var mesh = new Mesh();
        if (poly == null || poly.Length < 3) return mesh;

        // ensure CCW winding for ear clipping
        var pts = new Vector2[poly.Length];
        for (int i = 0; i < poly.Length; i++) pts[i] = poly[i];
        if (SignedArea(pts) < 0f) System.Array.Reverse(pts);

        // vertices
        var verts3 = new Vector3[pts.Length];
        for (int i = 0; i < pts.Length; i++) verts3[i] = new Vector3(pts[i].x, pts[i].y, 0f);
        mesh.vertices = verts3;

        // ear clipping triangulation for arbitrary simple polygons
        var triangles = EarClipTriangulate(pts);
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    float SignedArea(Vector2[] p)
    {
        float a = 0f;
        for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            a += (p[j].x * p[i].y - p[i].x * p[j].y);
        return a * 0.5f;
    }

    int[] EarClipTriangulate(Vector2[] poly)
    {
        var n = poly.Length;
        var indices = new List<int>(n);
        for (int i = 0; i < n; i++) indices.Add(i);
        var tris = new List<int>();
        int safeguard = 0;
        while (indices.Count > 3 && safeguard++ < n * n)
        {
            bool earFound = false;
            for (int i = 0; i < indices.Count; i++)
            {
                int i0 = indices[(i - 1 + indices.Count) % indices.Count];
                int i1 = indices[i];
                int i2 = indices[(i + 1) % indices.Count];
                var a = poly[i0];
                var b = poly[i1];
                var c = poly[i2];
                if (!IsConvex(a, b, c)) continue;
                bool anyInside = false;
                for (int j = 0; j < indices.Count; j++)
                {
                    int vi = indices[j];
                    if (vi == i0 || vi == i1 || vi == i2) continue;
                    if (PointInTriangle(poly[vi], a, b, c)) { anyInside = true; break; }
                }
                if (anyInside) continue;
                // ear
                tris.Add(i0);
                tris.Add(i1);
                tris.Add(i2);
                indices.RemoveAt(i);
                earFound = true;
                break;
            }
            if (!earFound) break; // fallback to avoid infinite loop
        }
        if (indices.Count == 3)
        {
            tris.Add(indices[0]); tris.Add(indices[1]); tris.Add(indices[2]);
        }
        return tris.ToArray();
    }

    bool IsConvex(Vector2 a, Vector2 b, Vector2 c)
    {
        // assuming CCW polygon, convex if cross > 0
        return ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) > 1e-6f;
    }

    bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        // barycentric technique
        var v0 = c - a;
        var v1 = b - a;
        var v2 = p - a;
        float dot00 = Vector2.Dot(v0, v0);
        float dot01 = Vector2.Dot(v0, v1);
        float dot02 = Vector2.Dot(v0, v2);
        float dot11 = Vector2.Dot(v1, v1);
        float dot12 = Vector2.Dot(v1, v2);
        float denom = dot00 * dot11 - dot01 * dot01;
        if (Mathf.Abs(denom) < 1e-9f) return false;
        float u = (dot11 * dot02 - dot01 * dot12) / denom;
        float v = (dot00 * dot12 - dot01 * dot02) / denom;
        return (u >= -1e-4f) && (v >= -1e-4f) && (u + v <= 1 + 1e-4f);
    }

    // --- Generate a Texture2D overlay of Voronoi cells clipped to the polygon bounds ---
    Texture2D GenerateVoronoiOverlay(List<Vector2> worldPoly, List<(Point Site, List<Point> Cell)> cells, int size)
    {
        if (worldPoly == null || worldPoly.Count == 0) return null;
        // compute bounds
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var v in worldPoly)
        {
            if (v.x < minX) minX = v.x;
            if (v.y < minY) minY = v.y;
            if (v.x > maxX) maxX = v.x;
            if (v.y > maxY) maxY = v.y;
        }
        float dataW = Mathf.Max(1e-6f, maxX - minX);
        float dataH = Mathf.Max(1e-6f, maxY - minY);

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        // use a reusable pixel buffer to avoid SetPixel overhead
        var fillColor = new Color32(255, 255, 255, 0);
        if (s_pixelBuffer == null || s_pixelBuffer.Length < size * size) s_pixelBuffer = new Color32[size * size];
        for (int i = 0; i < size * size; i++) s_pixelBuffer[i] = fillColor;

        System.Random rnd = new System.Random(randomSeed);

        // helper to map world -> pixel
        int MapX(float x) => Mathf.RoundToInt(((x - minX) / dataW) * (size - 1));
        int MapY(float y) => Mathf.RoundToInt(((y - minY) / dataH) * (size - 1));

        // Rasterize each clipped cell (clip to the world polygon first, then rasterize)
        foreach (var (site, cell) in cells)
        {
            if (cell == null || cell.Count < 3) continue;
            // clip voronoi cell to the source polygon so we only draw inside it
            var clippedCell = SutherlandHodgmanClip(cell, worldPoly);
            if (clippedCell == null || clippedCell.Count < 3) continue;
            // convert cell points to pixel coordinates
            var polyPx = new List<Vector2Int>();
            foreach (var p in clippedCell)
            {
                int px = MapX((float)p.X);
                int py = MapY((float)p.Y);
                polyPx.Add(new Vector2Int(px, py));
            }
            // random color
            byte r = (byte)rnd.Next(64, 256);
            byte g = (byte)rnd.Next(64, 256);
            byte b = (byte)rnd.Next(64, 256);
            var col = new Color32(r, g, b, 255);

            // centroid fan triangulation using buffer rasterizer
            var v0 = polyPx[0];
            for (int i = 1; i < polyPx.Count - 1; i++)
            {
                var v1 = polyPx[i];
                var v2 = polyPx[i + 1];
                RasterizeTriangleToBuffer(s_pixelBuffer, size, size, v0, v1, v2, col);
            }
        }

        tex.SetPixels32(s_pixelBuffer);
        tex.Apply();
        return tex;
    }

    // Reusable pixel buffer for faster rasterization
    Color32[] s_pixelBuffer = null;

    // Fast scanline/edge-function rasterizer that writes into a Color32 buffer
    void RasterizeTriangleToBuffer(Color32[] buffer, int bufW, int bufH, Vector2Int a, Vector2Int b, Vector2Int c, Color32 color)
    {
        // bounding box
        int minX = Mathf.Clamp(Mathf.Min(a.x, Mathf.Min(b.x, c.x)), 0, bufW - 1);
        int maxX = Mathf.Clamp(Mathf.Max(a.x, Mathf.Max(b.x, c.x)), 0, bufW - 1);
        int minY = Mathf.Clamp(Mathf.Min(a.y, Mathf.Min(b.y, c.y)), 0, bufH - 1);
        int maxY = Mathf.Clamp(Mathf.Max(a.y, Mathf.Max(b.y, c.y)), 0, bufH - 1);

        // edge function coefficients
        int ax = a.x, ay = a.y;
        int bx = b.x, by = b.y;
        int cx = c.x, cy = c.y;

        int area = EdgeFunction(ax, ay, bx, by, cx, cy);
        if (area == 0) return;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int w0 = EdgeFunction(bx, by, cx, cy, x, y);
                int w1 = EdgeFunction(cx, cy, ax, ay, x, y);
                int w2 = EdgeFunction(ax, ay, bx, by, x, y);
                // allow pixels on the edge
                if ((w0 >= 0 && w1 >= 0 && w2 >= 0) || (w0 <= 0 && w1 <= 0 && w2 <= 0))
                {
                    int idx = y * bufW + x;
                    buffer[idx] = color;
                }
            }
        }
    }

    int EdgeFunction(int ax, int ay, int bx, int by, int cx, int cy)
    {
        return (cx - ax) * (by - ay) - (cy - ay) * (bx - ax);
    }

    Color ColorFromSite(Point site)
    {
        // deterministic color derived from site coordinates and seed
        unchecked
        {
            int hx = site.X.GetHashCode();
            int hy = site.Y.GetHashCode();
            int seed = randomSeed ^ hx ^ (hy << 16);
            var rnd = new System.Random(seed);
            return new Color(rnd.Next(64, 256) / 255f, rnd.Next(64, 256) / 255f, rnd.Next(64, 256) / 255f, 1f);
        }
    }

    Texture2D GenerateFragmentTexture(List<Point> worldPoly, Color color, int size)
    {
        if (worldPoly == null || worldPoly.Count < 3) return null;
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var p in worldPoly)
        {
            if ((float)p.X < minX) minX = (float)p.X;
            if ((float)p.Y < minY) minY = (float)p.Y;
            if ((float)p.X > maxX) maxX = (float)p.X;
            if ((float)p.Y > maxY) maxY = (float)p.Y;
        }
        float dataW = Mathf.Max(1e-6f, maxX - minX);
        float dataH = Mathf.Max(1e-6f, maxY - minY);

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var fillColor = new Color32(255, 255, 255, 0);
        if (s_pixelBuffer == null || s_pixelBuffer.Length < size * size) s_pixelBuffer = new Color32[size * size];
        for (int i = 0; i < size * size; i++) s_pixelBuffer[i] = fillColor;

        int MapX(float x) => Mathf.RoundToInt(((x - minX) / dataW) * (size - 1));
        int MapY(float y) => Mathf.RoundToInt(((y - minY) / dataH) * (size - 1));

        // convert worldPoly to pixel coords
        var polyPx = new List<Vector2Int>();
        foreach (var p in worldPoly)
            polyPx.Add(new Vector2Int(MapX((float)p.X), MapY((float)p.Y)));

        var col32 = new Color32((byte)(color.r * 255), (byte)(color.g * 255), (byte)(color.b * 255), 255);
        var v0 = polyPx[0];
        for (int i = 1; i < polyPx.Count - 1; i++)
        {
            var v1 = polyPx[i];
            var v2 = polyPx[i + 1];
            RasterizeTriangleToBuffer(s_pixelBuffer, size, size, v0, v1, v2, col32);
        }
        tex.SetPixels32(s_pixelBuffer);
        tex.Apply();
        return tex;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawSites && !drawCells) return;
        if (polyCollider == null) polyCollider = GetComponent<PolygonCollider2D>();
        if (polyCollider == null || polyCollider.points.Length < 3) return;
        var worldPoly = new List<Vector2>();
        foreach (var pt in polyCollider.points)
            worldPoly.Add(transform.TransformPoint(pt));
        Gizmos.color = Color.yellow;
        for (int i = 0; i < worldPoly.Count; i++)
            Gizmos.DrawLine(worldPoly[i], worldPoly[(i + 1) % worldPoly.Count]);
        // Generate sites deterministically for preview
        var bounds = polyCollider.bounds;
        var rnd = new System.Random(randomSeed);
        int placed = 0, maxTries = siteCount * 10;
        var sites = new List<Vector2>();
        while (placed < siteCount && maxTries-- > 0)
        {
            float x = (float)(bounds.min.x + rnd.NextDouble() * bounds.size.x);
            float y = (float)(bounds.min.y + rnd.NextDouble() * bounds.size.y);
            var v2 = new Vector2(x, y);
            if (PointInPolygon(v2, worldPoly))
            {
                sites.Add(v2);
                placed++;
            }
        }
        if (drawSites)
        {
            Gizmos.color = Color.red;
            foreach (var s in sites)
                Gizmos.DrawSphere(s, 0.05f);
        }
        if (drawCells && sites.Count >= 3)
        {
            // Use geometry lib for Voronoi, then clip to polygon for display
            var points = new List<Geometry.Point>();
            foreach (var s in sites)
                points.Add(new Geometry.Point(s.x, s.y));
            var tris = Geometry.DelaunayTriangulation.Triangulate(points);
            var cells = Geometry.VoronoiGenerator.FromDelaunay(tris);
            foreach (var (site, cell) in cells)
            {
                // Only draw the clipped cell, not the raw Voronoi cell
                var clipped = SutherlandHodgmanClip(cell, worldPoly);
                if (clipped.Count < 3) continue;
                Gizmos.color = Color.cyan;
                for (int i = 0; i < clipped.Count; i++)
                {
                    var a = new Vector3((float)clipped[i].X, (float)clipped[i].Y, 0);
                    var b = new Vector3((float)clipped[(i + 1) % clipped.Count].X, (float)clipped[(i + 1) % clipped.Count].Y, 0);
                    Gizmos.DrawLine(a, b);
                }
                // draw debug raw/clipped cells if available
                if (debugDrawRawCells && debugRawCells != null)
                {
                    Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
                    foreach (var poly in debugRawCells)
                    {
                        for (int i = 0; i < poly.Count; i++)
                        {
                            var a = poly[i];
                            var b = poly[(i + 1) % poly.Count];
                            Gizmos.DrawLine(new Vector3(a.x, a.y, 0), new Vector3(b.x, b.y, 0));
                        }
                    }
                }
                if (debugDrawClippedCells && debugClippedCells != null)
                {
                    Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
                    foreach (var poly in debugClippedCells)
                    {
                        for (int i = 0; i < poly.Count; i++)
                        {
                            var a = poly[i];
                            var b = poly[(i + 1) % poly.Count];
                            Gizmos.DrawLine(new Vector3(a.x, a.y, 0), new Vector3(b.x, b.y, 0));
                        }
                    }
                }
            }
        }
    }

    IEnumerator FractureCoroutine(List<Point> sites, List<Vector2> worldPoly, double clipMinX, double clipMinY, double clipMaxX, double clipMaxY)
    {
        int debugCellIndex = 0;
        int processedThisFrame = 0;
        // local helper for rect polygon using captured clip bounds
        List<Point> RectPolygonLocal() => new List<Point>
        {
            new Point(clipMinX, clipMinY),
            new Point(clipMaxX, clipMinY),
            new Point(clipMaxX, clipMaxY),
            new Point(clipMinX, clipMaxY)
        };

        float frameStart = Time.realtimeSinceStartup;
        foreach (var site in sites)
        {
            System.Diagnostics.Stopwatch swClip = null;
            if (enableProfiling) swClip = System.Diagnostics.Stopwatch.StartNew();
            var rectPoly = RectPolygonLocal();
            foreach (var other in sites)
            {
                if (other.X == site.X && other.Y == site.Y) continue;
                var midX = (site.X + other.X) / 2.0;
                var midY = (site.Y + other.Y) / 2.0;
                var nx = other.X - site.X;
                var ny = other.Y - site.Y;
                rectPoly = ClipWithHalfPlane(rectPoly, midX, midY, nx, ny);
                if (rectPoly.Count == 0) break;
            }
            if (debugDrawRawCells && rectPoly != null && debugRawCells.Count < debugPreviewCount)
                debugRawCells.Add(rectPoly.Select(p => new Vector2((float)p.X, (float)p.Y)).ToList());

            var srcPoly = worldPoly.Select(v => new Point(v.x, v.y)).ToList();
            foreach (var other in sites)
            {
                if (other.X == site.X && other.Y == site.Y) continue;
                var midX = (site.X + other.X) / 2.0;
                var midY = (site.Y + other.Y) / 2.0;
                var nx = other.X - site.X;
                var ny = other.Y - site.Y;
                srcPoly = ClipWithHalfPlane(srcPoly, midX, midY, nx, ny);
                if (srcPoly.Count == 0) break;
            }
            if (enableProfiling && swClip != null)
            {
                swClip.Stop();
                profiling_clippingMs += (float)swClip.Elapsed.TotalMilliseconds;
            }
            if (debugDrawClippedCells && srcPoly != null && debugClippedCells.Count < debugPreviewCount)
                debugClippedCells.Add(srcPoly.Select(p => new Vector2((float)p.X, (float)p.Y)).ToList());

            if (debugCellIndex < debugPreviewCount)
            {
                int rawCount = rectPoly == null ? 0 : rectPoly.Count;
                int srcCount = srcPoly == null ? 0 : srcPoly.Count;
                Debug.Log($"VoronoiFracture2D: Site[{debugCellIndex}] = ({site.X:0.###},{site.Y:0.###}), rawVerts={rawCount}, clippedVerts={srcCount}");
            }
            debugCellIndex++;

            if (srcPoly == null || srcPoly.Count < 3) continue;
            var color = ColorFromSite(site);
            int created = 0;
            bool createdFromClipper = false;
            try
            {
                var rectVec = rectPoly.Select(p => new Vector2((float)p.X, (float)p.Y)).ToList();
                var worldPolyVec = worldPoly.Select(v => new Vector2(v.x, v.y)).ToList();
                System.Diagnostics.Stopwatch swClipper = null;
                if (enableProfiling) swClipper = System.Diagnostics.Stopwatch.StartNew();
                var clipResults = ClipPolygonsWithClipper(rectVec, worldPolyVec);
                if (enableProfiling && swClipper != null)
                {
                    swClipper.Stop();
                    profiling_clipperMs += (float)swClipper.Elapsed.TotalMilliseconds;
                }
                if (clipResults != null && clipResults.Count > 0)
                {
                    foreach (var polyVec in clipResults)
                    {
                        if (polyVec == null || polyVec.Count < 3) continue;
                        if (fragmentCreatedCount < debugFragmentDump)
                        {
                            Debug.Log($"Clipper -> Fragment[{fragmentCreatedCount + 1}] polyPts={polyVec.Count}");
                        }
                        var polyPoints = polyVec.Select(v => new Point(v.x, v.y)).ToList();
                        CreateFragment(polyPoints, color);
                        created++;
                    }
                    if (created > 0) createdFromClipper = true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"VoronoiFracture2D: Clipper2 intersection failed: {ex.Message}");
            }

            if (createdFromClipper)
            {
                processedThisFrame++;
                bool shouldYield = false;
                if (useTimeBudgeting && timeBudgetMs > 0)
                {
                    float elapsedMs = (Time.realtimeSinceStartup - frameStart) * 1000f;
                    if (elapsedMs >= timeBudgetMs) shouldYield = true;
                }
                else if (spreadFractureOverFrames && fragmentsPerFrame > 0 && processedThisFrame >= fragmentsPerFrame)
                {
                    shouldYield = true;
                }

                if (shouldYield)
                {
                    processedThisFrame = 0;
                    yield return null;
                    frameStart = Time.realtimeSinceStartup;
                }
                continue;
            }

            // fallback: use the source-clipped polygon we computed earlier
            CreateFragment(srcPoly, color);
            processedThisFrame++;
            bool shouldYield2 = false;
            if (useTimeBudgeting && timeBudgetMs > 0)
            {
                float elapsedMs2 = (Time.realtimeSinceStartup - frameStart) * 1000f;
                if (elapsedMs2 >= timeBudgetMs) shouldYield2 = true;
            }
            else if (spreadFractureOverFrames && fragmentsPerFrame > 0 && processedThisFrame >= fragmentsPerFrame)
            {
                shouldYield2 = true;
            }
            if (shouldYield2)
            {
                processedThisFrame = 0;
                yield return null;
                frameStart = Time.realtimeSinceStartup;
            }
        }

        // Hide original after we've created all fragments
        gameObject.SetActive(false);
        currentFragmentParent = null;
    }

    // Runtime collision -> fracture support for source object.
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!enableRuntimeFracture) return;
        if (hasFracturedAtRuntime) return;
        if (collision == null) return;
        // approximate impact magnitude using relative velocity and other mass
        float otherMass = collision.rigidbody != null ? collision.rigidbody.mass : 1f;
        float impact = collision.relativeVelocity.magnitude * otherMass;
        if (impact >= breakImpactThreshold)
        {
            // mark and fracture
            hasFracturedAtRuntime = true;
            Fracture();
        }
    }



}
