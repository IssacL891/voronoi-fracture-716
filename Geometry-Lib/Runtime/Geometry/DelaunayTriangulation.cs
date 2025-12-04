using System;
using System.Collections.Generic;
using System.Linq;

namespace Geometry
{
    /// <summary>
    /// This class is used to compute the Delaunay triangles.
    /// </summary>
    public class DelaunayTriangulation
    {
        /// <summary>
        /// This function produces a list of triangles given points in space.
        /// </summary>
        /// <param name="points"> The points from which to find the Delaunay triangles </param>
        /// <returns> A list of triangles </returns>
        public static List<Triangle> Triangulate(List<Point> points)
        {
            double minX = points.Min(p => p.X); // min x value in point list
            double minY = points.Min(p => p.Y); // min y value in point list
            double maxX = points.Max(p => p.X); // max x value in point list
            double maxY = points.Max(p => p.Y); // max y value in point list

            double dx = maxX - minX; //distance covered on x axix
            double dy = maxY - minY; // distance covered on y axis
            double deltaMax = Math.Max(dx, dy) * 10; // bounding box that covers all points

            var p1 = new Point(minX - deltaMax, minY - deltaMax); // first vertex of super triangle. bottom left corner
            var p2 = new Point(minX + 0.5 * dx, maxY + deltaMax); // center, above all points
            var p3 = new Point(maxX + deltaMax, minY - deltaMax); // far right corner

            var triangles = new List<Triangle> { new Triangle(p1, p2, p3) };

            foreach (var point in points)
            {
                var badTriangles = new List<Triangle>(); // triangles that contain the new point in their circumcircle
                foreach (var tri in triangles)
                {
                    if (tri.ContainsInCircumcircle(point))
                        badTriangles.Add(tri);
                }

                var edgeCount = new Dictionary<Edge, int>();
                foreach (var tri in badTriangles)
                {
                    foreach (var edge in tri.Edges)
                    {
                        if (edgeCount.ContainsKey(edge)) edgeCount[edge]++;
                        else edgeCount[edge] = 1;
                    }
                }

                // Boundary edges = appear only once
                var boundaryEdges = edgeCount.Where(kv => kv.Value == 1).Select(kv => kv.Key).ToList();

                // removed the bad triangles
                triangles.RemoveAll(t => badTriangles.Contains(t));

                //connect the new point to the boundary edges
                foreach (var edge in boundaryEdges)
                    triangles.Add(new Triangle(edge.P1, edge.P2, point));
            }

            // remove triangles connected to super traingle vertex
            triangles.RemoveAll(t => t.ContainsVertex(p1) || t.ContainsVertex(p2) || t.ContainsVertex(p3));
            return triangles;
        }
    }
}
