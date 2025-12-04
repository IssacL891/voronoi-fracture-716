using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Geometry;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VoronoiFracture
{
    /// <summary>
    /// Factory for creating individual fragment GameObjects from Voronoi cells.
    /// Handles mesh, collider, rigidbody, and texture/overlay generation.
    /// </summary>
    public class FragmentFactory
    {
        // Configuration
        private Material fragmentMaterial;
        private bool generateOverlay;
        private int overlayTextureSize;
        private int gameObjectLayer;
        private Transform parentTransform;
        private static Material sharedOverlayMaterial;

        // Runtime fracture settings
        private VoronoiFracture2D ownerComponent;
        private bool enableRuntimeFracture;
        private bool waitForCollision;
        private float breakImpactThreshold;
        private int runtimeSiteCount;
        private int runtimeBreakDepth;

        public FragmentFactory(VoronoiFracture2D owner, Transform parent, Material material,
            bool overlay, int texSize, int layer)
        {
            ownerComponent = owner;
            parentTransform = parent;
            fragmentMaterial = material;
            generateOverlay = overlay;
            overlayTextureSize = texSize;
            gameObjectLayer = layer;
        }

        public void SetRuntimeFractureSettings(bool enabled, bool waitCollision, float threshold, int sites, int depth)
        {
            enableRuntimeFracture = enabled;
            waitForCollision = waitCollision;
            breakImpactThreshold = threshold;
            runtimeSiteCount = sites;
            runtimeBreakDepth = depth;
        }

        /// <summary>
        /// Create a fragment GameObject from a polygon cell.
        /// </summary>
        public void CreateFragment(List<Point> polygon, Color color)
        {
            if (polygon == null || polygon.Count < 3)
                return;

            // Clean the polygon
            var cleanedPolygon = PolygonUtility.CleanPolygon(polygon);
            if (cleanedPolygon.Count < 3)
                return;

            // Calculate centroid
            Vector2 centroid = CalculateCentroid(cleanedPolygon);

            // Create GameObject
            var fragmentGO = CreateFragmentGameObject(centroid);

            // Convert to local space relative to centroid
            var localVertices = ConvertToLocalSpace(cleanedPolygon, centroid);

            // Ensure CCW winding
            PolygonUtility.EnsureCCW(ref localVertices);

            // Set up components
            SetupCollider(fragmentGO, localVertices);
            SetupRigidbody(fragmentGO);
            var mesh = SetupMesh(fragmentGO, localVertices);

            // Generate overlay texture if enabled
            if (generateOverlay && mesh != null)
            {
                SetupOverlay(fragmentGO, mesh, cleanedPolygon, color, centroid);
            }

            // Add runtime fracture capability if enabled
            if (enableRuntimeFracture)
            {
                SetupRuntimeFracture(fragmentGO);
            }
            
            // Add GenericRewind for time rewinder support on fragments
            if (RewindManager.Instance != null)
            {
                var genericRewind = fragmentGO.AddComponent<GenericRewind>();
                
                // CRITICAL: Enable tracking fields using reflection (they're private SerializeFields)
                var type = typeof(GenericRewind);
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                
                // Enable all tracking
                type.GetField("trackObjectActiveState", flags)?.SetValue(genericRewind, true);
                type.GetField("trackTransform", flags)?.SetValue(genericRewind, true);
                type.GetField("trackVelocity", flags)?.SetValue(genericRewind, true);
                
                Debug.Log($"Added GenericRewind to fragment '{fragmentGO.name}' with tracking enabled");
                
                // Register with RewindManager
                RewindManager.Instance.AddObjectForTracking(genericRewind, RewindManager.OutOfBoundsBehaviour.DisableDestroy);
            }
        }

        private GameObject CreateFragmentGameObject(Vector2 centroid)
        {
#if UNITY_EDITOR
            var go = new GameObject("Fragment");
            go.transform.SetParent(parentTransform, true);
            Undo.RegisterCreatedObjectUndo(go, "Voronoi Fragment");
#else
            var go = new GameObject("Fragment");
            go.transform.SetParent(parentTransform, true);
#endif
            go.transform.position = new Vector3(centroid.x, centroid.y, 0f);
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.layer = gameObjectLayer;

            return go;
        }

        private Vector2 CalculateCentroid(List<Point> polygon)
        {
            Vector2 centroid = Vector2.zero;
            foreach (var point in polygon)
                centroid += new Vector2((float)point.X, (float)point.Y);
            return centroid / polygon.Count;
        }

        private Vector2[] ConvertToLocalSpace(List<Point> worldPolygon, Vector2 centroid)
        {
            var localVertices = new Vector2[worldPolygon.Count];
            for (int i = 0; i < worldPolygon.Count; i++)
            {
                var worldPos = new Vector2((float)worldPolygon[i].X, (float)worldPolygon[i].Y);
                localVertices[i] = worldPos - centroid;
            }
            return localVertices;
        }

        private void SetupCollider(GameObject go, Vector2[] localVertices)
        {
            var collider = go.AddComponent<PolygonCollider2D>();
            collider.points = localVertices;
        }

        private void SetupRigidbody(GameObject go)
        {
            var rigidbody = go.AddComponent<Rigidbody2D>();
            rigidbody.gravityScale = 1f;
        }

        private Mesh SetupMesh(GameObject go, Vector2[] localVertices)
        {
            var meshFilter = go.AddComponent<MeshFilter>();
            var meshRenderer = go.AddComponent<MeshRenderer>();

            if (fragmentMaterial != null)
                meshRenderer.sharedMaterial = fragmentMaterial;

            var mesh = FragmentMeshBuilder.CreateMesh(localVertices);
            meshFilter.sharedMesh = mesh;

            // Register with RewindManager if GenericRewind exists (fragments spawned at runtime)
            var genericRewind = go.GetComponent<GenericRewind>();
            if (genericRewind != null && RewindManager.Instance != null)
            {
                RewindManager.Instance.AddObjectForTracking(genericRewind, RewindManager.OutOfBoundsBehaviour.DisableDestroy);
            }

            return mesh;
        }

        private void SetupOverlay(GameObject fragmentGO, Mesh baseMesh, List<Point> worldPolygon,
            Color color, Vector2 centroid)
        {
            // Generate texture
            var texture = FragmentTextureGenerator.GenerateTexture(
                worldPolygon, color, Mathf.Clamp(overlayTextureSize, 64, 2048));

            if (texture == null)
                return;

            // Create overlay GameObject
#if UNITY_EDITOR
            var overlayGO = new GameObject("OverlayMesh");
            overlayGO.transform.SetParent(fragmentGO.transform, false);
            Undo.RegisterCreatedObjectUndo(overlayGO, "Voronoi Overlay");
#else
            var overlayGO = new GameObject("OverlayMesh");
            overlayGO.transform.SetParent(fragmentGO.transform, false);
#endif
            overlayGO.transform.localPosition = Vector3.zero;

            // Create mesh that matches fragment geometry
            var overlayFilter = overlayGO.AddComponent<MeshFilter>();
            var overlayRenderer = overlayGO.AddComponent<MeshRenderer>();

            var overlayMesh = CreateOverlayMesh(baseMesh, worldPolygon, centroid);
            overlayFilter.sharedMesh = overlayMesh;

            // Set up material
            SetupOverlayMaterial(overlayRenderer, texture);
        }

        private Mesh CreateOverlayMesh(Mesh baseMesh, List<Point> worldPolygon, Vector2 centroid)
        {
            var mesh = new Mesh();
            mesh.vertices = baseMesh.vertices;
            mesh.triangles = baseMesh.triangles;

            // Calculate UV bounds from world polygon
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var point in worldPolygon)
            {
                if ((float)point.X < minX) minX = (float)point.X;
                if ((float)point.Y < minY) minY = (float)point.Y;
                if ((float)point.X > maxX) maxX = (float)point.X;
                if ((float)point.Y > maxY) maxY = (float)point.Y;
            }

            float width = Mathf.Max(1e-6f, maxX - minX);
            float height = Mathf.Max(1e-6f, maxY - minY);

            // Generate UVs
            var uvs = FragmentMeshBuilder.CreateUVs(baseMesh.vertices, centroid, minX, minY, width, height);
            mesh.uv = uvs;
            mesh.RecalculateBounds();

            return mesh;
        }

        private void SetupOverlayMaterial(MeshRenderer renderer, Texture2D texture)
        {
            // Create or reuse shared overlay material
            if (sharedOverlayMaterial == null)
            {
                var shader = Shader.Find("Custom/PixelToon");
                if (shader != null)
                    sharedOverlayMaterial = new Material(shader);
                else
                    sharedOverlayMaterial = new Material(Shader.Find("Sprites/Default"));

                sharedOverlayMaterial.hideFlags = HideFlags.DontSave;

                // Set default shader properties
                if (sharedOverlayMaterial.HasProperty("_PosterizeLevels"))
                    sharedOverlayMaterial.SetFloat("_PosterizeLevels", 4f);
                if (sharedOverlayMaterial.HasProperty("_PixelSize"))
                    sharedOverlayMaterial.SetFloat("_PixelSize", 32f);
            }

            renderer.sharedMaterial = sharedOverlayMaterial;

            // Ensure overlay renders after fragment
            int baseQueue = fragmentMaterial != null ? fragmentMaterial.renderQueue : 3000;
            renderer.sharedMaterial.renderQueue = baseQueue + 1;

            // Set texture using MaterialPropertyBlock (no material instance needed)
            var propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetTexture("_MainTex", texture);
            renderer.SetPropertyBlock(propertyBlock);
        }

        private void SetupRuntimeFracture(GameObject fragmentGO)
        {
            var fracturable = fragmentGO.AddComponent<FracturablePiece2D>();
            fracturable.owner = ownerComponent;
            fracturable.breakImpactThreshold = breakImpactThreshold;
            fracturable.waitForCollision = waitForCollision;
            fracturable.siteCount = Mathf.Max(3, runtimeSiteCount);
            fracturable.remainingDepth = Mathf.Max(0, runtimeBreakDepth);
        }
    }
}
