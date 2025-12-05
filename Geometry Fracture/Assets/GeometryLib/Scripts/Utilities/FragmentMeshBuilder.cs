using UnityEngine;
using System.Collections.Generic;

namespace VoronoiFracture
{
    /// <summary>
    /// Builds 2D meshes from polygon data using ear clipping triangulation.
    /// </summary>
    public static class FragmentMeshBuilder
    {
        /// <summary>
        /// Convert a polygon to a Unity Mesh using ear clipping triangulation.
        /// </summary>
        /// <param name="polygon">Polygon vertices in local space</param>
        /// <returns>Generated mesh</returns>
        public static Mesh CreateMesh(Vector2[] polygon)
        {
            var mesh = new Mesh();
            if (polygon == null || polygon.Length < 3)
                return mesh;

            // Ensure CCW winding for ear clipping
            var vertices = new Vector2[polygon.Length];
            for (int i = 0; i < polygon.Length; i++)
                vertices[i] = polygon[i];

            PolygonUtility.EnsureCCW(ref vertices);

            // Convert to 3D vertices
            var vertices3D = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                vertices3D[i] = new Vector3(vertices[i].x, vertices[i].y, 0f);

            mesh.vertices = vertices3D;
            mesh.triangles = EarClipTriangulate(vertices);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Create UVs for a mesh based on world-space bounds.
        /// </summary>
        public static Vector2[] CreateUVs(Vector3[] meshVertices, Vector2 centroid, float minX, float minY, float width, float height)
        {
            var uvs = new Vector2[meshVertices.Length];
            for (int i = 0; i < meshVertices.Length; i++)
            {
                // Convert local vertex back to world space
                var worldPos = new Vector2(meshVertices[i].x + centroid.x, meshVertices[i].y + centroid.y);
                uvs[i] = new Vector2((worldPos.x - minX) / width, (worldPos.y - minY) / height);
            }
            return uvs;
        }

        /// <summary>
        /// Triangulate a simple polygon using ear clipping algorithm.
        /// </summary>
        private static int[] EarClipTriangulate(Vector2[] polygon)
        {
            int n = polygon.Length;
            var indices = new List<int>(n);
            for (int i = 0; i < n; i++)
                indices.Add(i);

            var triangles = new List<int>();
            int safeguard = 0;

            while (indices.Count > 3 && safeguard++ < n * n)
            {
                bool earFound = false;
                for (int i = 0; i < indices.Count; i++)
                {
                    int i0 = indices[(i - 1 + indices.Count) % indices.Count];
                    int i1 = indices[i];
                    int i2 = indices[(i + 1) % indices.Count];

                    var a = polygon[i0];
                    var b = polygon[i1];
                    var c = polygon[i2];

                    // Check if this is a convex vertex
                    if (!IsConvex(a, b, c))
                        continue;

                    // Check if any other vertex is inside this triangle
                    bool anyInside = false;
                    for (int j = 0; j < indices.Count; j++)
                    {
                        int vi = indices[j];
                        if (vi == i0 || vi == i1 || vi == i2)
                            continue;

                        if (PointInTriangle(polygon[vi], a, b, c))
                        {
                            anyInside = true;
                            break;
                        }
                    }

                    if (anyInside)
                        continue;

                    // Found an ear - add triangle and remove middle vertex
                    triangles.Add(i0);
                    triangles.Add(i1);
                    triangles.Add(i2);
                    indices.RemoveAt(i);
                    earFound = true;
                    break;
                }

                if (!earFound)
                    break;
            }

            // Add final triangle if exactly 3 vertices remain
            if (indices.Count == 3)
            {
                triangles.Add(indices[0]);
                triangles.Add(indices[1]);
                triangles.Add(indices[2]);
            }

            return triangles.ToArray();
        }

        /// <summary>
        /// Check if three consecutive vertices form a convex angle
        /// </summary>
        private static bool IsConvex(Vector2 a, Vector2 b, Vector2 c)
        {
            return ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) > 1e-6f;
        }

        /// <summary>
        /// Test if a point is inside a triangle.
        /// </summary>
        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            var v0 = c - a;
            var v1 = b - a;
            var v2 = p - a;

            float dot00 = Vector2.Dot(v0, v0);
            float dot01 = Vector2.Dot(v0, v1);
            float dot02 = Vector2.Dot(v0, v2);
            float dot11 = Vector2.Dot(v1, v1);
            float dot12 = Vector2.Dot(v1, v2);

            float denom = dot00 * dot11 - dot01 * dot01;
            if (Mathf.Abs(denom) < 1e-9f)
                return false;

            float u = (dot11 * dot02 - dot01 * dot12) / denom;
            float v = (dot00 * dot12 - dot01 * dot02) / denom;

            return (u >= -1e-4f) && (v >= -1e-4f) && (u + v <= 1 + 1e-4f);
        }
    }
}
