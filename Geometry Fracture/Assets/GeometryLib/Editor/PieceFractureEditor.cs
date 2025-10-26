using System;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PieceFracture))]
public class PieceFractureEditor : Editor
{
    SerializedProperty pieceMaterialProp;
    SerializedProperty thicknessProp;
    SerializedProperty splitSiteCountProp;
    SerializedProperty splitThresholdProp;

    void OnEnable()
    {
        pieceMaterialProp = serializedObject.FindProperty("pieceMaterial");
        thicknessProp = serializedObject.FindProperty("thickness");
        splitSiteCountProp = serializedObject.FindProperty("splitSiteCount");
        splitThresholdProp = serializedObject.FindProperty("splitThreshold");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(pieceMaterialProp);
        EditorGUILayout.PropertyField(thicknessProp);
        EditorGUILayout.PropertyField(splitSiteCountProp);
        EditorGUILayout.PropertyField(splitThresholdProp);

        EditorGUILayout.Space();
        if (GUILayout.Button("Force Split (editor)"))
        {
            var pf = (PieceFracture)target;
            // Call the new public API which forces a split at a local point. Use the object's local origin as contact
            // and a large impact so editor splitting is visible.
            try
            {
                Vector2 contactLocal = Vector2.zero;
                float impact = Mathf.Max(10f, pf.splitThreshold * 2f);
                pf.ForceSplitAt(contactLocal, impact);
            }
            catch (Exception ex)
            {
                Debug.LogError($"ForceSplit failed: {ex}");
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
