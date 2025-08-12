using Mirror;
using UnityEngine;

public class SoundTriggerScript : NetworkBehaviour { // tem que ser network behaviour porque gameobjects passados por blocos RpcClient t�m que ter network identity 

    private void OnTriggerEnter(Collider other) { // o som acontece quando entrarmos no s�tio do trigger
        if (other.gameObject.CompareTag("Player") || other.gameObject.CompareTag("PlayerUntagged")) {
            SoundManager.Instance.CmdPlaySound(gameObject);

        // isto estava a apanhar o primeiro collider que entrasse e eu n�o quero isso (estava a apanhar o FieldOfVIew, eu quero o NPC ent�o apanhamos o root que � o parent gameObject
        } else if (other.transform.root.gameObject.CompareTag("NPC")) {
            other.transform.root.gameObject.GetComponent<NPCScript>().ChangeHeardNoise(false);
        }
    }
}