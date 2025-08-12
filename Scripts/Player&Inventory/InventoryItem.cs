using Mirror;
using UnityEngine;

public class InventoryItem : MonoBehaviour {

    // ITENS GERAIS
    public int quantity;
    public string itemName;


    /*
     * porque s� o servidor � que deve instanciar objetos, se for um cliente que quer spawnar m�sseis tem de comunicar com 
     * ele porque � um cliente, mas se j� for o host ent�o n�o faz mal
     * mas como n�o quero fazer esta classe extender de networkBehaviour para n�o ter de meter um network identity em todos os gameObjects "item" que t�m este script
     * fa�o um script singleton que sirva para spawnar objetos na rede e que seja sempre command para n�o termos de distinguir entre host e cliente
     */
    public void Use(GameObject player) {
        NetworkSpawner.Instance.CmdSpawnMissile(player);
    }


}
