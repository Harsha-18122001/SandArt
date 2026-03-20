using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(SpriteRegionEditorTool))]
public class SandPouringFillEffect : MonoBehaviour
{
    [Header("Fill Settings")]
    [SerializeField] private float fillTime = 2f; // time in seconds to fill all regions
    [SerializeField] private float colorVariation = 0.4f; // 0-1, how much color varies
    [SerializeField] private float slopesteepness = 2f; // higher = steeper slope (vertical spread favored)
    [SerializeField] private int pixelationBlockSize = 4; // Groups pixels into blocks to fill chunks simultaneously
    [SerializeField] private float pixelationNoise = 2f; // Adds randomness to the fill progression edge
    
    [Header("Animation")]
    [SerializeField] private bool fillOnStart = true;
    [SerializeField] private AnimationCurve fillCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Progressive Fill Mode")]
    [SerializeField] private bool useProgressiveFill = false; // Enable for detector-based filling
    
    [Header("Region Locking")]
    [SerializeField] private bool useRegionLocking = false; // Enable region unlock system
    [SerializeField] private int initialUnlockedRegions = 3; // Number of regions unlocked at start
    [SerializeField] private List<int> unlockSequence = new List<int>(); // Custom unlock sequence (region indices)
    [SerializeField] private Color lockedRegionColor = new Color(0.3f, 0.3f, 0.3f, 1f); // Grey color for locked regions
    [SerializeField] private float unlockedRegionLightness = 0.7f; // Absolute lightness value for all unlocked regions (0-1)
    
    private SpriteRegionEditorTool regionTool;
    private Texture2D fillTexture;
    private SpriteRenderer spriteRenderer;
    private int width;
    private int height;
    private bool isFilling = false;
    
    // Progressive fill data
    private Dictionary<int, List<PixelFillData>> regionPixelData = new Dictionary<int, List<PixelFillData>>();
    private Dictionary<int, float> regionFillProgress = new Dictionary<int, float>();
    private Dictionary<int, float> regionTargetProgress = new Dictionary<int, float>(); // Target progress for smooth animation
    private Dictionary<int, Coroutine> regionFillCoroutines = new Dictionary<int, Coroutine>();
    
    // Region locking data
    private HashSet<int> unlockedRegions = new HashSet<int>();
    private int regionsCompleted = 0;
    
    private class PixelFillData
    {
        public Vector2Int position;
        public float distanceFromCenter;
        public Color targetColor;
        public int regionIndex;
    }
    
    void Start()
    {
        regionTool = GetComponent<SpriteRegionEditorTool>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        Debug.Log("SandPouringFillEffect Start() called");
        
        if (regionTool == null)
        {
            Debug.LogError("SpriteRegionEditorTool component not found!");
            return;
        }
        
        if (regionTool.sourceSprite != null)
        {
            width = regionTool.sourceSprite.texture.width;
            height = regionTool.sourceSprite.texture.height;
            Debug.Log($"Sprite dimensions: {width}x{height}");
            Debug.Log($"Regions detected: {regionTool.regions?.Count ?? 0}");
            
            if (useProgressiveFill)
            {
                InitializeProgressiveFill();
            }
            else if (fillOnStart)
            {
                StartFillAnimation();
            }
        }
        else
        {
            Debug.LogWarning("No source sprite assigned to SpriteRegionEditorTool!");
        }
    }
    
