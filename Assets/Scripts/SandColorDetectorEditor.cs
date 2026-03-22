using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SandColorDetector))]
public class SandColorDetectorEditor : Editor
{


    public override void OnInspectorGUI()
    {
        SandColorDetector detector = (SandColorDetector)target;
        
        // Draw default inspector for references and settings
        SerializedProperty regionTool = serializedObject.FindProperty("regionTool");
        SerializedProperty fillEffect = serializedObject.FindProperty("fillEffect");
        SerializedProperty colorLibrary = serializedObject.FindProperty("colorLibrary");
        SerializedProperty particleEffectObject = serializedObject.FindProperty("particleEffectObject");
        SerializedProperty colorMatchThreshold = serializedObject.FindProperty("colorMatchThreshold");
        SerializedProperty debugMode = serializedObject.FindProperty("debugMode");
        
        EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(regionTool);
        EditorGUILayout.PropertyField(fillEffect);
        EditorGUILayout.PropertyField(colorLibrary);
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Visual Effects", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(particleEffectObject, new GUIContent("Particle Effect Object"));
        
        SerializedProperty pouringParticlePrefab = serializedObject.FindProperty("pouringParticlePrefab");
        SerializedProperty particleMoveSpeed = serializedObject.FindProperty("particleMoveSpeed");
        SerializedProperty particlesPerPiece = serializedObject.FindProperty("particlesPerPiece");
        SerializedProperty topPixelOffset = serializedObject.FindProperty("topPixelOffset");
        SerializedProperty particleScaleMultiplier = serializedObject.FindProperty("particleScaleMultiplier");
        
        EditorGUILayout.PropertyField(pouringParticlePrefab, new GUIContent("Pouring Particle Prefab"));
        EditorGUILayout.PropertyField(particleMoveSpeed, new GUIContent("Particle Move Speed"));
        EditorGUILayout.PropertyField(particlesPerPiece, new GUIContent("Particles Per Piece"));
        EditorGUILayout.PropertyField(topPixelOffset, new GUIContent("Top Pixel Offset"));
        if (particleScaleMultiplier != null) EditorGUILayout.PropertyField(particleScaleMultiplier, new GUIContent("Particle Scale Multiplier"));
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Detection Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(colorMatchThreshold);
        EditorGUILayout.PropertyField(debugMode);
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Region Fill Settings", EditorStyles.boldLabel);
        
        // Get color library
        ColorMaterialLibrary library = (ColorMaterialLibrary)colorLibrary.objectReferenceValue;
        string[] colorNames = library != null ? library.GetAllColorNames() : new string[0];
        
        SerializedProperty regionFillSettings = serializedObject.FindProperty("regionFillSettings");
        
        if (regionFillSettings.arraySize > 0)
        {
            for (int i = 0; i < regionFillSettings.arraySize; i++)
            {
                SerializedProperty element = regionFillSettings.GetArrayElementAtIndex(i);
                SerializedProperty regionId = element.FindPropertyRelative("regionId");
                SerializedProperty colorName = element.FindPropertyRelative("colorName");
                SerializedProperty piecesNeeded = element.FindPropertyRelative("piecesNeededToFill");
                
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Region {regionId.intValue}", EditorStyles.boldLabel);
                
                // Color name dropdown
                if (library != null && colorNames.Length > 0)
                {
                    int currentIndex = System.Array.IndexOf(colorNames, colorName.stringValue);
                    if (currentIndex < 0) currentIndex = 0;
                    
                    int newIndex = EditorGUILayout.Popup("Color Name", currentIndex, colorNames);
                    if (newIndex >= 0 && newIndex < colorNames.Length)
                    {
                        colorName.stringValue = colorNames[newIndex];
                        
                        // Show color preview
                        Color previewColor = library.GetColorByName(colorNames[newIndex]);
                        EditorGUI.DrawRect(GUILayoutUtility.GetRect(100, 20), previewColor);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Assign Color Library to see available colors", MessageType.Warning);
                    EditorGUILayout.PropertyField(colorName);
                }
                
                EditorGUILayout.PropertyField(piecesNeeded);
                
                // Show progress (runtime only)
                if (Application.isPlaying)
                {
                    SerializedProperty piecesCollected = element.FindPropertyRelative("piecesCollected");
                    SerializedProperty fillProgress = element.FindPropertyRelative("fillProgress");
                    
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.IntField("Pieces Collected", piecesCollected.intValue);
                    EditorGUILayout.Slider("Fill Progress", fillProgress.floatValue, 0f, 1f);
                    EditorGUI.EndDisabledGroup();
                }
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No regions configured. Click 'Auto-Populate Regions' to get started.", MessageType.Info);
        }
        
        SerializedProperty totalPiecesPerColor = serializedObject.FindProperty("totalPiecesPerColor");
        if (totalPiecesPerColor != null && totalPiecesPerColor.arraySize > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Color Totals Summary", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            for (int i = 0; i < totalPiecesPerColor.arraySize; i++)
            {
                SerializedProperty elem = totalPiecesPerColor.GetArrayElementAtIndex(i);
                string colName = elem.FindPropertyRelative("colorName").stringValue;
                int count = elem.FindPropertyRelative("totalPiecesNeeded").intValue;
                
                // Draw color preview if available
                Color previewCol = Color.white;
                if (library != null) previewCol = library.GetColorByName(colName);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUI.DrawRect(GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20)), previewCol);
                EditorGUILayout.LabelField($"  {colName}:", EditorStyles.boldLabel, GUILayout.Width(100));
                
                int newCount = EditorGUILayout.DelayedIntField(count, GUILayout.Width(60));
                if (newCount != count && newCount > 0)
                {
                    Undo.RecordObject(detector, "Adjust Color Total");
                    detector.AdjustColorTotal(colName, newCount);
                    EditorUtility.SetDirty(detector);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                
                EditorGUILayout.LabelField("total pieces");
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space((float)2.0);
            }
            EditorGUILayout.EndVertical();
        }
        
        serializedObject.ApplyModifiedProperties();
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Region Management", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Auto-Populate Regions from Region Tool"))
        {
            detector.AutoPopulateRegions();
            EditorUtility.SetDirty(detector);
        }
        
