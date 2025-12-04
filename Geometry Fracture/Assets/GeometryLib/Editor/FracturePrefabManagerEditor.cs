using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// Custom editor for FracturePrefabManager with helper utilities.
/// </summary>
[CustomEditor(typeof(FracturePrefabManager))]
public class FracturePrefabManagerEditor : UnityEditor.Editor
{
    private SerializedProperty _prefabsProperty;
    private SerializedProperty _selectedPrefabIndexProperty;
    private SerializedProperty _targetCameraProperty;
    private SerializedProperty _spawnOffsetProperty;
    private SerializedProperty _spawnOnClickProperty;

    private void OnEnable()
    {
        _prefabsProperty = serializedObject.FindProperty("prefabs");
        _selectedPrefabIndexProperty = serializedObject.FindProperty("selectedPrefabIndex");
        _targetCameraProperty = serializedObject.FindProperty("targetCamera");
        _spawnOffsetProperty = serializedObject.FindProperty("spawnOffset");
        _spawnOnClickProperty = serializedObject.FindProperty("spawnOnClick");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var manager = (FracturePrefabManager)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Fracture Prefab Manager", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Manages a library of fracture prefabs with individual settings. " +
            "Use the Setup Wizard below to quickly configure the system.",
            MessageType.Info);

        EditorGUILayout.Space();

        // Camera and spawn settings
        EditorGUILayout.PropertyField(_targetCameraProperty);
        EditorGUILayout.PropertyField(_spawnOnClickProperty);
        EditorGUILayout.PropertyField(_spawnOffsetProperty);

        EditorGUILayout.Space();

        // Prefabs list
        EditorGUILayout.PropertyField(_prefabsProperty, new GUIContent("Prefab Library"), true);

        EditorGUILayout.Space();

