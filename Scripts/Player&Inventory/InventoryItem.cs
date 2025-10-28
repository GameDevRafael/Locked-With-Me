using Mirror;
using UnityEngine;

public class InventoryItem : MonoBehaviour {

    public int quantity;
    public string itemName;


    /*
     * the server is the one that should instance objects
     * if it's a client that wants to spawn missiles it has to communicate with the server, but if it's the host then it's fine
     * but i dont want this class to extand network behaviour so i dont have to place a network identity on all "item" game objects that have this script
     * so i make a singleton class that can spawn objects on the network
     */
    public void Use(GameObject player) {
        NetworkSpawner.Instance.CmdSpawnMissile(player);
    }


}