        if (GUILayout.Button("Reset All Region Progress"))
        {
            detector.ResetAllRegions();
            EditorUtility.SetDirty(detector);
        }
    }
}

[InitializeOnLoad]
public static class SandColorDetectorSceneHUD
{
    static SandColorDetectorSceneHUD()
    {
        SceneView.duringSceneGui += OnGlobalSceneGUI;
    }

    private static void OnGlobalSceneGUI(SceneView sceneView)
    {
        SandColorDetector detector = Object.FindObjectOfType<SandColorDetector>();
        if (detector == null) return;

        var totalPiecesPerColor = detector.totalPiecesPerColor;
        
        if (totalPiecesPerColor != null && totalPiecesPerColor.Count > 0)
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10, 10, 220, 400));
            
            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = EditorGUIUtility.whiteTexture; 
            
            Color oldColor = GUI.color;
            GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.9f); 
            GUILayout.BeginVertical(boxStyle);
            GUI.color = oldColor; 
            
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.normal.textColor = Color.white;
            GUILayout.Label("Total Sand Pieces Needed", titleStyle);
            GUILayout.Space(5);
            
            ColorMaterialLibrary library = detector.colorLibrary;
            
            for (int i = 0; i < totalPiecesPerColor.Count; i++)
            {
                var elem = totalPiecesPerColor[i];
                string colName = elem.colorName;
                int count = elem.totalPiecesNeeded;
                
                Color c = Color.white;
                if (library != null) c = library.GetColorByName(colName);
                
                GUILayout.BeginHorizontal();
                Rect r = GUILayoutUtility.GetRect(15, 15, GUILayout.Width(15));
                EditorGUI.DrawRect(r, c);
                
                GUIStyle textStyle = new GUIStyle(EditorStyles.boldLabel);
                textStyle.normal.textColor = Color.white;
                GUILayout.Label($"  {colName}: {count}", textStyle);
                GUILayout.EndHorizontal();
                GUILayout.Space(2);
            }
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
            Handles.EndGUI();
        }
    }
}
