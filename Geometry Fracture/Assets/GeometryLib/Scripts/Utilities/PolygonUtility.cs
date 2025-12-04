using UnityEngine;
using System.Collections.Generic;
using Geometry;

namespace VoronoiFracture
{
    /// <summary>
    /// Utility methods for polygon operations including area calculation, 
    /// point-in-polygon tests, and polygon simplification.
    /// </summary>
    public static class PolygonUtility
    {
        /// <summary>
        /// Calculate the signed area of a polygon.
        /// Positive = CCW winding, Negative = CW winding.
        /// </summary>
        public static float SignedArea(Vector2[] polygon)
        {
            float area = 0f;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
                area += (polygon[j].x * polygon[i].y - polygon[i].x * polygon[j].y);
            return area * 0.5f;
        }

        /// <summary>
        /// Test if a point is inside a polygon using ray casting algorithm.
        /// </summary>
        public static bool PointInPolygon(Vector2 point, List<Vector2> polygon)
        {
            int n = polygon.Count, j = n - 1;
            bool inside = false;
            for (int i = 0; i < n; j = i++)
            {
                if (((polygon[i].y > point.y) != (polygon[j].y > point.y)) &&
                    (point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x))
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>
        /// Find the nearest point on a polygon's edges to a given point.
        /// </summary>
        public static Vector2 NearestPointOnPolygon(Vector2 point, List<Vector2> polygon)
        {
            Vector2 best = polygon[0];
            float bestDist2 = float.MaxValue;
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                var q = NearestPointOnSegment(point, a, b);
                float d2 = (q - point).sqrMagnitude;
                if (d2 < bestDist2)
                {
                    bestDist2 = d2;
                    best = q;
                }
            }
            return best;
        }

        /// <summary>
        /// Find the nearest point on a line segment to a given point.
        /// </summary>
        public static Vector2 NearestPointOnSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float ab2 = Vector2.Dot(ab, ab);
            if (ab2 == 0f) return a;
            float t = Vector2.Dot(point - a, ab) / ab2;
            t = Mathf.Clamp01(t);
            return a + ab * t;
        }

        /// <summary>
        /// Ensure the polygon has counter-clockwise winding.
        /// </summary>
        public static void EnsureCCW(ref Vector2[] polygon)
        {
            if (SignedArea(polygon) < 0f)
                System.Array.Reverse(polygon);
        }

        /// <summary>
        /// Clean a polygon by removing duplicate and nearly-collinear consecutive points.
        /// </summary>
        public static List<Point> CleanPolygon(List<Point> polygon, float duplicateEpsilon = 1e-4f, float collinearEpsilon = 1e-4f)
        {
            // Remove consecutive near-duplicates
            var cleaned = new List<Point>();
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                if (System.Math.Abs(a.X - b.X) < duplicateEpsilon && System.Math.Abs(a.Y - b.Y) < duplicateEpsilon)
                    continue;
                cleaned.Add(a);
            }

            if (cleaned.Count < 3) return new List<Point>();

            // Remove nearly-collinear points
            var final = new List<Point>();
            for (int i = 0; i < cleaned.Count; i++)
            {
                var prev = cleaned[(i - 1 + cleaned.Count) % cleaned.Count];
                var cur = cleaned[i];
                var next = cleaned[(i + 1) % cleaned.Count];

                // Compute cross product of (cur-prev) × (next-cur)
                float ux = (float)(cur.X - prev.X);
                float uy = (float)(cur.Y - prev.Y);
                float vx = (float)(next.X - cur.X);
                float vy = (float)(next.Y - cur.Y);
                float cross = Mathf.Abs(ux * vy - uy * vx);

                if (cross > collinearEpsilon)
                    final.Add(cur);
            }

            return final;
        }

        /// <summary>
        /// Clean a Vector2 polygon by removing duplicates and collinear points.
        /// </summary>
        public static List<Vector2> CleanPolygon(List<Vector2> polygon, float duplicateEpsilon = 1e-4f, float collinearEpsilon = 1e-4f)
        {
            var cleaned = new List<Vector2>();
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                if (Mathf.Abs(a.x - b.x) < duplicateEpsilon && Mathf.Abs(a.y - b.y) < duplicateEpsilon)
                    continue;
                cleaned.Add(a);
            }

            if (cleaned.Count < 3) return new List<Vector2>();

            var final = new List<Vector2>();
            for (int i = 0; i < cleaned.Count; i++)
            {
                var prev = cleaned[(i - 1 + cleaned.Count) % cleaned.Count];
                var cur = cleaned[i];
                var next = cleaned[(i + 1) % cleaned.Count];
                var ux = cur.x - prev.x;
                var uy = cur.y - prev.y;
                var vx = next.x - cur.x;
                var vy = next.y - cur.y;
                var cross = Mathf.Abs(ux * vy - uy * vx);
                if (cross > collinearEpsilon)
                    final.Add(cur);
            }

            return final;
        }

        /// <summary>
        /// Check if a polygon is simple (no self-intersections).
        /// </summary>
        public static bool IsSimple(List<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 4) return true;
            int n = polygon.Count;
            for (int i = 0; i < n; i++)
            {
                var a1 = polygon[i];
                var a2 = polygon[(i + 1) % n];
                for (int j = i + 1; j < n; j++)
                {
                    // Skip adjacent edges
                    if (j == i || j == (i + 1) % n) continue;
                    if ((i == 0 && j == n - 1)) continue;
                    var b1 = polygon[j];
                    var b2 = polygon[(j + 1) % n];
                    if (SegmentsIntersect(a1, a2, b1, b2)) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Test if two line segments intersect.
        /// </summary>
        public static bool SegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
        {
            float d1 = Cross(a2 - a1, b1 - a1);
            float d2 = Cross(a2 - a1, b2 - a1);
            float d3 = Cross(b2 - b1, a1 - b1);
            float d4 = Cross(b2 - b1, a2 - b1);

            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
                return true;

            // Check collinear/endpoint overlap
            const float epsilon = 1e-8f;
            if (Mathf.Abs(d1) < epsilon && OnSegment(a1, a2, b1)) return true;
            if (Mathf.Abs(d2) < epsilon && OnSegment(a1, a2, b2)) return true;
            if (Mathf.Abs(d3) < epsilon && OnSegment(b1, b2, a1)) return true;
            if (Mathf.Abs(d4) < epsilon && OnSegment(b1, b2, a2)) return true;
            return false;
        }

        private static float Cross(Vector2 v, Vector2 w) => v.x * w.y - v.y * w.x;

        private static bool OnSegment(Vector2 a, Vector2 b, Vector2 p)
        {
            const float epsilon = 1e-8f;
            return p.x <= Mathf.Max(a.x, b.x) + epsilon && p.x + epsilon >= Mathf.Min(a.x, b.x)
                   && p.y <= Mathf.Max(a.y, b.y) + epsilon && p.y + epsilon >= Mathf.Min(a.y, b.y);
        }
    }
}
