using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Geometry;

// Attach to generated pieces to allow them to split on strong collisions.
public class PieceFracture : MonoBehaviour
{
    public Material pieceMaterial;
    public float thickness = 0.2f;
    public int splitSiteCount = 4;
    public float splitThreshold = 1.0f; // impact threshold (tweak)
    public VoronoiFracture.FracturePlane fracturePlane = VoronoiFracture.FracturePlane.XY;
    // Explosion / impulse tuning (tweak these to control how fragments fly apart)
    [Tooltip("Multiplier applied to the computed impact to form an explosion force")]
    public float explosionForce = 0.5f;
    [Tooltip("Radius used by AddExplosionForce when applying impulse")]
    public float explosionRadius = 1.0f;
    [Tooltip("Up modifier used by AddExplosionForce (0 = no up boost)")]
    public float explosionUpModifier = 0.0f;
    [Tooltip("Random variation applied to the explosion force (0..1)")]
    public float explosionRandomness = 0.25f;

    [Tooltip("Scale multiplier applied to the 2D polygon before extrusion for child fragments (1 = unchanged). Use >1 to stretch pieces in-plane.")]
    public float fragmentScale = 1.0f;

    // Recursion control: limit how many times a piece can be recursively fractured.
    [Tooltip("Maximum recursion depth for recursive fractures. 0 = no recursion (only this fracture).")]
    public int recursionLimit = 1;
    // Current recursion depth (0 for original whole)
    [HideInInspector]
    public int recursionDepth = 0;

    // Minimal runtime: fracture immediately on any collision.
    bool fractured = false;
    void Start()
    {
        // Intentionally left blank for minimal runtime behavior
    }
    void OnCollisionEnter(Collision collision)
    {
        if (fractured) return;

        // Immediately fracture on any collision: use first contact point as fracture origin
        if (collision == null) return;
        Vector3 contactWorld = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;
        Vector3 contactLocal3 = transform.InverseTransformPoint(contactWorld);
        var contactLocal = ProjectToPlane(contactLocal3, fracturePlane);

        // Compute impact magnitude. Prefer collision impulse if available; otherwise estimate from relative velocity and mass.
        float impact = 0f;
        try
        {
            if (collision.impulse.sqrMagnitude > 0f) impact = collision.impulse.magnitude;
            else impact = collision.relativeVelocity.magnitude * (collision.rigidbody != null ? collision.rigidbody.mass : 1f);
        }
        catch { impact = collision.relativeVelocity.magnitude; }

        // Respect the split threshold so resting contacts (small or zero impulse) do not fracture the piece
        if (impact < splitThreshold)
        {
            // small impact; ignore
            return;
        }

        ForceSplitAt(contactLocal, impact);
    }


