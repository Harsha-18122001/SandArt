using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SandCubeManager))]
public class SandCubeManagerEditor : Editor
{
    private Vector3 newCubePosition = Vector3.zero;
    private Vector3 newCubeRotation = Vector3.zero;
    private int selectedColorIndex = 0;
    private int selectedPrefabIndex = 0;

    // Grid System Additions
    private bool showGridPlacement = true;
    private int gridCols = 3;
    private int gridRows = 2;
    private float gridSpacingX = 2.0f;
    private float gridSpacingY = 2.0f;
    private Vector2 gridOffset = Vector2.zero;
    private bool gridUseXZ = false;
    
    private void OnSceneGUI()
    {
        SandCubeManager manager = (SandCubeManager)target;
        if (!showGridPlacement || manager == null) return;
        
        Event e = Event.current;
        int controlID = GUIUtility.GetControlID("GridPlacement".GetHashCode(), FocusType.Passive);
        
        Plane plane;
        if (gridUseXZ)
            plane = new Plane(manager.transform.up, manager.transform.position);
        else
            plane = new Plane(-manager.transform.forward, manager.transform.position);

        Vector3 mouseWorldPos = Vector3.zero;
        bool hitPlane = false;
        
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (plane.Raycast(ray, out float enter))
        {
            hitPlane = true;
            mouseWorldPos = ray.GetPoint(enter);
        }

        Color oldColor = Handles.color;
        
        for (int y = 0; y < gridRows; y++)
        {
            for (int x = 0; x < gridCols; x++)
            {
                float posX = (x - (gridCols - 1) * 0.5f) * gridSpacingX + gridOffset.x;
                float posY = (y - (gridRows - 1) * 0.5f) * gridSpacingY + gridOffset.y;
                
                Vector3 localPos = gridUseXZ 
                    ? new Vector3(posX, 0, posY) 
                    : new Vector3(posX, posY, 0);
                    
                Vector3 worldPos = manager.transform.TransformPoint(localPos);
                
                int existingCubeIndex = -1;
                for (int i = 0; i < manager.sandCubes.Count; i++)
                {
                    if (Vector3.Distance(manager.sandCubes[i].position, localPos) < 0.1f)
                    {
                        existingCubeIndex = i;
                        break;
                    }
                }
                
                float size = Mathf.Min(gridSpacingX, gridSpacingY) * 0.45f;
                Vector3 boxSize = gridUseXZ 
                    ? new Vector3(size * 2, 0.1f, size * 2) 
                    : new Vector3(size * 2, size * 2, 0.1f);
                    
                if (existingCubeIndex >= 0)
                {
                    Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
                    Handles.DrawWireCube(worldPos, boxSize);
                }
                else
                {
                    Handles.color = new Color(0.2f, 0.8f, 1f, 0.8f);
                    Handles.DrawWireCube(worldPos, boxSize);
                }
                
                // Interaction logic
                if (hitPlane)
                {
                    float distToMouse = Vector3.Distance(worldPos, mouseWorldPos);
                    if (distToMouse < size * 1.2f) // Hovering this cell
                    {
                        // Hover highlight
                        Handles.color = new Color(1f, 1f, 0f, 0.6f);
                        Handles.DrawWireCube(worldPos, boxSize * 1.1f);
                        
                        // Prevent the selection rect if we are actively dragging
                        if (e.type == EventType.Layout)
                        {
                            HandleUtility.AddControl(controlID, 0f); // intercept
                        }
                        
                        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
                        {
                            if (e.button == 0) // Left click -> Place
                            {
                                if (existingCubeIndex == -1 && manager.colorLibrary != null && manager.sandCubePrefabs.Count > 0)
                                {
                                    string[] colorNames = manager.colorLibrary.GetAllColorNames();
                                    string[] prefabNames = manager.GetPrefabNames();
                                    
                                    if (colorNames.Length > 0 && prefabNames.Length > 0)
                                    {
                                        Undo.RecordObject(manager, "Place Scene Grid Cube");
                                        
                                        int pIndex = Mathf.Clamp(selectedPrefabIndex, 0, prefabNames.Length - 1);
                                        int cIndex = Mathf.Clamp(selectedColorIndex, 0, colorNames.Length - 1);
                                        
                                        manager.sandCubes.Add(new SandCubeManager.SandCubeData
                                        {
                                            prefabName = prefabNames[pIndex],
                                            position = localPos,
                                            rotation = newCubeRotation,
                                            colorName = colorNames[cIndex],
                                            sandPiecesCount = 10
                                        });
                                        manager.CreateOrUpdateCubes();
                                        EditorUtility.SetDirty(manager);
                                    }
                                }
                                GUIUtility.hotControl = controlID;
                                e.Use();
                            }
                            else if (e.button == 1) // Right click -> Remove
                            {
                                if (existingCubeIndex >= 0)
                                {
                                    Undo.RecordObject(manager, "Remove Scene Grid Cube");
                                    
                                    if (manager.sandCubes[existingCubeIndex].cubeObject != null)
                                    {
                                        DestroyImmediate(manager.sandCubes[existingCubeIndex].cubeObject);
                                    }
                                    manager.sandCubes.RemoveAt(existingCubeIndex);
                                    manager.CreateOrUpdateCubes(); // Update live scene if needed
                                    EditorUtility.SetDirty(manager);
                                }
                                GUIUtility.hotControl = controlID;
                                e.Use();
                            }
                        }
                    }
                }
            }
        }
        
        if (e.type == EventType.MouseUp)
        {
            if (GUIUtility.hotControl == controlID)
            {
                GUIUtility.hotControl = 0;
                e.Use();
            }
        }
        
        Handles.color = oldColor;
        SceneView.RepaintAll(); // ensure hover responsiveness
    }
    