    void InitializeProgressiveFill()
    {
        // Create texture with region base colors visible from start
        fillTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        fillTexture.filterMode = FilterMode.Point;
        fillTexture.wrapMode = TextureWrapMode.Clamp;
        
        Color[] pixels = new Color[width * height];
        
        // Initialize with black background (or transparent if preferred, but assuming black for now)
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.black;
        
        // Draw border regions first
        if (regionTool.borderRegions != null)
        {
            foreach (var border in regionTool.borderRegions)
            {
                foreach (var pixel in border.pixels)
                {
                    int pixelIndex = pixel.y * width + pixel.x;
                    if (pixelIndex >= 0 && pixelIndex < pixels.Length)
                    {
                        pixels[pixelIndex] = border.color;
                    }
                }
            }
        }
        
        // Initialize region locking
        if (useRegionLocking)
        {
            // Use custom unlock sequence if provided, otherwise use default sequential order
            if (unlockSequence != null && unlockSequence.Count > 0)
            {
                // Unlock regions based on custom sequence
                int regionsToUnlock = Mathf.Min(initialUnlockedRegions, unlockSequence.Count);
                for (int i = 0; i < regionsToUnlock; i++)
                {
                    int regionIndex = unlockSequence[i];
                    if (regionIndex >= 0 && regionIndex < regionTool.regions.Count)
                    {
                        unlockedRegions.Add(regionIndex);
                    }
                }
                Debug.Log($"Unlocked first {unlockedRegions.Count} regions using custom sequence");
            }
            else
            {
                // Default: unlock first N regions sequentially
                for (int i = 0; i < Mathf.Min(initialUnlockedRegions, regionTool.regions.Count); i++)
                {
                    unlockedRegions.Add(i);
                }
                Debug.Log($"Unlocked first {unlockedRegions.Count} regions (sequential)");
            }
        }
        
        // Fill all regions with their base colors (or locked color)
        for (int regionIndex = 0; regionIndex < regionTool.regions.Count; regionIndex++)
        {
            var region = regionTool.regions[regionIndex];
            bool isUnlocked = !useRegionLocking || unlockedRegions.Contains(regionIndex);
            
            Color regionColor;
            if (isUnlocked)
            {
                // Unlocked regions show lighter version of their color
                regionColor = LightenColor(region.color, unlockedRegionLightness);
            }
            else
            {
                // Locked regions show grey
                regionColor = lockedRegionColor;
            }
            
            foreach (var pixel in region.pixels)
            {
                int pixelIndex = pixel.y * width + pixel.x;
                if (pixelIndex >= 0 && pixelIndex < pixels.Length)
                {
                    pixels[pixelIndex] = regionColor;
                }
            }
        }
        
        // Prepare pixel data for each region (for progressive filling with variations)
        for (int regionIndex = 0; regionIndex < regionTool.regions.Count; regionIndex++)
        {
            var region = regionTool.regions[regionIndex];
            Vector2 bottomCenter = CalculateRegionBottomCenter(region);
            
            List<PixelFillData> regionPixels = new List<PixelFillData>();
            
            foreach (var pixel in region.pixels)
            {
                // Block coordinates for pixelated fill chunks
                float blockX = pixelationBlockSize > 1 ? Mathf.Floor(pixel.x / pixelationBlockSize) * pixelationBlockSize : pixel.x;
                float blockY = pixelationBlockSize > 1 ? Mathf.Floor(pixel.y / pixelationBlockSize) * pixelationBlockSize : pixel.y;
                
                // Calculate weighted distance for steeper slope
                float dx = Mathf.Abs(blockX - bottomCenter.x);
                float dy = Mathf.Abs(blockY - bottomCenter.y);
                float noise = (pixelationNoise > 0f) ? Random.Range(-pixelationNoise, pixelationNoise) : 0f;
                float weightedDistance = dx + (dy / slopesteepness) + noise;
                
                PixelFillData fillData = new PixelFillData
                {
                    position = pixel,
                    distanceFromCenter = weightedDistance,
                    targetColor = GetVariedColor(region.color), // Pre-generate varied colors
                    regionIndex = regionIndex
                };
                regionPixels.Add(fillData);
            }
            
            // Sort by distance (closest first)
            regionPixels.Sort((a, b) => a.distanceFromCenter.CompareTo(b.distanceFromCenter));
            regionPixelData[regionIndex] = regionPixels;
            regionFillProgress[regionIndex] = 0f;
            regionTargetProgress[regionIndex] = 0f;
        }
        
        fillTexture.SetPixels(pixels);
        fillTexture.Apply();
        
        // Update sprite renderer
        spriteRenderer.sprite = Sprite.Create(
            fillTexture,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            regionTool.sourceSprite.pixelsPerUnit
        );
        
        Debug.Log("Progressive fill initialized with base region colors visible");
    }
    
    public void UpdateRegionFill(int regionId, float targetProgress)
    {
        if (!useProgressiveFill)
        {
            Debug.LogWarning("Progressive fill is not enabled!");
            return;
        }
        
        if (!regionPixelData.ContainsKey(regionId))
        {
            Debug.LogError($"Region {regionId} not found in pixel data!");
            return;
        }
        
        // Check if region is locked
        if (useRegionLocking && !unlockedRegions.Contains(regionId))
        {
            Debug.LogWarning($"Region {regionId} is locked! Cannot fill.");
            return;
        }
        
        targetProgress = Mathf.Clamp01(targetProgress);
        regionTargetProgress[regionId] = targetProgress;
        
        // Stop existing fill coroutine for this region if any
        if (regionFillCoroutines.ContainsKey(regionId) && regionFillCoroutines[regionId] != null)
        {
            StopCoroutine(regionFillCoroutines[regionId]);
        }
        
        // Start smooth fill animation
        Coroutine fillCoroutine = StartCoroutine(SmoothFillRegion(regionId, targetProgress));
        regionFillCoroutines[regionId] = fillCoroutine;
    }
    
