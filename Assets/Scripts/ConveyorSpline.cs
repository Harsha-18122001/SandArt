using UnityEngine;
using System.Collections.Generic;

public class ConveyorSpline : MonoBehaviour
{
    [Header("Spline Settings")]
    [SerializeField] private List<Transform> splinePoints = new List<Transform>();
    [SerializeField] private float moveSpeed = 2f; // Units per second (constant speed)
    [SerializeField] private bool closedLoop = true;
    [SerializeField] private bool useSmoothCurve = true;
    
    [Header("Sand Piece Settings")]
    [SerializeField] private GameObject sandPiecePrefab;
    [SerializeField] private float sandPieceHeightOffset = 0.5f;
    [SerializeField] private Vector3 sandPieceRotationOffset = Vector3.zero; // Rotation offset for sand pieces (X, Y, Z degrees)
    [SerializeField] private float distanceBetweenPieces = 1f; // Distance between spawned pieces in world units
    [SerializeField] private float sandPieceRadius = 0.5f; // Collision radius for sand pieces
    [SerializeField] private bool enableCollisionDetection = true; // Enable/disable collision system
    
    [Header("Particle Effects")]
    [SerializeField] private GameObject spawnParticleEffectPrefab; // Particle effect to enable when sand pieces are spawning
    
    [Header("Sand Cube Grinding")]
    [SerializeField] private float cubeHeightOffset = 1f;
    [SerializeField] private Vector3 cubePositionOffset = Vector3.zero;
    
    private List<SandPieceOnSpline> activeSandPieces = new List<SandPieceOnSpline>();
    private List<GrindingCube> grindingCubes = new List<GrindingCube>();
    private float cachedSplineLength = -1f;
    private float[] arcLengthTable;
    private int arcLengthSamples = 200;
    private float globalSplineTime = 0f; // Global time tracker for spawning (FPS independent)
    private Dictionary<int, float> cubeLastSpawnTime = new Dictionary<int, float>(); // Track last spawn time per cube
    
    void Start()
    {
        // Auto-detect spline points if not set
        if (splinePoints.Count == 0)
        {
            foreach (Transform child in transform)
            {
                splinePoints.Add(child);
            }
        }
        
        // Build arc length table for constant speed
        BuildArcLengthTable();
        cachedSplineLength = arcLengthTable[arcLengthTable.Length - 1];
    }
    
    void BuildArcLengthTable()
    {
        arcLengthTable = new float[arcLengthSamples + 1];
        arcLengthTable[0] = 0f;
        
        Vector3 prevPoint = GetPointOnSplineRaw(0f);
        float totalLength = 0f;
        
        for (int i = 1; i <= arcLengthSamples; i++)
        {
            float t = i / (float)arcLengthSamples;
            Vector3 currentPoint = GetPointOnSplineRaw(t);
            totalLength += Vector3.Distance(prevPoint, currentPoint);
            arcLengthTable[i] = totalLength;
            prevPoint = currentPoint;
        }
    }
    
    float GetTFromDistance(float distance)
    {
        // Wrap distance for closed loop
        if (closedLoop && cachedSplineLength > 0)
        {
            distance = distance % cachedSplineLength;
            if (distance < 0) distance += cachedSplineLength;
        }
        else
        {
            distance = Mathf.Clamp(distance, 0, cachedSplineLength);
        }
        
        // Binary search in arc length table
        for (int i = 0; i < arcLengthTable.Length - 1; i++)
        {
            if (distance >= arcLengthTable[i] && distance <= arcLengthTable[i + 1])
            {
                float segmentLength = arcLengthTable[i + 1] - arcLengthTable[i];
                if (segmentLength < 0.0001f) return i / (float)arcLengthSamples;
                
                float localT = (distance - arcLengthTable[i]) / segmentLength;
                float t = (i + localT) / arcLengthSamples;
                return t;
            }
        }
        
        return 1f;
    }
    
