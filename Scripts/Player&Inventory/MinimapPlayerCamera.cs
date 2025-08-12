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
        transform.rotation = Quaternion.Euler(90, 0, 0); // não quero que o minimapa rode quando o jogador rode, quero fique tudo a zeros exceto o X (para olhar para baixo)
    }
}
