namespace Geometry
{
    /// <summary>
    /// This class is used to represent an edge of a triangle
    /// </summary>
    public class Edge
    {
        public Point P1 { get; }
        public Point P2 { get; }
        /// <summary>
        /// This is a constructor of an edge.
        /// </summary>
        /// <param name="p1"> The first end point of an edge </param>
        /// <param name="p2"> The second end point of an edge</param>
        public Edge(Point p1, Point p2)
        {
            P1 = p1;
            P2 = p2;
        }

        /// <summary>
        /// This function is used to compare if two edges are the same 
        /// </summary>
        /// <param name="obj"> This is the object being compared to an edge </param>
        /// <returns></returns>
        public override bool Equals(object? obj)
        {
            if (obj is not Edge other) return false;
            return (P1.Equals(other.P1) && P2.Equals(other.P2)) || (P1.Equals(other.P2) && P2.Equals(other.P1));
        }

        public override int GetHashCode()
        {
            var (p1, p2) = P1.X < P2.X || (P1.X == P2.X && P1.Y <= P2.Y) ? (P1, P2) : (P2, P1);
            return HashCode.Combine(p1, p2);
        }
    }
}