using UnityEngine;
using UnityEngine.UIElements;

public class LookMovement : MonoBehaviour
{
    private Vector2 look_at;
    private Vector2 mousePosition;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // Mouse position in screen pixels
        Vector2 mouseScreen = Input.mousePosition;

        // Convert to world position
        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(mouseScreen);

        // Direction from player to mouse, length 1
        look_at = (mouseWorld - (Vector2)transform.position).normalized;

        Debug.Log(look_at);
    }
}
