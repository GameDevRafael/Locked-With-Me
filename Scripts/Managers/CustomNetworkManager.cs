using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class CustomNetworkManager : NetworkManager {
    [Header("Prefabs dos Jogadores")]
    public GameObject player1Prefab;
    public GameObject player2Prefab;

    [Header("Informações dos Jogadores")]
    public Transform[] spawnPoints;// i have two possible spawns for the players
    // this dictionairy is used to know which connection belongs to which player
    private Dictionary<NetworkConnectionToClient, GameObject> connectionToPlayer = new Dictionary<NetworkConnectionToClient, GameObject>();

    public static CustomNetworkManager Instance;


    public override void Awake() {
        base.Awake();
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // without this method the client sometimes doesn't work well, the host would turn on the game correctly and play, but the client wouldn't because its prefab was being created
    // when the method was being called
    // the method itself doesn't do anything, but i overrode it to register the players' prefabs
    // the method register prefab is used so the clients can instance objects locally
    public override void OnStartClient() {
        base.OnStartClient();

        // i used the registered prefabs list on the network manager, but sometimes it wasn't working, so i did it manually too
        NetworkClient.RegisterPrefab(player1Prefab);
        NetworkClient.RegisterPrefab(player2Prefab);
    }

    // this method already exists so it needs to have an override because i want to add players to the game with different prefabs
    public override void OnServerAddPlayer(NetworkConnectionToClient conn) {
        // when starting a game, going back to the menu and starting another one, the spawn points turn null, so i need to get them again
        if (spawnPoints[0] == null || spawnPoints[1] == null) {
            spawnPoints[0] = GameObject.Find("SpawnPoint1").transform;
            spawnPoints[1] = GameObject.Find("SpawnPoint2").transform;
        }

        GameObject player;
        Vector3 spawnPosition = Vector3.zero;
        Quaternion spawnRotation = Quaternion.identity;

        int currentPlayerIndex = Mathf.Clamp(GameManager.Instance.playerCount, 0, spawnPoints.Length - 1); // if the playerCount doesnt excede the number of spawn points
        Transform spawnPoint = spawnPoints[currentPlayerIndex]; // because there are only two spawns i can use the counter for existent players
        spawnPosition = spawnPoint.position;
        spawnRotation = spawnPoint.rotation;

        // different prefab for different players
        if (GameManager.Instance.playerCount == 0) {
            player = Instantiate(player1Prefab, spawnPosition, spawnRotation);
        } else {
            player = Instantiate(player2Prefab, spawnPosition, spawnRotation);
        }

        connectionToPlayer[conn] = player;

        GameManager.Instance.AddPlayer(player); // updates the counter of players and the isSinglePlayer variable

        // after instancing the second player and adding him to the list of active players, i can notify that we are no longer in single player
        if (GameManager.Instance.playerCount == 2) {
            AddPlayerToNPCs();
        }

        // add the player to the connection (game)
        NetworkServer.AddPlayerForConnection(conn, player);
    }

    // when a player leaves from the game it decrements the variable that takes into account the number of players in the game
    public override void OnServerDisconnect(NetworkConnectionToClient conn) {

        if (connectionToPlayer.ContainsKey(conn)) {
            GameObject playerToRemove = connectionToPlayer[conn];

            GameManager.Instance.RemovePlayer(playerToRemove); // removes the player
            NetworkSpawner.Instance.Destroy(playerToRemove, true);
            connectionToPlayer.Remove(conn);

            if (GameManager.Instance.playerCount <= 1) {
                RemovePlayerFromNPCs(); // when a player is disconnected i have to update the NPCs list of players
            }
        }

        // i have to call this at the end or else the connection is disconnected and then it's searching for a player from a connection that doesn't exist
        base.OnServerDisconnect(conn);
    }

    private void RemovePlayerFromNPCs() {
        NPCScript[] npcs = FindObjectsOfType<NPCScript>();
        foreach (NPCScript npc in npcs) {
            npc.OnPlayerDisconnected();
        }
    }

    private void AddPlayerToNPCs() {
        NPCScript[] npcs = FindObjectsOfType<NPCScript>();
        foreach (NPCScript npc in npcs) {
            npc.OnPlayerConnected();
        }
    }

    // if the servers stops then i reset the quantity of players and the list
    // if the host stops then he'll go back to the menu and so will the client (if in game aswell)
    public override void OnStopServer() {
        base.OnStopServer();
        connectionToPlayer.Clear();
        GameManager.Instance.ClearPlayers();
        UIManager.Instance.BackToMainMenu(false);

    }

    // if the client leaves the game then he'll be brought back to the menu
    public override void OnStopClient() {
        base.OnStopClient();
        UIManager.Instance.BackToMainMenu(false);
    }


    // method that adds the player to the game
    public override void OnClientConnect() {
        if (!clientLoadedScene) {
            if (!NetworkClient.ready)
                NetworkClient.Ready();

            //if (autoCreatePlayer) override this because it only instanciated prefabs if the checkbox to do it automatically was on
            NetworkClient.AddPlayer();
        }
    }

    public GameObject FindOtherPlayer(GameObject currentPlayer) {
        return GameManager.Instance.FindOtherPlayer(currentPlayer);
    }

}