    // Public: force a split at a local contact point with a given impact (usable from editor)
    public void ForceSplitAt(Vector2 contactLocal, float impact)
    {
        // Don't split for low-impact calls
        if (impact < splitThreshold) return;
        if (fractured) return;
        fractured = true;


        // Get polygon from mesh (top vertices are first half of extruded mesh), projected according to fracture plane
        var mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        var mesh = mf.sharedMesh;
        int n = mesh.vertexCount / 2;
        var verts = mesh.vertices;
        var poly = new List<Vector2>(n);
        for (int i = 0; i < n; i++) poly.Add(ProjectToPlane(verts[i], fracturePlane));

        // Generate split sites biased around contact point
        var rnd = new System.Random(Environment.TickCount);
        var sites = new List<Vector2>();
        // Add a site at the contact
        sites.Add(contactLocal);
        // Add random sites within polygon bounding box, biased towards contact
        var minX = poly.Min(p => p.x); var maxX = poly.Max(p => p.x);
        var minY = poly.Min(p => p.y); var maxY = poly.Max(p => p.y);
        for (int i = 1; i < splitSiteCount; i++)
        {
            float rx = (float)rnd.NextDouble();
            float ry = (float)rnd.NextDouble();
            var rxp = minX + rx * (maxX - minX);
            var ryp = minY + ry * (maxY - minY);
            var bx = Mathf.Lerp(rxp, contactLocal.x, 0.6f * (float)rnd.NextDouble());
            var by = Mathf.Lerp(ryp, contactLocal.y, 0.6f * (float)rnd.NextDouble());
            sites.Add(new Vector2(bx, by));
        }

        // Build points for Delaunay
        var pts = new List<Point>();
        foreach (var s in sites) pts.Add(new Point(s.x, s.y));

        var triangles = DelaunayTriangulation.Triangulate(pts);
        var vor = VoronoiGenerator.FromDelaunay(triangles);

        // For each voronoi cell, convert to Vector2 polygon and clip to original poly
        int idx = 0;
        var pieces = new List<GameObject>();
        foreach (object pair in vor)
        {
            Point site;
            List<Point> cellPoints;
            if (pair is ValueTuple<Point, List<Point>> vt)
            {
                site = vt.Item1; cellPoints = vt.Item2;
            }
            else if (pair is KeyValuePair<Point, List<Point>> kv)
            {
                site = kv.Key; cellPoints = kv.Value;
            }
            else
            {
                var t = pair.GetType();
                var f1 = t.GetField("Item1");
                var f2 = t.GetField("Item2");
                if (f1 != null && f2 != null)
                {
                    site = (Point)f1.GetValue(pair);
                    cellPoints = (List<Point>)f2.GetValue(pair);
                }
                else
                {
                    var pKey = t.GetProperty("Key");
                    var pValue = t.GetProperty("Value");
                    if (pKey != null && pValue != null)
                    {
                        site = (Point)pKey.GetValue(pair);
                        cellPoints = (List<Point>)pValue.GetValue(pair);
                    }
                    else continue;
                }
            }

            var cellPoly = cellPoints.Select(p => new Vector2((float)p.X, (float)p.Y)).ToList();
            if (cellPoly.Count < 3) continue;

            var clipped = ClipPolygonToPolygon(cellPoly, poly);
            if (clipped == null || clipped.Count < 3) continue;

            // Optionally scale the clipped polygon before extrusion
            if (!Mathf.Approximately(fragmentScale, 1f)) clipped = ScalePolygon(clipped, fragmentScale);

            var pieceMesh = CreateExtrudedMesh(clipped, thickness, fracturePlane);

            var go = new GameObject(gameObject.name + $"_frag_{idx}");
            go.transform.SetParent(transform.parent, false);
            go.transform.localPosition = transform.localPosition;
            go.transform.localRotation = transform.localRotation;
            var mfNew = go.AddComponent<MeshFilter>(); mfNew.sharedMesh = pieceMesh;
            var mrNew = go.AddComponent<MeshRenderer>(); if (pieceMaterial != null) mrNew.sharedMaterial = pieceMaterial;
            var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = pieceMesh; mc.convex = true;
            var rb = go.AddComponent<Rigidbody>(); rb.mass = Mathf.Max(0.05f, MeshArea(clipped));

            // Add PieceFracture to allow recursive splitting, but reduce site count and increase threshold
            // Only attach a PieceFracture if we haven't reached recursion limit
            if (recursionDepth + 1 <= recursionLimit)
            {
                var pf = go.AddComponent<PieceFracture>();
                pf.pieceMaterial = pieceMaterial; pf.thickness = thickness; pf.splitSiteCount = Math.Max(2, splitSiteCount - 1); pf.splitThreshold = splitThreshold * 1.25f;
                pf.recursionLimit = recursionLimit;
                pf.recursionDepth = recursionDepth + 1;
                // propagate in-plane scale to children
                pf.fragmentScale = fragmentScale;
            }

            // Apply an outward impulse from contact using explosion tuning params
            var centroid = Centroid(clipped);
            var worldCentroid = go.transform.TransformPoint(UnprojectFromPlane(centroid, fracturePlane));
            var contactWorld = transform.TransformPoint(UnprojectFromPlane(contactLocal, fracturePlane));
            // Compute base force from impact and tuning multiplier, add some variation
            float baseForce = impact * explosionForce;
            float variation = (float)(1.0 + (rnd.NextDouble() * 2.0 - 1.0) * explosionRandomness);
            float appliedForce = baseForce * variation;
            // Use AddExplosionForce so pieces fly outward from the contact point
            rb.AddExplosionForce(appliedForce, contactWorld, explosionRadius, explosionUpModifier, ForceMode.Impulse);

            pieces.Add(go);
            idx++;
        }

        // destroy original
        Destroy(gameObject);
    }

