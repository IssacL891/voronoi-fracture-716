using System;
using System.Collections.Generic;

namespace Geometry
{
    /// <summary>
    /// A class to represent a triangle
    /// </summary>
    public class Triangle
    {
        public Point A { get; }
        public Point B { get; }
        public Point C { get; }

        // A list containing all the edges of a triangle
        public List<Edge> Edges => new List<Edge>
        {
            new Edge(A,B),
            new Edge(B,C),
            new Edge(C,A)
        };

        /// <summary>
        /// This is a constructor of a triangle.
        /// </summary>
        /// <param name="a"> A point in the triangle </param>
        /// <param name="b"> A point in the triangle </param>
        /// <param name="c"> A point in the triangle </param>
        /// <exception cref="ArgumentException"> Points are to be distinct </exception>
        public Triangle(Point a, Point b, Point c)
        {
            if (a.Equals(b) || b.Equals(c) || c.Equals(a))
                throw new ArgumentException("Triangle vertices must be distinct.");

            A = a;
            B = b;
            C = c;
        }

        /// <summary>
        /// A function to check if a point is in the circumcenter of a traingle
        /// </summary>
        /// <param name="p"> The point being examined </param>
        /// <returns> A boolean depending on if the point was in circumcenter </returns>

        public bool ContainsInCircumcircle(Point p)
        {
            Point c = VoronoiGenerator.Circumcenter(this);
            double dx = A.X - c.X;
            double dy = A.Y - c.Y;
            double r2 = dx * dx + dy * dy;

            double dxp = p.X - c.X;
            double dyp = p.Y - c.Y;
            double dist2 = dxp * dxp + dyp * dyp;

            return dist2 <= r2;
        }
        /// <summary>
        /// This function is to check if a point is a given vertex of a traingle
        /// </summary>
        /// <param name="p"> The point being checked </param>
        /// <returns> A boolean depending on if point is vertex or not </returns>
        public bool ContainsVertex(Point p) => A.Equals(p) || B.Equals(p) || C.Equals(p);

    }
}
