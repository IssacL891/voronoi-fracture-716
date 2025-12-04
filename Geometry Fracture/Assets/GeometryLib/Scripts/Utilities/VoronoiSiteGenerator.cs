using UnityEngine;
using System.Collections.Generic;
using Geometry;

namespace VoronoiFracture
{
    /// <summary>
    /// Generates random Voronoi sites within a polygon boundary.
    /// </summary>
    public class VoronoiSiteGenerator
    {
        private System.Random random;
        private float jitterAmount;
        private float deduplicationEpsilon;

        public VoronoiSiteGenerator(int seed, float jitter = 0.2f)
        {
            random = new System.Random(seed);
            jitterAmount = jitter;
        }

        /// <summary>
        /// Generate random sites inside the given polygon bounds.
        /// </summary>
        /// <param name="polygon">Polygon boundary in world space</param>
        /// <param name="bounds">Bounding box of the polygon</param>
        /// <param name="count">Number of sites to generate</param>
        /// <returns>List of generated sites</returns>
        public List<Point> GenerateSites(List<Vector2> polygon, Bounds bounds, int count)
        {
            var sites = new List<Point>();
            int placed = 0;
            int maxAttempts = System.Math.Max(200, count * 100);

            // Set deduplication epsilon based on polygon size
            deduplicationEpsilon = Mathf.Max(1e-4f, Mathf.Min(bounds.size.x, bounds.size.y) * 1e-4f);

            while (placed < count && maxAttempts-- > 0)
            {
                // Generate random point in bounds
                float x = (float)(bounds.min.x + random.NextDouble() * bounds.size.x);
                float y = (float)(bounds.min.y + random.NextDouble() * bounds.size.y);
                var point = new Vector2(x, y);

                // Must be inside polygon
                if (!PolygonUtility.PointInPolygon(point, polygon))
                    continue;

                // Apply jitter if enabled
                if (jitterAmount != 0f)
                {
                    point = ApplyJitter(point, polygon);
                }

                // Skip near-duplicates to avoid degenerate cells
                if (IsTooCloseToExisting(point, sites))
                    continue;

                sites.Add(new Point(point.x, point.y));
                placed++;
            }

            return sites;
        }

        private Vector2 ApplyJitter(Vector2 point, List<Vector2> polygon)
        {
            float jx = (float)(random.NextDouble() * 2.0 - 1.0);
            float jy = (float)(random.NextDouble() * 2.0 - 1.0);
            var jittered = point + new Vector2(jx, jy) * jitterAmount;

            // If jittered point is still inside, use it; otherwise clamp to polygon edge
            if (PolygonUtility.PointInPolygon(jittered, polygon))
            {
                return jittered;
            }
            else
            {
                return PolygonUtility.NearestPointOnPolygon(jittered, polygon);
            }
        }

        private bool IsTooCloseToExisting(Vector2 point, List<Point> sites)
        {
            float threshold = deduplicationEpsilon * deduplicationEpsilon;
            foreach (var site in sites)
            {
                var sitePos = new Vector2((float)site.X, (float)site.Y);
                if (Vector2.SqrMagnitude(sitePos - point) <= threshold)
                    return true;
            }
            return false;
        }
    }
}