        // Current selection
        if (manager.prefabs.Count > 0)
        {
            EditorGUILayout.LabelField("Current Selection", EditorStyles.boldLabel);
            _selectedPrefabIndexProperty.intValue = EditorGUILayout.IntSlider(
                "Selected Index",
                _selectedPrefabIndexProperty.intValue,
                0,
                manager.prefabs.Count - 1);

            var selected = manager.GetSelectedPrefab();
            if (selected != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"Name: {selected.name}");
                EditorGUILayout.LabelField($"Site Count: {selected.siteCount}");
                EditorGUILayout.LabelField($"Site Jitter: {selected.siteJitter:F2}");
                EditorGUILayout.LabelField($"Runtime Fracture: {(selected.enableRuntimeFracture ? "Enabled" : "Disabled")}");
                EditorGUI.indentLevel--;
            }
        }

        EditorGUILayout.Space();

        // Quick setup buttons
        EditorGUILayout.LabelField("Quick Setup", EditorStyles.boldLabel);

        if (GUILayout.Button("Create Example Prefabs", GUILayout.Height(30)))
        {
            CreateExamplePrefabs(manager);
        }

        if (GUILayout.Button("Setup UI Toolkit Interface", GUILayout.Height(30)))
        {
            SetupUIToolkit(manager);
        }

        EditorGUILayout.Space();

        // Play mode testing
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("Play Mode Controls", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("◀ Previous"))
            {
                manager.SelectPreviousPrefab();
            }
            if (GUILayout.Button("Spawn Selected"))
            {
                manager.SpawnSelected();
            }
            if (GUILayout.Button("Next ▶"))
            {
                manager.SelectNextPrefab();
            }
            EditorGUILayout.EndHorizontal();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void CreateExamplePrefabs(FracturePrefabManager manager)
    {
        if (!EditorUtility.DisplayDialog(
            "Create Example Prefabs",
            "This will create 3 example fracture prefabs (Circle, Square, Triangle) in Assets/Prefabs/FracturePrefabs/. Continue?",
            "Create",
            "Cancel"))
        {
            return;
        }

        string prefabDir = "Assets/Prefabs/FracturePrefabs";
        if (!AssetDatabase.IsValidFolder(prefabDir))
        {
            string parentDir = "Assets/Prefabs";
            if (!AssetDatabase.IsValidFolder(parentDir))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }
            AssetDatabase.CreateFolder(parentDir, "FracturePrefabs");
        }

        // Create circle prefab
        CreateShapePrefab("Circle", prefabDir, manager, ShapeType.Circle);
        CreateShapePrefab("Square", prefabDir, manager, ShapeType.Square);
        CreateShapePrefab("Triangle", prefabDir, manager, ShapeType.Triangle);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Prefabs Created",
            $"3 example prefabs created in {prefabDir}.\nThey have been added to the Prefab Library.",
            "OK");
    }

    private enum ShapeType { Circle, Square, Triangle }

    private void CreateShapePrefab(string name, string directory, FracturePrefabManager manager, ShapeType shape)
    {
        // Create temporary GameObject
        var go = new GameObject(name);

        // Add SpriteRenderer
        var sr = go.AddComponent<SpriteRenderer>();
        sr.color = Color.white;

        // Add PolygonCollider2D
        var poly = go.AddComponent<PolygonCollider2D>();

        // Create shape points
        Vector2[] points = null;
        switch (shape)
        {
            case ShapeType.Circle:
                points = CreateCirclePoints(1f, 20);
                break;
            case ShapeType.Square:
                points = new Vector2[]
                {
                    new Vector2(-1f, -1f),
                    new Vector2(1f, -1f),
                    new Vector2(1f, 1f),
                    new Vector2(-1f, 1f)
                };
                break;
            case ShapeType.Triangle:
                points = new Vector2[]
                {
                    new Vector2(0f, 1f),
                    new Vector2(-0.866f, -0.5f),
                    new Vector2(0.866f, -0.5f)
                };
                break;
        }

        poly.points = points;

        // Add VoronoiFracture2D
        var fracture = go.AddComponent<VoronoiFracture2D>();
        fracture.siteCount = shape == ShapeType.Circle ? 12 : 8;
        fracture.siteJitter = 0.2f;
        fracture.enableRuntimeFracture = true;
        fracture.generateOverlay = true;

        // Add Rigidbody2D
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;

        // Save as prefab
        string path = $"{directory}/{name}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        DestroyImmediate(go);

        // Add to manager
        Undo.RecordObject(manager, "Add Prefab to Library");
        manager.prefabs.Add(new FracturePrefabManager.FracturePrefabData
        {
            name = name,
            prefab = prefab,
            siteCount = fracture.siteCount,
            siteJitter = fracture.siteJitter,
            enableRuntimeFracture = true,
            breakImpactThreshold = 5f,
            waitForCollision = false,
            runtimeSiteCount = 6,
            runtimeBreakDepth = 1,
            generateOverlay = true,
            overlayTextureSize = 512,
            spawnScale = 1f
        });

        EditorUtility.SetDirty(manager);
    }

    private Vector2[] CreateCirclePoints(float radius, int segments)
    {
        Vector2[] points = new Vector2[segments];
        float angleStep = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            points[i] = new Vector2(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius
            );
        }
        return points;
    }

    private void SetupUIToolkit(FracturePrefabManager manager)
    {
        // Check if UXML exists
        string uxmlPath = "Assets/GeometryLib/UI/FractureUI.uxml";
        var uxmlAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);

        if (uxmlAsset == null)
        {
            EditorUtility.DisplayDialog(
                "UXML Not Found",
                $"Could not find {uxmlPath}.\nPlease ensure FractureUI.uxml exists in the project.",
                "OK");
            return;
        }

        // Find or create UI Document
        var uiDoc = FindObjectOfType<UIDocument>();
        GameObject uiGameObject = null;

        if (uiDoc == null)
        {
            // Create new UI Document
            uiGameObject = new GameObject("FractureUI");
            uiDoc = uiGameObject.AddComponent<UIDocument>();
            Undo.RegisterCreatedObjectUndo(uiGameObject, "Create UI Document");
        }
        else
        {
            uiGameObject = uiDoc.gameObject;
        }

        // Assign UXML
        Undo.RecordObject(uiDoc, "Setup UI Document");
        uiDoc.visualTreeAsset = uxmlAsset;

        // Find or create Panel Settings
        string panelSettingsPath = "Assets/GeometryLib/UI/FracturePanelSettings.asset";
        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelSettingsPath);

        if (panelSettings == null)
        {
            // Create panel settings
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            panelSettings.scale = 1f;
            panelSettings.sortingOrder = 0;

            string panelDir = "Assets/GeometryLib/UI";
            if (!AssetDatabase.IsValidFolder(panelDir))
            {
                AssetDatabase.CreateFolder("Assets/GeometryLib", "UI");
            }

            AssetDatabase.CreateAsset(panelSettings, panelSettingsPath);
            AssetDatabase.SaveAssets();
        }

        uiDoc.panelSettings = panelSettings;

        // Add or get FractureUIController
        var controller = uiGameObject.GetComponent<FractureUIController>();
        if (controller == null)
        {
            controller = uiGameObject.AddComponent<FractureUIController>();
            Undo.RegisterCreatedObjectUndo(controller, "Add FractureUIController");
        }

        // Link to manager
        Undo.RecordObject(controller, "Link UI Controller");
        controller.prefabManager = manager;

        EditorUtility.SetDirty(uiDoc);
        EditorUtility.SetDirty(controller);

        Selection.activeGameObject = uiGameObject;

        EditorUtility.DisplayDialog(
            "UI Setup Complete",
            "UI Toolkit interface has been configured!\n\n" +
            "- UIDocument GameObject created/updated\n" +
            "- FractureUI.uxml assigned\n" +
            "- Panel Settings created\n" +
            "- FractureUIController linked to manager\n\n" +
            "Enter Play Mode to test the UI!",
            "OK");
    }
}
