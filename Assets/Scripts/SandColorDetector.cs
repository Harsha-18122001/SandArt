using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class SandColorDetector : MonoBehaviour
{
    [System.Serializable]
    public class RegionFillData
    {
        public int regionId;
        public string colorName; // Reference to ColorMaterialLibrary color name
        public int piecesNeededToFill = 10;
        [HideInInspector] public int piecesCollected = 0;
        [HideInInspector] public float fillProgress = 0f; // 0 to 1
    }
    
    [Header("References")]
    [SerializeField] private SpriteRegionEditorTool regionTool;
    [SerializeField] private SandPouringFillEffect fillEffect;
    [SerializeField] private ColorMaterialLibrary colorLibrary;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject particleEffectObject; // Particle system to enable when detecting
    [SerializeField] private GameObject pouringParticlePrefab; // Particle to spawn and move to region
    [SerializeField] private float particleMoveSpeed = 5f; // Speed of particle movement
    [SerializeField] private int particlesPerPiece = 2; // Number of particles spawned per sand piece
    [SerializeField] private float topPixelOffset = 0.2f; // Offset from top (0 = very top, higher = more downward)
    
    [Header("Region Fill Settings")]
    [SerializeField] private List<RegionFillData> regionFillSettings = new List<RegionFillData>();
    
    [Header("Detection Settings")]
    [SerializeField] private float colorMatchThreshold = 0.1f; // How close colors need to be to match
    [SerializeField] private bool debugMode = true;
    
    private Dictionary<int, RegionFillData> regionDataMap = new Dictionary<int, RegionFillData>();
    private float lastDetectionTime = 0f;
    private float particleDisableDelay = 0.5f; // Time to wait before disabling particles after last detection
    private int activePouringParticles = 0; // Track number of particles still moving
    
    void Start()
    {
        // Ensure this has a trigger collider
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        
        // Validate color library
        if (colorLibrary == null)
        {
            Debug.LogError("ColorMaterialLibrary not assigned to SandColorDetector!");
            return;
        }
        
        // Build region data map
        foreach (var data in regionFillSettings)
        {
            if (!regionDataMap.ContainsKey(data.regionId))
            {
                regionDataMap.Add(data.regionId, data);
            }
        }
        
        // Ensure particle effect is disabled at start
        if (particleEffectObject != null)
        {
            particleEffectObject.SetActive(false);
        }
        
        if (debugMode)
        {
            Debug.Log($"<color=orange>Sand Color Detector initialized with {regionFillSettings.Count} regions</color>");
        }
    }
    
    void Update()
    {
        // Only disable particle effect if:
        // 1. No recent detections (after delay)
        // 2. AND all pouring particles have finished
        if (particleEffectObject != null && particleEffectObject.activeSelf)
        {
            bool noRecentDetections = Time.time - lastDetectionTime > particleDisableDelay;
            bool allPouringParticlesFinished = activePouringParticles == 0;
            
            if (noRecentDetections && allPouringParticlesFinished)
            {
                particleEffectObject.SetActive(false);
                if (debugMode)
                {
                    Debug.Log("<color=grey>Particle effect disabled (no recent detections and all pouring particles finished)</color>");
                }
            }
            else if (debugMode && activePouringParticles > 0)
            {
                // Keep effect active while particles are still moving
                if (Time.frameCount % 60 == 0) // Log every 60 frames
                {
                    Debug.Log($"<color=cyan>Keeping particle effect active. Active pouring particles: {activePouringParticles}</color>");
                }
            }
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        // Check if it's a sand piece
        if (other.CompareTag("SandPiece") || other.name.Contains("SandPiece"))
        {
            ProcessSandPiece(other.gameObject);
        }
    }
    
    void ProcessSandPiece(GameObject sandPiece)
    {
        // Get the color name from the sand piece identifier
        SandColorIdentifier identifier = sandPiece.GetComponent<SandColorIdentifier>();
        if (identifier == null || !identifier.HasColorName())
        {
            if (debugMode) Debug.LogWarning($"Sand piece {sandPiece.name} has no SandColorIdentifier or color name!");
            return;
        }
        
        string sandColorName = identifier.GetColorName();
        
        if (debugMode)
        {
            Debug.Log($"<color=cyan>Detected sand piece with color name: {sandColorName}</color>");
        }
        
        // Find matching region by color name
        RegionFillData matchedRegion = FindMatchingRegionByName(sandColorName);
        
        if (matchedRegion != null)
        {
            // Check if region is unlocked
            if (fillEffect != null && !fillEffect.IsRegionUnlocked(matchedRegion.regionId))
            {
                if (debugMode)
                {
                    Debug.Log($"<color=orange>Region {matchedRegion.regionId} is locked! Sand piece ignored.</color>");
                }
                return; // Don't process locked regions
            }
            
            // Get the color from the color library
            Color sandColor = colorLibrary.GetColorByName(sandColorName);
            
            // Enable particle effect and set its color
            if (particleEffectObject != null)
            {
                if (!particleEffectObject.activeSelf)
                {
                    particleEffectObject.SetActive(true);
                    if (debugMode)
                    {
                        Debug.Log("<color=magenta>Particle effect enabled (matching color detected)</color>");
                    }
                }
                
                // Update particle system color
                SetParticleColor(particleEffectObject, sandColor);
            }
            
            // Update last detection time
            lastDetectionTime = Time.time;
            
            // Destroy the sand piece
            Destroy(sandPiece);
            
            // Spawn pouring particles moving to region
            if (pouringParticlePrefab != null && fillEffect != null)
            {
                Vector3 regionTopCenter = GetRegionTopCenter(matchedRegion.regionId);
                SpawnPouringParticles(transform.position, regionTopCenter, sandColor, particlesPerPiece);
            }
            
            // Increment pieces collected
            matchedRegion.piecesCollected++;
            matchedRegion.fillProgress = Mathf.Clamp01((float)matchedRegion.piecesCollected / matchedRegion.piecesNeededToFill);
            
            if (debugMode)
            {
                Debug.Log($"<color=green>Region {matchedRegion.regionId} ({matchedRegion.colorName}) collected piece {matchedRegion.piecesCollected}/{matchedRegion.piecesNeededToFill} ({matchedRegion.fillProgress * 100f:F1}%)</color>");
            }
            
            // Update region fill
            if (fillEffect != null)
            {
                fillEffect.UpdateRegionFill(matchedRegion.regionId, matchedRegion.fillProgress);
            }
            
            // Check if region is complete
            if (matchedRegion.piecesCollected >= matchedRegion.piecesNeededToFill)
            {
                if (debugMode)
                {
                    Debug.Log($"<color=yellow>Region {matchedRegion.regionId} ({matchedRegion.colorName}) is now COMPLETE!</color>");
                }
                
                // Notify fill effect that region is completed (for unlocking next region)
                if (fillEffect != null)
                {
                    fillEffect.OnRegionCompleted(matchedRegion.regionId);
                }
            }
        }
        else
        {
            if (debugMode)
            {
                // Check if the color exists but all regions are full
                bool colorExists = false;
                foreach (var region in regionFillSettings)
                {
                    if (string.Equals(region.colorName, sandColorName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        colorExists = true;
                        break;
                    }
                }

                if (colorExists)
                {
                    Debug.Log($"<color=orange>All regions for color '{sandColorName}' are already FULL. Sand piece ignored.</color>");
                }
                else
                {
                    Debug.Log($"<color=red>No matching region found for color name '{sandColorName}'. Make sure to click 'Auto-Populate Regions' on SandColorDetector if you've updated your regions!</color>");
                }
            }
            
            // Always destroy the sand piece if it reaches the detector but can't be matched/filled
            Destroy(sandPiece);
        }
    }
    
    void SetParticleColor(GameObject particleObject, Color color)
    {
        // Get all particle systems (including children)
        ParticleSystem[] particleSystems = particleObject.GetComponentsInChildren<ParticleSystem>();
        
        foreach (ParticleSystem ps in particleSystems)
        {
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(color);
            
            if (debugMode)
            {
                Debug.Log($"<color=magenta>Set particle system '{ps.name}' color to {color}</color>");
            }
        }
    }
    
    void SpawnPouringParticles(Vector3 startPos, Vector3 targetPos, Color color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject particle = Instantiate(pouringParticlePrefab, startPos, Quaternion.identity);
            
            // Set particle color
            Renderer renderer = particle.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }
            
            // Set particle system color if it has one
            ParticleSystem ps = particle.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(color);
            }
            
            // Add slight random offset to start position
            Vector3 randomOffset = new Vector3(
                Random.Range(-0.2f, 0.2f),
                Random.Range(-0.1f, 0.1f),
                Random.Range(-0.2f, 0.2f)
            );
            particle.transform.position += randomOffset;
            
            // Increment active particle count
            activePouringParticles++;
            
            // Start movement coroutine
            StartCoroutine(MoveParticleToTarget(particle, startPos + randomOffset, targetPos));
        }
        
        if (debugMode)
        {
            Debug.Log($"<color=cyan>Spawned {count} pouring particles. Active particles: {activePouringParticles}</color>");
        }
    }
    
    System.Collections.IEnumerator MoveParticleToTarget(GameObject particle, Vector3 startPos, Vector3 targetPos)
    {
        if (particle == null)
        {
            activePouringParticles--;
            yield break;
        }
        
        float elapsed = 0f;
        float duration = Vector3.Distance(startPos, targetPos) / particleMoveSpeed;
        
        if (debugMode)
        {
            Debug.Log($"<color=cyan>Particle starting journey. Duration: {duration:F2}s, Distance: {Vector3.Distance(startPos, targetPos):F2}</color>");
        }
        
        while (elapsed < duration)
        {
            if (particle == null)
            {
                activePouringParticles--;
                yield break;
            }
            
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            
            // Simple linear interpolation - straight line movement
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, t);
            particle.transform.position = currentPos;
            
            yield return null;
        }
        
        // Particle reached target - turn off particle system(s)
        if (particle != null)
        {
            particle.transform.position = targetPos;
            
            if (debugMode)
            {
                Debug.Log($"<color=green>Particle reached target at {targetPos}. Stopping particle system.</color>");
            }
            
            // Stop all particle systems on this particle
            ParticleSystem[] particleSystems = particle.GetComponentsInChildren<ParticleSystem>();
            foreach (ParticleSystem ps in particleSystems)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            
            // Destroy the GameObject after particles have finished playing
            // Wait for the longest particle lifetime
            float maxLifetime = 0f;
            foreach (ParticleSystem ps in particleSystems)
            {
                float lifetime = ps.main.startLifetime.constantMax;
                if (lifetime > maxLifetime)
                {
                    maxLifetime = lifetime;
                }
            }
            
            // Destroy after particles fade out
            Destroy(particle, maxLifetime + 0.5f);
        }
        
        // Decrement active particle count
        activePouringParticles--;
        
        if (debugMode)
        {
            Debug.Log($"<color=grey>Pouring particle system stopped. Remaining active: {activePouringParticles}</color>");
        }
    }
    
    Vector3 GetRegionTopCenter(int regionId)
    {
        if (regionTool == null || regionId >= regionTool.regions.Count)
        {
            Debug.LogError($"Invalid region ID: {regionId}");
            return transform.position;
        }
        
        var region = regionTool.regions[regionId];
        
        if (region.pixels.Count == 0)
        {
            Debug.LogError($"Region {regionId} has no pixels!");
            return transform.position;
        }
        
        // Find the topmost Y coordinate
        int maxY = int.MinValue;
        foreach (var pixel in region.pixels)
        {
            if (pixel.y > maxY)
            {
                maxY = pixel.y;
            }
        }
        
        // Find all pixels at the topmost row and get their center X
        int sumX = 0;
        int count = 0;
        foreach (var pixel in region.pixels)
        {
            if (pixel.y == maxY)
            {
                sumX += pixel.x;
                count++;
            }
        }
        
        if (count == 0)
        {
            Debug.LogError($"No pixels found at top row for region {regionId}!");
            return transform.position;
        }
        
        // Center X of the topmost pixels
        float centerX = (float)sumX / count;
        float targetY = maxY;
        
        // Get sprite renderer and convert pixel to world coordinates
        SpriteRenderer spriteRenderer = regionTool.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            Debug.LogError("SpriteRenderer or sprite not found!");
            return regionTool.transform.position;
        }
        
        Sprite sprite = spriteRenderer.sprite;
        float pixelsPerUnit = sprite.pixelsPerUnit;
        
        // CORRECT METHOD: Use sprite's rect and pivot for accurate conversion
        Rect spriteRect = sprite.rect;
        Vector2 spritePivot = sprite.pivot;
        
        // Convert pixel coordinates relative to sprite's rect (not texture)
        float localPixelX = centerX - spriteRect.x;
        float localPixelY = targetY - spriteRect.y;
        
        // Convert to local sprite coordinates (relative to pivot)
        float localX = (localPixelX - spritePivot.x) / pixelsPerUnit;
        float localY = (localPixelY - spritePivot.y) / pixelsPerUnit;
        
        // Transform to world coordinates using the sprite's transform
        Vector3 localPos = new Vector3(localX, localY, 0);
        Vector3 worldPos = regionTool.transform.TransformPoint(localPos);
        
        // Apply the top pixel offset (move slightly down from the very top)
        worldPos.y -= topPixelOffset;
        
        if (debugMode)
        {
            Debug.Log($"<color=yellow>Region {regionId}: TopPixel({centerX:F1}, {targetY}) Count:{count}</color>");
            Debug.Log($"<color=yellow>SpriteRect: {spriteRect}, Pivot: {spritePivot}, PPU: {pixelsPerUnit}</color>");
            Debug.Log($"<color=yellow>LocalPixel: ({localPixelX:F1}, {localPixelY:F1}) -> LocalPos: {localPos} -> WorldPos: {worldPos}</color>");
        }
        
        return worldPos;
    }
    
    RegionFillData FindMatchingRegionByName(string sandColorName)
    {
        foreach (var regionData in regionFillSettings)
        {
            // Match by color name (case-insensitive)
            if (string.Equals(regionData.colorName, sandColorName, System.StringComparison.OrdinalIgnoreCase))
            {
                // Skip if region is already full
                if (regionData.piecesCollected >= regionData.piecesNeededToFill)
                {
                    continue;
                }
                
                return regionData;
            }
        }
        
        return null;
    }
    
    // Legacy methods - no longer used but kept for compatibility
    Material GetSandPieceMaterial(GameObject sandPiece)
    {
        // Check child MeshRenderers
        MeshRenderer[] renderers = sandPiece.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length > 0 && renderers[0].material != null)
        {
            return renderers[0].material;
        }
        
        // Check direct MeshRenderer
        MeshRenderer renderer = sandPiece.GetComponent<MeshRenderer>();
        if (renderer != null && renderer.material != null)
        {
            return renderer.material;
        }
        
        return null;
    }
    
    RegionFillData FindMatchingRegion(Color sandColor)
    {
        RegionFillData closestMatch = null;
        float closestDistance = float.MaxValue;
        
        foreach (var regionData in regionFillSettings)
        {
            // Skip if region is already full
            if (regionData.piecesCollected >= regionData.piecesNeededToFill)
            {
                continue;
            }
            
            // Get the target color from the color library
            Color targetColor = colorLibrary.GetColorByName(regionData.colorName);
            
            // Calculate color distance
            float distance = ColorDistance(sandColor, targetColor);
            
            if (distance < colorMatchThreshold && distance < closestDistance)
            {
                closestDistance = distance;
                closestMatch = regionData;
            }
        }
        
        return closestMatch;
    }
    
    float ColorDistance(Color a, Color b)
    {
        // Calculate Euclidean distance in RGB space
        float dr = a.r - b.r;
        float dg = a.g - b.g;
        float db = a.b - b.b;
        return Mathf.Sqrt(dr * dr + dg * dg + db * db);
    }
    
    public void AutoPopulateRegions()
    {
        if (regionTool == null)
        {
            Debug.LogError("Region Tool not assigned!");
            return;
        }
        
        if (colorLibrary == null)
        {
            Debug.LogError("Color Library not assigned!");
            return;
        }
        
        // Map of current progress by region ID to preserve it
        Dictionary<int, int> currentProgress = new Dictionary<int, int>();
        foreach (var data in regionFillSettings)
        {
            if (!currentProgress.ContainsKey(data.regionId))
            {
                currentProgress.Add(data.regionId, data.piecesCollected);
            }
        }
        
        regionFillSettings.Clear();
        
        for (int i = 0; i < regionTool.regions.Count; i++)
        {
            var region = regionTool.regions[i];
            
            // Try to restore previous progress for this region index
            int collected = 0;
            currentProgress.TryGetValue(i, out collected);
            
            RegionFillData fillData = new RegionFillData
            {
                regionId = i,
                colorName = region.colorName, // Auto-fill from region's color name
                piecesNeededToFill = CalculatePiecesNeeded(region.PixelCount),
                piecesCollected = collected
            };
            
            // Recalculate progress ratio
            fillData.fillProgress = Mathf.Clamp01((float)fillData.piecesCollected / fillData.piecesNeededToFill);
            
            regionFillSettings.Add(fillData);
        }
        
        Debug.Log($"Auto-populated {regionFillSettings.Count} regions with color names from SpriteRegionEditorTool. Preserved progress for matching region IDs.");
    }
    
    int CalculatePiecesNeeded(int pixelCount)
    {
        // Calculate based on region size
        // Adjust this formula as needed
        return Mathf.Max(1, pixelCount / 100); // 1 piece per 100 pixels
    }
    
    // Reset all region progress
    public void ResetAllRegions()
    {
        foreach (var data in regionFillSettings)
        {
            data.piecesCollected = 0;
            data.fillProgress = 0f;
        }
        
        Debug.Log("All regions reset");
    }
}
