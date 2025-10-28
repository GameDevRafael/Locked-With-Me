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


    [ClientRpc]
    public void RpcSetYouWin() {
        UIManager.Instance.ShowYouWinForLocalPlayer();
    }



    // it only adds players if this is the host
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

    // same thing here
    [Server]
    public void RemovePlayer(GameObject player) {
        if (activePlayers.Contains(player)) {
            activePlayers.Remove(player);
            playerCount--;

            Debug.Log($"Player removido. Total de jogadores: {playerCount}");

            if (playerCount <= 1) {
                isSinglePlayer = true;
            }

            // making sure the player count never becomes negative
            if (playerCount < 0) {
                playerCount = 0;
            }
        }
    }

    // and here
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


    public GameObject FindOtherPlayer(GameObject currentPlayer) {
        foreach (GameObject player in activePlayers) {
            if (player != currentPlayer) {
                return player;
            }
        }
        return null;
    }


}