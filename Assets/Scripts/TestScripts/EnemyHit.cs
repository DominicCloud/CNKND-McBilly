using UnityEngine;

public class EnemyHit : MonoBehaviour
{

    public BoxCollider2D hitbox;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void onTriggerEnter2D(BoxCollider2D other)
    {
        Debug.Log("Attacked");
    }
}
