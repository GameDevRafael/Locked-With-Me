using UnityEngine;

public class MinimapPlayerCamera : MonoBehaviour
{

    private Transform player;



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        player = transform.parent;
    }


    void LateUpdate() {
        // i dont want the minimap to rotate when the player rotates, i want it to be all zeros (except for X axis for it to be looking down)
        transform.rotation = Quaternion.Euler(90, 0, 0);
    }
}
