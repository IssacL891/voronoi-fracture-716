using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace Geometry;

class Program
{
    static void Main()
    {
        var points = new List<Point>
        {
            new Point(0,0),
            new Point(100,0),
            new Point(50,100),
            new Point(75,50)
        };

        var triangles = DelaunayTriangulation.Triangulate(points);
        var voronoiCells = VoronoiGenerator.FromDelaunay(triangles);

        int width = 200, height = 200;

        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        // white background
        canvas.Clear(SKColors.White);

        // Compute bounds of all sites and cell vertices so we can scale/translate to the image
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;

        void Consider(double x, double y)
        {
            if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y))
                return;
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x > maxX) maxX = x;
            if (y > maxY) maxY = y;
        }

        // include sites
        int cellIdx = 0;
        foreach (var (site, cell) in voronoiCells)
        {
            if (cellIdx == 0)
            {
                Console.WriteLine($"Site[0]=({site.X},{site.Y}), cell0 count={(cell == null ? 0 : cell.Count)}");
                if (cell != null)
                {
                    for (int vi = 0; vi < cell.Count; vi++)
                        Console.WriteLine($"  v[{vi}] = ({cell[vi].X},{cell[vi].Y})");
                }
            }
            Consider(site.X, site.Y);
            if (cell != null)
            {
                foreach (var p in cell)
                    Consider(p.X, p.Y);
            }
        }

        if (minX == double.PositiveInfinity)
        {
            Console.WriteLine("No points to draw.");
            return;
        }

        // add a small padding
        const int pad = 8;
        double dataW = Math.Max(1e-6, maxX - minX);
        double dataH = Math.Max(1e-6, maxY - minY);
        double scaleX = (width - 2 * pad) / dataW;
        double scaleY = (height - 2 * pad) / dataH;
        double scale = Math.Min(scaleX, scaleY);

        Console.WriteLine($"Drawing {voronoiCells.Count} cells. Bounds: ({minX:0.##},{minY:0.##}) - ({maxX:0.##},{maxY:0.##}), scale={scale:0.###}");

        // draw Delaunay triangles for debugging
        Console.WriteLine($"Triangles: {triangles.Count}");
        using var triPaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = SKColors.DarkBlue, StrokeWidth = 2, IsAntialias = true };
        using var centerPaint = new SKPaint { Style = SKPaintStyle.Fill, Color = SKColors.Magenta, IsAntialias = true };
        foreach (var t in triangles)
        {
            canvas.DrawLine(MapX(t.A.X), MapY(t.A.Y), MapX(t.B.X), MapY(t.B.Y), triPaint);
            canvas.DrawLine(MapX(t.B.X), MapY(t.B.Y), MapX(t.C.X), MapY(t.C.Y), triPaint);
            canvas.DrawLine(MapX(t.C.X), MapY(t.C.Y), MapX(t.A.X), MapY(t.A.Y), triPaint);
            var cc = VoronoiGenerator.Circumcenter(t);
            canvas.DrawCircle(MapX(cc.X), MapY(cc.Y), 3f, centerPaint);
        }

        // mapping: data (x,y) -> image coords (ix,iy)
        float MapX(double x) => (float)(((x - minX) * scale) + pad + ((width - 2 * pad) - dataW * scale) / 2.0);
        // flip Y so that larger Y in data is higher on image (optional depending on your coord system)
        float MapY(double y) => (float)(height - ((y - minY) * scale + pad + ((height - 2 * pad) - dataH * scale) / 2.0));

        var rnd = new Random();

        // Build clipped Voronoi cells by intersecting half-planes (perpendicular bisectors)
        Console.WriteLine("Building clipped Voronoi cells by half-plane intersection (bounding box clipping)");

        // initial clip rectangle in data coords: expand bounds a bit to include far rays
        double margin = Math.Max(dataW, dataH) * 2.0 + 1.0;
        double clipMinX = minX - margin;
        double clipMinY = minY - margin;
        double clipMaxX = maxX + margin;
        double clipMaxY = maxY + margin;

        List<(double X, double Y)> RectPolygon() => new List<(double, double)>
        {
            (clipMinX, clipMinY),
            (clipMaxX, clipMinY),
            (clipMaxX, clipMaxY),
            (clipMinX, clipMaxY)
        };

        // clip subject polygon with half-plane defined by (pt - mid) dot v <= 0
        List<(double X, double Y)> ClipWithHalfPlane(List<(double X, double Y)> subject, double midX, double midY, double nx, double ny)
        {
            var output = new List<(double X, double Y)>();
            if (subject == null || subject.Count == 0) return output;
            for (int i = 0; i < subject.Count; i++)
            {
                var A = subject[i];
                var B = subject[(i + 1) % subject.Count];
                double va = (A.X - midX) * nx + (A.Y - midY) * ny;
                double vb = (B.X - midX) * nx + (B.Y - midY) * ny;
                bool ina = va <= 0;
                bool inb = vb <= 0;
                if (ina && inb)
                {
                    output.Add(B);
                }
                else if (ina && !inb)
                {
                    // A in, B out -> add intersection
                    double t = va / (va - vb);
                    var ix = A.X + (B.X - A.X) * t;
                    var iy = A.Y + (B.Y - A.Y) * t;
                    output.Add((ix, iy));
                }
                else if (!ina && inb)
                {
                    // A out, B in -> add intersection then B
                    double t = va / (va - vb);
                    var ix = A.X + (B.X - A.X) * t;
                    var iy = A.Y + (B.Y - A.Y) * t;
                    output.Add((ix, iy));
                    output.Add(B);
                }
                // else both out -> nothing
            }
            return output;
        }

        // For each site from the original points list, compute clipped Voronoi cell
        foreach (var site in points)
        {
            var poly = RectPolygon();
            foreach (var other in points)
            {
                if (other.X == site.X && other.Y == site.Y) continue;
                var midX = (site.X + other.X) / 2.0;
                var midY = (site.Y + other.Y) / 2.0;
                var nx = other.X - site.X; // normal pointing toward 'other'
                var ny = other.Y - site.Y;
                poly = ClipWithHalfPlane(poly, midX, midY, nx, ny);
                if (poly.Count == 0) break;
            }

            if (poly.Count < 3)
            {
                Console.WriteLine($"Site ({site.X},{site.Y}) poly empty or <3");
                continue;
            }

            // diagnostics: polygon bbox
            double pminX = poly.Min(p => p.X);
            double pminY = poly.Min(p => p.Y);
            double pmaxX = poly.Max(p => p.X);
            double pmaxY = poly.Max(p => p.Y);
            Console.WriteLine($"Site ({site.X},{site.Y}) poly verts={poly.Count} bbox=({pminX:0.##},{pminY:0.##})-({pmaxX:0.##},{pmaxY:0.##})");

            // draw polygon
            var color = new SKColor((byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256), 200);
            using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, Color = color, IsAntialias = true };
            using var strokePaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = SKColors.Black, StrokeWidth = 1, IsAntialias = true };

            using var path = new SKPath();
            path.MoveTo(MapX(poly[0].X), MapY(poly[0].Y));
            for (int i = 1; i < poly.Count; i++)
                path.LineTo(MapX(poly[i].X), MapY(poly[i].Y));
            path.Close();
            canvas.DrawPath(path, fillPaint);
            canvas.DrawPath(path, strokePaint);

            // draw site
            using var sitePaint = new SKPaint { Style = SKPaintStyle.Fill, Color = SKColors.Red, IsAntialias = true };
            canvas.DrawCircle(MapX(site.X), MapY(site.Y), 2f, sitePaint);
        }

        // Flush any pending draw operations
        canvas.Flush();

        // snapshot surface to image
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using (var stream = System.IO.File.OpenWrite("voronoi.png"))
        {
            data.SaveTo(stream);
        }

        // quick pixel check to ensure non-white pixels were drawn
        using var bmp = SKBitmap.FromImage(image);
        long nonWhite = 0;
        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                var c = bmp.GetPixel(x, y);
                if (!(c.Red == 255 && c.Green == 255 && c.Blue == 255))
                    nonWhite++;
            }
        }

        Console.WriteLine($"Saved voronoi.png — non-white pixels: {nonWhite}");
    }
}