    public override void OnInspectorGUI()
    {
        SandCubeManager manager = (SandCubeManager)target;
        
        EditorGUI.BeginChangeCheck();
        
        // References Section
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
        
        manager.colorLibrary = (ColorMaterialLibrary)EditorGUILayout.ObjectField(
            "Color Library", 
            manager.colorLibrary, 
            typeof(ColorMaterialLibrary), 
            false
        );
        
        if (manager.colorLibrary == null)
        {
            EditorGUILayout.HelpBox(
                "Please assign a Color Material Library!\n\n" +
                "Create one: Right-click in Project > Create > Sand System > Color Material Library",
                MessageType.Warning
            );
        }
        
        // Sand Cube Prefabs Section
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Sand Cube Prefabs", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Add your sand cube prefabs here (e.g., 'Sand Cube Small', 'Sand Cube Large')", MessageType.Info);
        
        for (int i = 0; i < manager.sandCubePrefabs.Count; i++)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            
            manager.sandCubePrefabs[i].prefabName = EditorGUILayout.TextField("Name", manager.sandCubePrefabs[i].prefabName);
            
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                manager.sandCubePrefabs.RemoveAt(i);
                EditorUtility.SetDirty(manager);
                return;
            }
            
            EditorGUILayout.EndHorizontal();
            
            manager.sandCubePrefabs[i].prefab = (GameObject)EditorGUILayout.ObjectField(
                "Prefab", 
                manager.sandCubePrefabs[i].prefab, 
                typeof(GameObject), 
                false
            );
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
        
        if (GUILayout.Button("+ Add Prefab Type", GUILayout.Height(25)))
        {
            manager.sandCubePrefabs.Add(new SandCubeManager.SandCubePrefab
            {
                prefabName = "Sand Cube"
            });
            EditorUtility.SetDirty(manager);
        }
        
        if (manager.sandCubePrefabs.Count == 0)
        {
            EditorGUILayout.HelpBox("Please add at least one sand cube prefab!", MessageType.Warning);
        }
        
        // Movement Settings
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Movement Settings", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("These settings apply to all sand cubes when they are created/updated.", MessageType.Info);
        
        manager.defaultMoveSpeed = EditorGUILayout.FloatField("Move Speed", manager.defaultMoveSpeed);
        manager.defaultMoveDistance = EditorGUILayout.FloatField("Move Distance", manager.defaultMoveDistance);
        
        // Only show cube management if references are assigned
        if (manager.colorLibrary != null && manager.sandCubePrefabs.Count > 0)
        {
            string[] colorNames = manager.colorLibrary.GetAllColorNames();
            string[] prefabNames = manager.GetPrefabNames();
            
            if (colorNames.Length == 0)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox(
                    "No colors defined in the Color Library!\n" +
                    "Open the Color Library asset and add some colors first.",
                    MessageType.Warning
                );
            }
            else
            {
                // Add New Cube Section
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Add New Sand Cube", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginVertical("box");
                
                selectedPrefabIndex = EditorGUILayout.Popup("Prefab Type", selectedPrefabIndex, prefabNames);
                newCubePosition = EditorGUILayout.Vector3Field("Position", newCubePosition);
                newCubeRotation = EditorGUILayout.Vector3Field("Rotation", newCubeRotation);
                selectedColorIndex = EditorGUILayout.Popup("Color", selectedColorIndex, colorNames);
                
                int newSandPiecesCount = EditorGUILayout.IntField("Sand Pieces Count", 10);
                
                if (GUILayout.Button("Add Cube", GUILayout.Height(30)))
                {
                    manager.sandCubes.Add(new SandCubeManager.SandCubeData
                    {
                        prefabName = prefabNames[selectedPrefabIndex],
                        position = newCubePosition,
                        rotation = newCubeRotation,
                        colorName = colorNames[selectedColorIndex],
                        sandPiecesCount = newSandPiecesCount
                    });
                    
                    manager.CreateOrUpdateCubes();
                    EditorUtility.SetDirty(manager);
                }
                
                EditorGUILayout.EndVertical();
                
                // --- QUICK GRID PLACEMENT ---
                EditorGUILayout.Space(10);
                showGridPlacement = EditorGUILayout.Foldout(showGridPlacement, "Quick Visual Grid Placement", true, EditorStyles.foldoutHeader);
                if (showGridPlacement)
                {
                    EditorGUILayout.BeginVertical("box");
                    
                    EditorGUILayout.BeginHorizontal();
                    gridCols = EditorGUILayout.IntSlider("Columns (X)", gridCols, 1, 10);
                    gridRows = EditorGUILayout.IntSlider("Rows (Y)", gridRows, 1, 10);
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.BeginHorizontal();
                    gridSpacingX = EditorGUILayout.FloatField("Spacing X", gridSpacingX);
                    gridSpacingY = EditorGUILayout.FloatField("Spacing Y", gridSpacingY);
                    EditorGUILayout.EndHorizontal();
                    
                    gridOffset = EditorGUILayout.Vector2Field("Center Offset", gridOffset);
                    gridUseXZ = EditorGUILayout.Toggle("Use X/Z (Top-Down)", gridUseXZ);
                    
                    EditorGUILayout.Space(10);
                    GUILayout.Label("In the Scene View:", EditorStyles.boldLabel);
                    GUILayout.Label("• Left-Click on a grid cell to PLACE a cube", EditorStyles.label);
                    GUILayout.Label("• Right-Click on a grid cell to REMOVE a cube", EditorStyles.label);
                    GUILayout.Label("• You can click and drag to paint multiple cubes", EditorStyles.label);
                    
                    // Draw visual grid only in inspector to show where they are roughly
                    EditorGUILayout.Space(10);
                    for (int y = gridRows - 1; y >= 0; y--)
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        for (int x = 0; x < gridCols; x++)
                        {
                            float posX = (x - (gridCols - 1) * 0.5f) * gridSpacingX + gridOffset.x;
                            float posY = (y - (gridRows - 1) * 0.5f) * gridSpacingY + gridOffset.y;
                            
                            Vector3 targetPos = gridUseXZ 
                                ? new Vector3(posX, 0, posY) 
                                : new Vector3(posX, posY, 0);
                            
                            // Check if cube exists roughly here
                            bool cubeExists = false;
                            foreach (var c in manager.sandCubes)
                            {
                                if (Vector3.Distance(c.position, targetPos) < 0.1f)
                                {
                                    cubeExists = true;
                                    break;
                                }
                            }
                            
                            GUI.backgroundColor = cubeExists ? Color.gray : new Color(0.8f, 0.9f, 1f);
                            if (GUILayout.Button(cubeExists ? "■" : "+", GUILayout.Width(45), GUILayout.Height(45)))
                            {
                                if (!cubeExists)
                                {
                                    Undo.RecordObject(manager, "Place Grid Cube");
                                    manager.sandCubes.Add(new SandCubeManager.SandCubeData
                                    {
                                        prefabName = prefabNames[selectedPrefabIndex],
                                        position = targetPos,
                                        rotation = newCubeRotation,
                                        colorName = colorNames[selectedColorIndex],
                                        sandPiecesCount = 10
                                    });
                                    manager.CreateOrUpdateCubes();
                                    EditorUtility.SetDirty(manager);
                                }
                            }
                            GUI.backgroundColor = Color.white;
                        }
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();
                    }
                    
                    EditorGUILayout.EndVertical();
                }
                
                // Existing Cubes Section
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Existing Sand Cubes", EditorStyles.boldLabel);
                
                if (manager.sandCubes.Count == 0)
                {
                    EditorGUILayout.HelpBox("No sand cubes added yet.", MessageType.Info);
                }
                else
                {
                    for (int i = 0; i < manager.sandCubes.Count; i++)
                    {
                        EditorGUILayout.BeginVertical("box");
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"Cube {i + 1} ({manager.sandCubes[i].prefabName})", EditorStyles.boldLabel);
                        
                        if (GUILayout.Button("Remove", GUILayout.Width(70)))
                        {
                            if (manager.sandCubes[i].cubeObject != null)
                            {
                                DestroyImmediate(manager.sandCubes[i].cubeObject);
                            }
                            manager.sandCubes.RemoveAt(i);
                            EditorUtility.SetDirty(manager);
                            return;
                        }
                        
                        EditorGUILayout.EndHorizontal();
                        
                        // Prefab type dropdown
                        int currentPrefabIndex = System.Array.FindIndex(prefabNames, name => name == manager.sandCubes[i].prefabName);
                        if (currentPrefabIndex < 0) currentPrefabIndex = 0;
                        
                        int newPrefabIndex = EditorGUILayout.Popup("Prefab Type", currentPrefabIndex, prefabNames);
                        manager.sandCubes[i].prefabName = prefabNames[newPrefabIndex];
                        
                        manager.sandCubes[i].position = EditorGUILayout.Vector3Field("Position", manager.sandCubes[i].position);
                        manager.sandCubes[i].rotation = EditorGUILayout.Vector3Field("Rotation", manager.sandCubes[i].rotation);
                        
                        // Sand pieces count
                        manager.sandCubes[i].sandPiecesCount = EditorGUILayout.IntField("Sand Pieces Count", manager.sandCubes[i].sandPiecesCount);
                        
                        // Color dropdown
                        int currentColorIndex = System.Array.FindIndex(colorNames, name => name == manager.sandCubes[i].colorName);
                        if (currentColorIndex < 0) currentColorIndex = 0;
                        
                        int newColorIndex = EditorGUILayout.Popup("Color", currentColorIndex, colorNames);
                        manager.sandCubes[i].colorName = colorNames[newColorIndex];
                        
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(5);
                    }
                }
                
                // Action Buttons
                EditorGUILayout.Space(10);
                
                if (GUILayout.Button("Update All Cubes", GUILayout.Height(35)))
                {
                    manager.CreateOrUpdateCubes();
                    EditorUtility.SetDirty(manager);
                }
                
                GUILayout.Space(5);
                GUI.color = new Color(0.7f, 1f, 0.7f); // light green
                if (GUILayout.Button("Auto-Assign Colors & Pieces", GUILayout.Height(35)))
                {
                    Undo.RecordObject(manager, "Auto-Setup Cubes");
                    manager.AutoSetupCubes();
                    EditorUtility.SetDirty(manager);
                }
                GUI.color = Color.white;
                
                if (manager.sandCubes.Count > 0)
                {
                    if (GUILayout.Button("Clear All Cubes", GUILayout.Height(30)))
                    {
                        if (EditorUtility.DisplayDialog("Clear All Cubes", 
                            "Are you sure you want to remove all sand cubes?", "Yes", "Cancel"))
                        {
                            manager.ClearAllCubes();
                            EditorUtility.SetDirty(manager);
                        }
                    }
                }
            }
        }
        
        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(manager);
            SceneView.RepaintAll(); // Make sure Scene wireframe instantly updates inside Unity
        }
    }
}
