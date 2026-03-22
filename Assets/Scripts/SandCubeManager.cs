using UnityEngine;
using System.Collections.Generic;

public class SandCubeManager : MonoBehaviour
{
    [System.Serializable]
    public class SandCubePrefab
    {
        public string prefabName;
        public GameObject prefab;
    }
    
    [System.Serializable]
    public class SandCubeData
    {
        public Vector3 position;
        public Vector3 rotation;
        public string prefabName;
        public string colorName;
        public int sandPiecesCount = 10; // Number of sand pieces this cube will spawn
        [HideInInspector]
        public GameObject cubeObject;
    }
    
    [Header("References")]
    public ColorMaterialLibrary colorLibrary;
    
    [Header("Sand Cube Prefabs")]
    public List<SandCubePrefab> sandCubePrefabs = new List<SandCubePrefab>();
    
    [Header("Sand Cubes")]
    public List<SandCubeData> sandCubes = new List<SandCubeData>();
    
    [Header("Movement Settings")]
    public float defaultMoveSpeed = 5f;
    public float defaultMoveDistance = 10f;
    
    public void CreateOrUpdateCubes()
    {
        if (sandCubePrefabs.Count == 0)
        {
            Debug.LogWarning("No Sand Cube Prefabs assigned!");
            return;
        }
        
        if (colorLibrary == null)
        {
            Debug.LogWarning("Color Material Library is not assigned!");
            return;
        }
        
        // Clean up existing cubes
        foreach (var cubeData in sandCubes)
        {
            if (cubeData.cubeObject != null)
            {
                #if UNITY_EDITOR
                DestroyImmediate(cubeData.cubeObject);
                #else
                Destroy(cubeData.cubeObject);
                #endif
            }
        }
        
        // Create new cubes
        foreach (var cubeData in sandCubes)
        {
            CreateCube(cubeData);
        }
    }
    
