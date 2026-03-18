using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class SpriteRegionEditorTool : MonoBehaviour
{
    public Sprite sourceSprite;
    
    [Header("Color Library")]
    public ColorMaterialLibrary colorLibrary;
    
    [Header("Border Expansion")]
    public bool detectBorderRegions = true;
    [SerializeField] [Range(0f, 1f)] private float borderShrinkAmount = 0f; // 0 = no shrink, 1 = remove all border
    
    [System.Serializable]
    public class BorderRegion
    {
        [SerializeField]
        public string regionName = "Border";
        
        [SerializeField]
        public List<Vector2Int> pixels = new List<Vector2Int>();
        
        [SerializeField]
        public Color color = Color.black; // Border color (default black)
        
        [SerializeField] [Range(0f, 1f)]
        public float shrinkAmount = 0f; // 0 = no shrink, 1 = remove all border
        
        public int PixelCount => pixels.Count;
        
        // Constructor to ensure color is properly set
        public BorderRegion()
        {
            color = Color.black;
        }
        
        public BorderRegion(Color initialColor)
        {
            color = initialColor;
        }
    }
    
    [SerializeField]
    public List<BorderRegion> borderRegions = new List<BorderRegion>();

    [System.Serializable]
    public class Region
    {
        [SerializeField]
        public string regionName = "Region";
        
        [SerializeField]
        public List<Vector2Int> pixels = new List<Vector2Int>();
        
        [SerializeField]
        public Color color = Color.white;
        
        [SerializeField]
        public string colorName = ""; // Reference to ColorMaterialLibrary
        
        public int PixelCount => pixels.Count;
    }

    [SerializeField]
    public List<Region> regions = new List<Region>();

    private Texture2D sourceTexture;
    private Texture2D previewTexture;
    private Color[] sourcePixels;

    private bool[,] visited;
    private int width;
    private int height;

    private float whiteThreshold = 0.9f;

    public void DetectRegions()
{
    if (sourceSprite == null)
    {
        Debug.LogWarning("No source sprite assigned!");
        return;
    }
    
    regions.Clear();

    sourceTexture = sourceSprite.texture;
    width = sourceTexture.width;
    height = sourceTexture.height;

    sourcePixels = sourceTexture.GetPixels();
    visited = new bool[width, height];

    for (int x = 0; x < width; x++)
    {
        for (int y = 0; y < height; y++)
        {
            if (!visited[x, y] && IsWhite(GetPixel(x, y)))
            {
                Region region = new Region();
                FloodFill(x, y, region);
                
                // Assign a starting color
                region.color = Random.ColorHSV();
                region.regionName = $"Region {regions.Count}";

                regions.Add(region);
            }
        }
    }

    // Apply fair color distribution if library is available
    if (colorLibrary != null && colorLibrary.colorMaterials.Count > 0)
    {
        List<string> fairColors = GetFairColorDistribution(regions.Count);
        
        for (int i = 0; i < regions.Count && i < fairColors.Count; i++)
        {
            regions[i].colorName = fairColors[i];
            regions[i].color = colorLibrary.GetColorByName(fairColors[i]);
        }
    }

    Debug.Log($"Detected {regions.Count} regions.");
    
    // Only detect border regions if none exist yet, or if explicitly requested
    if (detectBorderRegions && borderRegions.Count == 0)
    {
        DetectBorderRegions();
    }
    
    GeneratePreview();
}

// Helper to find the best string match from the library
private string GetClosestColorName(Color target)
{
    if (colorLibrary == null || colorLibrary.colorMaterials.Count == 0) return "";

    string bestMatch = colorLibrary.colorMaterials[0].colorName;
    float minDistance = float.MaxValue;

    foreach (var entry in colorLibrary.colorMaterials)
    {
        // Calculate RGB distance
        float dist = Mathf.Sqrt(
            Mathf.Pow(target.r - entry.color.r, 2) +
            Mathf.Pow(target.g - entry.color.g, 2) +
            Mathf.Pow(target.b - entry.color.b, 2)
        );

        if (dist < minDistance)
        {
            minDistance = dist;
            bestMatch = entry.colorName;
        }
    }
    return bestMatch;
}

// Fair color distribution helper
private List<string> GetFairColorDistribution(int regionCount)
{
    if (colorLibrary == null || colorLibrary.colorMaterials.Count == 0) 
        return new List<string>();
    
    string[] availableColors = colorLibrary.GetAllColorNames();
    List<string> distributedColors = new List<string>();
    
    // Calculate how many times each color should appear
    int colorsPerType = regionCount / availableColors.Length;
    int remainder = regionCount % availableColors.Length;
    
    // Add colors evenly
    for (int i = 0; i < availableColors.Length; i++)
    {
        int timesToAdd = colorsPerType;
        if (i < remainder) timesToAdd++; // Distribute remainder
        
        for (int j = 0; j < timesToAdd; j++)
        {
            distributedColors.Add(availableColors[i]);
        }
    }
    
    // Shuffle the list for random distribution
    for (int i = 0; i < distributedColors.Count; i++)
    {
        string temp = distributedColors[i];
        int randomIndex = Random.Range(i, distributedColors.Count);
        distributedColors[i] = distributedColors[randomIndex];
        distributedColors[randomIndex] = temp;
    }
    
    return distributedColors;
}

    void FloodFill(int startX, int startY, Region region)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(startX, startY));
        visited[startX, startY] = true;

        while (queue.Count > 0)
        {
            Vector2Int p = queue.Dequeue();
            region.pixels.Add(p);

            foreach (var n in GetNeighbors(p))
            {
                if (IsInside(n) &&
                    !visited[n.x, n.y] &&
                    IsWhite(GetPixel(n.x, n.y)))
                {
                    visited[n.x, n.y] = true;
                    queue.Enqueue(n);
                }
            }
        }
    }

    public void GeneratePreview()
    {
        if (sourceSprite == null) return;
        
        // Ensure texture data is initialized
        if (sourceTexture == null || sourcePixels == null || width == 0 || height == 0)
        {
            sourceTexture = sourceSprite.texture;
            if (sourceTexture == null) return;
            
            width = sourceTexture.width;
            height = sourceTexture.height;
            sourcePixels = sourceTexture.GetPixels();
        }

        if (previewTexture == null || previewTexture.width != width || previewTexture.height != height)
        {
            previewTexture = new Texture2D(width, height);
            previewTexture.hideFlags = HideFlags.HideAndDontSave;
        }
        Color[] previewPixels = new Color[width * height];

        // Start with black
        for (int i = 0; i < previewPixels.Length; i++)
            previewPixels[i] = Color.black;

        // Draw color regions
        foreach (var region in regions)
        {
            foreach (var p in region.pixels)
            {
                previewPixels[p.y * width + p.x] = region.color;
            }
        }
        
        // Draw border regions with shrinking applied
        foreach (var borderRegion in borderRegions)
        {
            float effectiveShrink = Mathf.Max(borderShrinkAmount, borderRegion.shrinkAmount);
            
            Debug.Log($"GeneratePreview() - Border {borderRegion.regionName}: Color = {borderRegion.color}, Shrink = {effectiveShrink}");
            
            if (effectiveShrink > 0f)
            {
                ShrinkAndFillBorder(borderRegion, previewPixels, effectiveShrink);
            }
            else
            {
                // Draw border region with its color (no shrinking)
                foreach (var p in borderRegion.pixels)
                {
                    int pixelIndex = p.y * width + p.x;
                    if (pixelIndex >= 0 && pixelIndex < previewPixels.Length)
                    {
                        previewPixels[pixelIndex] = borderRegion.color;
                    }
                }
            }
        }

        previewTexture.SetPixels(previewPixels);
        previewTexture.Apply();

        var renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = gameObject.AddComponent<SpriteRenderer>();

        // Reuse existing sprite if possible to avoid memory leak in editor
        if (renderer.sprite != null && renderer.sprite.texture == previewTexture)
        {
            // Sprite already points to this texture, just need to update
        }
        else
        {
            renderer.sprite = Sprite.Create(previewTexture,
                                            new Rect(0, 0, width, height),
                                            new Vector2(0.5f, 0.5f),
                                            sourceSprite.pixelsPerUnit);
        }
    }
    
    public void RefreshColorsFromLibrary()
    {
        if (colorLibrary == null) return;
        
        bool changed = false;
        foreach (var region in regions)
        {
            if (!string.IsNullOrEmpty(region.colorName))
            {
                Color libraryColor = colorLibrary.GetColorByName(region.colorName);
                if (region.color != libraryColor)
                {
                    region.color = libraryColor;
                    changed = true;
                }
            }
        }
        
        if (changed)
        {
            GeneratePreview();
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
    }
    
    void ShrinkAndFillBorder(BorderRegion borderRegion, Color[] previewPixels, float shrinkAmount)
    {
        // Create a map to track which region owns each pixel
        int[,] regionMap = new int[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                regionMap[x, y] = -1;
            }
        }
        
        // Mark existing region pixels
        for (int i = 0; i < regions.Count; i++)
        {
            foreach (var pixel in regions[i].pixels)
            {
                regionMap[pixel.x, pixel.y] = i;
            }
        }
        
        // Calculate distance from each border pixel to nearest color region
        Dictionary<Vector2Int, float> borderDistances = new Dictionary<Vector2Int, float>();
        float maxDistance = 0f;
        
        foreach (var borderPixel in borderRegion.pixels)
        {
            float minDist = float.MaxValue;
            
            // Find distance to nearest color region pixel
            foreach (var neighbor in GetNeighbors(borderPixel))
            {
                if (IsInside(neighbor) && regionMap[neighbor.x, neighbor.y] >= 0)
                {
                    minDist = 0f; // Adjacent to region
                    break;
                }
            }
            
            // If not adjacent, do a simple distance check
            if (minDist == float.MaxValue)
            {
                // Use BFS to find distance to nearest region
                minDist = GetDistanceToNearestRegion(borderPixel, regionMap);
            }
            
            borderDistances[borderPixel] = minDist;
            if (minDist > maxDistance) maxDistance = minDist;
        }
        
        // Determine threshold distance based on shrink amount
        float threshold = maxDistance * (1f - shrinkAmount);
        
        // Fill or keep black based on distance
        foreach (var borderPixel in borderRegion.pixels)
        {
            float dist = borderDistances[borderPixel];
            
            if (dist <= threshold)
            {
                // Fill this pixel with adjacent region color
                Dictionary<int, int> adjacencyCount = new Dictionary<int, int>();
                
                foreach (var neighbor in GetNeighbors(borderPixel))
                {
                    if (IsInside(neighbor) && regionMap[neighbor.x, neighbor.y] >= 0)
                    {
                        int regionId = regionMap[neighbor.x, neighbor.y];
                        if (!adjacencyCount.ContainsKey(regionId))
                        {
                            adjacencyCount[regionId] = 0;
                        }
                        adjacencyCount[regionId]++;
                    }
                }
                
                // Assign to region with most adjacent pixels
                if (adjacencyCount.Count > 0)
                {
                    int bestRegion = -1;
                    int maxCount = 0;
                    foreach (var kvp in adjacencyCount)
                    {
                        if (kvp.Value > maxCount)
                        {
                            maxCount = kvp.Value;
                            bestRegion = kvp.Key;
                        }
                    }
                    
                    if (bestRegion >= 0)
                    {
                        int pixelIndex = borderPixel.y * width + borderPixel.x;
                        if (pixelIndex >= 0 && pixelIndex < previewPixels.Length)
                        {
                            previewPixels[pixelIndex] = regions[bestRegion].color;
                            regionMap[borderPixel.x, borderPixel.y] = bestRegion;
                        }
                    }
                }
            }
            else
            {
                // Keep with border color
                int pixelIndex = borderPixel.y * width + borderPixel.x;
                if (pixelIndex >= 0 && pixelIndex < previewPixels.Length)
                {
                    previewPixels[pixelIndex] = borderRegion.color;
                }
            }
        }
    }
    
    float GetDistanceToNearestRegion(Vector2Int pixel, int[,] regionMap)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        Dictionary<Vector2Int, int> distances = new Dictionary<Vector2Int, int>();
        
        queue.Enqueue(pixel);
        visited.Add(pixel);
        distances[pixel] = 0;
        
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int currentDist = distances[current];
            
            // Check if we reached a region
            if (regionMap[current.x, current.y] >= 0)
            {
                return currentDist;
            }
            
            // Explore neighbors
            foreach (var neighbor in GetNeighbors(current))
            {
                if (IsInside(neighbor) && !visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    distances[neighbor] = currentDist + 1;
                    queue.Enqueue(neighbor);
                    
                    // Early exit if we found a region
                    if (regionMap[neighbor.x, neighbor.y] >= 0)
                    {
                        return currentDist + 1;
                    }
                }
            }
            
            // Limit search depth to avoid performance issues
            if (currentDist > 50) break;
        }
        
        return 50f; // Max distance if not found
    }


    void OnValidate()
    {
        // Only regenerate preview, don't re-detect regions/borders
        GeneratePreview();
    }
    
    void Start()
    {
        // Don't generate preview at runtime if SandPouringFillEffect is handling it
        if (GetComponent<SandPouringFillEffect>() != null)
        {
            return;
        }
        
        // Initialize at runtime to maintain colors when game starts
        if (sourceSprite != null)
        {
            sourceTexture = sourceSprite.texture;
            width = sourceTexture.width;
            height = sourceTexture.height;
            sourcePixels = sourceTexture.GetPixels();
            
            // Debug: Log border region colors at start
            Debug.Log($"Start() - Found {borderRegions.Count} border regions:");
            for (int i = 0; i < borderRegions.Count; i++)
            {
                Debug.Log($"Border {i}: {borderRegions[i].regionName} - Color: {borderRegions[i].color}");
            }
            
            // Only generate preview, don't re-detect regions/borders
            GeneratePreview();
        }
    }

    bool IsWhite(Color c)
    {
        return c.r > whiteThreshold &&
               c.g > whiteThreshold &&
               c.b > whiteThreshold;
    }

    Color GetPixel(int x, int y)
    {
        return sourcePixels[y * width + x];
    }

    bool IsInside(Vector2Int p)
    {
        return p.x >= 0 && p.x < width &&
               p.y >= 0 && p.y < height;
    }

    List<Vector2Int> GetNeighbors(Vector2Int p)
    {
        return new List<Vector2Int>
        {
            new Vector2Int(p.x + 1, p.y),
            new Vector2Int(p.x - 1, p.y),
            new Vector2Int(p.x, p.y + 1),
            new Vector2Int(p.x, p.y - 1)
        };
    }
    
    void ExpandRegionsIntoBorders()
    {
        // Create a map to track which region owns each pixel
        int[,] regionMap = new int[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                regionMap[x, y] = -1; // -1 means unassigned (black border)
            }
        }
        
        // Mark existing region pixels
        for (int i = 0; i < regions.Count; i++)
        {
            foreach (var pixel in regions[i].pixels)
            {
                regionMap[pixel.x, pixel.y] = i;
            }
        }
        
        // Find all black border pixels
        List<Vector2Int> borderPixels = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (regionMap[x, y] == -1 && !IsWhite(GetPixel(x, y)))
                {
                    // Check if this black pixel is adjacent to any region
                    bool isAdjacentToRegion = false;
                    foreach (var neighbor in GetNeighbors(new Vector2Int(x, y)))
                    {
                        if (IsInside(neighbor) && regionMap[neighbor.x, neighbor.y] >= 0)
                        {
                            isAdjacentToRegion = true;
                            break;
                        }
                    }
                    
                    if (isAdjacentToRegion)
                    {
                        borderPixels.Add(new Vector2Int(x, y));
                    }
                }
            }
        }
        
        Debug.Log($"Found {borderPixels.Count} border pixels to distribute");
        
        // Assign each border pixel to the nearest region(s)
        foreach (var borderPixel in borderPixels)
        {
            // Find all adjacent regions
            HashSet<int> adjacentRegions = new HashSet<int>();
            foreach (var neighbor in GetNeighbors(borderPixel))
            {
                if (IsInside(neighbor) && regionMap[neighbor.x, neighbor.y] >= 0)
                {
                    adjacentRegions.Add(regionMap[neighbor.x, neighbor.y]);
                }
            }
            
            if (adjacentRegions.Count > 0)
            {
                // If multiple regions are adjacent, assign to the one with most adjacent pixels
                Dictionary<int, int> adjacencyCount = new Dictionary<int, int>();
                foreach (var regionId in adjacentRegions)
                {
                    adjacencyCount[regionId] = 0;
                }
                
                foreach (var neighbor in GetNeighbors(borderPixel))
                {
                    if (IsInside(neighbor) && regionMap[neighbor.x, neighbor.y] >= 0)
                    {
                        int regionId = regionMap[neighbor.x, neighbor.y];
                        if (adjacencyCount.ContainsKey(regionId))
                        {
                            adjacencyCount[regionId]++;
                        }
                    }
                }
                
                // Find region with most adjacent pixels
                int bestRegion = -1;
                int maxCount = 0;
                foreach (var kvp in adjacencyCount)
                {
                    if (kvp.Value > maxCount)
                    {
                        maxCount = kvp.Value;
                        bestRegion = kvp.Key;
                    }
                }
                
                // Assign border pixel to the best region
                if (bestRegion >= 0)
                {
                    regions[bestRegion].pixels.Add(borderPixel);
                    regionMap[borderPixel.x, borderPixel.y] = bestRegion;
                }
            }
        }
        
        Debug.Log($"Expanded regions into borders");
    }
    
    public void ForceBorderColors()
    {
        // Force regenerate preview with current colors
        GeneratePreview();
    }
    
    public void SaveBorderColors()
    {
        // Force serialization by marking the object dirty
        #if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        #endif
        
        Debug.Log($"Saved {borderRegions.Count} border region colors");
        for (int i = 0; i < borderRegions.Count; i++)
        {
            Debug.Log($"Saved Border {i}: {borderRegions[i].regionName} - Color: {borderRegions[i].color}");
        }
    }
    
    public void ForceDetectBorderRegions()
    {
        DetectBorderRegions();
        GeneratePreview();
    }
    
    void DetectBorderRegions()
    {
        // Store existing border region colors before clearing
        Dictionary<string, Color> existingBorderColors = new Dictionary<string, Color>();
        foreach (var borderRegion in borderRegions)
        {
            existingBorderColors[borderRegion.regionName] = borderRegion.color;
        }
        
        borderRegions.Clear();
        
        // Create a visited map for border detection
        bool[,] borderVisited = new bool[width, height];
        
        // Mark all white region pixels as visited
        for (int i = 0; i < regions.Count; i++)
        {
            foreach (var pixel in regions[i].pixels)
            {
                borderVisited[pixel.x, pixel.y] = true;
            }
        }
        
        // Find black border regions using flood fill
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!borderVisited[x, y] && !IsWhite(GetPixel(x, y)))
                {
                    BorderRegion borderRegion = new BorderRegion();
                    FloodFillBorder(x, y, borderRegion, borderVisited);
                    borderRegion.regionName = $"Border {borderRegions.Count}";
                    
                    // Restore previous color if it existed, otherwise use black
                    if (existingBorderColors.ContainsKey(borderRegion.regionName))
                    {
                        borderRegion.color = existingBorderColors[borderRegion.regionName];
                    }
                    else
                    {
                        borderRegion.color = Color.black; // Default for new borders
                    }
                    
                    borderRegions.Add(borderRegion);
                }
            }
        }
        
        Debug.Log($"Detected {borderRegions.Count} border regions (preserved existing colors)");
    }
    
    void FloodFillBorder(int startX, int startY, BorderRegion borderRegion, bool[,] borderVisited)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(startX, startY));
        borderVisited[startX, startY] = true;

        while (queue.Count > 0)
        {
            Vector2Int p = queue.Dequeue();
            borderRegion.pixels.Add(p);

            foreach (var n in GetNeighbors(p))
            {
                if (IsInside(n) &&
                    !borderVisited[n.x, n.y] &&
                    !IsWhite(GetPixel(n.x, n.y)))
                {
                    borderVisited[n.x, n.y] = true;
                    queue.Enqueue(n);
                }
            }
        }
    }
    
    public void RemoveBorderRegion(int borderIndex)
    {
        if (borderIndex < 0 || borderIndex >= borderRegions.Count)
        {
            Debug.LogError($"Invalid border index: {borderIndex}");
            return;
        }
        
        BorderRegion borderToRemove = borderRegions[borderIndex];
        
        // Create a map to track which region owns each pixel
        int[,] regionMap = new int[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                regionMap[x, y] = -1;
            }
        }
        
        // Mark existing region pixels
        for (int i = 0; i < regions.Count; i++)
        {
            foreach (var pixel in regions[i].pixels)
            {
                regionMap[pixel.x, pixel.y] = i;
            }
        }
        
        // Distribute border pixels to adjacent regions
        foreach (var borderPixel in borderToRemove.pixels)
        {
            // Find all adjacent regions
            Dictionary<int, int> adjacencyCount = new Dictionary<int, int>();
            
            foreach (var neighbor in GetNeighbors(borderPixel))
            {
                if (IsInside(neighbor) && regionMap[neighbor.x, neighbor.y] >= 0)
                {
                    int regionId = regionMap[neighbor.x, neighbor.y];
                    if (!adjacencyCount.ContainsKey(regionId))
                    {
                        adjacencyCount[regionId] = 0;
                    }
                    adjacencyCount[regionId]++;
                }
            }
            
            // Assign to region with most adjacent pixels
            if (adjacencyCount.Count > 0)
            {
                int bestRegion = -1;
                int maxCount = 0;
                foreach (var kvp in adjacencyCount)
                {
                    if (kvp.Value > maxCount)
                    {
                        maxCount = kvp.Value;
                        bestRegion = kvp.Key;
                    }
                }
                
                if (bestRegion >= 0)
                {
                    regions[bestRegion].pixels.Add(borderPixel);
                    regionMap[borderPixel.x, borderPixel.y] = bestRegion;
                }
            }
        }
        
        // Remove the border region from the list
        borderRegions.RemoveAt(borderIndex);
        
        Debug.Log($"Removed border region {borderIndex} and distributed pixels to adjacent regions");
        
        GeneratePreview();
    }
    
    // Methods for SpriteSequenceManager to check completion status
    public int GetTotalRegionCount()
    {
        return regions.Count;
    }
    
    public int GetFilledRegionCount()
    {
        // Get the SandPouringFillEffect component to check actual sand fill progress
        SandPouringFillEffect fillEffect = GetComponent<SandPouringFillEffect>();
        if (fillEffect != null)
        {
            // Use actual sand fill progress from SandPouringFillEffect
            return fillEffect.GetCompletelyFilledRegionsCount();
        }
        
        // Fallback: Count regions that have been assigned a color name (from sand pieces)
        int filledCount = 0;
        foreach (var region in regions)
        {
            // Primary check: region has a color name assigned (this happens when sand pieces fill it)
            if (!string.IsNullOrEmpty(region.colorName))
            {
                filledCount++;
            }
            // Secondary check: region color has been changed from default white
            else if (region.color != Color.white && region.color.a > 0.9f)
            {
                // Additional check to ensure it's not just a very light color
                float colorIntensity = (region.color.r + region.color.g + region.color.b) / 3f;
                if (colorIntensity < 0.95f) // Not pure white or very close to white
                {
                    filledCount++;
                }
            }
        }
        return filledCount;
    }
    
    public bool IsCompletelyFilled()
    {
        // Get the SandPouringFillEffect component to check actual sand fill progress
        SandPouringFillEffect fillEffect = GetComponent<SandPouringFillEffect>();
        if (fillEffect != null)
        {
            // Use actual sand fill progress from SandPouringFillEffect
            return fillEffect.IsAllRegionsCompletelyFilled();
        }
        
        // Fallback method
        return GetTotalRegionCount() > 0 && GetFilledRegionCount() >= GetTotalRegionCount();
    }
    
    public float GetFillProgress()
    {
        // Get the SandPouringFillEffect component to check actual sand fill progress
        SandPouringFillEffect fillEffect = GetComponent<SandPouringFillEffect>();
        if (fillEffect != null)
        {
            // Use actual sand fill progress from SandPouringFillEffect
            return fillEffect.GetOverallFillProgress();
        }
        
        // Fallback method
        int total = GetTotalRegionCount();
        if (total == 0) return 1f; // Consider empty sprite as complete
        return (float)GetFilledRegionCount() / total;
    }
}
