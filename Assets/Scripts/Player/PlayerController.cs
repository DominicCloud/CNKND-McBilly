using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

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
    
    [SerializeField] private Tilemap groundTilemap; // Reference to the tilemap for cell lookup
    [Tooltip("Adjustable movement speed in seconds per cell.")]
    [SerializeField] private float moveSpeed = 5f;
    [Tooltip("Starting cell position in the tilemap.")]
    [SerializeField] private Vector3Int startingCell;


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

    private Vector3Int heldDirection;
    private float nextMoveTime;
    
    //private GameObject player;
    private Vector3Int currentCell; // Current cell position
    private Vector3Int targetCell; // Target cell position
    private bool isMoving = false;
    
    
    
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
    }
    
    private void Start()
    {
        if (groundTilemap == null) 
        {
            Debug.LogError("Tilemap reference is not set in PlayerController.");
            return;
        }
        
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
        
        Debug.Log(
            $"Player spawned at cell {currentCell}, world position {transform.position}.",
            this
        );

    }
    
    private void Update()
    {
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

    private void TryMove(Vector3Int direction)
    {
        Vector3Int targetCell = currentCell + direction;

        if (!groundTilemap.HasTile(targetCell))
            return;

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
        Vector3 destination = groundTilemap.GetCellCenterWorld(cell);
        
        destination.z = transform.position.z;
        
        transform.position = destination;
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
        Debug.Log("Dash action triggered!");
    }

    private void OnParry(InputAction.CallbackContext context)
    {
        Debug.Log("Parry action triggered!");
    }
    #endregion
    
}
