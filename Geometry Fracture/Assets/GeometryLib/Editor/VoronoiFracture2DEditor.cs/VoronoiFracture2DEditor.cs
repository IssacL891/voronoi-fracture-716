using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VoronoiFracture2D))]
public class VoronoiFracture2DEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        VoronoiFracture2D vf = (VoronoiFracture2D)target;
        EditorGUILayout.Space();

        if (GUILayout.Button("Fracture (with Undo)"))
        {
            // Register the whole object hierarchy for undo
            Undo.RegisterFullObjectHierarchyUndo(vf.gameObject, "Voronoi Fracture");
            vf.Fracture();
        }

        if (GUILayout.Button("Clear Fragments"))
        {
            // Use the built-in ClearFragments method which handles undo
            Undo.RegisterFullObjectHierarchyUndo(vf.gameObject, "Voronoi Clear");
            vf.ClearFragments();
            EditorUtility.SetDirty(vf.gameObject);
        }
    }
}
