using System.Collections.Generic;
using Mirror;
using NUnit.Framework;
using UnityEngine;

public class FieldOfView : NetworkBehaviour
{
    private NPCScript nPC;
    

    private void Start() {
        nPC = transform.parent.GetComponent<NPCScript>();
    }

    private void OnTriggerEnter(Collider other) {
        if (other.gameObject.CompareTag("Player") && !nPC.playersInFOV.Contains(other.gameObject)) {
            if (isServer)
                nPC.playersInFOV.Add(other.gameObject);
        }
    }

    private void OnTriggerExit(Collider other) {
        if (other.gameObject.CompareTag("Player") && nPC.playersInFOV.Contains(other.gameObject)) {
            if (isServer)
                nPC.playersInFOV.Remove(other.gameObject);
        }
    }
}
