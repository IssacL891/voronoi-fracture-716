using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Geometry;
using Clipper2Lib;

namespace VoronoiFracture
{
    /// <summary>
    /// Generates Voronoi cells using Delaunay triangulation from Geometry-Lib,
    /// then uses half-plane clipping and Clipper2 for fixing unbounded edge cells.
    /// </summary>
    public class VoronoiCellClipper
    {
        private const double ClipperScale = 1000.0;

        /// <summary>
        /// Generate all Voronoi cells using Delaunay triangulation from Geometry-Lib.
        /// Each cell is then clipped to the boundary polygon.
        /// For unbounded edge cells, fix with half-plane clipping.
        /// </summary>
        /// <param name="sites">List of Voronoi sites</param>
        /// <param name="boundaryPolygon">Polygon boundary in world space</param>
        /// <param name="clipRect">Bounding box for clipping</param>
        /// <returns>List of (Site, ClippedCell) tuples</returns>
        public List<(Point Site, List<Point> Cell)> GenerateVoronoiCells(List<Point> sites, List<Vector2> boundaryPolygon, Bounds clipRect)
        {
            var result = new List<(Point Site, List<Point> Cell)>();

            if (sites == null || sites.Count < 3)
            {
                Debug.LogWarning("VoronoiCellClipper: Need at least 3 sites for Delaunay triangulation.");
                return result;
            }

            try
            {
                // Compute Delaunay triangulation using Geometry-Lib
                var triangles = DelaunayTriangulation.Triangulate(sites);

                if (triangles == null || triangles.Count == 0)
                {
                    Debug.LogWarning("VoronoiCellClipper: Delaunay triangulation produced no triangles. Falling back to direct half-plane clipping.");
                    return GenerateVoronoiCellsFallback(sites, boundaryPolygon, clipRect);
                }

                // Build neighbor map from Delaunay triangulation
                // Each site's Voronoi cell is bounded by perpendicular bisectors to its Delaunay neighbors
                var neighbors = BuildNeighborMapFromDelaunay(sites, triangles);

                // For each site, compute its Voronoi cell using half-plane clipping
                // against the perpendicular bisectors to all neighbors discovered from Delaunay
                foreach (var site in sites)
                {
                    List<Point> neighborList;
                    if (!neighbors.TryGetValue(site, out neighborList) || neighborList.Count == 0)
                    {
                        // Site has no neighbors in Delaunay - use all other sites
                        neighborList = sites.Where(s => s != site).ToList();
                    }

                    // Start with bounding rectangle
                    var cell = CreateBoundingRectangle(clipRect);

                    // Clip against each Delaunay neighbor using half-plane intersection
                    foreach (var neighbor in neighborList)
                    {
                        if (cell.Count < 3) break;

                        var midX = (site.X + neighbor.X) / 2.0;
                        var midY = (site.Y + neighbor.Y) / 2.0;
                        var nx = neighbor.X - site.X;
                        var ny = neighbor.Y - site.Y;

                        cell = ClipWithHalfPlane(cell, midX, midY, nx, ny);
                    }

                    if (cell.Count < 3) continue;

                    // Clip the Voronoi cell against the boundary polygon
                    var clipped = ClipAgainstBoundary(cell, boundaryPolygon);
                    
                    if (clipped != null && clipped.Count >= 3)
                    {
                        result.Add((site, clipped));
                    }
                }

                return result;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"VoronoiCellClipper: Error in Delaunay-based generation: {ex.Message}. Falling back to direct method.");
                return GenerateVoronoiCellsFallback(sites, boundaryPolygon, clipRect);
            }
        }

        /// <summary>
        /// Build a map of each site to its Delaunay neighbors from the triangulation.
        /// Two sites are neighbors if they share an edge in any Delaunay triangle.
        /// </summary>
        private Dictionary<Point, List<Point>> BuildNeighborMapFromDelaunay(List<Point> sites, List<Triangle> triangles)
        {
            var neighbors = new Dictionary<Point, List<Point>>();
            
            // Initialize empty lists for all sites
            foreach (var site in sites)
            {
                neighbors[site] = new List<Point>();
            }

            // For each triangle, add edges to the neighbor map
            foreach (var tri in triangles)
            {
                AddNeighborPair(neighbors, tri.A, tri.B);
                AddNeighborPair(neighbors, tri.B, tri.C);
                AddNeighborPair(neighbors, tri.C, tri.A);
            }

            return neighbors;
        }

