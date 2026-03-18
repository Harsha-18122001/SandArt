using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpriteRegionEditorTool))]
public class SpriteRegionEditorToolEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SpriteRegionEditorTool tool = (SpriteRegionEditorTool)target;

        // Use serializedObject for the top fields for built-in undo support
        serializedObject.Update();
        SerializedProperty sourceSprite = serializedObject.FindProperty("sourceSprite");
        SerializedProperty colorLibrary = serializedObject.FindProperty("colorLibrary");
        SerializedProperty detectBorderRegions = serializedObject.FindProperty("detectBorderRegions");
        SerializedProperty borderShrinkAmount = serializedObject.FindProperty("borderShrinkAmount");

        EditorGUILayout.PropertyField(sourceSprite);
        EditorGUILayout.PropertyField(colorLibrary);
        EditorGUILayout.PropertyField(detectBorderRegions, new GUIContent("Detect Border Regions"));
        
        if (tool.detectBorderRegions && tool.borderRegions != null && tool.borderRegions.Count > 0)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.Slider(borderShrinkAmount, 0f, 1f, new GUIContent("Border Shrink Amount"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                tool.GeneratePreview();
            }
        }

        serializedObject.ApplyModifiedProperties();

        GUILayout.Space(10);

        if (GUILayout.Button("Detect Regions"))
        {
            Undo.RecordObject(tool, "Detect Regions");
            tool.DetectRegions();
            EditorUtility.SetDirty(tool);
        }
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Preview"))
        {
            tool.GeneratePreview();
        }
        
        if (GUILayout.Button("Sync Colors with Library"))
        {
            Undo.RecordObject(tool, "Sync Colors with Library");
            tool.RefreshColorsFromLibrary();
            EditorUtility.SetDirty(tool);
        }
        EditorGUILayout.EndHorizontal();
        
        // Add button to detect border regions separately
        if (tool.detectBorderRegions)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Border Management", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Detect Border Regions"))
            {
                Undo.RecordObject(tool, "Detect Border Regions");
                tool.ForceDetectBorderRegions();
                EditorUtility.SetDirty(tool);
            }
            
            if (GUILayout.Button("Save Border Colors"))
            {
                tool.SaveBorderColors();
            }
            EditorGUILayout.EndHorizontal();
        }

        if (tool.regions != null && tool.regions.Count > 0)
        {
            GUILayout.Space(10);
            GUILayout.Label($"Detected Regions: {tool.regions.Count}", EditorStyles.boldLabel);

            ColorMaterialLibrary library = tool.colorLibrary;
            string[] colorNames = library != null ? library.GetAllColorNames() : new string[0];

            for (int i = tool.regions.Count - 1; i >= 0; i--)
            {
                var region = tool.regions[i];

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField($"Region {i}", GUILayout.Width(70));

                // COLOR PICKER CHANGE CHECK
                EditorGUI.BeginChangeCheck();
                Color newColor = EditorGUILayout.ColorField(region.color, GUILayout.Width(60), GUILayout.Height(30));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(tool, "Change Region Color");
                    region.color = newColor;
                    EditorUtility.SetDirty(tool);
                    tool.GeneratePreview();
                }

                // DROPDOWN CHANGE CHECK
                if (library != null && colorNames.Length > 0)
                {
                    int currentIndex = System.Array.IndexOf(colorNames, region.colorName);
                    if (currentIndex < 0) currentIndex = 0;

                    EditorGUI.BeginChangeCheck();
                    int newIndex = EditorGUILayout.Popup(currentIndex, colorNames, GUILayout.Width(100));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(tool, "Change Region Dropdown");
                        region.colorName = colorNames[newIndex];
                        // Automatically update the color picker to match the library choice
                        region.color = library.GetColorByName(region.colorName);
                        EditorUtility.SetDirty(tool);
                        tool.GeneratePreview();
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No Color Library", GUILayout.Width(100));
                }

                // PIXEL COUNT
                EditorGUILayout.LabelField($"{region.PixelCount} pixels", GUILayout.Width(80));

                // REMOVE BUTTON
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("X", GUILayout.Width(25), GUILayout.Height(30)))
                {
                    Undo.RecordObject(tool, "Remove Region");
                    tool.regions.RemoveAt(i);
                    EditorUtility.SetDirty(tool);
                    tool.GeneratePreview();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
        }
        
        // Display border regions
        if (tool.borderRegions != null && tool.borderRegions.Count > 0)
        {
            GUILayout.Space(10);
            GUILayout.Label($"Border Regions: {tool.borderRegions.Count}", EditorStyles.boldLabel);
            
            for (int i = tool.borderRegions.Count - 1; i >= 0; i--)
            {
                var borderRegion = tool.borderRegions[i];
                
                EditorGUILayout.BeginVertical("box");
                
                // First row: name, color, pixel count, remove button
                EditorGUILayout.BeginHorizontal();
                
                // Border region name
                EditorGUILayout.LabelField($"Border {i}", GUILayout.Width(70));
                
                // Border color picker (editable)
                EditorGUI.BeginChangeCheck();
                Color newColor = EditorGUILayout.ColorField(borderRegion.color, GUILayout.Width(60), GUILayout.Height(30));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(tool, "Change Border Color");
                    borderRegion.color = newColor;
                    
                    // Force Unity to save the changes
                    EditorUtility.SetDirty(tool);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
                    
                    tool.GeneratePreview(); // Immediately update preview
                    
                    Debug.Log($"Changed border {i} color to {newColor}");
                }
                
                // Pixel count
                EditorGUILayout.LabelField($"{borderRegion.PixelCount} pixels", GUILayout.Width(100));
                
                // Remove button
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("Remove & Fill", GUILayout.Width(100), GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog("Remove Border Region", 
                        $"Remove Border {i} and fill with adjacent region colors?", 
                        "Yes", "No"))
                    {
                        Undo.RecordObject(tool, "Remove Border Region");
                        tool.RemoveBorderRegion(i);
                        EditorUtility.SetDirty(tool);
                    }
                }
                GUI.backgroundColor = Color.white;
                
                EditorGUILayout.EndHorizontal();
                
                // Second row: shrink slider
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Shrink:", GUILayout.Width(50));
                
                EditorGUI.BeginChangeCheck();
                float newShrinkAmount = EditorGUILayout.Slider(borderRegion.shrinkAmount, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(tool, "Change Border Shrink");
                    borderRegion.shrinkAmount = newShrinkAmount;
                    EditorUtility.SetDirty(tool);
                    tool.GeneratePreview();
                }
                
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.EndVertical();
            }
        }
    }
}