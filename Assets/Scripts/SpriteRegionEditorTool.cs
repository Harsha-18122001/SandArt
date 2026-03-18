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

    private int width;
    private int height;

    private float whiteThreshold = 0.9f;

    // Highlight support for the editor
    [System.NonSerialized] public int highlightedRegionIndex = -1;
    [System.NonSerialized] public bool showRegionLabels = true;

    /// <summary>
    /// Generates a preview that highlights only a single region (dimming everything else).
    /// Pass -1 to show normal preview.
    /// </summary>
    public void GenerateHighlightPreview(int regionIndex)
    {
        highlightedRegionIndex = regionIndex;

        if (regionIndex < 0)
        {
            GeneratePreview();
            return;
        }

        if (sourceSprite == null) return;

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
        Color dimColor = new Color(0.15f, 0.15f, 0.15f, 1f);

        // Fill everything dim
        for (int i = 0; i < previewPixels.Length; i++)
            previewPixels[i] = dimColor;

        // Draw the highlighted region in bright white with a colored outline
        if (regionIndex >= 0 && regionIndex < regions.Count)
        {
            var region = regions[regionIndex];
            foreach (var p in region.pixels)
            {
                if (IsInside(p))
                    previewPixels[p.y * width + p.x] = region.color;
            }
        }

        previewTexture.SetPixels(previewPixels);
        previewTexture.Apply();

        var renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = gameObject.AddComponent<SpriteRenderer>();

        if (renderer.sprite == null || renderer.sprite.texture != previewTexture)
        {
            renderer.sprite = Sprite.Create(previewTexture,
                                            new Rect(0, 0, width, height),
                                            new Vector2(0.5f, 0.5f),
                                            sourceSprite.pixelsPerUnit);
        }
    }

    /// <summary>
    /// Returns the world-space center of a region (for drawing scene labels).
    /// </summary>
    public Vector3 GetRegionWorldCenter(int regionIndex)
    {
        if (regionIndex < 0 || regionIndex >= regions.Count) return transform.position;
        var region = regions[regionIndex];
        if (region.pixels.Count == 0) return transform.position;

        long sumX = 0, sumY = 0;
        foreach (var p in region.pixels) { sumX += p.x; sumY += p.y; }
        float cx = (float)sumX / region.pixels.Count;
        float cy = (float)sumY / region.pixels.Count;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return transform.position;

        Sprite sprite = sourceSprite != null ? sourceSprite : sr.sprite;
        float ppu = sprite.pixelsPerUnit;
        Rect rect = sprite.rect;
        Vector2 pivot = sprite.pivot;

        float localX = (cx - rect.x - pivot.x) / ppu;
        float localY = (cy - rect.y - pivot.y) / ppu;

        return transform.TransformPoint(new Vector3(localX, localY, 0));
    }

    public void DetectRegions()
    {
        if (sourceSprite == null)
        {
            Debug.LogWarning("No source sprite assigned!");
            return;
        }

        sourceTexture = sourceSprite.texture;
        width = sourceTexture.width;
        height = sourceTexture.height;
        sourcePixels = sourceTexture.GetPixels();

        regions.Clear();
        // Preserve border colors if they exist
        Dictionary<string, Color> existingBorderColors = new Dictionary<string, Color>();
        foreach (var br in borderRegions) existingBorderColors[br.regionName] = br.color;
        borderRegions.Clear();

        int pixelCount = width * height;
        bool[] handled = new bool[pixelCount];

        // One pass to detect everything
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                if (handled[idx]) continue;

                Color c = sourcePixels[idx];
                bool isLocallyWhite = c.r > whiteThreshold && c.g > whiteThreshold && c.b > whiteThreshold;

                if (isLocallyWhite)
                {
                    Region region = new Region { regionName = $"Region {regions.Count}" };
                    FastFloodFill(idx, region.pixels, handled, true);
                    regions.Add(region);
                }
                else if (detectBorderRegions)
                {
                    BorderRegion bRegion = new BorderRegion { regionName = $"Border {borderRegions.Count}" };
                    FastFloodFill(idx, bRegion.pixels, handled, false);
                    
                    if (existingBorderColors.TryGetValue(bRegion.regionName, out Color savedColor))
                        bRegion.color = savedColor;
                    else
                        bRegion.color = Color.black;

                    borderRegions.Add(bRegion);
                }
                else
                {
                    handled[idx] = true;
                }
            }
        }

        // Apply fair color distribution
        if (colorLibrary != null && colorLibrary.colorMaterials.Count > 0)
        {
            List<string> fairColors = GetFairColorDistribution(regions.Count);
            for (int i = 0; i < regions.Count && i < fairColors.Count; i++)
            {
                regions[i].colorName = fairColors[i];
                regions[i].color = colorLibrary.GetColorByName(fairColors[i]);
            }
        }

        Debug.Log($"Detected {regions.Count} regions and {borderRegions.Count} border regions in one pass.");
        GeneratePreview();
    }

    private void FastFloodFill(int startIdx, List<Vector2Int> pixelList, bool[] handledMap, bool targetWhite)
    {
        Queue<int> queue = new Queue<int>(1024);
        queue.Enqueue(startIdx);
        handledMap[startIdx] = true;

        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            int px = idx % width;
            int py = idx / width;
            pixelList.Add(new Vector2Int(px, py));

            // Neighbors
            CheckFastNeighbor(px + 1, py, queue, handledMap, targetWhite);
            CheckFastNeighbor(px - 1, py, queue, handledMap, targetWhite);
            CheckFastNeighbor(px, py + 1, queue, handledMap, targetWhite);
            CheckFastNeighbor(px, py - 1, queue, handledMap, targetWhite);
        }
    }

    private void CheckFastNeighbor(int x, int y, Queue<int> queue, bool[] handledMap, bool targetWhite)
    {
        if (x >= 0 && x < width && y >= 0 && y < height)
        {
            int idx = y * width + x;
            if (!handledMap[idx])
            {
                Color c = sourcePixels[idx];
                bool isLocallyWhite = c.r > whiteThreshold && c.g > whiteThreshold && c.b > whiteThreshold;
                
                if (isLocallyWhite == targetWhite)
                {
                    handledMap[idx] = true;
                    queue.Enqueue(idx);
                }
            }
        }
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


    private int[] globalRegionMap;
    private float[] globalDistanceMap;
    private int[] globalOwnerMap;

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
        for (int i = 0; i < previewPixels.Length; i++)
            previewPixels[i] = Color.black;

        // 1. Build a map of color regions once
        int pixelCount = width * height;
        globalRegionMap = new int[pixelCount];
        for (int i = 0; i < pixelCount; i++)
        {
            globalRegionMap[i] = -1;
        }

        for (int i = 0; i < regions.Count; i++)
        {
            foreach (var p in regions[i].pixels)
            {
                if (IsInside(p))
                {
                    int idx = p.y * width + p.x;
                    previewPixels[idx] = regions[i].color;
                    globalRegionMap[idx] = i;
                }
            }
        }

        // 2. If we need to shrink borders, pre-calculate distances for ALL pixels from ALL regions at once
        bool anyShrink = borderShrinkAmount > 0f || borderRegions.Exists(b => b.shrinkAmount > 0f);
        if (anyShrink && regions.Count > 0)
        {
            CalculateGlobalDistanceMap();
        }

        // 3. Draw border regions
        foreach (var borderRegion in borderRegions)
        {
            float effectiveShrink = Mathf.Max(borderShrinkAmount, borderRegion.shrinkAmount);
            
            if (effectiveShrink > 0f && anyShrink && globalDistanceMap != null)
            {
                FastShrinkAndFillBorder(borderRegion, previewPixels, effectiveShrink);
            }
            else
            {
                foreach (var p in borderRegion.pixels)
                {
                    int pixelIndex = p.y * width + p.x;
                    if (pixelIndex >= 0 && pixelIndex < previewPixels.Length)
                        previewPixels[pixelIndex] = borderRegion.color;
                }
            }
        }

        previewTexture.SetPixels(previewPixels);
        previewTexture.Apply();

        var renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = gameObject.AddComponent<SpriteRenderer>();

        if (renderer.sprite == null || renderer.sprite.texture != previewTexture)
        {
            renderer.sprite = Sprite.Create(previewTexture,
                                            new Rect(0, 0, width, height),
                                            new Vector2(0.5f, 0.5f),
                                            sourceSprite.pixelsPerUnit);
        }
    }

    private void CalculateGlobalDistanceMap()
    {
        int pixelCount = width * height;
        globalDistanceMap = new float[pixelCount];
        globalOwnerMap = new int[pixelCount];
        Queue<int> queue = new Queue<int>(pixelCount / 10);

        for (int i = 0; i < pixelCount; i++)
        {
            if (globalRegionMap[i] >= 0)
            {
                globalDistanceMap[i] = 0;
                globalOwnerMap[i] = globalRegionMap[i];
                queue.Enqueue(i);
            }
            else
            {
                globalDistanceMap[i] = float.MaxValue;
                globalOwnerMap[i] = -1;
            }
        }

        // Multi-source BFS to calculate distances to nearest region
        while (queue.Count > 0)
        {
            int currentIdx = queue.Dequeue();
            int cx = currentIdx % width;
            int cy = currentIdx / width;
            
            float currentDist = globalDistanceMap[currentIdx];
            int owner = globalOwnerMap[currentIdx];

            if (currentDist >= 20) continue; // Limit distance for performance, borders aren't usually that thick

            TryAddDistanceNeighbor(cx + 1, cy, currentDist, owner, queue);
            TryAddDistanceNeighbor(cx - 1, cy, currentDist, owner, queue);
            TryAddDistanceNeighbor(cx, cy + 1, currentDist, owner, queue);
            TryAddDistanceNeighbor(cx, cy - 1, currentDist, owner, queue);
        }
    }

    private void TryAddDistanceNeighbor(int x, int y, float currentDist, int owner, Queue<int> queue)
    {
        if (x >= 0 && x < width && y >= 0 && y < height)
        {
            int idx = y * width + x;
            if (globalDistanceMap[idx] == float.MaxValue)
            {
                globalDistanceMap[idx] = currentDist + 1;
                globalOwnerMap[idx] = owner;
                queue.Enqueue(idx);
            }
        }
    }

    private void FastShrinkAndFillBorder(BorderRegion borderRegion, Color[] previewPixels, float shrinkAmount)
    {
        // Find max distance in this specific border region to normalize threshold
        float regionMaxDist = 0;
        foreach (var p in borderRegion.pixels)
        {
            int idx = p.y * width + p.x;
            float d = globalDistanceMap[idx];
            if (d != float.MaxValue && d > regionMaxDist) regionMaxDist = d;
        }

        float threshold = regionMaxDist * (1f - shrinkAmount);

        foreach (var p in borderRegion.pixels)
        {
            int idx = p.y * width + p.x;
            float dist = globalDistanceMap[idx];
            int owner = globalOwnerMap[idx];

            if (dist <= threshold && owner >= 0)
            {
                previewPixels[idx] = regions[owner].color;
            }
            else
            {
                previewPixels[idx] = borderRegion.color;
            }
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
    
    // Simplified as DetectRegions now handles this
    void DetectBorderRegions()
    {
        // This is now redundant but kept for any specific calls
        DetectRegions(); 
    }
    
    public void ForceDetectBorderRegions()
    {
        if (sourceSprite == null) return;
        sourceTexture = sourceSprite.texture;
        width = sourceTexture.width;
        height = sourceTexture.height;
        sourcePixels = sourceTexture.GetPixels();

        Dictionary<string, Color> existingBorderColors = new Dictionary<string, Color>();
        foreach (var br in borderRegions) existingBorderColors[br.regionName] = br.color;
        borderRegions.Clear();

        int pixelCount = width * height;
        bool[] handled = new bool[pixelCount];

        // Ensure all current regions are marked as handled so we skip them
        foreach (var region in regions)
        {
             foreach (var p in region.pixels)
             {
                  handled[p.y * width + p.x] = true;
             }
        }

        // Scan purely for unhandled non-white pixels
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                if (handled[idx]) continue;

                Color c = sourcePixels[idx];
                bool isLocallyWhite = c.r > whiteThreshold && c.g > whiteThreshold && c.b > whiteThreshold;

                if (!isLocallyWhite)
                {
                    BorderRegion bRegion = new BorderRegion { regionName = $"Border {borderRegions.Count}" };
                    FastFloodFill(idx, bRegion.pixels, handled, false);
                    
                    if (existingBorderColors.TryGetValue(bRegion.regionName, out Color savedColor))
                        bRegion.color = savedColor;
                    else
                        bRegion.color = Color.black;

                    borderRegions.Add(bRegion);
                }
                else
                {
                    handled[idx] = true;
                }
            }
        }
        
        Debug.Log($"Detected {borderRegions.Count} border regions (preserved existing regions).");
        GeneratePreview();
    }

    public void ExpandBorderPixels(int amount = 1)
    {
        if (amount <= 0 || borderRegions.Count == 0 || regions.Count == 0) return;

        int pixelCount = width * height;
        if (pixelCount == 0 || sourcePixels == null) return; // In case texture isn't loaded

        int[] distanceMap = new int[pixelCount];
        int[] borderOwnerMap = new int[pixelCount];
        Queue<int> queue = new Queue<int>(pixelCount / 5);

        for (int i = 0; i < pixelCount; i++)
        {
            distanceMap[i] = int.MaxValue;
            borderOwnerMap[i] = -1;
        }

        for (int i = 0; i < borderRegions.Count; i++)
        {
            foreach (var p in borderRegions[i].pixels)
            {
                int idx = p.y * width + p.x;
                distanceMap[idx] = 0;
                borderOwnerMap[idx] = i;
                queue.Enqueue(idx);
            }
        }

        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            int currentDist = distanceMap[idx];
            int owner = borderOwnerMap[idx];

            if (currentDist >= amount) continue;

            int cx = idx % width;
            int cy = idx / width;

            TryExpandBorderNeighbor(cx + 1, cy, currentDist, owner, distanceMap, borderOwnerMap, queue);
            TryExpandBorderNeighbor(cx - 1, cy, currentDist, owner, distanceMap, borderOwnerMap, queue);
            TryExpandBorderNeighbor(cx, cy + 1, currentDist, owner, distanceMap, borderOwnerMap, queue);
            TryExpandBorderNeighbor(cx, cy - 1, currentDist, owner, distanceMap, borderOwnerMap, queue);
        }

        for (int i = 0; i < borderRegions.Count; i++) borderRegions[i].pixels.Clear();
        
        List<Vector2Int>[] newRegionPixels = new List<Vector2Int>[regions.Count];
        for (int i = 0; i < regions.Count; i++) newRegionPixels[i] = new List<Vector2Int>();

        int[] originalRegionOwner = new int[pixelCount];
        for (int i = 0; i < pixelCount; i++) originalRegionOwner[i] = -1;
        for (int i = 0; i < regions.Count; i++)
        {
            foreach(var p in regions[i].pixels) originalRegionOwner[p.y * width + p.x] = i;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                if (borderOwnerMap[idx] >= 0) 
                {
                    borderRegions[borderOwnerMap[idx]].pixels.Add(new Vector2Int(x, y));
                }
                else 
                {
                    int rIdx = originalRegionOwner[idx];
                    if (rIdx >= 0)
                    {
                        newRegionPixels[rIdx].Add(new Vector2Int(x, y));
                    }
                }
            }
        }

        for (int i = 0; i < regions.Count; i++)
        {
            regions[i].pixels = newRegionPixels[i];
        }

        Debug.Log($"Expanded borders into regions by {amount} pixels");
        GeneratePreview();
    }

    private void TryExpandBorderNeighbor(int x, int y, int dist, int owner, int[] distanceMap, int[] borderOwnerMap, Queue<int> queue)
    {
        if (x >= 0 && x < width && y >= 0 && y < height)
        {
            int idx = y * width + x;
            if (distanceMap[idx] > dist + 1)
            {
                distanceMap[idx] = dist + 1;
                borderOwnerMap[idx] = owner;
                queue.Enqueue(idx);
            }
        }
    }

    public void ShrinkBorderPixels(int amount = 1)
    {
        if (amount <= 0 || borderRegions.Count == 0 || regions.Count == 0) return;

        int pixelCount = width * height;
        if (pixelCount == 0 || sourcePixels == null) return;

        int[] distanceMap = new int[pixelCount];
        int[] regionOwnerMap = new int[pixelCount];
        Queue<int> queue = new Queue<int>(pixelCount / 5);

        for (int i = 0; i < pixelCount; i++)
        {
            distanceMap[i] = int.MaxValue;
            regionOwnerMap[i] = -1;
        }

        for (int i = 0; i < regions.Count; i++)
        {
            foreach (var p in regions[i].pixels)
            {
                int idx = p.y * width + p.x;
                distanceMap[idx] = 0;
                regionOwnerMap[idx] = i;
                queue.Enqueue(idx);
            }
        }

        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            int currentDist = distanceMap[idx];
            int owner = regionOwnerMap[idx];

            if (currentDist >= amount) continue;

            int cx = idx % width;
            int cy = idx / width;

            TryExpandRegionNeighbor(cx + 1, cy, currentDist, owner, distanceMap, regionOwnerMap, queue);
            TryExpandRegionNeighbor(cx - 1, cy, currentDist, owner, distanceMap, regionOwnerMap, queue);
            TryExpandRegionNeighbor(cx, cy + 1, currentDist, owner, distanceMap, regionOwnerMap, queue);
            TryExpandRegionNeighbor(cx, cy - 1, currentDist, owner, distanceMap, regionOwnerMap, queue);
        }

        for (int i = 0; i < regions.Count; i++) regions[i].pixels.Clear();
        
        List<Vector2Int>[] newBorderPixels = new List<Vector2Int>[borderRegions.Count];
        for (int i = 0; i < borderRegions.Count; i++) newBorderPixels[i] = new List<Vector2Int>();

        int[] originalBorderOwner = new int[pixelCount];
        for (int i = 0; i < pixelCount; i++) originalBorderOwner[i] = -1;
        for (int i = 0; i < borderRegions.Count; i++)
        {
            foreach(var p in borderRegions[i].pixels) originalBorderOwner[p.y * width + p.x] = i;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                if (regionOwnerMap[idx] >= 0) 
                {
                    regions[regionOwnerMap[idx]].pixels.Add(new Vector2Int(x, y));
                }
                else 
                {
                    int bIdx = originalBorderOwner[idx];
                    if (bIdx >= 0)
                    {
                        newBorderPixels[bIdx].Add(new Vector2Int(x, y));
                    }
                }
            }
        }

        for (int i = 0; i < borderRegions.Count; i++)
        {
            borderRegions[i].pixels = newBorderPixels[i];
        }

        Debug.Log($"Shrunk borders by {amount} pixels (regions expanded)");
        GeneratePreview();
    }

    private void TryExpandRegionNeighbor(int x, int y, int dist, int owner, int[] distanceMap, int[] regionOwnerMap, Queue<int> queue)
    {
        if (x >= 0 && x < width && y >= 0 && y < height)
        {
            int idx = y * width + x;
            if (distanceMap[idx] > dist + 1)
            {
                distanceMap[idx] = dist + 1;
                regionOwnerMap[idx] = owner;
                queue.Enqueue(idx);
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
        
        // Create a flat map to track which region owns each pixel
        int pixelCount = width * height;
        int[] regionMap = new int[pixelCount];
        for (int i = 0; i < pixelCount; i++)
        {
            regionMap[i] = -1;
        }
        
        // Mark existing region pixels
        for (int i = 0; i < regions.Count; i++)
        {
            foreach (var pixel in regions[i].pixels)
            {
                regionMap[pixel.y * width + pixel.x] = i;
            }
        }
        
        // Distribute border pixels to adjacent regions
        foreach (var borderPixel in borderToRemove.pixels)
        {
            Dictionary<int, int> adjacencyCount = new Dictionary<int, int>();
            
            // Check direct neighbors
            TryCountNeighborRegion(borderPixel.x + 1, borderPixel.y, regionMap, adjacencyCount);
            TryCountNeighborRegion(borderPixel.x - 1, borderPixel.y, regionMap, adjacencyCount);
            TryCountNeighborRegion(borderPixel.x, borderPixel.y + 1, regionMap, adjacencyCount);
            TryCountNeighborRegion(borderPixel.x, borderPixel.y - 1, regionMap, adjacencyCount);
            
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
                    regionMap[borderPixel.y * width + borderPixel.x] = bestRegion;
                }
            }
        }
        
        // Remove the border region from the list
        borderRegions.RemoveAt(borderIndex);
        
        Debug.Log($"Removed border region {borderIndex} and distributed pixels to adjacent regions");
        
        GeneratePreview();
    }

    private void TryCountNeighborRegion(int nx, int ny, int[] regionMap, Dictionary<int, int> adjacencyCount)
    {
        if (nx >= 0 && nx < width && ny >= 0 && ny < height)
        {
            int regionId = regionMap[ny * width + nx];
            if (regionId >= 0)
            {
                if (!adjacencyCount.ContainsKey(regionId))
                    adjacencyCount[regionId] = 0;
                adjacencyCount[regionId]++;
            }
        }
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
