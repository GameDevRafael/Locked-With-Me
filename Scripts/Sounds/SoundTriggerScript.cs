using Mirror;
using UnityEngine;

public class SoundTriggerScript : NetworkBehaviour { // tem que ser network behaviour porque gameobjects passados por blocos RpcClient têm que ter network identity 

    private void OnTriggerEnter(Collider other) { // o som acontece quando entrarmos no sítio do trigger
        if (other.gameObject.CompareTag("Player") || other.gameObject.CompareTag("PlayerUntagged")) {
            SoundManager.Instance.CmdPlaySound(gameObject);

        // isto estava a apanhar o primeiro collider que entrasse e eu não quero isso (estava a apanhar o FieldOfVIew, eu quero o NPC então apanhamos o root que é o parent gameObject
        } else if (other.transform.root.gameObject.CompareTag("NPC")) {
            other.transform.root.gameObject.GetComponent<NPCScript>().ChangeHeardNoise(false);
        }
    }
}