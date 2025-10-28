using Mirror;
using UnityEngine;

public class SoundTriggerScript : NetworkBehaviour { // it has to be network behaviour because gameobjects passed through RPCClient blocks must have network identity

    private void OnTriggerEnter(Collider other) { // the sound is played when we enter the trigger's area
        if (other.gameObject.CompareTag("Player") || other.gameObject.CompareTag("PlayerUntagged")) {
            SoundManager.Instance.CmdPlaySound(gameObject);

        // this was catching the first collider that entered and I didn't want that
        // It was getting the FieldOfView and I want the NPC so we have to catch the root which is the parent gameobject
        } else if (other.transform.root.gameObject.CompareTag("NPC")) {
            other.transform.root.gameObject.GetComponent<NPCScript>().ChangeHeardNoise(false);
        }
    }
}
