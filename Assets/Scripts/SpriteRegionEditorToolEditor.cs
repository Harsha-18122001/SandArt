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
        SerializedProperty autoDetectPictureColors = serializedObject.FindProperty("autoDetectPictureColors");
        SerializedProperty colorTolerance = serializedObject.FindProperty("colorTolerance");
        SerializedProperty minRegionPixels = serializedObject.FindProperty("minRegionPixels");

        EditorGUILayout.PropertyField(sourceSprite);
        EditorGUILayout.PropertyField(colorLibrary);
        EditorGUILayout.PropertyField(detectBorderRegions, new GUIContent("Detect Border Regions"));
        EditorGUILayout.PropertyField(autoDetectPictureColors, new GUIContent("Auto Detect Picture Colors"));
        
        if (autoDetectPictureColors.boolValue)
        {
            EditorGUILayout.PropertyField(colorTolerance, new GUIContent("Color Tolerance"));
        }
        
        EditorGUILayout.PropertyField(minRegionPixels, new GUIContent("Min Region Pixels"));
        
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
        
        GUILayout.Space(5);
        if (tool.regions != null && tool.regions.Count > 0)
        {
            if (GUILayout.Button("Create Color Library from Regions"))
            {
                string path = EditorUtility.SaveFilePanelInProject(
                    "Save Color Library", 
                    tool.gameObject.name + "_ColorLibrary", 
                    "asset", 
                    "Save color library based on detected regions"
                );

                if (!string.IsNullOrEmpty(path)) 
                {
                    ColorMaterialLibrary newLib = ScriptableObject.CreateInstance<ColorMaterialLibrary>();
                    System.Collections.Generic.HashSet<string> seenColors = new System.Collections.Generic.HashSet<string>();
                    
                    AssetDatabase.CreateAsset(newLib, path);
                    
                    int colorIdx = 1;
                    foreach(var r in tool.regions) 
                    {
                        string hex = ColorUtility.ToHtmlStringRGB(r.color);
                        string colorName = "Color_" + colorIdx.ToString() + "_" + hex;
                        
                        if (!seenColors.Contains(hex)) 
                        {
                            seenColors.Add(hex);
                            var cm = new ColorMaterialLibrary.ColorMaterial();
                            cm.colorName = colorName;
                            cm.color = r.color;
                            
                            // Try to find a valid shader
                            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                            if (shader == null) shader = Shader.Find("Standard");
                            
                            if (shader != null) {
                                Material mat = new Material(shader);
                                mat.color = r.color;
                                mat.name = "Mat_" + colorName;
                                AssetDatabase.AddObjectToAsset(mat, newLib);
                                cm.material = mat;
                            }
                            
                            newLib.colorMaterials.Add(cm);
                            colorIdx++;
                        }
                        
                        // Update the region's assigned color name immediately to the generated name
                        // We must find the assigned name corresponding to this color by using the hex representation 
                        string assignedName = newLib.colorMaterials.Find(c => ColorUtility.ToHtmlStringRGB(c.color) == hex).colorName;
                        r.colorName = assignedName;
                    }
                    
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    
                    Undo.RecordObject(tool, "Assign generated Color Library");
                    tool.colorLibrary = newLib;
                    EditorUtility.SetDirty(tool);
                    tool.GeneratePreview();
                    
                    Debug.Log($"Created new ColorMaterialLibrary at {path} with {newLib.colorMaterials.Count} unique colors.");
                }
            }
        }

        
        // Add button to detect border regions separately
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Border Management", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Detect Border Regions"))
            {
                Undo.RecordObject(tool, "Detect Border Regions");
                tool.ForceDetectBorderRegions();
                EditorUtility.SetDirty(tool);
            }
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Shrink Borders (-1px)"))
            {
                Undo.RecordObject(tool, "Shrink Borders");
                tool.ShrinkBorderPixels(1);
                EditorUtility.SetDirty(tool);
            }
            
            if (GUILayout.Button("Expand Borders (+1px)"))
            {
                Undo.RecordObject(tool, "Expand Borders");
                tool.ExpandBorderPixels(1);
                EditorUtility.SetDirty(tool);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Border Colors"))
            {
                tool.SaveBorderColors();
            }
            EditorGUILayout.EndHorizontal();

        if (tool.regions != null && tool.regions.Count > 0)
        {
            GUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Detected Regions: {tool.regions.Count}", EditorStyles.boldLabel);
            tool.showRegionLabels = GUILayout.Toggle(tool.showRegionLabels, "Show Scene Labels");
            EditorGUILayout.EndHorizontal();

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

                // HIGHLIGHT BUTTON
                bool isHighlighted = (tool.highlightedRegionIndex == i);
                if (isHighlighted) GUI.backgroundColor = Color.yellow;
                if (GUILayout.Button(isHighlighted ? "Show All" : "Highlight", GUILayout.Width(70), GUILayout.Height(30)))
                {
                    if (isHighlighted)
                        tool.GenerateHighlightPreview(-1); // Stop highlighting
                    else
                        tool.GenerateHighlightPreview(i);
                }
                GUI.backgroundColor = Color.white;

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

    void OnSceneGUI()
    {
        SpriteRegionEditorTool tool = (SpriteRegionEditorTool)target;
        if (tool == null || tool.regions == null || tool.regions.Count == 0 || !tool.showRegionLabels) return;

        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white;
        style.fontStyle = FontStyle.Bold;
        style.fontSize = 20;
        style.alignment = TextAnchor.MiddleCenter;

        // Draw a dark background for the text
        GUIStyle bgStyle = new GUIStyle(GUI.skin.box);
        bgStyle.normal.background = Texture2D.whiteTexture; // Or custom
        
        for (int i = 0; i < tool.regions.Count; i++)
        {
            if (tool.regions[i].PixelCount == 0) continue;

            Vector3 worldCenter = tool.GetRegionWorldCenter(i);

            Handles.BeginGUI();
            Vector2 screenPos = HandleUtility.WorldToGUIPoint(worldCenter);
            
            // Draw a tiny black drop shadow for readability
            style.normal.textColor = Color.black;
            GUI.Label(new Rect(screenPos.x - 9, screenPos.y - 9, 20, 20), i.ToString(), style);
            
            // Draw white text
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(screenPos.x - 10, screenPos.y - 10, 20, 20), i.ToString(), style);
            Handles.EndGUI();
        }
    }
}