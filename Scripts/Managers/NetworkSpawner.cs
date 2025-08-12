using Mirror;
using UnityEngine;

public class NetworkSpawner : NetworkBehaviour {
    public static NetworkSpawner Instance;

    // ROCKET LAUNCHER
    public GameObject missilePrefab;
    private Transform firePosition;

    private Vector3 direction;

    void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start() {
        firePosition = null;
    }


    /*
     * os clientes também devem conseguir spawnar mísseis porque podem apanhar um rocketlauncher e apanhar mísseis, então para que os clientes possam usar este método temos de dizer que 
     * o Command não requer autoridade do host/server
     */
    [Command(requiresAuthority = false)]
    public void CmdSpawnMissile(GameObject player) {
        firePosition = player.transform.GetComponentInChildren<FirePositionScript>().transform;

        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f); // apanhar o centro do ecrã (onde a mira está)
        Ray ray = player.GetComponentInChildren<Camera>().ScreenPointToRay(screenCenter); // mandamos um ray para o centro e para fazermos isso temos de usar a câmara para sabermos onde o meio do ecrã está a apontar

        if (Physics.Raycast(ray, out RaycastHit hit)) {
            // a direção do tiro vai ser no sítio onde a mira está a conincidir (menos esta posição para termos a direção porque é um vetor)
            direction = (hit.point - firePosition.position).normalized;

        } else {
            // se a mira não estiver a bater num local (por ex a olhar para o céu) então a direção do tiro segue simplesmente em frente
            // ou seja, em vez de ir desde o fire position apra o meio do ecrã só vai em frente (porque não estamos a calcular o vetor entre as duas posições)
            direction = ray.direction.normalized;
        }

        // para onde o míssil vai estar a olhar/apontar
        firePosition.rotation = Quaternion.LookRotation(direction);

        GameObject missile = Instantiate(missilePrefab, firePosition.position, firePosition.rotation);
        NetworkServer.Spawn(missile);
        SoundManager.Instance.RpcPlayMissileShootSound(missile);

        missile.GetComponent<MissileScript>().wasShot = true;

        // o modo default de AddForce é Force que adiciona Force ao longo do tempo
        // o modo Impulse adiciona força logo no momento como se algo lhe tivesse batido com força e tivesse logo um arranque, que é o que queremos
        // o míssil não passa pelas paredes porque usamos rigidbody contínuo e não discreto (checka colisões entre frames e não a cada frame)
        missile.GetComponent<Rigidbody>().AddForce(direction * 50, ForceMode.Impulse);
        missile.transform.Rotate(0, -90, 0);
    }

    [Server]
    public void Destroy(GameObject asset, bool hasNetworkIdentity) {
        if (hasNetworkIdentity)
            NetworkServer.Destroy(asset); // não é preciso também dar destroy asset porque o network server já toma conta disso
        else
            RpcDestroyAsset(asset);

    }

    [ClientRpc]
    private void RpcDestroyAsset(GameObject asset) {
        Destroy(asset);
    }
}