    // Helper: centroid
    Vector2 Centroid(List<Vector2> poly)
    {
        float x = 0, y = 0; float a = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            var p1 = poly[i]; var p2 = poly[(i + 1) % poly.Count];
            float cross = p1.x * p2.y - p2.x * p1.y;
            x += (p1.x + p2.x) * cross; y += (p1.y + p2.y) * cross; a += cross;
        }
        a *= 0.5f; if (Mathf.Approximately(a, 0f)) return poly[0];
        return new Vector2(x / (6f * a), y / (6f * a));
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

    // Clip against directed edge AB, keep left side
    List<Vector2> ClipAgainstEdge(List<Vector2> subject, Vector2 A, Vector2 B)
    {
        var result = new List<Vector2>();
        if (subject.Count == 0) return result;
        Vector2 prev = subject[subject.Count - 1];
        bool prevInside = IsLeft(A, B, prev) >= 0f;
        for (int i = 0; i < subject.Count; i++)
        {
            Vector2 cur = subject[i]; bool curInside = IsLeft(A, B, cur) >= 0f;
            if (curInside)
            {
                if (!prevInside) if (LineIntersect(prev, cur, A, B, out Vector2 ip)) result.Add(ip);
                result.Add(cur);
            }
            else if (prevInside)
            {
                if (LineIntersect(prev, cur, A, B, out Vector2 ip)) result.Add(ip);
            }
            prev = cur; prevInside = curInside;
        }
        return result;
    }

    float IsLeft(Vector2 A, Vector2 B, Vector2 P) => (B.x - A.x) * (P.y - A.y) - (B.y - A.y) * (P.x - A.x);

    // Simple centroid-based scaling used when stretching fragments in-plane
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