    public void OnRegionCompleted(int regionId)
    {
        if (!useRegionLocking) return;
        
        regionsCompleted++;
        Debug.Log($"<color=yellow>Region {regionId} completed! Total completed: {regionsCompleted}</color>");
        
        // Unlock next region based on sequence
        int nextRegionIndex = -1;
        
        if (unlockSequence != null && unlockSequence.Count > 0)
        {
            // Use custom unlock sequence
            int nextSequenceIndex = initialUnlockedRegions + regionsCompleted - 1;
            if (nextSequenceIndex < unlockSequence.Count)
            {
                nextRegionIndex = unlockSequence[nextSequenceIndex];
            }
        }
        else
        {
            // Default sequential unlock
            nextRegionIndex = initialUnlockedRegions + regionsCompleted - 1;
        }
        
        if (nextRegionIndex >= 0 && nextRegionIndex < regionTool.regions.Count && !unlockedRegions.Contains(nextRegionIndex))
        {
            UnlockRegion(nextRegionIndex);
        }
    }
    
    void UnlockRegion(int regionId)
    {
        if (unlockedRegions.Contains(regionId)) return;
        
        unlockedRegions.Add(regionId);
        Debug.Log($"<color=green>Unlocked region {regionId}!</color>");
        
        // Reveal the region's lighter color (not full color yet)
        var region = regionTool.regions[regionId];
        Color lighterColor = LightenColor(region.color, unlockedRegionLightness);
        Color[] pixels = fillTexture.GetPixels();
        
        foreach (var pixel in region.pixels)
        {
            int pixelIndex = pixel.y * width + pixel.x;
            if (pixelIndex >= 0 && pixelIndex < pixels.Length)
            {
                pixels[pixelIndex] = lighterColor;
            }
        }
        
        fillTexture.SetPixels(pixels);
        fillTexture.Apply();
    }
    
    Color LightenColor(Color baseColor, float lightnessAmount)
    {
        // Lerp towards white to make the color lighter while preserving the hue
        // lightnessAmount: 0 = original color, 1 = white
        Color lighterColor = Color.Lerp(baseColor, Color.white, lightnessAmount);
        lighterColor.a = baseColor.a;
        
        Debug.Log($"LightenColor called: base={baseColor}, amount={lightnessAmount}, result={lighterColor}");
        
        return lighterColor;
    }
    
    public bool IsRegionUnlocked(int regionId)
    {
        if (!useRegionLocking) return true;
        return unlockedRegions.Contains(regionId);
    }
    
    IEnumerator SmoothFillRegion(int regionId, float targetProgress)
    {
        float startProgress = regionFillProgress[regionId];
        float elapsed = 0f;
        
        List<PixelFillData> regionPixels = regionPixelData[regionId];
        
        while (elapsed < fillTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fillTime);
            
            // Interpolate progress smoothly
            float currentProgress = Mathf.Lerp(startProgress, targetProgress, fillCurve.Evaluate(t));
            regionFillProgress[regionId] = currentProgress;
            
            // Calculate pixels to fill
            int pixelsToFill = Mathf.RoundToInt(regionPixels.Count * currentProgress);
            
            Color[] pixels = fillTexture.GetPixels();
            
            // Fill pixels up to the current progress with varied colors
            for (int i = 0; i < pixelsToFill && i < regionPixels.Count; i++)
            {
                var fillData = regionPixels[i];
                int pixelIndex = fillData.position.y * width + fillData.position.x;
                
                if (pixelIndex >= 0 && pixelIndex < pixels.Length)
                {
                    pixels[pixelIndex] = fillData.targetColor;
                }
            }
            
            fillTexture.SetPixels(pixels);
            fillTexture.Apply();
            
            yield return null;
        }
        
