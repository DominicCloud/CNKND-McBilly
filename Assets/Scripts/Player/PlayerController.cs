using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    /*
     PlayerController (MonoBehaviour)
    ├── Fields
    │   ├── Tilemap reference (for cell lookup)
    │   ├── Movement speed / move duration
    │   ├── Current cell position
    │   ├── Target cell position
    │   ├── isMoving flag
    │   └── Input settings (keys or axes)
    ├── Lifecycle
    │   ├── Start()      → snap to starting cell
    │   └── Update()     → read input, begin move if idle
    ├── Input
    │   └── ReadInput()  → returns a direction (Vector2Int)
    ├── Grid logic
    │   ├── CanMoveTo(cell)     → checks walls / bounds / occupancy
    │   └── TryMove(direction)  → sets target if legal
    ├── Movement
    │   ├── MoveRoutine() or per-frame lerp → glide to target
    │   └── OnArrived()  → update current cell, clear isMoving
    └── Helpers
        ├── CellToWorld(cell)
        └── WorldToCell(position)
     */
    
    [Header("Grid Settings")]
    [SerializeField] private Tilemap groundTilemap; // Reference to the tilemap for cell lookup
    [Tooltip("Adjustable movement speed in seconds per cell.")]
    [SerializeField] private float moveSpeed = 5f;
    [Tooltip("Starting cell position in the tilemap.")]
    [SerializeField] private Vector3Int startingCell;
    [Tooltip("Optional Tilemap for walls. If set, player cannot move into cells with tiles in this tilemap.")]
    [SerializeField] private Tilemap wallTilemap;


    [Header("Input Settings")]
    [Tooltip("Reference to the Input Action for movement.")]
    [SerializeField] private InputActionReference moveAction;
    [Tooltip("Reference to the Input Action for dash.")]
    [SerializeField] private InputActionReference dashAction;
    [Tooltip("Reference to the Input Action for parry.")]
    [SerializeField] private InputActionReference parryAction;
    
    
    [Header("Held Input")]
    [SerializeField, Min(0f)]
    private float initialRepeatDelay = 0.25f;
    [SerializeField, Min(0.01f)]
    private float repeatInterval = 0.1f;

    [Header("Dash Settings")]
    [Tooltip("Max cells covered by a dash. Holes inside this ranges are jumped over.")]
    [SerializeField, Min(1)] private int dashDistance = 3;

    [Tooltip("Seconds the dash glide takes, regardless of distance.")] 
    [SerializeField, Min(0.01f)] private float dashDuration = 0.12f;
    [Tooltip("Seconds of dash cooldown")]
    [SerializeField, Min(0f)] private float dashCooldown = 0.4f;

    [Tooltip("Seconds of I-frames during dash")] 
    [SerializeField, Min(0f)] private float dashIFrameDuration = 0.15f;

    [Tooltip("Easing for the dash. X = time (0 - 1), Y = progress (0 - 1)")] 
    [SerializeField] 
    private AnimationCurve dashCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Optional TrailRender on the player. Emits during dash, if present.")]
    [SerializeField] private TrailRenderer dashTrail;

    [Header("Dash Ghosting")]
    [Tooltip("The player's SpriteRenderer. Used to ghost the player during dash.")]
    [SerializeField] private SpriteRenderer playerSprite;

    [Tooltip("Tint and starting opacity of each ghost.")] [SerializeField]
    private Color ghostColor = new Color(0.4f, 0.9f, 1f, 0.6f);
    [Tooltip("Seconds for each ghost to fade out.")] 
    [SerializeField, Min(0.01f)] private float ghostFadeTime = 0.3f;

    [Tooltip("Seconds between ghost while dashing.")] 
    [SerializeField, Min(0.005f)] private float ghostSpawnInterval = 0.025f;

    [Tooltip("Player sprite opacity during the dash (1 = fully solid).")] 
    [SerializeField, Range(0f, 1f)] private float dashSpriteAlpha = 0.35f;

    private Color baseSpriteColor = Color.white;
    

    private Vector3Int heldDirection;
    private float nextMoveTime;
    
    //private GameObject player;
    private Vector3Int currentCell; // Current cell position
    private Vector3Int targetCell; // Target cell position MIGHT REMOVE
    private bool isMoving = false;
    
    private Vector3Int facingDirection = Vector3Int.right; // Default facing direction

    private bool isDashing;
    private float nextDashTime;
    private float invincibleUntil;
    private Coroutine dashRoutine;
    
    public bool IsDashing => isDashing;
    public bool IsInvincible => Time.time < invincibleUntil;

    public event Action OnDashed;
    
    
    
    private Vector2 moveInput; // Stores the current movement input
    
    private void OnEnable()
    {
        moveAction.action.Enable();
        dashAction.action.Enable();
        parryAction.action.Enable();

        //moveAction.action.performed += OnMove;
        dashAction.action.performed += OnDash;
        parryAction.action.performed += OnParry;
    }
    
    private void OnDisable()
    {
        moveAction.action.Disable();
        dashAction.action.Disable();
        parryAction.action.Disable();
        
        //moveAction.action.performed -= OnMove;
        dashAction.action.performed -= OnDash;
        parryAction.action.performed -= OnParry;

        // To smooth out mid dash disabled
        if (isDashing)
        {
            StopCoroutine(dashRoutine);
            FinishDash();
            SnapToCell(currentCell);
        }
    }
    
    private void Start()
    {
        if (groundTilemap == null) 
        {
            Debug.LogError("Tilemap reference is not set in PlayerController.");
            return;
        }

        if (dashTrail != null)
        {
            dashTrail.emitting = false;
        }

        /*
        if (playerSprite != null)
        {
            baseSpriteColor = playerSprite.color;
        }
        */
        
        // Snap to starting cell
        currentCell = new Vector3Int(
            startingCell.x,
            startingCell.y,
            0);

        if (!groundTilemap.HasTile(currentCell))
        {
            Debug.LogError($"Starting cell {currentCell} is not a valid tile in the tilemap.");
            return;
        }
        SnapToCell(currentCell);

        if (dashTrail != null)
        {
            dashTrail.Clear();
        }
        
        Debug.Log(
            $"Player spawned at cell {currentCell}, world position {transform.position}.",
            this
        );

    }
    
    private void Update()
    {
        // No stepping while dashing
        if (isDashing) return;
        
        Vector2 input = moveAction.action.ReadValue<Vector2>();
        Vector3Int direction = GetGridDirection(input);

        // The input was released.
        if (direction == Vector3Int.zero)
        {
            heldDirection = Vector3Int.zero;
            return;
        }

        // New press or changed direction: move immediately.
        if (direction != heldDirection)
        {
            heldDirection = direction;
            TryMove(direction);

            nextMoveTime = Time.unscaledTime + initialRepeatDelay;
            return;
        }

        // Continue moving after the initial delay.
        if (Time.unscaledTime >= nextMoveTime)
        {
            TryMove(direction);
            nextMoveTime = Time.unscaledTime + repeatInterval;
        }
        
    }
    

    #region Input

    private Vector3 ReadInput()
    {
        moveInput = moveAction.action.ReadValue<Vector2>();
        return new Vector3(moveInput.x, moveInput.y, 0f); // Placeholder
    }

    #endregion
    
    #region Grid Logic

    private bool CanMoveTo(Vector3Int cell)
    {
        return groundTilemap.HasTile(cell) && !IsSolid(cell);
    }
    
    private bool IsSolid(Vector3Int cell)
    {
        return wallTilemap != null && wallTilemap.HasTile(cell);
    }

    private void TryMove(Vector3Int direction)
    {
        if (isDashing) return;
        // Face the direction even if blocked
        facingDirection = direction;
        
        Vector3Int targetCell = currentCell + direction;
        if (!CanMoveTo(targetCell))
        {
            return;
        }

        /*
        if (!groundTilemap.HasTile(targetCell))
            return;
        */

        currentCell = targetCell;
        SnapToCell(currentCell);
    }
    
    #endregion

    #region Actions

    private void OnMove(InputAction.CallbackContext context)
    {
        Vector2 input = context.ReadValue<Vector2>();
        Debug.Log($"Move action triggered with input: {input}");
        Vector3Int direction = GetGridDirection(input);

        if (direction == Vector3Int.zero) return;
        
        Vector3Int targetCell = currentCell + direction;
        
        // prevent movement outside of bounds
        if (!groundTilemap.HasTile(targetCell)) return;
        
        currentCell = targetCell;
        SnapToCell(currentCell);
    }

    private void SnapToCell(Vector3Int cell)
    {
        /*
        Vector3 destination = groundTilemap.GetCellCenterWorld(cell);
        
        destination.z = transform.position.z;
        
        transform.position = destination;
        */
        transform.position = CellToWorld(cell);
    }
    
    private Vector3 CellToWorld(Vector3Int cell)
    {
        Vector3 world = groundTilemap.GetCellCenterWorld(cell);
        world.z = transform.position.z; // Maintain current Z position
        return world;
    }

    private Vector3Int GetGridDirection(Vector2 input)
    {
        // no Diagonal movement, prioritize horizontal over vertical
        if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
        {
            return input.x > 0 ? Vector3Int.right : Vector3Int.left;
        }

        if (Mathf.Abs(input.y) > 0.01f)
        {
            return input.y > 0 ? Vector3Int.up : Vector3Int.down;
        }
        return Vector3Int.zero;
    }
    private void OnDash(InputAction.CallbackContext context)
    {
        TryDash();
    }

    private void TryDash()
    {
        if (isDashing || Time.time < nextDashTime) return;
        // read input
        Vector3Int direction = GetGridDirection(moveAction.action.ReadValue<Vector2>());
        if (direction == Vector3Int.zero)
        {
            direction = facingDirection;
        }

        if (!TryGetDashDestination(direction, out Vector3Int destination))
        {
            return;
        }
        OnDashed?.Invoke();

        facingDirection = direction;
        currentCell = destination;
        invincibleUntil = Time.time + dashIFrameDuration;
        nextDashTime = Time.time + dashDuration + dashCooldown;

        dashRoutine = StartCoroutine(DashRoutine(transform.position, CellToWorld(destination)));
    }

    private bool TryGetDashDestination(Vector3Int direction, out Vector3Int destination)
    {
        destination = currentCell;

        for (int i = 1; i <= dashDistance; i++)
        {
            Vector3Int cell = currentCell + direction * i;

            if (IsSolid(cell)) break;
            if (groundTilemap.HasTile(cell))
            {
                destination = cell;
            }
        }
        return destination != currentCell;
    }

    private IEnumerator DashRoutine(Vector3 from, Vector3 to)
    {
        isDashing = true;
        if (dashTrail != null)
            dashTrail.emitting = true;

        // Phase out the player sprite during dash
        if (playerSprite != null)
        {
            baseSpriteColor = playerSprite.color;
            Color c = baseSpriteColor;
            c.a *= dashSpriteAlpha;
            playerSprite.color = c;
        }

        SpawnGhost();
        
        float elapsed = 0f;
        float ghostTimer = 0f;
        while (elapsed < dashDuration)
        {
            elapsed += Time.deltaTime;
            float t = dashCurve.Evaluate(Mathf.Clamp01(elapsed / dashDuration));
            transform.position = Vector3.LerpUnclamped(from, to, t);

            ghostTimer += Time.deltaTime;
            if (ghostTimer >= ghostSpawnInterval)
            {
                ghostTimer -= ghostSpawnInterval;
                SpawnGhost();
            }
            yield return null;
        }

        transform.position = to;
        FinishDash();
    }

    private void SpawnGhost()
    {
        if (playerSprite == null) return;

        Transform src = playerSprite.transform;
        GameObject go = new GameObject("DashGhost");
        go.transform.SetPositionAndRotation(src.position, src.rotation);
        go.transform.localScale = src.lossyScale;
        
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = playerSprite.sprite;
        sr.flipX = playerSprite.flipX;
        sr.flipY = playerSprite.flipY;
        sr.sortingLayerID = playerSprite.sortingLayerID;
        sr.sortingOrder = playerSprite.sortingOrder - 1; // Behind the player
        sr.color = ghostColor;

        StartCoroutine(FadeGhost(sr));
    }

    private IEnumerator FadeGhost(SpriteRenderer sr)
    {
        Color start = sr.color;

        for (float t = 0f; t < ghostFadeTime; t += Time.deltaTime)
        {
            if (sr == null) yield break;
            sr.color = new Color(start.r, start.g, start.b, start.a * (1f - t / ghostFadeTime));
            yield return null;
        }
        
        if (sr != null)
        {
            Destroy(sr.gameObject);
        }
    }

    private void FinishDash()
    {
        isDashing = false;
        dashRoutine = null;
        
        if (dashTrail != null)
            dashTrail.emitting = false;

        if (playerSprite != null)
        {
            playerSprite.color = baseSpriteColor;
        }
        
        nextMoveTime = Time.unscaledTime + initialRepeatDelay; // Reset move delay after dash
    }

    private void OnParry(InputAction.CallbackContext context)
    {
        Debug.Log("Parry action triggered!");
    }
    #endregion
    
}
