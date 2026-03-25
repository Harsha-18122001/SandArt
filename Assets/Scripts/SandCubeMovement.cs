using UnityEngine;

public class SandCubeMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float moveDistance = 10f;
    [SerializeField] private LayerMask conveyorLayerMask = -1; // What layers to check for conveyor collision
    [SerializeField] private LayerMask cubeLayerMask = -1; // What layers to check for cube collision
    [SerializeField] private float collisionCheckDistance = 0.5f; // How far ahead to check for collisions
    
    [Header("Collision Animation")]
    [SerializeField] private float returnAnimationSpeed = 3f;
    [SerializeField] private AnimationCurve returnCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve blinkCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Outline Settings")]
    [SerializeField] private Color redOutlineColor = Color.red;
    [SerializeField] private float blinkDuration = 0.2f;
    
    [Header("Arrow Reference")]
    [SerializeField] private Transform arrowTransform;
    
    [Header("Direction Settings")]
    [Tooltip("Which local axis of the arrow points in the movement direction")]
    [SerializeField] private ArrowAxis arrowAxis = ArrowAxis.Up;
    
    [Tooltip("Constrain movement to specific world axes")]
    [SerializeField] private bool constrainToZAxis = false;
    
    public enum ArrowAxis
    {
        Forward,    // Z axis (blue)
        Up,         // Y axis (green)
        Right       // X axis (red)
    }
    
    private bool isMoving = false;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private Vector3 moveDirection;
    private float distanceTraveled = 0f;
    private bool hasCollided = false;
    private bool isReturning = false;
    private bool outlineDisabled = false;
    
    // Store original outline properties for restoration
    private struct OutlineData
    {
        public Material material;
        public string propertyName;
        public float originalValue;
        public Color originalColor;
        public bool isColorProperty;
    }
    private System.Collections.Generic.List<OutlineData> originalOutlineData = new System.Collections.Generic.List<OutlineData>();
    
    void Start()
    {
        // If arrow transform is not assigned, try to find it
        if (arrowTransform == null)
        {
            // Look for a child object with "arrow" in its name
            foreach (Transform child in GetComponentsInChildren<Transform>())
            {
                if (child.name.ToLower().Contains("arrow"))
                {
                    arrowTransform = child;
                    break;
                }
            }
        }
        
        if (arrowTransform == null)
        {
            Debug.LogWarning($"Arrow transform not found in {gameObject.name}. Using cube's forward direction.");
        }
    }
    
    void Update()
    {
        if (isMoving && !hasCollided && !isReturning)
        {
            // FPS-independent movement
            float frameDistance = moveSpeed * Time.deltaTime;
            
            // Check for cube collision first
            if (CheckForCubeCollision(frameDistance))
            {
                // Collision with another cube - return to start
                StartCoroutine(ReturnToStartPosition());
                return;
            }
            
            // Check for conveyor collision
            if (CheckForConveyorCollision(frameDistance))
            {
                // Stop at collision point
                hasCollided = true;
                isMoving = false;
                Debug.Log($"<color=yellow>{gameObject.name} collided with conveyor at position {transform.position}</color>");
                return;
            }
            
            // Move the cube
            distanceTraveled += frameDistance;
            Vector3 newPosition = startPosition + (moveDirection * distanceTraveled);
            transform.position = newPosition;
            
            // Check if reached target distance
            if (distanceTraveled >= moveDistance)
            {
                transform.position = targetPosition;
                isMoving = false;
                Debug.Log($"<color=green>{gameObject.name} reached target position</color>");
            }
        }
    }
    
    bool CheckForCubeCollision(float frameDistance)
    {
        // Get the cube's collider bounds
        Collider cubeCollider = GetComponent<Collider>();
        if (cubeCollider == null) return false;
        
        // Use a more conservative radius for collision detection
        float radius = Mathf.Min(cubeCollider.bounds.size.x, cubeCollider.bounds.size.y, cubeCollider.bounds.size.z) * 0.3f;
        
        // Check for overlapping colliders in the movement path
        Vector3 checkPosition = transform.position + (moveDirection * (frameDistance + collisionCheckDistance));
        
        // Use OverlapSphere for more accurate collision detection
        Collider[] overlapping = Physics.OverlapSphere(checkPosition, radius, cubeLayerMask);
        
        foreach (Collider other in overlapping)
        {
            // Skip self
            if (other.gameObject == gameObject) continue;
            
            // Check if it's another sand cube (not a conveyor)
            SandCubeMovement otherCube = other.GetComponent<SandCubeMovement>();
            ConveyorSpline conveyor = other.GetComponent<ConveyorSpline>();
            
            if (otherCube != null && conveyor == null)
            {
                // Additional distance check to ensure we're actually close
                float actualDistance = Vector3.Distance(transform.position, other.transform.position);
                float minDistance = (cubeCollider.bounds.size.magnitude + other.bounds.size.magnitude) * 0.4f;
                
                // Only consider it a collision if we're moving towards the other cube
                Vector3 directionToOther = (other.transform.position - transform.position).normalized;
                float dot = Vector3.Dot(moveDirection, directionToOther);
                
                if (actualDistance <= minDistance && dot > 0.5f) // Moving towards the other cube
                {
                    Debug.Log($"<color=red>{gameObject.name} will collide with cube {other.name} at distance {actualDistance:F2}</color>");
                    return true;
                }
            }
        }
        
        return false;
    }
    
    bool CheckForConveyorCollision(float frameDistance)
    {
        // Get the cube's collider bounds
        Collider cubeCollider = GetComponent<Collider>();
        if (cubeCollider == null) return false;
        
        // Calculate the movement vector for this frame
        Vector3 movementVector = moveDirection * frameDistance;
        
        // Extend the check distance to account for collision detection
        Vector3 extendedMovement = moveDirection * (frameDistance + collisionCheckDistance);
        
        // Use BoxCast or SphereCast based on collider type
        RaycastHit hit;
        bool hasHit = false;
        
        if (cubeCollider is BoxCollider boxCollider)
        {
            // Use BoxCast for box colliders
            Vector3 center = transform.position + cubeCollider.bounds.center - transform.position;
            Vector3 halfExtents = boxCollider.size * 0.5f;
            
            hasHit = Physics.BoxCast(
                center,
                halfExtents,
                moveDirection,
                out hit,
                transform.rotation,
                frameDistance + collisionCheckDistance,
                conveyorLayerMask
            );
        }
        else
        {
            // Use SphereCast for other colliders
            float radius = Mathf.Max(cubeCollider.bounds.size.x, cubeCollider.bounds.size.y, cubeCollider.bounds.size.z) * 0.5f;
            
            hasHit = Physics.SphereCast(
                transform.position,
                radius,
                moveDirection,
                out hit,
                frameDistance + collisionCheckDistance,
                conveyorLayerMask
            );
        }
        
        if (hasHit)
        {
            // Check if it's a conveyor
            ConveyorSpline conveyor = hit.collider.GetComponent<ConveyorSpline>();
            if (conveyor != null)
            {
                // Position the cube at the collision point (slightly before to avoid penetration)
                Vector3 collisionPoint = hit.point;
                Vector3 safePosition = collisionPoint - (moveDirection * 0.1f); // Small offset
                transform.position = safePosition;
                
                // Attach to conveyor
                conveyor.AttachCube(transform, collisionPoint);
                return true;
            }
        }
        
        return false;
    }
    
    public void StartMovement()
    {
        if (isMoving || isReturning) return;
        
        // Get the direction from the arrow (or cube if arrow not found)
        if (arrowTransform != null)
        {
            // The arrow points in its local right direction (red axis)
            moveDirection = arrowTransform.TransformDirection(Vector3.right);
            
            Debug.Log($"Arrow local rotation: {arrowTransform.localRotation.eulerAngles}");
            Debug.Log($"Arrow world rotation: {arrowTransform.rotation.eulerAngles}");
            Debug.Log($"Arrow right in world space: {moveDirection}");
        }
        else
        {
            // Fallback to cube's forward direction
            moveDirection = transform.forward;
        }
        
        // Normalize the direction
        moveDirection.Normalize();
        
        // Constrain to Z axis if enabled
        if (constrainToZAxis)
        {
            // Keep only the Z component, determine forward or backward based on sign
            float zSign = Mathf.Sign(moveDirection.z);
            if (zSign == 0) zSign = 1; // Default to forward if exactly zero
            moveDirection = new Vector3(0, 0, zSign);
        }
        
        // Calculate target position
        startPosition = transform.position;
        targetPosition = startPosition + (moveDirection * moveDistance);
        
        isMoving = true;
        distanceTraveled = 0f;
        hasCollided = false;
        isReturning = false;
        outlineDisabled = true;
        
        Debug.Log($"{gameObject.name} moving from {startPosition} to {targetPosition}");
        Debug.Log($"Movement direction: {moveDirection}");
    }
    
    public void StopMovement()
    {
        isMoving = false;
        isReturning = false;
        distanceTraveled = 0f;
        hasCollided = false;
    }
    
    public void ResetPosition()
    {
        if (isMoving || isReturning)
        {
            StopAllCoroutines();
            transform.position = startPosition;
            isMoving = false;
            isReturning = false;
            distanceTraveled = 0f;
            hasCollided = false;
            
            // Restore outline if it was disabled
            if (outlineDisabled)
            {
                RestoreOriginalOutline();
                outlineDisabled = false;
            }
        }
    }
    
    void OnMouseDown()
    {
        // Only allow click if not currently moving or returning
        if (!isMoving && !isReturning)
        {
            // Store original outline data before disabling
            StoreOriginalOutlineData();
            
            // Disable outline immediately when clicked
            DisableOutlineOnCube();
            
            StartMovement();
        }
    }
    
    void StoreOriginalOutlineData()
    {
        originalOutlineData.Clear();
        
        // Get all MeshRenderers on this cube and its children
        MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>();
        
        foreach (MeshRenderer renderer in meshRenderers)
        {
            if (renderer.material != null)
            {
                Material mat = renderer.material;
                
                // Store original outline properties
                string[] outlineProperties = {
                    "_OutlineWidth", "_Outline", "_OutlineSize", "_OutlineThickness",
                    "_EnableOutline", "_UseOutline", "_OutlineColor"
                };
                
                foreach (string property in outlineProperties)
                {
                    if (mat.HasProperty(property))
                    {
                        OutlineData data = new OutlineData();
                        data.material = mat;
                        data.propertyName = property;
                        data.isColorProperty = property.Contains("Color");
                        
                        if (data.isColorProperty)
                        {
                            data.originalColor = mat.GetColor(property);
                        }
                        else
                        {
                            data.originalValue = mat.GetFloat(property);
                        }
                        
                        originalOutlineData.Add(data);
                    }
                }
            }
        }
    }
    
    System.Collections.IEnumerator ReturnToStartPosition()
    {
        isReturning = true;
        hasCollided = true;
        isMoving = false;
        
        // Start red blink effect immediately when collision happens
        StartCoroutine(BlinkRedOutline());
        
        Vector3 currentPosition = transform.position;
        float elapsed = 0f;
        float duration = 1f / returnAnimationSpeed;
        
        Debug.Log($"<color=orange>{gameObject.name} returning to start position</color>");
        
        // Animate return to start position
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Apply easing curve
            t = returnCurve.Evaluate(t);
            
            transform.position = Vector3.Lerp(currentPosition, startPosition, t);
            
            yield return null;
        }
        
        // Ensure exact position
        transform.position = startPosition;
        
        // Reset states
        isReturning = false;
        hasCollided = false;
        distanceTraveled = 0f;
        outlineDisabled = false; // Mark outline as enabled again
        
        Debug.Log($"<color=green>{gameObject.name} returned to start position with outline restored</color>");
    }
    
    void RestoreOriginalOutline()
    {
        foreach (var data in originalOutlineData)
        {
            if (data.material != null)
            {
                if (data.isColorProperty)
                {
                    data.material.SetColor(data.propertyName, data.originalColor);
                }
                else
                {
                    data.material.SetFloat(data.propertyName, data.originalValue);
                }
            }
        }
        
        outlineDisabled = false;
        Debug.Log($"<color=cyan>Restored original outline on {gameObject.name}</color>");
    }
    
    System.Collections.IEnumerator BlinkRedOutline()
    {
        // Get all materials for blinking
        MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>();
        
        float elapsed = 0f;
        float halfDuration = blinkDuration * 0.5f;
        
        // Phase 1: Red outline fade from 1.0 to 0.75
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            
            // Fade from 1.0 to 0.75
            float alpha = Mathf.Lerp(1f, 0.75f, blinkCurve.Evaluate(t));
            
            // Apply red outline with fading alpha
            foreach (MeshRenderer renderer in meshRenderers)
            {
                if (renderer.material != null)
                {
                    Material mat = renderer.material;
                    
                    // Set red outline color with fading alpha
                    if (mat.HasProperty("_OutlineColor"))
                    {
                        Color blinkColor = redOutlineColor;
                        blinkColor.a = alpha;
                        mat.SetColor("_OutlineColor", blinkColor);
                    }
                    
                    // Ensure outline is visible
                    SetOutlineVisible(mat, true);
                }
            }
            
            yield return null;
        }
        
        // Phase 2: Fade from red to black with alpha 1.0
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            
            // Fade from red to black, keep alpha at 1
            Color currentColor = Color.Lerp(redOutlineColor, Color.black, blinkCurve.Evaluate(t));
            currentColor.a = 1f; // Keep alpha at 1
            
            foreach (MeshRenderer renderer in meshRenderers)
            {
                if (renderer.material != null)
                {
                    Material mat = renderer.material;
                    
                    if (mat.HasProperty("_OutlineColor"))
                    {
                        mat.SetColor("_OutlineColor", currentColor);
                    }
                    
                    // Ensure outline remains visible
                    SetOutlineVisible(mat, true);
                }
            }
            
            yield return null;
        }
        
        // Final: Ensure black outline is properly set and visible
        foreach (MeshRenderer renderer in meshRenderers)
        {
            if (renderer.material != null)
            {
                Material mat = renderer.material;
                
                if (mat.HasProperty("_OutlineColor"))
                {
                    mat.SetColor("_OutlineColor", Color.black);
                }
                
                // Ensure outline is visible with proper settings
                SetOutlineVisible(mat, true);
            }
        }
        
        Debug.Log($"<color=red>Red blink completed on {gameObject.name} - black outline restored</color>");
    }
    
    void SetOutlineVisible(Material material, bool visible)
    {
        if (material == null) return;
        
        if (visible)
        {
            // Set outline width/size for visibility
            if (material.HasProperty("_OutlineWidth"))
            {
                // Use the original stored value or a default
                foreach (var data in originalOutlineData)
                {
                    if (data.material == material && data.propertyName == "_OutlineWidth")
                    {
                        material.SetFloat("_OutlineWidth", data.originalValue);
                        return;
                    }
                }
                // Fallback to default value
                material.SetFloat("_OutlineWidth", 5f);
            }
            else if (material.HasProperty("_OutlineSize"))
            {
                foreach (var data in originalOutlineData)
                {
                    if (data.material == material && data.propertyName == "_OutlineSize")
                    {
                        material.SetFloat("_OutlineSize", data.originalValue);
                        return;
                    }
                }
                material.SetFloat("_OutlineSize", 0.03f);
            }
            
            // Enable outline flags
            if (material.HasProperty("_EnableOutline"))
            {
                material.SetFloat("_EnableOutline", 1f);
            }
            if (material.HasProperty("_UseOutline"))
            {
                material.SetFloat("_UseOutline", 1f);
            }
        }
    }
    
    void DisableOutlineOnCube()
    {
        // Get all MeshRenderers on this cube and its children
        MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>();
        
        foreach (MeshRenderer renderer in meshRenderers)
        {
            if (renderer.material != null)
            {
                DisableOutline(renderer.material);
            }
        }
        
        Debug.Log($"<color=cyan>Disabled outline on cube {gameObject.name} when clicked</color>");
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
                }
                // Try to disable outline by setting enable flags to false
                else if (property.Contains("Enable") || property.Contains("Use"))
                {
                    material.SetFloat(property, 0f); // 0 = false
                }
                // Set outline color to transparent
                else if (property.Contains("Color"))
                {
                    material.SetColor(property, Color.clear);
                }
            }
        }
        
        // Special handling for Toony Colors Pro shader
        if (material.shader.name.Contains("Toony") || material.shader.name.Contains("TCP"))
        {
            if (material.HasProperty("_TCP2_OUTLINE"))
            {
                material.SetFloat("_TCP2_OUTLINE", 0f);
            }
        }
    }

/*   
    // Visualize the movement direction in the editor
    void OnDrawGizmosSelected()
    {
        if (arrowTransform != null)
        {
            // Show the arrow's right direction in world space (movement direction)
            Vector3 direction = arrowTransform.TransformDirection(Vector3.right);
            
            if (constrainToZAxis)
            {
                float zSign = Mathf.Sign(direction.z);
                if (zSign == 0) zSign = 1;
                direction = new Vector3(0, 0, zSign);
            }
            
            // Yellow line shows movement direction
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, direction * moveDistance);
            Gizmos.DrawWireSphere(transform.position + direction * moveDistance, 0.5f);
            
            // Draw arrow's local axes for debugging
            Gizmos.color = Color.red;
            Gizmos.DrawRay(arrowTransform.position, arrowTransform.right * 2f);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(arrowTransform.position, arrowTransform.up * 2f);
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(arrowTransform.position, arrowTransform.forward * 2f);
        }
    }
    */
    // Public properties for inspector control
    public float MoveSpeed
    {
        get { return moveSpeed; }
        set { moveSpeed = value; }
    }
    
    public float MoveDistance
    {
        get { return moveDistance; }
        set { moveDistance = value; }
    }
}
