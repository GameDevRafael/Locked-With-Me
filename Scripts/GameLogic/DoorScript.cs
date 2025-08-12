using Mirror;
using UnityEngine;

public class DoorScript : NetworkBehaviour {
    // SyncVar porque queremos que todos os clientes tenham o mesmo valor para cada porta e que estejam sincronizados
    // podemos usar um hook que é chamado em todos os clientes e assim não precisamos usar comandos RPC
    [SyncVar(hook = nameof(OnDoorStateChanged))] public bool isOpen = false;

    // a porta se abrir ou fechar o movimento vai ocorrer em ambos jogos por causa do network transport reliable, como este evento é disparado independentemente dos clientes podemos
    // só ver o host em vez de ver também o cliente
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
        transform.parent.GetComponent<Animator>().SetBool("open", newValue); // mudamos a animação de acordo com o novo valor atribuído à variável isOpen
        SoundManager.Instance.PlayDoorSound(transform.parent.gameObject, newValue); // embora não seja RPC não faz mal porque o hook é chamado em todos os clientes
    }
}