using System;
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
    
    [SerializeField] private Tilemap tileMap; // Reference to the tilemap for cell lookup
    [Tooltip("Adjustable movement speed in seconds per cell.")]
    [SerializeField] private float moveSpeed = 0.2f;
    
    private GameObject player;
    private Vector2Int currentCell; // Current cell position
    private Vector2Int targetCell; // Target cell position
    private bool isMoving = false;
    
    
    public InputAction moveAction { get; private set; }


    private void Start()
    {
        if (tileMap == null) 
        {
            Debug.LogError("Tilemap reference is not set in PlayerController.");
            return;
        }
        
        if (player == null)
        {
            Debug.LogError("Player GameObject reference is not set in PlayerController.");
            return;
        }
        
        // Snap to starting cell
        // TODO
        
    }
    
    private void Update()
    {
        if (isMoving) return; // Ignore input while moving

        Vector2Int direction = ReadInput();
        if (direction != Vector2Int.zero)
        {
            TryMove(direction);
        }
    }

    #region Input

    private Vector2Int ReadInput()
    {
        // TODO: Implement input reading using Unity's Input System
        return new Vector2Int(0, 0); // Placeholder
    }

    #endregion
    
    #region Grid Logic

    private bool CanMoveTo(Vector2Int cell)
    {
        return true; // Placeholder: Implement wall/bounds/occupancy checks
    }

    private void TryMove(Vector2Int cell)
    {
        // TODO: Implement movement logic, set targetCell, and start movement coroutine
    }
    
    #endregion
    
    
}
