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
     * os clientes tamb�m devem conseguir spawnar m�sseis porque podem apanhar um rocketlauncher e apanhar m�sseis, ent�o para que os clientes possam usar este m�todo temos de dizer que 
     * o Command n�o requer autoridade do host/server
     */
    [Command(requiresAuthority = false)]
    public void CmdSpawnMissile(GameObject player) {
        firePosition = player.transform.GetComponentInChildren<FirePositionScript>().transform;

        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f); // apanhar o centro do ecr� (onde a mira est�)
        Ray ray = player.GetComponentInChildren<Camera>().ScreenPointToRay(screenCenter); // mandamos um ray para o centro e para fazermos isso temos de usar a c�mara para sabermos onde o meio do ecr� est� a apontar

        if (Physics.Raycast(ray, out RaycastHit hit)) {
            // a dire��o do tiro vai ser no s�tio onde a mira est� a conincidir (menos esta posi��o para termos a dire��o porque � um vetor)
            direction = (hit.point - firePosition.position).normalized;

        } else {
            // se a mira n�o estiver a bater num local (por ex a olhar para o c�u) ent�o a dire��o do tiro segue simplesmente em frente
            // ou seja, em vez de ir desde o fire position apra o meio do ecr� s� vai em frente (porque n�o estamos a calcular o vetor entre as duas posi��es)
            direction = ray.direction.normalized;
        }

        // para onde o m�ssil vai estar a olhar/apontar
        firePosition.rotation = Quaternion.LookRotation(direction);

        GameObject missile = Instantiate(missilePrefab, firePosition.position, firePosition.rotation);
        NetworkServer.Spawn(missile);
        SoundManager.Instance.RpcPlayMissileShootSound(missile);

        missile.GetComponent<MissileScript>().wasShot = true;

        // o modo default de AddForce � Force que adiciona Force ao longo do tempo
        // o modo Impulse adiciona for�a logo no momento como se algo lhe tivesse batido com for�a e tivesse logo um arranque, que � o que queremos
        // o m�ssil n�o passa pelas paredes porque usamos rigidbody cont�nuo e n�o discreto (checka colis�es entre frames e n�o a cada frame)
        missile.GetComponent<Rigidbody>().AddForce(direction * 50, ForceMode.Impulse);
        missile.transform.Rotate(0, -90, 0);
    }

    [Server]
    public void Destroy(GameObject asset, bool hasNetworkIdentity) {
        if (hasNetworkIdentity)
            NetworkServer.Destroy(asset); // n�o � preciso tamb�m dar destroy asset porque o network server j� toma conta disso
        else
            RpcDestroyAsset(asset);

    }

    [ClientRpc]
    private void RpcDestroyAsset(GameObject asset) {
        Destroy(asset);
    }
}