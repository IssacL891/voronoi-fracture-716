using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VoronoiFracture2D))]
public class VoronoiFracture2DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        VoronoiFracture2D vf = (VoronoiFracture2D)target;
        EditorGUILayout.Space();
        if (GUILayout.Button("Fracture (with Undo)"))
        {
            // register the whole object hierarchy so we can restore activation/children state
            Undo.RegisterFullObjectHierarchyUndo(vf.gameObject, "Voronoi Fracture");
            vf.Fracture();
        }

        if (GUILayout.Button("Clear Fragments"))
        {
            // undoable clear: find the fragment parent (scene-wide) and destroy it via Undo so it can be restored
            var existingName = $"Fragments_{vf.gameObject.name}";
            var existing = GameObject.Find(existingName);
            if (existing != null)
            {
                Undo.RegisterFullObjectHierarchyUndo(vf.gameObject, "Voronoi Clear");
                Undo.DestroyObjectImmediate(existing);
            }
            // restore the source object's active state
            Undo.RecordObject(vf.gameObject, "Voronoi Clear");
            vf.gameObject.SetActive(true);
            // mark scene dirty
            EditorUtility.SetDirty(vf.gameObject);
        }
    }
}
