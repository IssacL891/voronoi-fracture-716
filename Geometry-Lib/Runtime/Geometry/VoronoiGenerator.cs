using System;
using System.Collections.Generic;
using System.Linq;

namespace Geometry
{
    /// <summary>
    /// This is the class that converts the Dalaunay triangles to Voroini polygons
    /// </summary>
    public class VoronoiGenerator
    {
        /// <summary>
        /// This function takes in the computed Delaunay triangles and gives the dual 
        /// Voronoi polygons 
        /// </summary>
        /// <param name="triangles"> This is the Delaunay triangles given</param>
        /// <returns> Returns a list of points mapped to all the points that make up the cell
        /// sorted in CCW order </returns>
        public static List<(Point Site, List<Point> Cell)> FromDelaunay(List<Triangle> triangles)
        {
            //mapping from each site to a voronoi cell
            var siteToCell = new Dictionary<Point, List<Point>>();

            foreach (var tri in triangles)
            {
                var center = Circumcenter(tri);

                // Associate each triangle vertex with this circumcenter
                foreach (var v in new[] { tri.A, tri.B, tri.C })
                {
                    if (!siteToCell.ContainsKey(v))
                        siteToCell[v] = new List<Point>();

                    siteToCell[v].Add(center);
                }
            }
            var result = new List<(Point, List<Point>)>();

            foreach (var kv in siteToCell)
            {
                var site = kv.Key;
                var cell = kv.Value;

                // sorting CCW
                cell.Sort((p1, p2) =>
                {
                    double cross = (p1.X - site.X) * (p2.Y - site.Y) - (p1.Y - site.Y) * (p2.X - site.X);
                    if (cross > 0) return -1; // p1 comes before p2 CCW
                    if (cross < 0) return 1;  // p2 comes before p1 CCW

                    // If collinear, sort by distance to site
                    double d1 = (p1.X - site.X) * (p1.X - site.X) + (p1.Y - site.Y) * (p1.Y - site.Y);
                    double d2 = (p2.X - site.X) * (p2.X - site.X) + (p2.Y - site.Y) * (p2.Y - site.Y);
                    return d1.CompareTo(d2);
                });

                result.Add((site, cell));
            }

            return result;
        }

        /// <summary>
        /// This function is used to find the circumcenter of a given triangle
        /// </summary>
        /// <param name="tri"> This is the triangle to compute its circumcenter </param>
        /// <returns> The point (x,y) of the circumcenter </returns>
        public static Point Circumcenter(Triangle tri)
        {
            double denominator = 2 * (tri.A.X * (tri.B.Y - tri.C.Y) +
                            tri.B.X * (tri.C.Y - tri.A.Y) +
                            tri.C.X * (tri.A.Y - tri.B.Y));

            double ux = ((tri.A.X * tri.A.X + tri.A.Y * tri.A.Y) * (tri.B.Y - tri.C.Y) +
                         (tri.B.X * tri.B.X + tri.B.Y * tri.B.Y) * (tri.C.Y - tri.A.Y) +
                         (tri.C.X * tri.C.X + tri.C.Y * tri.C.Y) * (tri.A.Y - tri.B.Y)) / denominator;

            double uy = ((tri.A.X * tri.A.X + tri.A.Y * tri.A.Y) * (tri.C.X - tri.B.X) +
                         (tri.B.X * tri.B.X + tri.B.Y * tri.B.Y) * (tri.A.X - tri.C.X) +
                         (tri.C.X * tri.C.X + tri.C.Y * tri.C.Y) * (tri.B.X - tri.A.X)) / denominator;

            return new Point(ux, uy);
        }

    }
}
