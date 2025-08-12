using UnityEngine;
using Mirror;
using System.Collections.Generic;
using Mirror.Examples.AdditiveLevels;
using UnityEngine.Rendering;

public class GameManager : NetworkBehaviour {
    [SyncVar] public int playerCount = 0;
    [SyncVar] public bool isSinglePlayer = true;
    public SyncList<GameObject> activePlayers = new SyncList<GameObject>();

    public GameObject portal;
    public GameObject portal2;

    public GameObject DAM;

    public static GameManager Instance;

    void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /*[ClientRpc] o cliente n�o estava com a interface a ligar e ainda se ocnseguia mexer, se calhar n�o existia na lista?
    public void RpcSetYouWin() {
        foreach(GameObject playerGameObject in activePlayers) {
            PlayerMovement player = null;
            if (playerGameObject != null)
                player = playerGameObject.GetComponent<PlayerMovement>();
            
            if(player != null) {
                if (player.isLocalPlayer) { // como isto vai correr em todos os jogadores temos de checkar apenas se � o local player
                    player.gameOver = true;
                    player.playerCamera.GetComponent<CameraScript>().canRotate = false;
                    UIManager.Instance.youWinInterface.SetActive(true); // n�o pode ser passado por par�metro sen�o n�o funciona
                    //youWinInterface.SetActive(true); 
                }
            }
        }
    }*/

    [ClientRpc]
    public void RpcSetYouWin() {
        UIManager.Instance.ShowYouWinForLocalPlayer(); // assim chamamos esta fun��o no UIManager que j� tem as vari�veis e vem de um bloco RPC
    }



    // s� adicionamos jogadores se formos o host
    [Server]
    public void AddPlayer(GameObject player) {
        if (!activePlayers.Contains(player)) {
            activePlayers.Add(player);
            playerCount++;

            Debug.Log($"Player adicionado. Total de jogadores: {playerCount}");

            if (playerCount >= 2) {
                isSinglePlayer = false;
            }
        }
    }

    // mesma coisa aqui, s� se formos o host
    [Server]
    public void RemovePlayer(GameObject player) {
        if (activePlayers.Contains(player)) {
            activePlayers.Remove(player);
            playerCount--;

            Debug.Log($"Player removido. Total de jogadores: {playerCount}");

            if (playerCount <= 1) {
                isSinglePlayer = true;
            }

            // CORRE��O: Garantir que playerCount nunca fique negativo
            if (playerCount < 0) {
                playerCount = 0;
            }
        }
    }

    // e o mesmo aqui
    [Server]
    public void ClearPlayers() {
        activePlayers.Clear();
        playerCount = 0;
        isSinglePlayer = true;
    }

    [ClientRpc]
    public void RpcActivatePortal() {
        portal.SetActive(true);
        portal.GetComponent<ParticleSystem>().Play();
        SoundManager.Instance.PlaySound(portal.GetComponent<AudioSource>());
    }


    // encontramos o outro jogador
    public GameObject FindOtherPlayer(GameObject currentPlayer) {
        foreach (GameObject player in activePlayers) {
            if (player != currentPlayer) {
                return player;
            }
        }
        return null;
    }


}