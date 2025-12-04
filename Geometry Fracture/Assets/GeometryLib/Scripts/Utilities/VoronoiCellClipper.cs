using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Geometry;
using Clipper2Lib;

namespace VoronoiFracture
{
    /// <summary>
    /// Handles clipping of Voronoi cells using half-plane intersection and Clipper2 library.
    /// </summary>
    public class VoronoiCellClipper
    {
        private const double ClipperScale = 1000.0;

        /// <summary>
        /// Clip a Voronoi cell defined by a site against all other sites using half-plane intersection,
        /// then clip against the boundary polygon.
        /// </summary>
        public List<Point> ClipCellToPolygon(Point site, List<Point> allSites, List<Vector2> boundaryPolygon, Bounds clipRect)
        {
            // Start with a large bounding rectangle
            var cell = CreateBoundingRectangle(clipRect);

            // Clip against all other sites using half-plane intersection
            foreach (var otherSite in allSites)
            {
                if (otherSite.X == site.X && otherSite.Y == site.Y)
                    continue;

                var midX = (site.X + otherSite.X) / 2.0;
                var midY = (site.Y + otherSite.Y) / 2.0;
                var nx = otherSite.X - site.X;
                var ny = otherSite.Y - site.Y;

                cell = ClipWithHalfPlane(cell, midX, midY, nx, ny);
                if (cell.Count == 0) break;
            }

            if (cell.Count < 3) return new List<Point>();

            // Clip the Voronoi cell against the boundary polygon
            return ClipAgainstBoundary(cell, boundaryPolygon);
        }

        /// <summary>
        /// Clip Voronoi cell against boundary using Clipper2 (robust) or Sutherland-Hodgman (simple fallback).
        /// </summary>
        private List<Point> ClipAgainstBoundary(List<Point> cell, List<Vector2> boundary)
        {
            // Try Clipper2 first for robust intersection
            var cellVec = cell.Select(p => new Vector2((float)p.X, (float)p.Y)).ToList();
            var clipperResult = ClipWithClipper2(cellVec, boundary);

            if (clipperResult != null && clipperResult.Count > 0)
            {
                // Use the first (largest) polygon from Clipper result
                var bestPoly = clipperResult[0];
                return bestPoly.Select(v => new Point(v.x, v.y)).ToList();
            }

            // Fallback to Sutherland-Hodgman (only works for convex boundaries)
            return SutherlandHodgmanClip(cell, boundary);
        }

