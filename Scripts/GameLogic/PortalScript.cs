using Mirror;
using UnityEngine;

public class PortalScript : NetworkBehaviour {

    // it started with set active to true even though in the scene it's false, this is because the server instances everything and sets all to active
    // so i overrode it with this method
    public override void OnStartClient() {
        gameObject.SetActive(false);
    }
}