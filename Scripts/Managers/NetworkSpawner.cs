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
     * the clients should be able to spawn missiles because they can get a rocket launcher and missiles, so for that i need to use command that doesnt require authority
     */
    [Command(requiresAuthority = false)]
    public void CmdSpawnMissile(GameObject player) {
        firePosition = player.transform.GetComponentInChildren<FirePositionScript>().transform;

        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f); // grab center of screen (where the aim is at)
        // send an array to the center and to do that i need to use the camera to know where the middle of the screen is pointing at
        Ray ray = player.GetComponentInChildren<Camera>().ScreenPointToRay(screenCenter);

            // a direção do tiro vai ser no sítio onde a mira está a conincidir (menos esta posição para termos a direção porque é um vetor)
            direction = (hit.point - firePosition.position).normalized;

        } else {
            // if the ray didn't get a hit it means probably the player is looking at the sky so the shot simply goes forward instead
            direction = ray.direction.normalized;
        }

        // to where the missile is going to be pointing at
        firePosition.rotation = Quaternion.LookRotation(direction);

        GameObject missile = Instantiate(missilePrefab, firePosition.position, firePosition.rotation);
        NetworkServer.Spawn(missile);
        SoundManager.Instance.RpcPlayMissileShootSound(missile);

        missile.GetComponent<MissileScript>().wasShot = true;

        // the default mode of AddForte is Force that adds more Force overtime
        // Impulse adds Force right then and there as if something knocked him with force so it gets a boost, which is what i want
        // the missiles doesn't go through walls because i use rigidbody continuo and not discrete (it checks collisions between frames and not frame to frame)
        missile.GetComponent<Rigidbody>().AddForce(direction * 50, ForceMode.Impulse);
        missile.transform.Rotate(0, -90, 0);
    }

    [Server]
    public void Destroy(GameObject asset, bool hasNetworkIdentity) {
        if (hasNetworkIdentity)
            NetworkServer.Destroy(asset); // not needed to destroy the asset because network server already handles that
        else
            RpcDestroyAsset(asset);

    }

    [ClientRpc]
    private void RpcDestroyAsset(GameObject asset) {
        Destroy(asset);
    }
}