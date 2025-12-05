using UnityEngine;
using UnityEditor;
using System.IO;

namespace VoronoiFracture
{
    /// <summary>
    /// Editor tool to generate sprites from PolygonCollider2D shapes.
    /// </summary>
    public class PolygonSpriteGenerator : EditorWindow
    {
        private PolygonCollider2D targetCollider;
        private Color fillColor = Color.white;
        private int textureSize = 256;
        private string savePath = "Assets/GeneratedSprites";
        private string spriteName = "PolygonSprite";
        private float padding = 0.05f;

        [MenuItem("Tools/Polygon Sprite Generator")]
        public static void ShowWindow()
        {
            GetWindow<PolygonSpriteGenerator>("Polygon Sprite Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Polygon Sprite Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Generate a sprite from a PolygonCollider2D. " +
                "The sprite will exactly match the collider shape.",
                MessageType.Info);

            EditorGUILayout.Space();

            targetCollider = (PolygonCollider2D)EditorGUILayout.ObjectField(
                "Polygon Collider", targetCollider, typeof(PolygonCollider2D), true);

            EditorGUILayout.Space();

            fillColor = EditorGUILayout.ColorField("Fill Color", fillColor);
            textureSize = EditorGUILayout.IntSlider("Texture Size", textureSize, 64, 1024);
            padding = EditorGUILayout.Slider("Padding", padding, 0f, 0.2f);

            EditorGUILayout.Space();

            spriteName = EditorGUILayout.TextField("Sprite Name", spriteName);
            savePath = EditorGUILayout.TextField("Save Path", savePath);

            EditorGUILayout.Space();

            GUI.enabled = targetCollider != null;
            if (GUILayout.Button("Generate Sprite", GUILayout.Height(30)))
            {
                GenerateSprite();
            }

            if (GUILayout.Button("Generate & Assign to SpriteRenderer", GUILayout.Height(30)))
            {
                var sprite = GenerateSprite();
                if (sprite != null && targetCollider != null)
                {
                    var sr = targetCollider.GetComponent<SpriteRenderer>();
                    if (sr == null)
                    {
                        sr = targetCollider.gameObject.AddComponent<SpriteRenderer>();
                    }
                    Undo.RecordObject(sr, "Assign Generated Sprite");
                    sr.sprite = sprite;
                    EditorUtility.SetDirty(sr);
                }
            }
            GUI.enabled = true;

            EditorGUILayout.Space();

            // Auto-select from current selection
            if (Selection.activeGameObject != null)
            {
                var poly = Selection.activeGameObject.GetComponent<PolygonCollider2D>();
                if (poly != null && poly != targetCollider)
                {
                    EditorGUILayout.HelpBox(
                        $"Tip: Selected object '{Selection.activeGameObject.name}' has a PolygonCollider2D.",
                        MessageType.None);
                    if (GUILayout.Button("Use Selected Object"))
                    {
                        targetCollider = poly;
                        spriteName = Selection.activeGameObject.name + "_Sprite";
                    }
                }
            }
        }

        private Sprite GenerateSprite()
        {
            if (targetCollider == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a PolygonCollider2D first.", "OK");
                return null;
            }

            var points = targetCollider.points;
            if (points.Length < 3)
            {
                EditorUtility.DisplayDialog("Error", "Polygon must have at least 3 points.", "OK");
                return null;
            }

            // Calculate bounds
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var point in points)
            {
                if (point.x < minX) minX = point.x;
                if (point.y < minY) minY = point.y;
                if (point.x > maxX) maxX = point.x;
                if (point.y > maxY) maxY = point.y;
            }

            float width = Mathf.Max(1e-6f, maxX - minX);
            float height = Mathf.Max(1e-6f, maxY - minY);

            // Make square by using max dimension
            float size = Mathf.Max(width, height);
            float centerX = (minX + maxX) / 2f;
            float centerY = (minY + maxY) / 2f;

            // Apply padding
            float paddedSize = size * (1f + padding * 2f);
            float halfSize = paddedSize / 2f;

            // Create texture
            var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;

            // Clear to transparent
            var transparent = new Color32(0, 0, 0, 0);
            var pixels = new Color32[textureSize * textureSize];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = transparent;

            // Convert polygon to pixel coordinates
            var polygonPixels = new Vector2Int[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                float px = ((points[i].x - centerX + halfSize) / paddedSize) * (textureSize - 1);
                float py = ((points[i].y - centerY + halfSize) / paddedSize) * (textureSize - 1);
                polygonPixels[i] = new Vector2Int(
                    Mathf.Clamp(Mathf.RoundToInt(px), 0, textureSize - 1),
                    Mathf.Clamp(Mathf.RoundToInt(py), 0, textureSize - 1)
                );
            }

            // Fill color
            var fill = new Color32(
                (byte)(fillColor.r * 255),
                (byte)(fillColor.g * 255),
                (byte)(fillColor.b * 255),
                (byte)(fillColor.a * 255)
            );

            // Rasterize using triangle fan
            var v0 = polygonPixels[0];
            for (int i = 1; i < polygonPixels.Length - 1; i++)
            {
                var v1 = polygonPixels[i];
                var v2 = polygonPixels[i + 1];
                RasterizeTriangle(pixels, textureSize, v0, v1, v2, fill);
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            // Ensure directory exists
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
                AssetDatabase.Refresh();
            }

            // Save texture as PNG
            string fullPath = $"{savePath}/{spriteName}.png";
            byte[] pngData = texture.EncodeToPNG();
            File.WriteAllBytes(fullPath, pngData);
            AssetDatabase.Refresh();

            // Configure import settings
            var importer = AssetImporter.GetAtPath(fullPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = textureSize / paddedSize;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            // Load the sprite
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(fullPath);

            Debug.Log($"Generated sprite: {fullPath}");
            EditorUtility.DisplayDialog("Success", $"Sprite saved to:\n{fullPath}", "OK");

            // Ping the asset in Project window
            EditorGUIUtility.PingObject(sprite);

            return sprite;
        }

        private static void RasterizeTriangle(Color32[] buffer, int size,
            Vector2Int a, Vector2Int b, Vector2Int c, Color32 color)
        {
            int minX = Mathf.Clamp(Mathf.Min(a.x, Mathf.Min(b.x, c.x)), 0, size - 1);
            int maxX = Mathf.Clamp(Mathf.Max(a.x, Mathf.Max(b.x, c.x)), 0, size - 1);
            int minY = Mathf.Clamp(Mathf.Min(a.y, Mathf.Min(b.y, c.y)), 0, size - 1);
            int maxY = Mathf.Clamp(Mathf.Max(a.y, Mathf.Max(b.y, c.y)), 0, size - 1);

            int area = EdgeFunction(a.x, a.y, b.x, b.y, c.x, c.y);
            if (area == 0) return;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int w0 = EdgeFunction(b.x, b.y, c.x, c.y, x, y);
                    int w1 = EdgeFunction(c.x, c.y, a.x, a.y, x, y);
                    int w2 = EdgeFunction(a.x, a.y, b.x, b.y, x, y);

                    if ((w0 >= 0 && w1 >= 0 && w2 >= 0) || (w0 <= 0 && w1 <= 0 && w2 <= 0))
                    {
                        buffer[y * size + x] = color;
                    }
                }
            }
        }

        private static int EdgeFunction(int ax, int ay, int bx, int by, int cx, int cy)
        {
            return (cx - ax) * (by - ay) - (cy - ay) * (bx - ax);
        }
    }
}