    void Update()
    {
        // Increment global time tracker (FPS-independent)
        globalSplineTime += Time.deltaTime;
        
        // Calculate spawn interval in seconds
        float spawnIntervalTime = distanceBetweenPieces / moveSpeed;
        
        // Check each grinding cube and spawn if needed
        for (int i = grindingCubes.Count - 1; i >= 0; i--)
        {
            if (grindingCubes[i].cubeTransform == null)
            {
                // Cleanup: Stop particle coroutine and destroy particle effect
                if (grindingCubes[i].particleCoroutine != null)
                {
                    StopCoroutine(grindingCubes[i].particleCoroutine);
                }
                if (grindingCubes[i].particleEffect != null)
                {
                    grindingCubes[i].particleEffect.SetActive(false);
                    Destroy(grindingCubes[i].particleEffect);
                }
                
                // Remove from spawn time tracking
                cubeLastSpawnTime.Remove(i);
                grindingCubes.RemoveAt(i);
                continue;
            }
            
            GrindingCube cube = grindingCubes[i];
            
            // Check if this cube can and should spawn
            if (cube.grinder != null && cube.grinder.CanSpawnPiece())
            {
                bool canActuallySpawn = true;
                
                // Check if spline is full
                if (IsSplineFull())
                {
                    canActuallySpawn = false;
                }
                
                // Check if there's space at spawn position
                if (canActuallySpawn && !CanSpawnNewPiece(cube.splinePosition))
                {
                    canActuallySpawn = false;
                }
                
                // Only proceed with spawning if we can actually spawn
                if (!canActuallySpawn)
                {
                    continue;
                }
                
                // Check if enough time has passed since last spawn for this cube
                float lastSpawnTime = cubeLastSpawnTime.ContainsKey(i) ? cubeLastSpawnTime[i] : 0f;
                float timeSinceLastSpawn = globalSplineTime - lastSpawnTime;
                
                if (timeSinceLastSpawn >= spawnIntervalTime)
                {
                    // Spawn the piece with the cube's material and color name
                    SpawnSandPieceOnSpline(cube.splinePosition, cube.cubeTransform.name, cube.cubeMaterial, cube.colorName);
                    cube.grinder.IncrementPiecesSpawned();
                    
                    // Trigger particle burst for 1 second
                    TriggerParticleBurst(cube);
                    
                    // Update last spawn time for this cube
                    cubeLastSpawnTime[i] = globalSplineTime;
                    
                    Debug.Log($"<color=green>[Time {globalSplineTime:F2}] Spawned piece {cube.grinder.GetPiecesSpawned()}/{cube.grinder.totalSandPieces} from {cube.cubeTransform.name}. Next in {spawnIntervalTime:F2}s</color>");
                }
            }
            else
            {
                // Cube finished spawning - no more particle bursts needed
                if (cube.particleCoroutine != null)
                {
                    StopCoroutine(cube.particleCoroutine);
                    cube.particleCoroutine = null;
                }
                if (cube.particleEffect != null && cube.particleEffect.activeSelf)
                {
                    cube.particleEffect.SetActive(false);
                    Debug.Log($"<color=grey>Stopped particle effects for {cube.cubeTransform.name} (finished spawning)</color>");
                }
            }
        }
        
        // Update sand pieces on the spline with collision detection
        UpdateSandPiecesWithCollision();
    }
    
    void SpawnFromCube(GrindingCube cube)
    {
        // This method is no longer used
    }
    