    bool LineIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out Vector2 ip)
    {
        ip = Vector2.zero;
        var s1 = p2 - p1; var s2 = p4 - p3;
        float denom = (-s2.x * s1.y + s1.x * s2.y);
        if (Mathf.Approximately(denom, 0f)) return false;
        float s = (-s1.y * (p1.x - p3.x) + s1.x * (p1.y - p3.y)) / denom;
        float t = (s2.x * (p1.y - p3.y) - s2.y * (p1.x - p3.x)) / denom;
        if (s >= 0 && s <= 1 && t >= 0 && t <= 1)
        {
            ip = p1 + (t * s1); return true;
        }
        return false;
    }

    // Triangulation and mesh creation (similar to VoronoiFracture)
    List<int> Triangulate(List<Vector2> poly)
    {
        var indices = new List<int>(); int n = poly.Count; if (n < 3) return indices;
        List<int> V = Enumerable.Range(0, n).ToList(); int guard = 0;
        while (V.Count > 3 && guard++ < 1000)
        {
            bool earFound = false;
            for (int i = 0; i < V.Count; i++)
            {
                int prev = V[(i + V.Count - 1) % V.Count]; int curr = V[i]; int next = V[(i + 1) % V.Count];
                var A = poly[prev]; var B = poly[curr]; var C = poly[next];
                if (IsLeft(A, B, C) <= 0) continue;
                bool anyInside = false;
                for (int j = 0; j < V.Count; j++)
                {
                    int vi = V[j]; if (vi == prev || vi == curr || vi == next) continue;
                    if (PointInTriangle(poly[vi], A, B, C)) { anyInside = true; break; }
                }
                if (anyInside) continue;
                indices.Add(prev); indices.Add(curr); indices.Add(next); V.RemoveAt(i); earFound = true; break;
            }
            if (!earFound) break;
        }
        if (V.Count == 3) { indices.Add(V[0]); indices.Add(V[1]); indices.Add(V[2]); }
        return indices;
    }

    bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float a1 = IsLeft(p, a, b); float a2 = IsLeft(p, b, c); float a3 = IsLeft(p, c, a);
        bool hasNeg = (a1 < 0) || (a2 < 0) || (a3 < 0); bool hasPos = (a1 > 0) || (a2 > 0) || (a3 > 0);
        return !(hasNeg && hasPos);
    }

    Mesh CreateExtrudedMesh(List<Vector2> poly, float depth, VoronoiFracture.FracturePlane plane)
    {
        var mesh = new Mesh(); mesh.name = "VoronoiPiece";
        var triIndices = Triangulate(poly);
        int n = poly.Count;
        var verts = new List<Vector3>(n * 2);

        // Top vertices (positive normal offset)
        for (int i = 0; i < n; i++) verts.Add(UnprojectFromPlane(poly[i], plane, depth * 0.5f));
        // Bottom vertices (negative normal offset)
        for (int i = 0; i < n; i++) verts.Add(UnprojectFromPlane(poly[i], plane, -depth * 0.5f));

        var tris = new List<int>();
        // Top face
        for (int i = 0; i < triIndices.Count; i += 3) { tris.Add(triIndices[i]); tris.Add(triIndices[i + 1]); tris.Add(triIndices[i + 2]); }
        // Bottom face (reverse)
        for (int i = 0; i < triIndices.Count; i += 3) { tris.Add(n + triIndices[i + 2]); tris.Add(n + triIndices[i + 1]); tris.Add(n + triIndices[i]); }
        // Sides
        for (int i = 0; i < n; i++) { int ni = (i + 1) % n; int topA = i; int topB = ni; int botA = n + i; int botB = n + ni; tris.Add(topA); tris.Add(topB); tris.Add(botB); tris.Add(topA); tris.Add(botB); tris.Add(botA); }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Project a 3D local point to 2D according to the fracture plane
    public Vector2 ProjectToPlane(Vector3 v, VoronoiFracture.FracturePlane plane)
    {
        switch (plane)
        {
            case VoronoiFracture.FracturePlane.XZ: return new Vector2(v.x, v.z);
            case VoronoiFracture.FracturePlane.YZ: return new Vector2(v.y, v.z);
            case VoronoiFracture.FracturePlane.XY:
            default: return new Vector2(v.x, v.y);
        }
    }

    // Convert a 2D polygon point back into a 3D local position on the given plane, with optional offset along the normal (depth)
    Vector3 UnprojectFromPlane(Vector2 p, VoronoiFracture.FracturePlane plane, float normalOffset = 0f)
    {
        switch (plane)
        {
            case VoronoiFracture.FracturePlane.XZ: return new Vector3(p.x, normalOffset, p.y);
            case VoronoiFracture.FracturePlane.YZ: return new Vector3(normalOffset, p.x, p.y);
            case VoronoiFracture.FracturePlane.XY:
            default: return new Vector3(p.x, p.y, normalOffset);
        }
    }

    // Overload for centroid/unproject where depth is zero
    Vector3 UnprojectFromPlane(Vector2 p, VoronoiFracture.FracturePlane plane) => UnprojectFromPlane(p, plane, 0f);

    float MeshArea(List<Vector2> poly)
    {
        float a = 0f; for (int i = 0; i < poly.Count; i++) { var p1 = poly[i]; var p2 = poly[(i + 1) % poly.Count]; a += p1.x * p2.y - p2.x * p1.y; }
        return Mathf.Abs(a) * 0.5f;
    }
}