        // Ensure final progress is set
        regionFillProgress[regionId] = targetProgress;
    }
    
    public void StartFillAnimation()
    {
        if (isFilling) return;
        
        if (regionTool.regions == null || regionTool.regions.Count == 0)
        {
            Debug.LogWarning("No regions detected. Please detect regions first.");
            return;
        }
        
        StartCoroutine(FillRegionsWithSandEffect());
    }
    
    IEnumerator FillRegionsWithSandEffect()
    {
        isFilling = true;
        Debug.Log("Starting sand pouring effect...");
        
        // Create texture with base region colors visible from start
        fillTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        fillTexture.filterMode = FilterMode.Point;
        fillTexture.wrapMode = TextureWrapMode.Clamp;
        
        Color[] pixels = new Color[width * height];
        
        // Initialize with black background
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.black;
            
        // Draw border regions first
        if (regionTool.borderRegions != null)
        {
            foreach (var border in regionTool.borderRegions)
            {
                foreach (var pixel in border.pixels)
                {
                    int pixelIndex = pixel.y * width + pixel.x;
                    if (pixelIndex >= 0 && pixelIndex < pixels.Length)
                    {
                        pixels[pixelIndex] = border.color;
                    }
                }
            }
        }
        
        // Fill all regions with their base colors first
        int totalPixelsInRegions = 0;
        for (int regionIndex = 0; regionIndex < regionTool.regions.Count; regionIndex++)
        {
            var region = regionTool.regions[regionIndex];
            Debug.Log($"Region {regionIndex}: {region.pixels.Count} pixels, color: {region.color}");
            
            foreach (var pixel in region.pixels)
            {
                int pixelIndex = pixel.y * width + pixel.x;
                if (pixelIndex >= 0 && pixelIndex < pixels.Length)
                {
                    pixels[pixelIndex] = region.color;
                    totalPixelsInRegions++;
                }
            }
        }
        
        Debug.Log($"Total pixels in all regions: {totalPixelsInRegions}");
        
        fillTexture.SetPixels(pixels);
        fillTexture.Apply();
        
        // Update sprite renderer
        spriteRenderer.sprite = Sprite.Create(
            fillTexture,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            regionTool.sourceSprite.pixelsPerUnit
        );
        
        Debug.Log("Base colors applied, starting sand pouring animation...");
        
        // Wait a moment so user can see the base colors
        yield return new WaitForSeconds(0.5f);
        
        // Prepare all pixels with distance from bottom center for sand pouring effect
        List<PixelFillData> allPixels = new List<PixelFillData>();
        
        for (int regionIndex = 0; regionIndex < regionTool.regions.Count; regionIndex++)
        {
            var region = regionTool.regions[regionIndex];
            Vector2 bottomCenter = CalculateRegionBottomCenter(region);
            
            foreach (var pixel in region.pixels)
            {
                // Block coordinates for pixelated fill chunks
                float blockX = pixelationBlockSize > 1 ? Mathf.Floor(pixel.x / pixelationBlockSize) * pixelationBlockSize : pixel.x;
                float blockY = pixelationBlockSize > 1 ? Mathf.Floor(pixel.y / pixelationBlockSize) * pixelationBlockSize : pixel.y;
                
                // Calculate weighted distance for steeper slope
                float dx = Mathf.Abs(blockX - bottomCenter.x);
                float dy = Mathf.Abs(blockY - bottomCenter.y);
                
                // Weight vertical distance less to create steeper slope
                float noise = (pixelationNoise > 0f) ? Random.Range(-pixelationNoise, pixelationNoise) : 0f;
                float weightedDistance = dx + (dy / slopesteepness) + noise;
                
                PixelFillData fillData = new PixelFillData
                {
                    position = pixel,
                    distanceFromCenter = weightedDistance,
                    targetColor = GetVariedColor(region.color),
                    regionIndex = regionIndex
                };
                allPixels.Add(fillData);
            }
        }
        
        // Sort by distance from bottom center (closest first)
        allPixels = allPixels.OrderBy(p => p.distanceFromCenter).ToList();
        
        Debug.Log($"Prepared {allPixels.Count} pixels for sand pouring");
        Debug.Log($"First pixel color: {allPixels[0].targetColor}, Last pixel color: {allPixels[allPixels.Count-1].targetColor}");
        
        // FPS-independent filling using time-based progress
        float elapsed = 0f;
        int currentPixelIndex = 0;
        
        Debug.Log($"Fill time: {fillTime}s, Total pixels: {allPixels.Count}");
        
        // Now pour sand effect with color variations (FPS-independent)
        while (currentPixelIndex < allPixels.Count && elapsed < fillTime)
        {
            elapsed += Time.deltaTime;
            
            // Calculate how many pixels should be filled based on elapsed time
            float progress = Mathf.Clamp01(elapsed / fillTime);
            int targetPixelIndex = Mathf.RoundToInt(allPixels.Count * progress);
            int endIndex = Mathf.Min(targetPixelIndex, allPixels.Count);
            
            // Only update pixels if we have new ones to fill
            if (endIndex > currentPixelIndex)
            {
                // Get current pixels array
                Color[] currentPixels = fillTexture.GetPixels();
                
                // Update pixels with varied colors
                for (int i = currentPixelIndex; i < endIndex; i++)
                {
                    var fillData = allPixels[i];
                    int pixelIndex = fillData.position.y * width + fillData.position.x;
                    
                    if (pixelIndex >= 0 && pixelIndex < currentPixels.Length)
                    {
                        currentPixels[pixelIndex] = fillData.targetColor;
                    }
                }
                
                fillTexture.SetPixels(currentPixels);
                fillTexture.Apply();
                
                currentPixelIndex = endIndex;
            }
            
            yield return null;
        }
        
        // Ensure all pixels are filled at the end
        if (currentPixelIndex < allPixels.Count)
        {
            Color[] finalPixels = fillTexture.GetPixels();
            
            for (int i = currentPixelIndex; i < allPixels.Count; i++)
            {
                var fillData = allPixels[i];
                int pixelIndex = fillData.position.y * width + fillData.position.x;
                
                if (pixelIndex >= 0 && pixelIndex < finalPixels.Length)
                {
                    finalPixels[pixelIndex] = fillData.targetColor;
                }
            }
            
            fillTexture.SetPixels(finalPixels);
            fillTexture.Apply();
        }
        
        Debug.Log("Sand pouring effect complete!");
        isFilling = false;
    }
    
    Vector2 CalculateRegionCenter(SpriteRegionEditorTool.Region region)
    {
        if (region.pixels.Count == 0)
            return Vector2.zero;
        
        Vector2 sum = Vector2.zero;
        foreach (var pixel in region.pixels)
        {
            sum += new Vector2(pixel.x, pixel.y);
        }
        
        return sum / region.pixels.Count;
    }
    
    Vector2 CalculateRegionBottomCenter(SpriteRegionEditorTool.Region region)
    {
        if (region.pixels.Count == 0)
            return Vector2.zero;
        
        // Find the bottom-most Y coordinate and average X coordinate
        float minY = float.MaxValue;
        float sumX = 0;
        int count = 0;
        
        foreach (var pixel in region.pixels)
        {
            if (pixel.y < minY)
            {
                minY = pixel.y;
            }
            sumX += pixel.x;
            count++;
        }
        
        float avgX = sumX / count;
        
        return new Vector2(avgX, minY);
    }
    
    Color GetVariedColor(Color baseColor)
    {
        // Convert to HSV for better color variation
        Color.RGBToHSV(baseColor, out float h, out float s, out float v);
        
        // Vary the value (brightness) to create visible lighter/darker shades
        // Keep variation closer to original color
        float variation = Random.Range(-colorVariation * 0.5f, colorVariation * 0.5f);
        v = Mathf.Clamp01(v + variation);
        
        // Slightly vary saturation for more natural look (less variation)
        float satVariation = Random.Range(-colorVariation * 0.2f, colorVariation * 0.2f);
        s = Mathf.Clamp01(s + satVariation);
        
        // Very slight hue variation to keep color similar
        float hueVariation = Random.Range(-0.01f, 0.01f);
        h = Mathf.Repeat(h + hueVariation, 1f);
        
        Color result = Color.HSVToRGB(h, s, v);
        result.a = 1f; // Ensure full opacity
        
        return result;
    }
    
    // Public method to restart the animation
    public void RestartAnimation()
    {
        if (isFilling)
        {
            StopAllCoroutines();
            isFilling = false;
        }
        
        StartFillAnimation();
    }
    
    // Public methods for checking actual sand fill progress
    public float GetRegionFillProgress(int regionIndex)
    {
        if (regionFillProgress.ContainsKey(regionIndex))
        {
            return regionFillProgress[regionIndex];
        }
        return 0f;
    }
    
    public bool IsRegionCompletelyFilled(int regionIndex)
    {
        return GetRegionFillProgress(regionIndex) >= 1f;
    }
    
    public int GetTotalRegionsCount()
    {
        return regionTool != null ? regionTool.regions.Count : 0;
    }
    
    public int GetCompletelyFilledRegionsCount()
    {
        int count = 0;
        for (int i = 0; i < GetTotalRegionsCount(); i++)
        {
            if (IsRegionCompletelyFilled(i))
            {
                count++;
            }
        }
        return count;
    }
    
    public bool IsAllRegionsCompletelyFilled()
    {
        int totalRegions = GetTotalRegionsCount();
        if (totalRegions == 0) return false;
        
        return GetCompletelyFilledRegionsCount() >= totalRegions;
    }
    
    public float GetOverallFillProgress()
    {
        int totalRegions = GetTotalRegionsCount();
        if (totalRegions == 0) return 0f;
        
        float totalProgress = 0f;
        for (int i = 0; i < totalRegions; i++)
        {
            totalProgress += GetRegionFillProgress(i);
        }
        
        return totalProgress / totalRegions;
    }
}
