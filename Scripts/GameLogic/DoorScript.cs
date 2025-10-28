using Mirror;
using UnityEngine;

public class DoorScript : NetworkBehaviour {
    // sync var because i want all of the clients to have the same value for each door so they're synchronized
    [SyncVar(hook = nameof(OnDoorStateChanged))] public bool isOpen = false;

    // the door opening or closing is a movement that'll happen on both players because of the network transport reliable
    // because this event is fired independently i can check for the host only because it's easier
    private void OnTriggerEnter(Collider other) {

        if (isOpen == false && other.gameObject.CompareTag("NPC") && isServer) {
            isOpen = true;
        }
    }

    private void OnTriggerExit(Collider other) {
        if (isOpen && other.gameObject.CompareTag("NPC") && isServer) {
            isOpen = false;
        }
    }

    private void OnDoorStateChanged(bool oldValue, bool newValue) {
        transform.parent.GetComponent<Animator>().SetBool("open", newValue); // i change the animation according to the new value of the isOpen variable
        SoundManager.Instance.PlayDoorSound(transform.parent.gameObject, newValue);
    }
}