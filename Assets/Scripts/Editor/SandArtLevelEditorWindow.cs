using UnityEditor;
using UnityEngine;

public class SandArtLevelEditorWindow : EditorWindow
{
    private SpriteRegionEditorTool regionTool;
    private SandCubeManager cubeManager;
    private SandColorDetector colorDetector;

    private Vector2 scrollPos;

    [MenuItem("Window/Sand Art Level Editor")]
    public static void ShowWindow()
    {
        GetWindow<SandArtLevelEditorWindow>("Sand Art Level Editor");
    }

    private void OnEnable()
    {
        FindComponents();
    }

    private void FindComponents()
    {
        regionTool = FindObjectOfType<SpriteRegionEditorTool>();
        cubeManager = FindObjectOfType<SandCubeManager>();
        colorDetector = FindObjectOfType<SandColorDetector>();
    }

    private void OnGUI()
    {
        GUILayout.Label("Sand Art Level Configurator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("This wizard completely automates setting up a new level from an image to playable sand cubes.", MessageType.Info);
        
        if (GUILayout.Button("Refresh Scene References", GUILayout.Height(30)))
        {
            FindComponents();
        }

        if (regionTool == null || cubeManager == null)
        {
            EditorGUILayout.HelpBox("Could not find all necessary components in the scene! Ensure you have a SpriteRegionEditorTool and SandCubeManager active.", MessageType.Warning);
            return;
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        DrawStep1();
        EditorGUILayout.Space(10);
        DrawStep2();
        EditorGUILayout.Space(10);
        DrawStep3();
        
        EditorGUILayout.Space(20);
        DrawMagicButton();

        EditorGUILayout.EndScrollView();
    }

    private void DrawStep1()
    {
        GUILayout.Label("Step 1: Setup The Image & Colors", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        
        EditorGUI.BeginChangeCheck();
        regionTool.sourceSprite = (Sprite)EditorGUILayout.ObjectField("Level Image (Sprite)", regionTool.sourceSprite, typeof(Sprite), false);
        regionTool.colorLibrary = (ColorMaterialLibrary)EditorGUILayout.ObjectField("Color Library", regionTool.colorLibrary, typeof(ColorMaterialLibrary), false);

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(regionTool);
        }

        EditorGUILayout.Space();
        GUI.color = new Color(0.6f, 0.8f, 1f);
        if (GUILayout.Button("1. Detect Regions & Assign Colors", GUILayout.Height(35)))
        {
            Undo.RecordObject(regionTool, "Detect Regions");
            regionTool.DetectRegions();
            EditorUtility.SetDirty(regionTool);
        }
        GUI.color = Color.white;
        
        GUILayout.Label($"Currently detected regions: {regionTool.regions.Count}", EditorStyles.miniLabel);
        
        EditorGUILayout.EndVertical();
    }

    private void DrawStep2()
    {
        GUILayout.Label("Step 2: Calculate Required Sand Pieces", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        
        if (colorDetector == null)
        {
            EditorGUILayout.HelpBox("No SandColorDetector found in scene. (Optional)", MessageType.Info);
        }
        else
        {
            GUI.color = new Color(0.7f, 1f, 0.7f);
            if (GUILayout.Button("2. Calculate Pieces Needed Per Region", GUILayout.Height(35)))
            {
                Undo.RecordObject(colorDetector, "Populate Regions");
                colorDetector.colorLibrary = regionTool.colorLibrary;
                colorDetector.AutoPopulateRegions();
                EditorUtility.SetDirty(colorDetector);
            }
            GUI.color = Color.white;

            if (colorDetector.totalPiecesPerColor != null && colorDetector.totalPiecesPerColor.Count > 0)
            {
                int totalColors = 0;
                foreach (var info in colorDetector.totalPiecesPerColor)
                {
                    if (info.totalPiecesNeeded > 0) 
                    {
                        EditorGUILayout.LabelField($"{info.colorName}: {info.totalPiecesNeeded} total pieces needed");
                        totalColors++;
                    }
                }
                EditorGUILayout.HelpBox($"Ready to process {totalColors} unique required colors.", MessageType.Info);
            }
        }
        
        EditorGUILayout.EndVertical();
    }

    private void DrawStep3()
    {
        GUILayout.Label("Step 3: Auto-Setup Cubes", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        GUI.color = new Color(1f, 0.8f, 0.5f);
        if (GUILayout.Button("3. Auto-Assign Cube Colors & Amounts", GUILayout.Height(35)))
        {
            Undo.RecordObject(cubeManager, "Auto Setup Cubes");
            cubeManager.colorLibrary = regionTool.colorLibrary; // ensure sync
            cubeManager.AutoSetupCubes();
            EditorUtility.SetDirty(cubeManager);
        }
        GUI.color = Color.white;

        GUILayout.Label($"Total Active Sand Cubes: {cubeManager.sandCubes.Count}", EditorStyles.miniLabel);

        EditorGUILayout.Space();
        if (GUILayout.Button("Edit Cube Placements In Scene (Select Manager)", GUILayout.Height(30)))
        {
            Selection.activeGameObject = cubeManager.gameObject;
            SceneView.RepaintAll();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawMagicButton()
    {
        GUI.color = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("★ ONE-CLICK MAGIC SETUP ★\n(Does Step 1, 2, and 3 automatically)", GUILayout.Height(50)))
        {
            Undo.RecordObjects(new Object[] { regionTool, colorDetector, cubeManager }, "Magic Setup");
            
            // Step 1
            regionTool.DetectRegions();
            EditorUtility.SetDirty(regionTool);
            
            // Step 2
            if (colorDetector != null)
            {
                colorDetector.colorLibrary = regionTool.colorLibrary;
                colorDetector.AutoPopulateRegions();
                EditorUtility.SetDirty(colorDetector);
            }
            
            // Step 3
            cubeManager.colorLibrary = regionTool.colorLibrary;
            cubeManager.AutoSetupCubes();
            EditorUtility.SetDirty(cubeManager);
            
            Debug.Log("Magic Setup Complete!");
        }
        GUI.color = Color.white;
    }
}
