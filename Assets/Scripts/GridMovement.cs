using UnityEngine;
using UnityEngine.InputSystem;

public class player_movement : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public float speed = 200.0f;
    private Vector2 move_direction;
    private bool isMoving;
    private float movementInputCooldown = .1f;
    private InputAction movementAction;
    void Start()
    {
        movementAction = InputSystem.actions.FindAction("Move");
    }

    // Update is called once per frame
    void Update()
    {
        //if (Input.GetKey(KeyCode.W))
        //{
        //    //move_direction = Vector2.up;
        //    MovePlayer(Vector2.up);
        //    isMoving = false;
        //}
        //else if (Input.GetKey(KeyCode.S))
        //{
        //    //move_direction = Vector2.down;
        //    MovePlayer(Vector2.down);
        //    isMoving = false;
        //}
        //else if (Input.GetKey(KeyCode.A))
        //{
        //    //move_direction = Vector2.left;
        //    MovePlayer(Vector2.left);
        //    isMoving = false;
        //}
        //else if (Input.GetKey(KeyCode.D))
        //{
        //    //move_direction = Vector2.right;
        //    MovePlayer(Vector2.right);
        //    isMoving = false;
        //}
        //else
        //{
        //    //move_direction = Vector2.zero;
        //}

        Vector2 moveDir = movementAction.ReadValue<Vector2>();


        transform.Translate(moveDir * speed * Time.deltaTime);
    }
    private void MovePlayer(Vector2 move_direction)
    {
        if (movementInputCooldown <= 0.0f)
        {
            return;
        }
        isMoving = true;
        transform.position += (Vector3)(speed * move_direction *Time.deltaTime);
        
    }
}
