using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom editor for FracturePrefabManager.
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
            "Manages a library of fracture prefabs with individual settings.",
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

        serializedObject.ApplyModifiedProperties();
    }
}