    public void AttachCube(Transform cubeTransform, Vector3 collisionPoint)
    {
        // Find the closest point on the spline
        float closestT = FindClosestPointOnSpline(collisionPoint);
        
        // Get the cube's material (from MeshRenderer, excluding SpriteRenderer)
        Material cubeMaterial = null;
        MeshRenderer[] meshRenderers = cubeTransform.GetComponentsInChildren<MeshRenderer>();
        if (meshRenderers.Length > 0)
        {
            cubeMaterial = meshRenderers[0].sharedMaterial;
            
            // Disable outline on the cube when it gets attached
            foreach (MeshRenderer renderer in meshRenderers)
            {
                DisableOutline(renderer.material);
            }
            Debug.Log($"<color=cyan>Disabled outline on cube {cubeTransform.name}</color>");
        }
        
        // Get the cube's color name
        string colorName = "";
        SandColorIdentifier cubeIdentifier = cubeTransform.GetComponent<SandColorIdentifier>();
        if (cubeIdentifier != null)
        {
            colorName = cubeIdentifier.GetColorName();
        }
        
        // Start grinding the cube at the collision point
        SandCubeGrinder grinder = cubeTransform.GetComponent<SandCubeGrinder>();
        if (grinder != null)
        {
            // Calculate collision normal (pointing away from spline)
            Vector3 splinePoint = GetPointOnSplineRaw(closestT);
            Vector3 collisionNormal = (collisionPoint - splinePoint).normalized;
            
            grinder.StartGrinding(collisionPoint, collisionNormal);
        }
        
        // Create particle effect at grinding position (initially disabled)
        GameObject particleEffect = null;
        if (spawnParticleEffectPrefab != null)
        {
            Vector3 particlePos = GetPointOnSplineRaw(closestT) + Vector3.up * sandPieceHeightOffset;
            particleEffect = Instantiate(spawnParticleEffectPrefab, particlePos, Quaternion.identity);
            particleEffect.SetActive(false); // Start disabled - will burst when spawning
            
            // Set particle color to match cube
            if (cubeMaterial != null)
            {
                SetParticleEffectColor(particleEffect, cubeMaterial.color);
            }
            
            Debug.Log($"<color=magenta>Created particle effect for {cubeTransform.name} (ready for bursts)</color>");
        }
        
        // Schedule first spawn immediately (at current global time)
        GrindingCube grindingCube = new GrindingCube
        {
            cubeTransform = cubeTransform,
            grinder = grinder,
            splinePosition = closestT,
            fixedPosition = collisionPoint, // Store the fixed position
            cubeMaterial = cubeMaterial, // Store material for spawned pieces
            colorName = colorName, // Store color name for spawned pieces
            particleEffect = particleEffect, // Store particle effect reference
            particleCoroutine = null // Initialize coroutine reference
        };
        
        grindingCubes.Add(grindingCube);
        
        // Initialize spawn time tracking for this cube
        int cubeIndex = grindingCubes.Count - 1;
        cubeLastSpawnTime[cubeIndex] = globalSplineTime; // Allow immediate first spawn
        
        // Disable the cube's movement script
        SandCubeMovement movement = cubeTransform.GetComponent<SandCubeMovement>();
        if (movement != null)
        {
            movement.enabled = false;
        }
        
        // Disable rigidbody if present to prevent physics movement
        Rigidbody rb = cubeTransform.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Only set velocities if not already kinematic
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
        }
        
