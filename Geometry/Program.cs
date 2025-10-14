using System;
using System.Collections.Generic;
namespace Geometry;

class Program
{
    static void Main()
    {
        // Example points
        var points = new List<Point>
        {
            new Point(0,0),
            new Point(100,0),
            new Point(50,100),
            new Point(75,50)
        };
        // Compute Delaunay triangulation
        var triangles = DelaunayTriangulation.Triangulate(points);
        foreach (var tri in triangles)
        {
            Console.WriteLine($"Triangle: {tri.A}, {tri.B}, {tri.C}");
        }
        // Compute Voronoi diagram
        var voronoiCells = VoronoiGenerator.FromDelaunay(triangles);

        // Print results
        foreach (var (site, cell) in voronoiCells)
        {
            Console.WriteLine($"Site: ({site.X}, {site.Y})");
            foreach (var p in cell)
                Console.WriteLine($"  Cell vertex: ({p.X}, {p.Y})");
        }
    }
}