        /// <summary>
        /// Add two points as neighbors of each other.
        /// </summary>
        private void AddNeighborPair(Dictionary<Point, List<Point>> neighbors, Point a, Point b)
        {
            if (neighbors.ContainsKey(a) && !neighbors[a].Contains(b))
                neighbors[a].Add(b);
            if (neighbors.ContainsKey(b) && !neighbors[b].Contains(a))
                neighbors[b].Add(a);
        }

        /// <summary>
        /// Generate Voronoi cells using half-plane clipping against ALL other sites.
        /// </summary>
        private List<(Point Site, List<Point> Cell)> GenerateVoronoiCellsFallback(List<Point> sites, List<Vector2> boundaryPolygon, Bounds clipRect)
        {
            var result = new List<(Point Site, List<Point> Cell)>();
            
            foreach (var site in sites)
            {
                var cell = ClipCellToPolygon(site, sites, boundaryPolygon, clipRect);
                if (cell != null && cell.Count >= 3)
                {
                    result.Add((site, cell));
                }
            }
            
            return result;
        }


        /// <summary>
        /// Clip a Voronoi cell defined by a site against all other sites using half-plane intersection,
        /// then clip against the boundary polygon.
        /// Note: This is the legacy method. Use GenerateVoronoiCells() for Geometry-Lib implementation.
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
        /// Clip Voronoi cell against boundary using Clipper2.
        /// </summary>
        public List<Point> ClipAgainstBoundary(List<Point> cell, List<Vector2> boundary)
        {
            var cellVec = cell.Select(p => new Vector2((float)p.X, (float)p.Y)).ToList();
            var clipperResult = ClipWithClipper2(cellVec, boundary);

            if (clipperResult != null && clipperResult.Count > 0)
            {
                // Use the first (largest) polygon from Clipper result
                var bestPoly = clipperResult[0];
                return bestPoly.Select(v => new Point(v.x, v.y)).ToList();
            }

            return new List<Point>();
        }

        /// <summary>
        /// Clip using Clipper2 library.
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

                return ProcessClipperPaths(solution);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Clipper2 intersection failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Attempt to repair self-intersecting polygon using Union.
        /// </summary>
        private List<List<Vector2>> RepairSelfIntersectingPolygon(Path64 path)
        {
            try
            {
                var tempPaths = new Paths64 { path };
                var repaired = Clipper.Union(tempPaths, FillRule.NonZero);

                if (repaired == null || repaired.Count == 0)
                    return null;

                return ProcessClipperPaths(repaired);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Convert Clipper2 paths to cleaned Vector2 polygons.
        /// </summary>
        private List<List<Vector2>> ProcessClipperPaths(Paths64 paths)
        {
            var results = new List<List<Vector2>>();
            
            foreach (var path in paths)
            {
                if (path == null || path.Count < 3) continue;

                // Convert from Clipper2 integer coordinates to float
                var polygon = new List<Vector2>(path.Count);
                foreach (var pt in path)
                    polygon.Add(new Vector2((float)(pt.X / ClipperScale), (float)(pt.Y / ClipperScale)));

                // Clean and validate
                var cleaned = PolygonUtility.CleanPolygon(polygon);
                if (cleaned.Count < 3) continue;

                // Ensure CCW winding
                var arr = cleaned.ToArray();
                PolygonUtility.EnsureCCW(ref arr);

                // Filter tiny polygons
                float area = Mathf.Abs(PolygonUtility.SignedArea(arr));
                if (area >= 1e-6f)
                    results.Add(new List<Vector2>(arr));
            }

            return results.Count > 0 ? results : null;
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
