using Mirror;
using UnityEngine;

public class LockScript : NetworkBehaviour
{

    [SyncVar] public bool isLocked = true;

    [Command(requiresAuthority = false)]
    public void CmdUnlockDoor() {
        isLocked = false;
    }


}
