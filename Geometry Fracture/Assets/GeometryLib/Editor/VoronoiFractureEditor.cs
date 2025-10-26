using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(VoronoiFracture))]
public class VoronoiFractureEditor : Editor
{
    SerializedProperty useRandomSitesProp;
    SerializedProperty siteCountProp;
    SerializedProperty randomSeedProp;
    SerializedProperty sitesProp;

    SerializedProperty rectSizeProp;
    SerializedProperty rectCenterProp;

    SerializedProperty thicknessProp;
    SerializedProperty fragmentScaleProp;
    SerializedProperty pieceMaterialProp;
    SerializedProperty spawnPiecesImmediatelyProp;
    SerializedProperty fracturePlaneProp;
    // Fragment / explosion tuning
    SerializedProperty fragmentExplosionForceProp;
    SerializedProperty fragmentExplosionRadiusProp;
    SerializedProperty fragmentExplosionUpModifierProp;
    SerializedProperty fragmentExplosionRandomnessProp;
    SerializedProperty fragmentSplitThresholdProp;
    SerializedProperty maxRecursionProp;
    SerializedProperty verboseLogsProp;
    SerializedProperty attachCollisionProbeToWholeProp;
    SerializedProperty probeAutoSplitProp;
    SerializedProperty attachTriggerHelperToWholeProp;

    SerializedProperty generateOnStartProp;