    void CreateCube(SandCubeData cubeData)
    {
        // Find the prefab by name
        SandCubePrefab prefabData = sandCubePrefabs.Find(p => p.prefabName == cubeData.prefabName);
        if (prefabData == null || prefabData.prefab == null)
        {
            Debug.LogWarning($"Prefab '{cubeData.prefabName}' not found!");
            return;
        }
        
        // Instantiate prefab
        GameObject cube = Instantiate(prefabData.prefab, transform);
        cube.name = $"{cubeData.prefabName}_{cubeData.colorName}";
        cube.transform.localPosition = cubeData.position;
        cube.transform.localRotation = Quaternion.Euler(cubeData.rotation);
        // Scale is preserved from prefab
        
        // Add movement component if not present
        SandCubeMovement movement = cube.GetComponent<SandCubeMovement>();
        if (movement == null)
        {
            movement = cube.AddComponent<SandCubeMovement>();
        }
        movement.MoveSpeed = defaultMoveSpeed;
        movement.MoveDistance = defaultMoveDistance;
        
        // Store sand pieces count in the cube
        SandCubeGrinder grinder = cube.GetComponent<SandCubeGrinder>();
        if (grinder == null)
        {
            grinder = cube.AddComponent<SandCubeGrinder>();
        }
        grinder.totalSandPieces = cubeData.sandPiecesCount;
        
        // Add and set color identifier
        SandColorIdentifier colorIdentifier = cube.GetComponent<SandColorIdentifier>();
        if (colorIdentifier == null)
        {
            colorIdentifier = cube.AddComponent<SandColorIdentifier>();
        }
        colorIdentifier.colorLibrary = colorLibrary;
        colorIdentifier.SetColorName(cubeData.colorName);
        
        // Ensure there's a collider for click detection
        Collider collider = cube.GetComponent<Collider>();
        if (collider == null)
        {
            // Add a box collider if no collider exists
            BoxCollider boxCollider = cube.AddComponent<BoxCollider>();
            Debug.Log($"Added BoxCollider to {cube.name} for click detection");
        }
        
        // Add tag for conveyor detection
        if (cube.tag == "Untagged")
        {
            cube.tag = "SandCube";
        }
        
        // Apply material from color library (excluding sprites)
        Material material = colorLibrary.GetMaterialByName(cubeData.colorName);
        if (material != null)
        {
            // Apply material to all renderers except SpriteRenderers
            Renderer[] renderers = cube.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                // Skip SpriteRenderers to preserve arrow sprites
                if (renderer is SpriteRenderer)
                    continue;
                    
                renderer.material = material;
            }
        }
        else
        {
            // Fallback: create material with color
            Color color = colorLibrary.GetColorByName(cubeData.colorName);
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            
            Renderer[] renderers = cube.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                // Skip SpriteRenderers to preserve arrow sprites
                if (renderer is SpriteRenderer)
                    continue;
                    
                renderer.material = mat;
            }
        }
        
        cubeData.cubeObject = cube;
    }
    
    [ContextMenu("Auto-Setup Cubes (Colors & Pieces)")]
    public void AutoSetupCubes()
    {
        if (colorLibrary == null)
        {
            Debug.LogWarning("Color Material Library is not assigned!");
            return;
        }

        SandColorDetector detector = FindObjectOfType<SandColorDetector>();
        List<string> requiredColors = new List<string>();
        Dictionary<string, int> pieceTotals = new Dictionary<string, int>();

        if (detector != null && detector.totalPiecesPerColor != null && detector.totalPiecesPerColor.Count > 0)
        {
            foreach (var item in detector.totalPiecesPerColor)
            {
                if (item.totalPiecesNeeded > 0)
                {
                    requiredColors.Add(item.colorName);
                    pieceTotals[item.colorName] = item.totalPiecesNeeded;
                }
            }
        }
        else
        {
            string[] names = colorLibrary.GetAllColorNames();
            foreach (var n in names)
            {
                requiredColors.Add(n);
                pieceTotals[n] = 10;
            }
        }

        if (requiredColors.Count == 0)
        {
            Debug.LogWarning("No colors found to assign!");
            return;
        }

        // Gather manual child transforms created directly inside the scene
        List<GameObject> existingCubes = new List<GameObject>();
        foreach (var c in sandCubes) if (c.cubeObject != null) existingCubes.Add(c.cubeObject);
        
        List<Transform> manualChildren = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (!existingCubes.Contains(child.gameObject))
            {
                manualChildren.Add(child);
            }
        }

        if (manualChildren.Count > 0)
        {
            string defaultPrefab = sandCubePrefabs.Count > 0 ? sandCubePrefabs[0].prefabName : "";
            foreach (var child in manualChildren)
            {
                sandCubes.Add(new SandCubeData
                {
                    position = child.localPosition,
                    rotation = child.localEulerAngles,
                    prefabName = defaultPrefab,
                    colorName = requiredColors[0],
                    sandPiecesCount = 10
                });
                
                #if UNITY_EDITOR
                DestroyImmediate(child.gameObject);
                #else
                Destroy(child.gameObject);
                #endif
            }
        }

        if (sandCubes.Count == 0) return;

        List<string> assignedColors = new List<string>();
        for (int i = 0; i < sandCubes.Count; i++)
        {
            assignedColors.Add(requiredColors[i % requiredColors.Count]);
        }
        
        for (int i = 0; i < assignedColors.Count; i++)
        {
            int rnd = Random.Range(i, assignedColors.Count);
            string tmp = assignedColors[i];
            assignedColors[i] = assignedColors[rnd];
            assignedColors[rnd] = tmp;
        }
        
        Dictionary<string, int> colorCubeCount = new Dictionary<string, int>();
        foreach (var c in assignedColors)
        {
            if (!colorCubeCount.ContainsKey(c)) colorCubeCount[c] = 0;
            colorCubeCount[c]++;
        }

        for (int i = 0; i < sandCubes.Count; i++)
        {
            string col = assignedColors[i];
            sandCubes[i].colorName = col;
            
            int totalPiecesForColor = pieceTotals.ContainsKey(col) ? pieceTotals[col] : 10;
            int numCubesSharingColor = colorCubeCount[col];
            
            sandCubes[i].sandPiecesCount = Mathf.CeilToInt((float)totalPiecesForColor / numCubesSharingColor);
        }

        CreateOrUpdateCubes();
        Debug.Log("Successfully auto-assigned colors and pieces, and updated cubes!");
    }

    public void ClearAllCubes()
    {
        foreach (var cubeData in sandCubes)
        {
            if (cubeData.cubeObject != null)
            {
                #if UNITY_EDITOR
                DestroyImmediate(cubeData.cubeObject);
                #else
                Destroy(cubeData.cubeObject);
                #endif
            }
        }
        
        sandCubes.Clear();
    }
    
    public string[] GetPrefabNames()
    {
        string[] names = new string[sandCubePrefabs.Count];
        for (int i = 0; i < sandCubePrefabs.Count; i++)
        {
            names[i] = sandCubePrefabs[i].prefabName;
        }
        return names;
    }
}
