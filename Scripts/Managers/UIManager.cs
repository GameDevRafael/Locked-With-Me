using Mirror;
using Mirror.Discovery;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour {
    public GameObject swampTrees;
    public GameObject villageTrees;
    public GameObject carruagens;
    public GameObject streetLamps;
    public GameObject rocks;
    public GameObject nests;
    public GameObject crickets;
    public GameObject boats;
    public GameObject benches;
    public GameObject wells;
    public GameObject bridges;
    public GameObject houses;
    public GameObject wood;
    public float spawnInterval = 0.1f; // the spawn timer between each game object

    // buttons
    [Header("Buttons")]
    public GameObject jumpButton;
    public GameObject swapButton;
    public GameObject interactItemButton;
    public GameObject gamemodeButtons;
    public FixedJoystick joystick;
    public GameObject interactRocketLauncherButton;

    // PLAYER INFO AND STUFF
    [Header("Player Info & Stuff")]
    public Image fadeImage;
    public GameObject[] hearts;
    public Sprite grayHeartImage;
    private PlayerMovement localPlayerReference;
    private InventoryManager localInventoryReference;
    private Vector3 positionBeforeHideInChest;
    private Coroutine currentCoroutine; // the time interval for the pop up to appear
    private GameObject rocketLauncherMissile;
    private bool isInGodMode = false;
    private Transform chest; // the chest in which the player entered so we can activate its box collider after he leaves


    // MENUS
    [Header("Menus")]
    public GameObject pauseMenu;
    public GameObject gamemodeMenu;
    public GameObject gameInterface;
    public GameObject startMenu;
    public GameObject settingsMenu;
    public GameObject instructionsMenu;
    public GameObject gameOverInterface;
    public GameObject youWinInterface;
    public GameObject hostButtonButton;

    // SERVIDOR
    [Header("Server")]
    public NetworkDiscovery networkDiscovery; // searches for servers
    public CustomNetworkManager networkManager;
    public GameObject serverListPanel; // menu where the servers will be shown
    public GameObject serverEntryPrefab; // menu that has the servers that the players can click on to join games
    public Transform serverListContent; // servers' objects
    public Button refreshButton; // update the informations of the current servers
    // dictionairy that stores the server's IP and their information
    private Dictionary<string, ServerResponse> discoveredServers = new Dictionary<string, ServerResponse>();
    private bool isNetworkShuttingDown = false;
    private bool isNetworkReady = true;
    private Coroutine networkCleanupCoroutine;

    [Header("PopUp")]
    public GameObject interactCaption;
    public GameObject interactPopUp;
    public Image interactWarning;
    public Sprite warningLow;
    public Sprite warningMedium;
    public Sprite warningHigh;


    public static UIManager Instance;


    void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start() {
        // when i go back to the menu i have to find these because of the dont destroy on load
        networkDiscovery = GameObject.Find("NetworkManager").GetComponent<NetworkDiscovery>();
        networkManager = GameObject.Find("NetworkManager").GetComponent<CustomNetworkManager>();

        // when the players goes back to the menu for new games i have to reset the network and not only the scene because the network manager is dont destroy on load
        if (NetworkClient.isConnected || NetworkServer.active || NetworkClient.active) {
            StartNetworkCleanup();
        }

        discoveredServers.Clear(); // if i go back to the menu then reset
        // (it's already attributed on the inspector, but just in case)
        networkDiscovery.OnServerFound.AddListener(OnDiscoveredServer);

        StartCoroutine(SpawnTerrainObjects());
    }

    IEnumerator SpawnTerrainObjects() {
        yield return StartCoroutine(ActivateObjectsWithDelay(houses));
        yield return StartCoroutine(ActivateObjectsWithDelay(villageTrees));
        yield return StartCoroutine(ActivateObjectsWithDelay(streetLamps));
        yield return StartCoroutine(ActivateObjectsWithDelay(carruagens));
        yield return StartCoroutine(ActivateObjectsWithDelay(rocks));
        yield return StartCoroutine(ActivateObjectsWithDelay(swampTrees));
        yield return StartCoroutine(ActivateObjectsWithDelay(nests));
        yield return StartCoroutine(ActivateObjectsWithDelay(crickets));
        yield return StartCoroutine(ActivateObjectsWithDelay(boats));
        yield return StartCoroutine(ActivateObjectsWithDelay(benches));
        yield return StartCoroutine(ActivateObjectsWithDelay(wells));
        yield return StartCoroutine(ActivateObjectsWithDelay(bridges));
        yield return StartCoroutine(ActivateObjectsWithDelay(wood));
    }


    IEnumerator ActivateObjectsWithDelay(GameObject parentObject) {
        for (int i = 0; i < parentObject.transform.childCount; i++) {
            Transform child = parentObject.transform.GetChild(i);
            if (child.gameObject.activeSelf)
                continue;

            child.gameObject.SetActive(true);
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void StartNetworkCleanup() {
        isNetworkShuttingDown = true;
        isNetworkReady = false;

        // i stop the current network and start a new one, this is needed in case the player spams the button of going back to the menu that might cause problems
        // like this im sure it's only made one new corroutine when there is none
        if (networkCleanupCoroutine != null) {
            StopCoroutine(networkCleanupCoroutine);
        }

        networkCleanupCoroutine = StartCoroutine(CleanupNetworkComponents());
    }

    private IEnumerator CleanupNetworkComponents() {
        // deactivate the buttons of finding games because the network has to reset first
        SetNetworkButtonsInteractable(false);

        // stop the server, client and host
        if (NetworkServer.active) {
            networkManager.StopHost();
            yield return new WaitForSeconds(0.2f);
        }

        if (NetworkClient.isConnected) {
            networkManager.StopClient();
            yield return new WaitForSeconds(0.2f);
        }

        if (NetworkServer.active && !NetworkClient.isConnected) {
            networkManager.StopServer();
            yield return new WaitForSeconds(0.2f);
        }

        // stop finding games
        networkDiscovery.StopDiscovery();

        // wait a little bit longer to confirm it stopped correctly
        yield return new WaitForSeconds(0.5f);

        // now it's ready to re activate the buttons
        isNetworkShuttingDown = false;
        isNetworkReady = true;

        SetNetworkButtonsInteractable(true);

        networkCleanupCoroutine = null;
    }



    private void SetNetworkButtonsInteractable(bool interactable) {
        // disable/enable host button
        if (hostButtonButton != null) {
            Button hostBtn = hostButtonButton.GetComponent<Button>();
            if (hostBtn != null) {
                hostBtn.interactable = interactable;
            }
        }

        // disable/enable other network-related buttons
        Button[] networkButtons = gamemodeButtons.GetComponentsInChildren<Button>();
        foreach (Button btn in networkButtons) {
            btn.interactable = interactable;
        }

        if (refreshButton != null) {
            refreshButton.interactable = interactable;
        }
    }

    private bool CanPerformNetworkAction() {
        return isNetworkReady && !isNetworkShuttingDown &&
               !NetworkClient.isConnected && !NetworkServer.active && !NetworkClient.active;
    }

    // this is called everytime network discovery finds a server, so i add it to the dictionairy
    public void OnDiscoveredServer(ServerResponse info) {
        string serverIP = info.uri.ToString(); // this catches the servers' IP

        // if the IP doesn't exist then it adds, if it already exists nothing happens
        // this way i prevent many butons to be created for the same server
        if (discoveredServers.ContainsKey(serverIP) == false) {
            discoveredServers[serverIP] = info;
            UpdateServerList();
        }
    }

    // because i change the list of found servers i also have to change the UI so i deleted everything and re add
    private void UpdateServerList() {
        int contador = 1;

        foreach (Transform child in serverListContent) {
            Destroy(child.gameObject);
        }

        foreach (ServerResponse info in discoveredServers.Values) {
            GameObject serverEntry = Instantiate(serverEntryPrefab, serverListContent);

            TextMeshProUGUI textComponent = serverEntry.GetComponentInChildren<TextMeshProUGUI>();
                textComponent.text = $"Game #{contador}"; // number of servers (games / hosts) that exist

            // i add an event listener and when the button is clicked the client enters the server
            Button joinButton = serverEntry.GetComponentInChildren<Button>();
            joinButton.onClick.AddListener(() => JoinServer(info));

            contador++;
        }

        // when the player wants to join the host the servers' list appears
        serverListPanel.SetActive(true);
    }

    public void JoinServer(ServerResponse info) {
        networkManager.StartClient(info.uri); // connect the client to the correct server with the host's IP
        hideGamemodeMenuShowGameInterface();
        serverListPanel.SetActive(false);
        networkDiscovery.StopDiscovery();
    }

    public void RefreshServerList() {
        discoveredServers.Clear(); // everytime it searches for new servers it resets everything in case the information changes
        networkDiscovery.StartDiscovery();
        // i dont update the UI because the action listener of the start discovery already does that on OnDiscoveredServer
    }


    public void SetLocalPlayer(PlayerMovement player) {
        localPlayerReference = player;
        localPlayerReference.CmdSetGodMode(isInGodMode); // this only chanves the syncvar of the player in question, it's not for all of the players
        localInventoryReference = player.transform.GetComponent<InventoryManager>();
    }

    // START MENU
    public void startButton() {
        startMenu.SetActive(false);
        gamemodeMenu.SetActive(true);
    }

    public void quitButton() {
    #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
    #else
        Application.Quit();
    #endif
    }

    public void settingsButton() {
        startMenu.SetActive(false);
        settingsMenu.SetActive(true);
    }

    public void instructionsButton() {
        startMenu.SetActive(false);
        instructionsMenu.SetActive(true);
    }

    // change the player's tag so it's invisible to the NPCs
    // because i dont have the player yet on the main menu i can instead keep the future tag for him when i do have him
    public void GodMod() {
        Toggle toggle = settingsMenu.transform.Find("Toggle").GetComponent<Toggle>();
        bool isON = toggle.isOn;

        if (isON) {
            isInGodMode = true;

        } else {
            isInGodMode = false;
        }
    }

    public void pauseMenuButton() {
        if (gameInterface.activeSelf) {
            gameInterface.SetActive(false);
            pauseMenu.SetActive(true);

            if (localPlayerReference != null) {
                CameraScript playerCamera = localPlayerReference.GetComponentInChildren<CameraScript>();
                playerCamera.canRotate = false;
            }

        } else if (pauseMenu.activeSelf) {
            pauseMenu.SetActive(false);
            gameInterface.SetActive(true);

            if (localPlayerReference != null) {
                CameraScript playerCamera = localPlayerReference.GetComponentInChildren<CameraScript>();
                playerCamera.canRotate = true;
            }
        }
    }


    // GAMEMODE MENU
    public void hostButton() {
        if (!NetworkClient.isConnected && !NetworkServer.active && !NetworkClient.active) {
            networkManager.StartHost();
            networkDiscovery.AdvertiseServer();
            hideGamemodeMenuShowGameInterface();
            handleNetworkHUD();
        }
    }

    /*public void clienteButton() {
        if (!NetworkClient.isConnected && !NetworkServer.active && !NetworkClient.active) {
            networkManager.StartClient();
            networkDiscovery.AdvertiseServer();
            hideGamemodeMenuShowGameInterface();
            handleNetworkHUD();
        }
    }*/
    public void clienteButton() {
        if (!NetworkClient.isConnected && !NetworkServer.active && !NetworkClient.active) {
            GameObject.Find("GamemodeButtons").SetActive(false);
            serverListPanel.SetActive(true);
            RefreshServerList();
        }
    }

    public void singlePlayerButton() {
        hostButton();
    }

    public void backButton(int num) {
        switch (num) {
            // show the main menu when im in the gamemodes menu
            case 0:
                gamemodeMenu.SetActive(false);
                startMenu.SetActive(true);
                break;

            // when im searching for servers
            case 1:
                gamemodeButtons.SetActive(true);
                // stop looking for servers and remove the panel that shows themp
                networkDiscovery.StopDiscovery();
                serverListPanel.SetActive(false);
                break;

            // when im in the setting's menu
            case 2:
                settingsMenu.SetActive(false);
                startMenu.SetActive(true);
                break;

            case 3:
                instructionsMenu.SetActive(false);
                startMenu.SetActive(true);
                break;
        }
    }

    public void hideGamemodeMenuShowGameInterface() {
        gamemodeMenu.SetActive(false);
        gameInterface.SetActive(true);
    }

    public void handleNetworkHUD() {
        if (NetworkClient.isConnected && !NetworkClient.ready) {
            NetworkClient.Ready();
        }
    }

    private void OnDestroy() {
        if (networkDiscovery != null) {
            networkDiscovery.StopDiscovery();
        }
    }

    public void InteractWithObject() {
        if (localPlayerReference.isInChest) {
            localPlayerReference.LeaveChest(positionBeforeHideInChest);
            localPlayerReference.playerCamera.GetComponent<CameraScript>().insideChest = false;
            chest.GetComponentInChildren<BoxCollider>().enabled = true;
        }

        Ray ray = localPlayerReference.GetComponentInChildren<Camera>().ScreenPointToRay(new Vector2(Screen.width / 2f, Screen.height / 2f));

        if (Physics.Raycast(ray, out RaycastHit hit, 4f)) {
            switch (hit.transform.tag) {
                case "chest":
                    chest = hit.transform;
                    chest.GetComponentInChildren<BoxCollider>().enabled = false;
                    positionBeforeHideInChest = localPlayerReference.transform.position;
                    // i cant pass the transform because chests dont have the component network identity and it doesnt make sense giving it to them, so i pass their position
                    localPlayerReference.HideInChest(hit.transform.position);
                    localPlayerReference.playerCamera.GetComponent<CameraScript>().insideChest = true;
                    break;

                case "key":
                    GameObject keyAddedSlot = localInventoryReference.AddItem(hit.transform.parent.name);
                    bool wasKeyAdded = keyAddedSlot != null;

                    if (wasKeyAdded == false) {
                        ShowCaptionOnce("Inventory is full!", 1);
                    } else {
                        localPlayerReference.CmdDestroyObject(hit.transform.parent.gameObject); // im sending the child here, the networkIdentity is on the parent
                        SoundManager.Instance.PlaySound(SoundManager.Instance.grabItemSound);
                    }
                    break;

                case "lock":
                    bool hasKey = localPlayerReference.OpenLock(hit.transform); // here tho im sending the parent
                    if (hasKey == false) {
                        ShowCaptionOnce("If only I had a key...", 2);
                    } else {
                        SoundManager.Instance.CmdPlaySound(hit.transform.gameObject);
                        localPlayerReference.CmdLockFall(hit.transform.gameObject);
                        localInventoryReference.RemoveItem(localInventoryReference.FindKey(hit.transform.name));
                        hit.transform.GetComponent<DoorAttachedToLockScript>().lockedDoor.GetComponent<LockScript>().CmdUnlockDoor();
                    }
                    break;

                case "rocketLauncher":
                    if (localPlayerReference.hasRocketLauncher == false) {
                        GameObject rocketLauncherSlot = localInventoryReference.AddItem(hit.transform.name);
                        bool wasRocketLauncherAdded = rocketLauncherSlot != null;

                        if (wasRocketLauncherAdded == false) {
                            ShowCaptionOnce("Inventory is full!", 1);

                        } else {
                            localPlayerReference.EquipRocketLauncher(hit.transform.gameObject);
                            SoundManager.Instance.PlaySound(SoundManager.Instance.grabItemSound);
                            rocketLauncherMissile = hit.transform.Find("missile").gameObject;
                            // because when i was looking at it i could click on it and gain more rocket launcher, like this the hand's button doesnt become active
                            hit.transform.tag = "Untagged";
                            hit.transform.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast"); // so it's not ahead of the camera's raycasts once we grab it

                            // if the portal is already open it's because the player already started looking for the other rockets so i dont have to show the caption again
                            if(GameManager.Instance.portal.activeSelf == false)
                                ShowCaptionOnce("There are more inside the portal!", 2);
                            localPlayerReference.CmdActivatePortal();
                        }
                    } else {
                        ShowCaptionOnce("Don't be greedy!", 1);
                    }

                        break;

                case "missile":
                    if(localInventoryReference.HasItem("rocketLauncher")) {
                        GameObject missileAddedSlot = localInventoryReference.AddItem(hit.transform.name);
                        bool wasMissileAdded = missileAddedSlot != null;

                        if (wasMissileAdded == false) {
                            ShowCaptionOnce("Inventory is full!", 1);

                        } else {
                            localPlayerReference.CmdDestroyObject(hit.transform.gameObject);
                            SoundManager.Instance.PlaySound(SoundManager.Instance.grabItemSound);
                            interactRocketLauncherButton.GetComponent<Button>().interactable = true;

                            if (rocketLauncherMissile.activeSelf == false)
                                rocketLauncherMissile.SetActive(true);
                        }
                    } else {
                        ShowCaptionOnce("You need a Rocket Launcher...", 0);
                    }
                        break;

                default:
                    if(hit.transform.gameObject.CompareTag("door")) {
                        if(hit.transform.GetComponent<LockScript>() != null) {
                            if (hit.transform.GetComponent<LockScript>().isLocked == false) {
                                localPlayerReference.InteractDoor(hit.transform);
                            } else {
                                ShowCaptionOnce("It must be locked...", 1);
                            }
                        } else {
                            localPlayerReference.InteractDoor(hit.transform);
                        }
                    }
                    break;
            }
        }
    }

    public void InteractRocketLauncher() {
        InventoryItem missile = localInventoryReference.FindItem("missile");

        if (missile != null)
            Debug.Log("quantidade de misseis antes de usar: " + missile.quantity);
        else
            Debug.Log("quantidade de misseis antes de usar: " + 0);

        if (missile != null && missile.quantity > 0) {
            localInventoryReference.FindItem("rocketLauncher").Use(localPlayerReference.gameObject);
            localInventoryReference.RemoveItem("missile", 1);
            Debug.Log("quantidade de misseis depois de usar: " + missile.quantity);


        } else {
            ShowCaptionOnce("No ammo!", 1);
        }

        // search again because there may exist another stack
        if (localInventoryReference.FindItem("missile") == null || localInventoryReference.FindItem("missile").quantity == 0) {
            interactRocketLauncherButton.GetComponent<Button>().interactable = false;
            rocketLauncherMissile.SetActive(false);

        }
    }

    // when the player dies its screen is going to be black, for that i use a black image with full transparency that is slowly losing it and become opaque
    // i do it slowly, i only want to change its alpha values
    public IEnumerator FadeToBlack(float duration, PlayerMovement playerMovement) {
        float elapsed = 0f;
        fadeImage.gameObject.SetActive(true);

        Color startColor = fadeImage.color;
        Color targetColor = new Color(startColor.r, startColor.g, startColor.b, 1f);

        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration); // clamp01 because it makes it go from 0 to 1
            fadeImage.color = Color.Lerp(startColor, targetColor, t);
            yield return null; // this tells unity to continue the code on the ext frame (to be smoother)
        }

        fadeImage.color = targetColor;
        playerMovement.gameObject.GetComponentInChildren<AudioListener>().enabled = false;

        // wait a little bit before respawning
        yield return new WaitForSeconds(1f);

        if (playerMovement.playerLives != 0) {
            playerMovement.Reborn();

        } else {
            fadeImage.gameObject.SetActive(false);
            fadeImage.color = new Color(startColor.r, startColor.g, startColor.b, 0f); // reset to 0 transparency again
        }

    }

    public IEnumerator SetPlayerDeadBackground() {
        yield return new WaitForSeconds(2f); // wait 2 seconds because of the black image
        // the image is on the gameInterface so i only deactivate it after the image is ok
        gameInterface.SetActive(false);
        gameOverInterface.SetActive(true);

        // place the text on the top of the screen
        Transform gameOverText = gameOverInterface.transform.Find("GameOverText").transform;
        Vector3 pos = gameOverText.position;
        pos.y = 350;
        gameOverText.position = pos;

        gameOverInterface.transform.Find("MainMenu").gameObject.SetActive(true);
        gameOverInterface.transform.Find("QUIT").gameObject.SetActive(true);

        if (GameManager.Instance.isSinglePlayer == false) {
            // place the camera of the player that died on the other player in order to spectate
            GameObject otherPlayer = CustomNetworkManager.Instance.FindOtherPlayer(localPlayerReference.gameObject);
            Camera otherPlayerCamera = otherPlayer.GetComponentInChildren<CameraScript>().gameObject.GetComponent<Camera>();

            localPlayerReference.playerCamera.transform.SetParent(otherPlayerCamera.transform.parent);
            // it's not enought to simply place it on the correct transform, i also have to reset the position and rotation
            // alongside with the camera, the special compomnent and the script (so i cant move the camera)
            // but leave the audio listener so i can listen
            localPlayerReference.playerCamera.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            //localPlayerReference.CmdDestroyPlayer();
        }
            



    }


    public void ShowCaptionOnce(string text, int warningLevel) {
        if (currentCoroutine != null) {
            StopCoroutine(currentCoroutine);
        }

        currentCoroutine = StartCoroutine(ShowInteractPopUp(text, warningLevel));
    }

    private IEnumerator ShowInteractPopUp(string text, int warningLevel) {
        switch (warningLevel) {
            case 0:
                interactWarning.sprite = warningLow;
                break;
            case 1:
                interactWarning.sprite = warningMedium;
                break;
            case 2:
                interactWarning.sprite = warningHigh;
                break;
        }

        interactCaption.GetComponent<TextMeshProUGUI>().text = text;
        interactPopUp.SetActive(true);
        yield return new WaitForSeconds(2.5f);
        interactPopUp.SetActive(false);
        currentCoroutine = null;
    }



    public void BackToMainMenu(bool cameFromPauseButton) {
        if (cameFromPauseButton) {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name); // reload the scene for the game to start over

        } else
            StartCoroutine(CoroutineBackToMenu(2)); // 2 seconds because the black image takes 2 seconds to come
    }

    private IEnumerator CoroutineBackToMenu(int time) {
        yield return new WaitForSeconds(time);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name); // reload the scene of the game to start over
    }

    // this is called from a block if(isServer)
    public void SetYouWinInterface() {
        if (GameManager.Instance.isSinglePlayer) {
            ShowYouWinForLocalPlayer();
        } else {
            GameManager.Instance.RpcSetYouWin();
        }
    }

    public void ShowYouWinForLocalPlayer() {
        if (localPlayerReference != null) {
            localPlayerReference.gameOver = true;
            localPlayerReference.playerCamera.GetComponent<CameraScript>().canRotate = false;
            youWinInterface.SetActive(true);
        }
    }


}
