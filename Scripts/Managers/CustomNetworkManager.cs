using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class CustomNetworkManager : NetworkManager {
    [Header("Prefabs dos Jogadores")]
    public GameObject player1Prefab;
    public GameObject player2Prefab;

    [Header("Informações dos Jogadores")]
    public Transform[] spawnPoints; // vamos ter dois spawns possíveis, cada um na sua casa
    // este dicíonário serve para sabermos qual conexão pertence a qual jogador
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

    // sem este método o cliente às vezes não funciona não funciona, o host ligava o jogo corretamente e conseguia jogar, mas o cliente não porque não estava a ser criado a prefab e não se conectava bem
    // quando o método é chamado, o cliente é iniciado e liga-se ao servidor.
    // o método em si não faz nada, mas podemos dar override para fazer, então o que fazemos é registar os prefabs dos jogadores
    // o método register prefab é usado para os clientes conseguirem instanciar objetos localmente (nos seus jogos), primeiro registamos e depois instanciamos
    public override void OnStartClient() {
        base.OnStartClient();

        // registamos as prefabs
        // usei a lista de registered prefabs no network manager no unity, mas às vezes não estava a conseguir usá-lo, então adicionei este código para complementar e ter a certeza que funciona sempre
        NetworkClient.RegisterPrefab(player1Prefab);
        NetworkClient.RegisterPrefab(player2Prefab);
    }

    // este método já existe no script original NetworkManager e por isso temos de dar override porque eu quero adicionar os jogadores ao jogo com prefabs diferentes, o local para o fazer é aqui porque é quando são adicionados
    public override void OnServerAddPlayer(NetworkConnectionToClient conn) {
        // ao começar um jogo, voltar ao menu e começar outro, os spawn points ficam null então precisamos dos obter novamente
        if (spawnPoints[0] == null || spawnPoints[1] == null) {
            spawnPoints[0] = GameObject.Find("SpawnPoint1").transform;
            spawnPoints[1] = GameObject.Find("SpawnPoint2").transform;
        }

        GameObject player;
        Vector3 spawnPosition = Vector3.zero;
        Quaternion spawnRotation = Quaternion.identity;

        int currentPlayerIndex = Mathf.Clamp(GameManager.Instance.playerCount, 0, spawnPoints.Length - 1); // se o playerCount não excede o número de spawn points
        Transform spawnPoint = spawnPoints[currentPlayerIndex]; // como são só dois spawns podemos fazer usar o contador de jogadores existentes
        spawnPosition = spawnPoint.position;
        spawnRotation = spawnPoint.rotation;

        // metemos um prefab de jogador diferente para ambos os jogadores, possivelmente no futuro vão escolher com base num menu
        if (GameManager.Instance.playerCount == 0) {
            player = Instantiate(player1Prefab, spawnPosition, spawnRotation);
        } else {
            player = Instantiate(player2Prefab, spawnPosition, spawnRotation);
        }

        connectionToPlayer[conn] = player;

        GameManager.Instance.AddPlayer(player); // atualiza o contador de jogadores e isSinglePlayer

        // depois de instanciarmos o segundo jogador e adicioná-lo à lista dos jogadores ativos, podemos notificar que já não estamos em single player
        if (GameManager.Instance.playerCount == 2) {
            AddPlayerToNPCs();
        }

        // adicionamos o jogador à conexão (jogo)
        NetworkServer.AddPlayerForConnection(conn, player);
    }

    // em princípio nãó será necessário, mas quando um jogador sai do jogo convém decrementar a variável que tem em conta o número de jogadores no jogo
    public override void OnServerDisconnect(NetworkConnectionToClient conn) {

        if (connectionToPlayer.ContainsKey(conn)) {
            GameObject playerToRemove = connectionToPlayer[conn];

            GameManager.Instance.RemovePlayer(playerToRemove); // remove o jogador
            NetworkSpawner.Instance.Destroy(playerToRemove, true);
            connectionToPlayer.Remove(conn);

            if (GameManager.Instance.playerCount <= 1) {
                RemovePlayerFromNPCs(); // quando um jogador é desconectado temos de atualizar a lista dos NPCs para não haver problemas com nulos a aparecerem
            }
        }

        base.OnServerDisconnect(conn); // temos de chamar isto no fim senão a conexão é desconectada e depois estamos à procura do player de uma conexão que não existe
        // e por isso a variável player count continuava igual sem ser decrementada
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

    // se o servidor pára então damos reset à quantidade de jogadores e à lista
    // se o host pára então ele volta ao menu e consequentemente o cliente também terá de voltar (se estiver no jogo)
    public override void OnStopServer() {
        base.OnStopServer();
        connectionToPlayer.Clear();
        GameManager.Instance.ClearPlayers();
        UIManager.Instance.BackToMainMenu(false);

    }

    // se o cliente sair do jogo então volta ao menu, damos override para isso
    public override void OnStopClient() {
        base.OnStopClient();
        UIManager.Instance.BackToMainMenu(false);
    }


    // método para adicionar o jogador ao jogo
    public override void OnClientConnect() {
        if (!clientLoadedScene) {
            if (!NetworkClient.ready)
                NetworkClient.Ready();

            //if (autoCreatePlayer) damos override do método porque ele só instanciava prefabs se a checkbox do fazer automaticamente estivesse ligada, ignoramos isso
            NetworkClient.AddPlayer();
        }
    }

    public GameObject FindOtherPlayer(GameObject currentPlayer) {
        return GameManager.Instance.FindOtherPlayer(currentPlayer);
    }

}
