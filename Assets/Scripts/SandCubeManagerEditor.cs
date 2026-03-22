using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SandCubeManager))]
public class SandCubeManagerEditor : Editor
{
    private Vector3 newCubePosition = Vector3.zero;
    private Vector3 newCubeRotation = Vector3.zero;
    private int selectedColorIndex = 0;
    private int selectedPrefabIndex = 0;
    
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
        }
    }
}