    void OnEnable()
    {
        useRandomSitesProp = serializedObject.FindProperty("useRandomSites");
        siteCountProp = serializedObject.FindProperty("siteCount");
        randomSeedProp = serializedObject.FindProperty("randomSeed");
        sitesProp = serializedObject.FindProperty("sites");

        rectSizeProp = serializedObject.FindProperty("rectSize");
        rectCenterProp = serializedObject.FindProperty("rectCenter");

        thicknessProp = serializedObject.FindProperty("thickness");
        fragmentScaleProp = serializedObject.FindProperty("fragmentScale");
        pieceMaterialProp = serializedObject.FindProperty("pieceMaterial");

        fragmentExplosionForceProp = serializedObject.FindProperty("fragmentExplosionForce");
        fragmentExplosionRadiusProp = serializedObject.FindProperty("fragmentExplosionRadius");
        fragmentExplosionUpModifierProp = serializedObject.FindProperty("fragmentExplosionUpModifier");
        fragmentExplosionRandomnessProp = serializedObject.FindProperty("fragmentExplosionRandomness");
        fragmentSplitThresholdProp = serializedObject.FindProperty("fragmentSplitThreshold");
        maxRecursionProp = serializedObject.FindProperty("maxRecursion");
        verboseLogsProp = serializedObject.FindProperty("verboseLogs");
        attachCollisionProbeToWholeProp = serializedObject.FindProperty("attachCollisionProbeToWhole");
        probeAutoSplitProp = serializedObject.FindProperty("probeAutoSplit");
        attachTriggerHelperToWholeProp = serializedObject.FindProperty("attachTriggerHelperToWhole");

        spawnPiecesImmediatelyProp = serializedObject.FindProperty("spawnPiecesImmediately");
        fracturePlaneProp = serializedObject.FindProperty("fracturePlane");

        generateOnStartProp = serializedObject.FindProperty("generateOnStart");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Sites", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(useRandomSitesProp);
        EditorGUILayout.PropertyField(siteCountProp);
        EditorGUILayout.PropertyField(randomSeedProp);
        EditorGUILayout.PropertyField(sitesProp, true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Target Shape", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(rectSizeProp);
        EditorGUILayout.PropertyField(rectCenterProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Fragment Impulse & Splitting", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(fragmentSplitThresholdProp, new GUIContent("Fragment Split Threshold", "Impact threshold required for generated fragments/whole piece to split on collision"));
        EditorGUILayout.PropertyField(fragmentExplosionForceProp, new GUIContent("Fragment Explosion Force", "Multiplier applied to impact to form an explosion force for spawned fragments"));
        EditorGUILayout.PropertyField(fragmentExplosionRadiusProp, new GUIContent("Fragment Explosion Radius", "Radius used by AddExplosionForce when applying impulse to spawned fragments"));
        EditorGUILayout.PropertyField(fragmentExplosionUpModifierProp, new GUIContent("Fragment Explosion Up", "Up modifier used by AddExplosionForce for spawned fragments"));
        EditorGUILayout.PropertyField(fragmentExplosionRandomnessProp, new GUIContent("Fragment Explosion Randomness", "Random variation applied to the explosion force (0..1) for spawned fragments"));

        // Recursion limit for splitting
        EditorGUILayout.PropertyField(maxRecursionProp, new GUIContent("Max Recursion", "Maximum recursive split depth for fragments (0 = no recursion)"));

        EditorGUILayout.PropertyField(verboseLogsProp, new GUIContent("Verbose Logs", "Enable verbose logging for generation and pieces (turn off to avoid huge logs)"));
        EditorGUILayout.PropertyField(attachCollisionProbeToWholeProp, new GUIContent("Attach Collision Probe (whole)", "Attach a small probe to the whole piece to log collisions"));
        EditorGUILayout.PropertyField(probeAutoSplitProp, new GUIContent("Probe Auto Split", "If enabled the collision probe will auto-split the whole piece when it detects a collision (debug only)"));
        EditorGUILayout.PropertyField(attachTriggerHelperToWholeProp, new GUIContent("Attach Trigger Helper (whole)", "Attach a kinematic trigger collider to the whole piece to detect overlaps via OnTriggerEnter"));

        EditorGUILayout.LabelField("Piece Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(thicknessProp);
        EditorGUILayout.PropertyField(fragmentScaleProp, new GUIContent("Fragment Scale", "Scale multiplier applied to the 2D polygon before extrusion. Use >1 to enlarge pieces, <1 to shrink."));
        EditorGUILayout.PropertyField(pieceMaterialProp);
        EditorGUILayout.PropertyField(spawnPiecesImmediatelyProp, new GUIContent("Spawn Pieces Immediately", "When true spawn all pieces immediately; otherwise create a whole piece that splits on collision"));
        EditorGUILayout.PropertyField(fracturePlaneProp, new GUIContent("Fracture Plane", "Which local plane to project the host mesh and extrude fragments along"));

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(generateOnStartProp);

        EditorGUILayout.Space();
        // Buttons
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate"))
        {
            (target as VoronoiFracture).Generate();
            EditorUtility.SetDirty(target);
        }
        if (GUILayout.Button("Clear"))
        {
            (target as VoronoiFracture).ClearExistingChildren();
            EditorUtility.SetDirty(target);
        }
        EditorGUILayout.EndHorizontal();

        // Apply defaults to existing pieces
        if (GUILayout.Button("Apply Defaults To Child Pieces"))
        {
            var vf = (target as VoronoiFracture);
            foreach (Transform t in vf.transform)
            {
                var pf = t.GetComponent<PieceFracture>();
                if (pf != null)
                {
                    pf.pieceMaterial = vf.pieceMaterial;
                    pf.thickness = vf.thickness;
                    // also apply fragment impulse and split threshold settings
                    pf.explosionForce = vf.fragmentExplosionForce;
                    pf.explosionRadius = vf.fragmentExplosionRadius;
                    pf.explosionUpModifier = vf.fragmentExplosionUpModifier;
                    pf.explosionRandomness = vf.fragmentExplosionRandomness;
                    pf.splitThreshold = vf.fragmentSplitThreshold;
                }
            }
            EditorUtility.SetDirty(target);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debug / Test", EditorStyles.boldLabel);
        if (GUILayout.Button("Log Child Pieces"))
        {
            var vf = (target as VoronoiFracture);
            Debug.Log($"VoronoiFracture: childCount={vf.transform.childCount}");
            int i = 0;
            foreach (Transform t in vf.transform)
            {
                var go = t.gameObject;
                var mf = go.GetComponent<MeshFilter>();
                var mc = go.GetComponent<Collider>();
                var rb = go.GetComponent<Rigidbody>();
                var pf = go.GetComponent<PieceFracture>();
                Debug.Log($"Child[{i}] name={go.name} active={go.activeSelf} mesh={(mf != null ? mf.sharedMesh.vertexCount.ToString() : "none")} collider={(mc != null ? mc.GetType().Name : "none")} rb={(rb != null ? "present" : "missing")} kinematic={(rb != null ? rb.isKinematic.ToString() : "n/a")} pf={(pf != null ? "present" : "missing")} ");
                i++;
            }
        }

        if (GUILayout.Button("Force-split First Child"))
        {
            var vf = (target as VoronoiFracture);
            if (vf.transform.childCount == 0) { Debug.Log("VoronoiFracture: no child pieces to force-split"); }
            else
            {
                var first = vf.transform.GetChild(0).GetComponent<PieceFracture>();
                if (first == null) { Debug.Log("VoronoiFracture: first child has no PieceFracture component"); }
                else
                {
                    // Call ForceSplitAt at local zero with a high impact to ensure split
                    first.ForceSplitAt(new Vector2(0f, 0f), 1000f);
                    Debug.Log($"VoronoiFracture: forced split on {first.gameObject.name}");
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
