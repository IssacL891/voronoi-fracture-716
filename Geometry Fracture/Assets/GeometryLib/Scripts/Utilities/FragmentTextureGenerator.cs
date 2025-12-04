using UnityEngine;
using System.Collections.Generic;
using Geometry;

namespace VoronoiFracture
{
    /// <summary>
    /// Generates textures for Voronoi fragments using efficient scanline rasterization.
    /// </summary>
    public class FragmentTextureGenerator
    {
        private static Color32[] pixelBuffer;

        /// <summary>
        /// Generate a texture for a single fragment.
        /// </summary>
        /// <param name="polygon">Fragment polygon in world space</param>
        /// <param name="color">Fill color for the fragment</param>
        /// <param name="textureSize">Texture size in pixels</param>
        /// <returns>Generated texture</returns>
        public static Texture2D GenerateTexture(List<Point> polygon, Color color, int textureSize)
        {
            if (polygon == null || polygon.Count < 3)
                return null;

            // Calculate polygon bounds
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var point in polygon)
            {
                if ((float)point.X < minX) minX = (float)point.X;
                if ((float)point.Y < minY) minY = (float)point.Y;
                if ((float)point.X > maxX) maxX = (float)point.X;
                if ((float)point.Y > maxY) maxY = (float)point.Y;
            }

            float width = Mathf.Max(1e-6f, maxX - minX);
            float height = Mathf.Max(1e-6f, maxY - minY);

            // Create texture
            var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;

            // Initialize pixel buffer
            var transparentColor = new Color32(255, 255, 255, 0);
            if (pixelBuffer == null || pixelBuffer.Length < textureSize * textureSize)
                pixelBuffer = new Color32[textureSize * textureSize];

            for (int i = 0; i < textureSize * textureSize; i++)
                pixelBuffer[i] = transparentColor;

            // Convert polygon to pixel coordinates
            var polygonPixels = new List<Vector2Int>();
            foreach (var point in polygon)
            {
                int px = Mathf.RoundToInt(((float)point.X - minX) / width * (textureSize - 1));
                int py = Mathf.RoundToInt(((float)point.Y - minY) / height * (textureSize - 1));
                polygonPixels.Add(new Vector2Int(px, py));
            }

            // Rasterize using triangle fan from first vertex
            var fillColor = new Color32(
                (byte)(color.r * 255),
                (byte)(color.g * 255),
                (byte)(color.b * 255),
                255
            );

            var v0 = polygonPixels[0];
            for (int i = 1; i < polygonPixels.Count - 1; i++)
            {
                var v1 = polygonPixels[i];
                var v2 = polygonPixels[i + 1];
                RasterizeTriangle(pixelBuffer, textureSize, textureSize, v0, v1, v2, fillColor);
            }

            texture.SetPixels32(pixelBuffer);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Rasterize a triangle into the pixel buffer using scanline algorithm.
        /// </summary>
        private static void RasterizeTriangle(Color32[] buffer, int width, int height,
            Vector2Int a, Vector2Int b, Vector2Int c, Color32 color)
        {
            // Calculate bounding box
            int minX = Mathf.Clamp(Mathf.Min(a.x, Mathf.Min(b.x, c.x)), 0, width - 1);
            int maxX = Mathf.Clamp(Mathf.Max(a.x, Mathf.Max(b.x, c.x)), 0, width - 1);
            int minY = Mathf.Clamp(Mathf.Min(a.y, Mathf.Min(b.y, c.y)), 0, height - 1);
            int maxY = Mathf.Clamp(Mathf.Max(a.y, Mathf.Max(b.y, c.y)), 0, height - 1);

            // Edge function test for each pixel in bounding box
            int area = EdgeFunction(a.x, a.y, b.x, b.y, c.x, c.y);
            if (area == 0) return;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int w0 = EdgeFunction(b.x, b.y, c.x, c.y, x, y);
                    int w1 = EdgeFunction(c.x, c.y, a.x, a.y, x, y);
                    int w2 = EdgeFunction(a.x, a.y, b.x, b.y, x, y);

                    // Check if pixel is inside triangle (allowing edge pixels)
                    if ((w0 >= 0 && w1 >= 0 && w2 >= 0) || (w0 <= 0 && w1 <= 0 && w2 <= 0))
                    {
                        int index = y * width + x;
                        buffer[index] = color;
                    }
                }
            }
        }

        /// <summary>
        /// Edge function for triangle rasterization.
        /// Returns signed area of triangle formed by three points.
        /// </summary>
        private static int EdgeFunction(int ax, int ay, int bx, int by, int cx, int cy)
        {
            return (cx - ax) * (by - ay) - (cy - ay) * (bx - ax);
        }

        /// <summary>
        /// Generate a deterministic color from a Voronoi site position.
        /// </summary>
        public static Color ColorFromSite(Point site, int randomSeed)
        {
            unchecked
            {
                int hx = site.X.GetHashCode();
                int hy = site.Y.GetHashCode();
                int seed = randomSeed ^ hx ^ (hy << 16);
                var random = new System.Random(seed);
                return new Color(
                    random.Next(64, 256) / 255f,
                    random.Next(64, 256) / 255f,
                    random.Next(64, 256) / 255f,
                    1f
                );
            }
        }
    }
}