        /// <summary>
        /// Clip using Clipper2 library for robust polygon intersection.
        /// </summary>
        private List<List<Vector2>> ClipWithClipper2(List<Vector2> subject, List<Vector2> clip)
        {
            if (subject == null || clip == null || subject.Count < 3 || clip.Count < 3)
                return null;

            try
            {
                // Convert to Clipper2 integer paths
                var subjPaths = new Paths64();
                var clipPaths = new Paths64();

                var subjPath = new Path64();
                foreach (var v in subject)
                    subjPath.Add(new Point64((long)Math.Round(v.x * ClipperScale), (long)Math.Round(v.y * ClipperScale)));
                subjPaths.Add(subjPath);

                var clipPath = new Path64();
                foreach (var v in clip)
                    clipPath.Add(new Point64((long)Math.Round(v.x * ClipperScale), (long)Math.Round(v.y * ClipperScale)));
                clipPaths.Add(clipPath);

                // Compute intersection
                Paths64 solution = null;
                try
                {
                    solution = Clipper.Intersect(subjPaths, clipPaths, FillRule.EvenOdd);
                }
                catch
                {
                    solution = null;
                }

                if (solution == null || solution.Count == 0)
                {
                    try
                    {
                        solution = Clipper.Intersect(subjPaths, clipPaths, FillRule.NonZero);
                    }
                    catch
                    {
                        solution = null;
                    }
                }

                if (solution == null || solution.Count == 0)
                    return null;

                var results = new List<List<Vector2>>();
                foreach (var path in solution)
                {
                    if (path == null || path.Count < 3) continue;

                    var polygon = new List<Vector2>(path.Count);
                    foreach (var pt in path)
                        polygon.Add(new Vector2((float)(pt.X / ClipperScale), (float)(pt.Y / ClipperScale)));

                    // Clean and validate the polygon
                    var cleaned = PolygonUtility.CleanPolygon(polygon);
                    if (cleaned.Count < 3) continue;

                    // Ensure CCW winding
                    var arr = cleaned.ToArray();
                    if (PolygonUtility.SignedArea(arr) < 0f)
                        Array.Reverse(arr);

                    // If self-intersecting, try to repair with Union
                    if (!PolygonUtility.IsSimple(new List<Vector2>(arr)))
                    {
                        var repaired = RepairSelfIntersectingPolygon(path);
                        if (repaired != null)
                        {
                            results.AddRange(repaired);
                            continue;
                        }
                    }

                    // Filter tiny polygons
                    float area = Mathf.Abs(PolygonUtility.SignedArea(arr));
                    if (area >= 1e-6f)
                        results.Add(new List<Vector2>(arr));
                }

                return results.Count > 0 ? results : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Clipper2 intersection failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Attempt to repair self-intersecting polygon using Clipper2 Union.
        /// </summary>
        private List<List<Vector2>> RepairSelfIntersectingPolygon(Path64 path)
        {
            try
            {
                var tempPaths = new Paths64 { path };
                var repaired = Clipper.Union(tempPaths, FillRule.NonZero);

                if (repaired == null || repaired.Count == 0)
                    return null;

                var results = new List<List<Vector2>>();
                foreach (var rp in repaired)
                {
                    if (rp == null || rp.Count < 3) continue;

                    var polygon = new List<Vector2>(rp.Count);
                    foreach (var pt in rp)
                        polygon.Add(new Vector2((float)(pt.X / ClipperScale), (float)(pt.Y / ClipperScale)));

                    var cleaned = PolygonUtility.CleanPolygon(polygon);
                    if (cleaned.Count < 3) continue;

                    var arr = cleaned.ToArray();
                    if (PolygonUtility.SignedArea(arr) < 0f)
                        Array.Reverse(arr);

                    float area = Mathf.Abs(PolygonUtility.SignedArea(arr));
                    if (area >= 1e-6f)
                        results.Add(new List<Vector2>(arr));
                }

                return results.Count > 0 ? results : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Sutherland-Hodgman polygon clipping (works only for convex clip polygons).
        /// </summary>
        private List<Point> SutherlandHodgmanClip(List<Point> subject, List<Vector2> clip)
        {
            var output = new List<Point>(subject);
            for (int i = 0; i < clip.Count; i++)
            {
                var input = new List<Point>(output);
                output.Clear();
                Vector2 edgeA = clip[i];
                Vector2 edgeB = clip[(i + 1) % clip.Count];

                for (int j = 0; j < input.Count; j++)
                {
                    var current = input[j];
                    var next = input[(j + 1) % input.Count];

                    if (IsInside(current, edgeA, edgeB))
                    {
                        if (!IsInside(next, edgeA, edgeB))
                            output.Add(LineIntersect(current, next, edgeA, edgeB));
                        output.Add(next);
                    }
                    else if (IsInside(next, edgeA, edgeB))
                    {
                        output.Add(LineIntersect(current, next, edgeA, edgeB));
                    }
                }
            }
            return output;
        }

        private bool IsInside(Point p, Vector2 edgeA, Vector2 edgeB)
        {
            return ((edgeB.x - edgeA.x) * ((float)p.Y - edgeA.y) - (edgeB.y - edgeA.y) * ((float)p.X - edgeA.x)) >= 0;
        }

        private Point LineIntersect(Point p1, Point p2, Vector2 edgeA, Vector2 edgeB)
        {
            float a1 = (float)(p2.Y - p1.Y);
            float b1 = (float)(p1.X - p2.X);
            float c1 = a1 * (float)p1.X + b1 * (float)p1.Y;

            float a2 = edgeB.y - edgeA.y;
            float b2 = edgeA.x - edgeB.x;
            float c2 = a2 * edgeA.x + b2 * edgeA.y;

            float det = a1 * b2 - a2 * b1;
            if (Mathf.Abs(det) < 1e-5f) return p1;

            float x = (b2 * c1 - b1 * c2) / det;
            float y = (a1 * c2 - a2 * c1) / det;
            return new Point(x, y);
        }

        /// <summary>
        /// Clip a polygon with a half-plane defined by (pt - mid) · n ≤ 0.
        /// </summary>
        private List<Point> ClipWithHalfPlane(List<Point> subject, double midX, double midY, double nx, double ny)
        {
            var output = new List<Point>();
            if (subject == null || subject.Count == 0) return output;

            for (int i = 0; i < subject.Count; i++)
            {
                var current = subject[i];
                var next = subject[(i + 1) % subject.Count];

                double currentDist = (current.X - midX) * nx + (current.Y - midY) * ny;
                double nextDist = (next.X - midX) * nx + (next.Y - midY) * ny;

                bool currentInside = currentDist <= 0;
                bool nextInside = nextDist <= 0;

                if (currentInside && nextInside)
                {
                    output.Add(next);
                }
                else if (currentInside && !nextInside)
                {
                    double t = currentDist / (currentDist - nextDist);
                    var intersectX = current.X + (next.X - current.X) * t;
                    var intersectY = current.Y + (next.Y - current.Y) * t;
                    output.Add(new Point(intersectX, intersectY));
                }
                else if (!currentInside && nextInside)
                {
                    double t = currentDist / (currentDist - nextDist);
                    var intersectX = current.X + (next.X - current.X) * t;
                    var intersectY = current.Y + (next.Y - current.Y) * t;
                    output.Add(new Point(intersectX, intersectY));
                    output.Add(next);
                }
            }

            return output;
        }

        /// <summary>
        /// Create a bounding rectangle around the clip area.
        /// </summary>
        private List<Point> CreateBoundingRectangle(Bounds bounds)
        {
            double margin = Mathf.Max(bounds.size.x, bounds.size.y) * 2.0 + 1.0;
            return new List<Point>
            {
                new Point(bounds.min.x - margin, bounds.min.y - margin),
                new Point(bounds.max.x + margin, bounds.min.y - margin),
                new Point(bounds.max.x + margin, bounds.max.y + margin),
                new Point(bounds.min.x - margin, bounds.max.y + margin)
            };
        }
    }
}
