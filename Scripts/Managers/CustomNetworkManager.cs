using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class CustomNetworkManager : NetworkManager {
    [Header("Prefabs dos Jogadores")]
    public GameObject player1Prefab;
    public GameObject player2Prefab;

    [Header("Informa��es dos Jogadores")]
    public Transform[] spawnPoints; // vamos ter dois spawns poss�veis, cada um na sua casa
    // este dic�on�rio serve para sabermos qual conex�o pertence a qual jogador
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

    // sem este m�todo o cliente �s vezes n�o funciona n�o funciona, o host ligava o jogo corretamente e conseguia jogar, mas o cliente n�o porque n�o estava a ser criado a prefab e n�o se conectava bem
    // quando o m�todo � chamado, o cliente � iniciado e liga-se ao servidor.
    // o m�todo em si n�o faz nada, mas podemos dar override para fazer, ent�o o que fazemos � registar os prefabs dos jogadores
    // o m�todo register prefab � usado para os clientes conseguirem instanciar objetos localmente (nos seus jogos), primeiro registamos e depois instanciamos
    public override void OnStartClient() {
        base.OnStartClient();

        // registamos as prefabs
        // usei a lista de registered prefabs no network manager no unity, mas �s vezes n�o estava a conseguir us�-lo, ent�o adicionei este c�digo para complementar e ter a certeza que funciona sempre
        NetworkClient.RegisterPrefab(player1Prefab);
        NetworkClient.RegisterPrefab(player2Prefab);
    }

    // este m�todo j� existe no script original NetworkManager e por isso temos de dar override porque eu quero adicionar os jogadores ao jogo com prefabs diferentes, o local para o fazer � aqui porque � quando s�o adicionados
    public override void OnServerAddPlayer(NetworkConnectionToClient conn) {
        // ao come�ar um jogo, voltar ao menu e come�ar outro, os spawn points ficam null ent�o precisamos dos obter novamente
        if (spawnPoints[0] == null || spawnPoints[1] == null) {
            spawnPoints[0] = GameObject.Find("SpawnPoint1").transform;
            spawnPoints[1] = GameObject.Find("SpawnPoint2").transform;
        }

        GameObject player;
        Vector3 spawnPosition = Vector3.zero;
        Quaternion spawnRotation = Quaternion.identity;

        int currentPlayerIndex = Mathf.Clamp(GameManager.Instance.playerCount, 0, spawnPoints.Length - 1); // se o playerCount n�o excede o n�mero de spawn points
        Transform spawnPoint = spawnPoints[currentPlayerIndex]; // como s�o s� dois spawns podemos fazer usar o contador de jogadores existentes
        spawnPosition = spawnPoint.position;
        spawnRotation = spawnPoint.rotation;

        // metemos um prefab de jogador diferente para ambos os jogadores, possivelmente no futuro v�o escolher com base num menu
        if (GameManager.Instance.playerCount == 0) {
            player = Instantiate(player1Prefab, spawnPosition, spawnRotation);
        } else {
            player = Instantiate(player2Prefab, spawnPosition, spawnRotation);
        }

        connectionToPlayer[conn] = player;

        GameManager.Instance.AddPlayer(player); // atualiza o contador de jogadores e isSinglePlayer

        // depois de instanciarmos o segundo jogador e adicion�-lo � lista dos jogadores ativos, podemos notificar que j� n�o estamos em single player
        if (GameManager.Instance.playerCount == 2) {
            AddPlayerToNPCs();
        }

        // adicionamos o jogador � conex�o (jogo)
        NetworkServer.AddPlayerForConnection(conn, player);
    }

    // em princ�pio n�� ser� necess�rio, mas quando um jogador sai do jogo conv�m decrementar a vari�vel que tem em conta o n�mero de jogadores no jogo
    public override void OnServerDisconnect(NetworkConnectionToClient conn) {

        if (connectionToPlayer.ContainsKey(conn)) {
            GameObject playerToRemove = connectionToPlayer[conn];

            GameManager.Instance.RemovePlayer(playerToRemove); // remove o jogador
            NetworkSpawner.Instance.Destroy(playerToRemove, true);
            connectionToPlayer.Remove(conn);

            if (GameManager.Instance.playerCount <= 1) {
                RemovePlayerFromNPCs(); // quando um jogador � desconectado temos de atualizar a lista dos NPCs para n�o haver problemas com nulos a aparecerem
            }
        }

        base.OnServerDisconnect(conn); // temos de chamar isto no fim sen�o a conex�o � desconectada e depois estamos � procura do player de uma conex�o que n�o existe
        // e por isso a vari�vel player count continuava igual sem ser decrementada
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

    // se o servidor p�ra ent�o damos reset � quantidade de jogadores e � lista
    // se o host p�ra ent�o ele volta ao menu e consequentemente o cliente tamb�m ter� de voltar (se estiver no jogo)
    public override void OnStopServer() {
        base.OnStopServer();
        connectionToPlayer.Clear();
        GameManager.Instance.ClearPlayers();
        UIManager.Instance.BackToMainMenu(false);

    }

    // se o cliente sair do jogo ent�o volta ao menu, damos override para isso
    public override void OnStopClient() {
        base.OnStopClient();
        UIManager.Instance.BackToMainMenu(false);
    }


    // m�todo para adicionar o jogador ao jogo
    public override void OnClientConnect() {
        if (!clientLoadedScene) {
            if (!NetworkClient.ready)
                NetworkClient.Ready();

            //if (autoCreatePlayer) damos override do m�todo porque ele s� instanciava prefabs se a checkbox do fazer automaticamente estivesse ligada, ignoramos isso
            NetworkClient.AddPlayer();
        }
    }

    public GameObject FindOtherPlayer(GameObject currentPlayer) {
        return GameManager.Instance.FindOtherPlayer(currentPlayer);
    }

}