        Debug.Log($"<color=yellow>Cube {cubeTransform.name} attached. Will spawn {grinder.totalSandPieces} pieces every {distanceBetweenPieces / moveSpeed:F2} seconds. Color: {colorName}</color>");
    }
    
    void UpdateGrindingCube(GrindingCube grindingCube)
    {
        // This method is no longer used - spawning is handled in Update()
    }
    

    Vector3 GetPointOnSplineRaw(float t)
    {
        return GetPointOnSpline(t);
    }
    
    void SpawnSandPieceOnSpline(float splinePosition, string cubeName, Material material, string colorName)
    {
        if (sandPiecePrefab == null) return;
        
        // Count pieces BEFORE spawning
        int countBefore = activeSandPieces.Count;
        
        Vector3 spawnPos = GetPointOnSplineRaw(splinePosition) + Vector3.up * sandPieceHeightOffset;
        
        // Get the initial rotation based on spline direction at spawn position
        Vector3 spawnDirection = GetTangentOnSpline(splinePosition);
        Quaternion spawnRotation = Quaternion.identity;
        if (spawnDirection != Vector3.zero)
        {
            spawnRotation = Quaternion.LookRotation(spawnDirection, Vector3.up);
            // Apply rotation offset
            spawnRotation *= Quaternion.Euler(sandPieceRotationOffset);
        }
        
        GameObject sandPiece = Instantiate(sandPiecePrefab, spawnPos, spawnRotation);
        
        // Verify only ONE object was created
        if (sandPiece == null)
        {
            Debug.LogError("<color=red>Failed to instantiate sand piece!</color>");
            return;
        }
        
        // Apply material to all child MeshRenderers (7 child objects)
        if (material != null)
        {
            MeshRenderer[] childRenderers = sandPiece.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in childRenderers)
            {
                renderer.material = material;
                
                // Disable outline if the material has outline properties
                DisableOutline(renderer.material);
            }
        }
        
        // Add and set color identifier
        SandColorIdentifier identifier = sandPiece.GetComponent<SandColorIdentifier>();
        if (identifier == null)
        {
            identifier = sandPiece.AddComponent<SandColorIdentifier>();
        }
        identifier.SetColorName(colorName);
        
        // Convert spline position to distance
        float spawnDistance = splinePosition * cachedSplineLength;
        
        SandPieceOnSpline pieceData = new SandPieceOnSpline
        {
            pieceTransform = sandPiece.transform,
            distanceTraveled = spawnDistance,
            isBlocked = false,
            targetSpeed = moveSpeed
        };
        
        activeSandPieces.Add(pieceData);
        
        // Count pieces AFTER spawning
        int countAfter = activeSandPieces.Count;
        int piecesAdded = countAfter - countBefore;
        
        if (piecesAdded != 1)
        {
            Debug.LogError($"<color=red>ERROR: Expected to add 1 piece, but added {piecesAdded}! Before: {countBefore}, After: {countAfter}</color>");
        }
        
        Debug.Log($"<color=cyan>[Frame {Time.frameCount}] Created sand piece #{countAfter} from {cubeName} with color '{colorName}' facing spline direction</color>");
    }
    
    void TriggerParticleBurst(GrindingCube cube)
    {
        if (cube.particleEffect == null) return;
        
        // Stop any existing particle coroutine
        if (cube.particleCoroutine != null)
        {
            StopCoroutine(cube.particleCoroutine);
        }
        
        // Start new particle burst
        cube.particleCoroutine = StartCoroutine(ParticleBurstCoroutine(cube));
    }
    
    System.Collections.IEnumerator ParticleBurstCoroutine(GrindingCube cube)
    {
        if (cube.particleEffect != null)
        {
            // Enable particle effect
            cube.particleEffect.SetActive(true);
            Debug.Log($"<color=cyan>Particle burst started for {cube.cubeTransform.name}</color>");
            
            // Wait for 1 second
            yield return new WaitForSeconds(1f);
            
            // Disable particle effect
            cube.particleEffect.SetActive(false);
            Debug.Log($"<color=grey>Particle burst ended for {cube.cubeTransform.name}</color>");
        }
        
        // Clear coroutine reference
        cube.particleCoroutine = null;
    }
    
    void DisableOutline(Material material)
    {
        if (material == null) return;
        
        // Common outline property names in different shaders
        string[] outlineProperties = {
            "_OutlineWidth",
            "_Outline",
            "_OutlineColor",
            "_OutlineSize",
            "_OutlineThickness",
            "_EnableOutline",
            "_UseOutline"
        };
        
        foreach (string property in outlineProperties)
        {
            if (material.HasProperty(property))
            {
                // Try to disable outline by setting width/size to 0
                if (property.Contains("Width") || property.Contains("Size") || property.Contains("Thickness"))
                {
                    material.SetFloat(property, 0f);
                    Debug.Log($"Disabled outline property: {property} on material {material.name}");
                }
                // Try to disable outline by setting enable flags to false
                else if (property.Contains("Enable") || property.Contains("Use"))
                {
                    material.SetFloat(property, 0f); // 0 = false
                    Debug.Log($"Disabled outline flag: {property} on material {material.name}");
                }
                // Set outline color to transparent
                else if (property.Contains("Color"))
                {
                    material.SetColor(property, Color.clear);
                    Debug.Log($"Set outline color to transparent: {property} on material {material.name}");
                }
            }
        }
        
        // Special handling for Toony Colors Pro shader
        if (material.shader.name.Contains("Toony") || material.shader.name.Contains("TCP"))
        {
            if (material.HasProperty("_TCP2_OUTLINE"))
            {
                material.SetFloat("_TCP2_OUTLINE", 0f);
                Debug.Log("Disabled TCP2 outline on material " + material.name);
            }
        }
    }
    
    void SetParticleEffectColor(GameObject particleObject, Color color)
    {
        // Get all particle systems (including children)
        ParticleSystem[] particleSystems = particleObject.GetComponentsInChildren<ParticleSystem>();
        
        foreach (ParticleSystem ps in particleSystems)
        {
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(color);
        }
    }
    
    void UpdateSandPieceOnSpline(SandPieceOnSpline pieceData)
    {
        // Fallback method for simple movement without collision detection
        UpdateSandPieceOnSplineWithCollision(pieceData);
    }
    
    void UpdateSandPiecesWithCollision()
    {
        // Clean up null references
        for (int i = activeSandPieces.Count - 1; i >= 0; i--)
        {
            if (activeSandPieces[i].pieceTransform == null)
            {
                activeSandPieces.RemoveAt(i);
            }
        }
        
        if (!enableCollisionDetection)
        {
            // Simple update without collision detection
            foreach (var piece in activeSandPieces)
            {
                UpdateSandPieceOnSpline(piece);
            }
            return;
        }
        
        // Sort pieces by distance traveled (furthest first)
        activeSandPieces.Sort((a, b) => b.distanceTraveled.CompareTo(a.distanceTraveled));
        
        // Update collision states
        for (int i = 0; i < activeSandPieces.Count; i++)
        {
            SandPieceOnSpline currentPiece = activeSandPieces[i];
            currentPiece.isBlocked = false;
            
            // Check collision with piece ahead (next in sorted list)
            if (i > 0)
            {
                SandPieceOnSpline pieceAhead = activeSandPieces[i - 1];
                float distanceToAhead = pieceAhead.distanceTraveled - currentPiece.distanceTraveled;
                
                // Handle wrapping for closed loops
                if (closedLoop && distanceToAhead < 0)
                {
                    distanceToAhead += cachedSplineLength;
                }
                
                // Check if too close to piece ahead
                if (distanceToAhead < distanceBetweenPieces)
                {
                    currentPiece.isBlocked = true;
                }
            }
            
            // Set target speed based on blocking state
            currentPiece.targetSpeed = currentPiece.isBlocked ? 0f : moveSpeed;
        }
        
        // Update piece positions with smooth speed transitions
        foreach (var piece in activeSandPieces)
        {
            UpdateSandPieceOnSplineWithCollision(piece);
        }
    }
    
    void UpdateSandPieceOnSplineWithCollision(SandPieceOnSpline pieceData)
    {
        // Calculate current speed based on blocking state
        float targetSpeed = pieceData.isBlocked ? 0f : moveSpeed;
        
        // Smooth speed transition (FPS independent)
        float speedTransitionRate = 10f; // How fast speed changes
        float currentSpeed = Mathf.MoveTowards(pieceData.targetSpeed, targetSpeed, speedTransitionRate * Time.deltaTime);
        pieceData.targetSpeed = currentSpeed;
        
        // Move along the spline at current speed (FPS independent)
        pieceData.distanceTraveled += currentSpeed * Time.deltaTime;
        
        // Handle looping for non-closed splines
        if (!closedLoop && pieceData.distanceTraveled > cachedSplineLength)
        {
            // Reset to start for continuous loop
            pieceData.distanceTraveled = 0f;
        }
        
        // Convert distance to t parameter using arc length table
        float t = GetTFromDistance(pieceData.distanceTraveled);
        
        // Get position on spline
        Vector3 splinePos = GetPointOnSplineRaw(t);
        Vector3 targetPos = splinePos + Vector3.up * sandPieceHeightOffset;
        
        // Update piece position
        pieceData.pieceTransform.position = targetPos;
        
        // Rotate piece to face forward along spline direction
        Vector3 forward = GetTangentOnSpline(t);
        if (forward != Vector3.zero)
        {
            // Ensure the piece faces the direction of movement along the spline
            Quaternion targetRotation = Quaternion.LookRotation(forward, Vector3.up);
            
            // Apply rotation offset
            targetRotation *= Quaternion.Euler(sandPieceRotationOffset);
            
            pieceData.pieceTransform.rotation = targetRotation;
        }
    }
    
    public bool CanSpawnNewPiece(float spawnPosition)
    {
        if (!enableCollisionDetection) return true;
        
        // Convert spawn position to distance
        float spawnDistance = spawnPosition * cachedSplineLength;
        
        // Check if there's enough space at spawn position
        foreach (var piece in activeSandPieces)
        {
            float distanceToExisting = Mathf.Abs(piece.distanceTraveled - spawnDistance);
            
            // Handle wrapping for closed loops
            if (closedLoop)
            {
                float wrappedDistance = cachedSplineLength - distanceToExisting;
                distanceToExisting = Mathf.Min(distanceToExisting, wrappedDistance);
            }
            
            if (distanceToExisting < distanceBetweenPieces)
            {
                return false; // Too close to existing piece
            }
        }
        
        return true;
    }
    
    public bool IsSplineFull()
    {
        if (!enableCollisionDetection) return false;
        
        // Calculate maximum pieces that can fit on spline
        int maxPieces = Mathf.FloorToInt(cachedSplineLength / distanceBetweenPieces);
        
        // Consider spline full if we have 90% of max capacity
        return activeSandPieces.Count >= (maxPieces * 0.9f);
    }
    
    public int GetActivePieceCount()
    {
        return activeSandPieces.Count;
    }
    
    public int GetMaxPieceCapacity()
    {
        return Mathf.FloorToInt(cachedSplineLength / distanceBetweenPieces);
    }
    
    float FindClosestPointOnSpline(Vector3 worldPoint)
    {
        if (splinePoints.Count < 2) return 0f;
        
        float closestT = 0f;
        float closestDistance = float.MaxValue;
        
        // Sample the spline to find closest point
        int samples = 50;
        for (int i = 0; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector3 pointOnSpline = GetPointOnSpline(t);
            float distance = Vector3.Distance(worldPoint, pointOnSpline);
            
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestT = t;
            }
        }
        
        return closestT;
    }
    
    public Vector3 GetPointOnSpline(float t)
    {
        if (splinePoints.Count == 0) return Vector3.zero;
        if (splinePoints.Count == 1) return splinePoints[0].position;
        
        // Ensure t is in valid range
        if (closedLoop)
        {
            // Wrap t for closed loop
            t = t - Mathf.Floor(t);
        }
        else
        {
            t = Mathf.Clamp01(t);
        }
        
        int pointCount = splinePoints.Count;
        float scaledT = t * pointCount;
        int index = Mathf.FloorToInt(scaledT);
        float localT = scaledT - index;
        
        // Handle edge case for closed loop
        if (closedLoop && index >= pointCount)
        {
            index = 0;
            localT = 0;
        }
        
        if (!closedLoop && index >= pointCount - 1)
        {
            return splinePoints[pointCount - 1].position;
        }
        
        // Get points for interpolation
        Vector3 p0, p1, p2, p3;
        
        if (useSmoothCurve)
        {
            // Catmull-Rom spline for smooth curves
            p0 = GetSplinePoint(index - 1);
            p1 = GetSplinePoint(index);
            p2 = GetSplinePoint(index + 1);
            p3 = GetSplinePoint(index + 2);
            
            return CatmullRom(p0, p1, p2, p3, localT);
        }
        else
        {
            // Linear interpolation
            p1 = GetSplinePoint(index);
            p2 = GetSplinePoint(index + 1);
            return Vector3.Lerp(p1, p2, localT);
        }
    }
    
    Vector3 GetSplinePoint(int index)
    {
        if (splinePoints.Count == 0) return Vector3.zero;
        
        if (closedLoop)
        {
            // Wrap index for closed loop
            index = ((index % splinePoints.Count) + splinePoints.Count) % splinePoints.Count;
        }
        else
        {
            // Clamp for open spline
            index = Mathf.Clamp(index, 0, splinePoints.Count - 1);
        }
        
        return splinePoints[index].position;
    }
    
    Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        // Catmull-Rom spline interpolation for smooth curves
        float t2 = t * t;
        float t3 = t2 * t;
        
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }
    
    public Vector3 GetTangentOnSpline(float t)
    {
        float delta = 0.001f; // Smaller delta for more accurate tangent calculation
        
        // Handle edge cases for closed and open splines
        float t1, t2;
        
        if (closedLoop)
        {
            // For closed loops, wrap the t values
            t1 = (t - delta + 1f) % 1f;
            t2 = (t + delta) % 1f;
        }
        else
        {
            // For open splines, clamp to valid range
            t1 = Mathf.Clamp01(t - delta);
            t2 = Mathf.Clamp01(t + delta);
        }
        
        Vector3 p1 = GetPointOnSpline(t1);
        Vector3 p2 = GetPointOnSpline(t2);
        
        Vector3 tangent = (p2 - p1).normalized;
        
        // Ensure we have a valid direction
        if (tangent == Vector3.zero)
        {
            // Fallback: use a larger delta or default forward direction
            tangent = Vector3.forward;
        }
        
        return tangent;
    }
    
    public float CalculateSplineLength()
    {
        if (arcLengthTable != null && arcLengthTable.Length > 0)
        {
            return arcLengthTable[arcLengthTable.Length - 1];
        }
        return 1f;
    }
    
    float GetSplineLength()
    {
        if (cachedSplineLength < 0)
        {
            cachedSplineLength = CalculateSplineLength();
        }
        return cachedSplineLength;
    }
    
    void OnDrawGizmos()
    {
        if (splinePoints.Count < 2) return;
        
        Gizmos.color = Color.cyan;
        
        // Draw spline with more detail
        int segments = 50;
        for (int i = 0; i < segments; i++)
        {
            float t1 = i / (float)segments;
            float t2 = (i + 1) / (float)segments;
            
            Vector3 p1 = GetPointOnSpline(t1);
            Vector3 p2 = GetPointOnSpline(t2);
            
            Gizmos.DrawLine(p1, p2);
        }
        
        // Draw spline points
        Gizmos.color = Color.white;
        foreach (Transform point in splinePoints)
        {
            if (point != null)
            {
                Gizmos.DrawWireSphere(point.position, 0.3f);
            }
        }
        
        // Draw sand piece height offset
        Gizmos.color = Color.yellow;
        for (int i = 0; i < segments; i++)
        {
            float t1 = i / (float)segments;
            float t2 = (i + 1) / (float)segments;
            
            Vector3 p1 = GetPointOnSpline(t1) + Vector3.up * sandPieceHeightOffset;
            Vector3 p2 = GetPointOnSpline(t2) + Vector3.up * sandPieceHeightOffset;
            
            Gizmos.DrawLine(p1, p2);
        }
        
        // Draw cube grinding position
        Gizmos.color = Color.red;
        for (int i = 0; i < splinePoints.Count; i++)
        {
            if (splinePoints[i] != null)
            {
                Vector3 cubePos = splinePoints[i].position + Vector3.up * cubeHeightOffset + cubePositionOffset;
                Gizmos.DrawWireCube(cubePos, Vector3.one * 0.5f);
            }
        }
        
        // Draw sand piece collision radii if collision detection is enabled
        if (enableCollisionDetection && Application.isPlaying)
        {
            Gizmos.color = Color.green;
            foreach (var piece in activeSandPieces)
            {
                if (piece.pieceTransform != null)
                {
                    Vector3 pos = piece.pieceTransform.position;
                    Gizmos.DrawWireSphere(pos, sandPieceRadius);
                    
                    // Draw different color for blocked pieces
                    if (piece.isBlocked)
                    {
                        Gizmos.color = Color.red;
                        Gizmos.DrawWireSphere(pos, sandPieceRadius * 0.8f);
                        Gizmos.color = Color.green;
                    }
                }
            }
        }
    }
    
    [System.Serializable]
    private class GrindingCube
    {
        public Transform cubeTransform;
        public SandCubeGrinder grinder;
        public float splinePosition;
        public Vector3 fixedPosition; // The collision point where cube stays
        public Material cubeMaterial; // Material to apply to spawned pieces
        public string colorName; // Color name to copy to spawned pieces
        public GameObject particleEffect; // Particle effect for this cube
        public Coroutine particleCoroutine; // Coroutine for timed particle bursts
    }
    
    [System.Serializable]
    private class SandPieceOnSpline
    {
        public Transform pieceTransform;
        public float distanceTraveled; // Distance traveled along spline in world units
        public bool isBlocked = false; // Whether this piece is blocked by another piece
        public float targetSpeed = 0f; // Current target speed (for smooth stopping/starting)
    }
}
