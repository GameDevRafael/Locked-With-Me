using Mirror;
using UnityEngine;

public class InventoryItem : MonoBehaviour {

    // ITENS GERAIS
    public int quantity;
    public string itemName;


    /*
     * porque só o servidor é que deve instanciar objetos, se for um cliente que quer spawnar mísseis tem de comunicar com 
     * ele porque é um cliente, mas se já for o host então não faz mal
     * mas como não quero fazer esta classe extender de networkBehaviour para não ter de meter um network identity em todos os gameObjects "item" que têm este script
     * faço um script singleton que sirva para spawnar objetos na rede e que seja sempre command para não termos de distinguir entre host e cliente
     */
    public void Use(GameObject player) {
        NetworkSpawner.Instance.CmdSpawnMissile(player);
    }


}
