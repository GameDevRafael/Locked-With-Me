using Mirror;
using Mirror.Discovery;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.SceneManagement;

/**
 * tive de fazer um UIManager classe porque n�o estava a conseguir obter os bot�es nem o joystick ap�s criar os players atrav�s do networkManagers, ent�o fiz esta classe
 * para termos acesso a esses objetos de outra forma
 */
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
    public float spawnInterval = 0.1f; // o intervalo entre cada objeto

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
    private Coroutine currentCoroutine; // da quantidade de tmepo para o pop up aparecer
    private GameObject rocketLauncherMissile;
    private bool isInGodMode = false;
    private Transform chest; // o chest em que o jogador entrou, para podermos voltar a ativar o box collider dele


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
    public NetworkDiscovery networkDiscovery; // procura por servidores
    public CustomNetworkManager networkManager;
    public GameObject serverListPanel; // menu onde v�o mostrar os servidores
    public GameObject serverEntryPrefab; // menu que tem os servidores onde podemos clicar neles e juntarmo-nos
    public Transform serverListContent; // objetos dos servidores
    public Button refreshButton; // atualizar informa��es dos servidores atuais
    // dicion�rio dos servidores que guarda o IP do servidor com a informa��o do servidor
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
        // para quando voltamos ao menu temos de voltar a apanhar estes componentes porque o custom network manager tem DontDestoryOnLoad
        networkDiscovery = GameObject.Find("NetworkManager").GetComponent<NetworkDiscovery>();
        networkManager = GameObject.Find("NetworkManager").GetComponent<CustomNetworkManager>();

        // quando o jogador volta ao menu inicial para fazer novos jogos temos de resetar a network porque resetar a cena n�o basta visto que
        // o network manager est� dont destroy on load
        if (NetworkClient.isConnected || NetworkServer.active || NetworkClient.active) {
            StartNetworkCleanup();
        }

        discoveredServers.Clear(); // se tivermos voltado ao menu limpamos tudo
        // ja atribu� isto no inspetor do componente no gameobject do network manager, mas para ter a certeza que funciona meto aqui tamb�m
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

        // paramos a atual e come�amos uma nova, isto � preciso caso o jogador spame o bot�o de voltar ao menu ent�o pode causar problemas
        // assim temos a certeza que s� � gerada uma corrotina nova quando �n�o houver nenhuma
        if (networkCleanupCoroutine != null) {
            StopCoroutine(networkCleanupCoroutine);
        }

        networkCleanupCoroutine = StartCoroutine(CleanupNetworkComponents());
    }

    private IEnumerator CleanupNetworkComponents() {
        // desativamos os bot�es de come�ar jogos porque a network tem de resetar primeiro
        SetNetworkButtonsInteractable(false);

        // paramos o server o cliente e o host
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

        // paramos de descobrir jogos come�ados
        networkDiscovery.StopDiscovery();

        // esperamos mais um poouco para confirmar que parou corretamente
        yield return new WaitForSeconds(0.5f);

        // e agora j� estamos prontos e ativamos de volta os bot�es
        isNetworkShuttingDown = false;
        isNetworkReady = true;

        SetNetworkButtonsInteractable(true);

        networkCleanupCoroutine = null;
    }



    private void SetNetworkButtonsInteractable(bool interactable) {
        // Disable/enable host button
        if (hostButtonButton != null) {
            Button hostBtn = hostButtonButton.GetComponent<Button>();
            if (hostBtn != null) {
                hostBtn.interactable = interactable;
            }
        }

        // Disable/enable other network-related buttons
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

    // isto � chamado sempre que o newtwork discovery encontra um servidor, ent�o adicionamos ao dicion�rio
    public void OnDiscoveredServer(ServerResponse info) {
        string serverIP = info.uri.ToString(); // isto apanha o IP do servidor (depois metemos em string)

        // se o IP n�o existir ent�o adiciona, se j� existir n�o faz nada
        // assim impedimos que v�rios bot�es sejam criados para o mesmo servidor
        if (discoveredServers.ContainsKey(serverIP) == false) {
            discoveredServers[serverIP] = info;
            UpdateServerList();
        }
    }

    // Como alteramos a lista de servidores descobertos tamb�m temos de alterar a UI ent�o apagamos tudo e voltamos a meter
    private void UpdateServerList() {
        int contador = 1;

        foreach (Transform child in serverListContent) {
            Destroy(child.gameObject);
        }

        foreach (ServerResponse info in discoveredServers.Values) {
            GameObject serverEntry = Instantiate(serverEntryPrefab, serverListContent);

            TextMeshProUGUI textComponent = serverEntry.GetComponentInChildren<TextMeshProUGUI>();
                textComponent.text = $"Game #{contador}"; // n�mero de servidores (jogos/hosts) que existem

            // metemos um event listener e quando o bot�o for clicado o cliente entra no servidor
            Button joinButton = serverEntry.GetComponentInChildren<Button>();
            joinButton.onClick.AddListener(() => JoinServer(info));

            contador++;
        }

        // quando o jogador quiser juntar-se ao host aparece a lista de servidores
        serverListPanel.SetActive(true);
    }

    public void JoinServer(ServerResponse info) {
        networkManager.StartClient(info.uri); // conectamos o cliente ao servidor correto com o IP do host
        hideGamemodeMenuShowGameInterface();
        serverListPanel.SetActive(false);
        networkDiscovery.StopDiscovery();
    }

    // esta fun��o procura por servidores
    public void RefreshServerList() {
        discoveredServers.Clear(); // sempre que procuramos servidores novos resetamos tudo para o caso da informa��o mudar
        networkDiscovery.StartDiscovery();
        // n�o atualizamos a UI porque o action listener do start discovery ja faz isso OnDiscoveredServer
    }


    public void SetLocalPlayer(PlayerMovement player) {
        localPlayerReference = player;
        localPlayerReference.CmdSetGodMode(isInGodMode); // isto s� muda a syncvar do jogador em quest�o, n�o s�o de todos os jogadores que vai mudar
        localInventoryReference = player.transform.GetComponent<InventoryManager>();
    }

    // START MENU
    public void startButton() {
        startMenu.SetActive(false);
        gamemodeMenu.SetActive(true);
    }

    public void quitButton() {
        // se estiver no Unity o bot�o vai parar o modo de jogar, se for a aplica��o j� constru�da ent�o sai da aplica��o
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


    // mudar a layer do jogador faz com que seja invis�vel para os NPCs
    // como ainda n�o temos o objeto do jogador no menu inicial o que podemos fazer � guardar a futura tag dele e quando o tivermos damos lhe a tag
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
            // mostrar o menu inicial quando est�s no menu de bot�es de gamemode
            case 0:
                gamemodeMenu.SetActive(false);
                startMenu.SetActive(true);
                break;

            // quando est�s � procura de servers e voltas para os bot�es de gamemodes
            case 1:
                gamemodeButtons.SetActive(true);
                // paramos de procurar por servidores e tiramos o pain�l que os mostra
                networkDiscovery.StopDiscovery();
                serverListPanel.SetActive(false);
                break;

            // quando est�mos no menu de settings e queres ovltar ao menu inicial
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
                    localPlayerReference.HideInChest(hit.transform.position); // n�o posso passar o transform porque os ba�s n�o t�m a componente Netowrk Identity e n�o faz sentido tar a meter, ent�o passo a posi��o
                    localPlayerReference.playerCamera.GetComponent<CameraScript>().insideChest = true;
                    break;

                case "key":
                    GameObject keyAddedSlot = localInventoryReference.AddItem(hit.transform.parent.name);
                    bool wasKeyAdded = keyAddedSlot != null;

                    if (wasKeyAdded == false) {
                        ShowCaptionOnce("Inventory is full!", 1);
                    } else {
                        localPlayerReference.CmdDestroyObject(hit.transform.parent.gameObject); // aqui estamos a mandar o child, a networkIdentity est� no parent
                        SoundManager.Instance.PlaySound(SoundManager.Instance.grabItemSound);
                    }
                    break;

                case "lock":
                    bool hasKey = localPlayerReference.OpenLock(hit.transform); // aqui estamos a mandar o parent
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
                            hit.transform.tag = "Untagged"; // porque quando olhavas para ele podias clicar nele e ganhar mais rocket launchers, assim o bot�o na m�o n�o fica ativo
                            hit.transform.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast"); // para n�o ficar � frente dos raycasts da c�mara uma vez que o apanhamos

                            // se o portal j� estiver aberto � porque os jogadores j� foram � procura dos outros rockets por isso n�o mostramos outra vez a caption
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

        if (localInventoryReference.FindItem("missile") == null || localInventoryReference.FindItem("missile").quantity == 0) { // procuramos novamente porque podemos ter outro stack
            interactRocketLauncherButton.GetComponent<Button>().interactable = false;
            rocketLauncherMissile.SetActive(false);

        }
    }

    // quando o jogador morrer o seu ecr� vai ficar preto, para tal usamos uma imagem preta com full transpar�ncia que lentamente vai perdendo transpar�ncia e fica opaca
    // fazemos isso lentamente, a imagem � preta, s� queremos mudar o alfa, � medida que o tempo avan�a o alfa vai aumentando
    public IEnumerator FadeToBlack(float duration, PlayerMovement playerMovement) {
        float elapsed = 0f;
        fadeImage.gameObject.SetActive(true);

        Color startColor = fadeImage.color;
        Color targetColor = new Color(startColor.r, startColor.g, startColor.b, 1f);

        // com o passar do tempo metemos a imagem a ficar com cor preta
        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration); // clamp01 porque faz ir de 0 a 1
            fadeImage.color = Color.Lerp(startColor, targetColor, t);
            yield return null; // isto diz ao unity para continuar o c�digo na pr�xima frame para ser mais smooth
        }

        fadeImage.color = targetColor;
        playerMovement.gameObject.GetComponentInChildren<AudioListener>().enabled = false;

        // esperamos um pouco antes de renascermos
        yield return new WaitForSeconds(1f);

        if (playerMovement.playerLives != 0) {
            playerMovement.Reborn();

        } else {
            fadeImage.gameObject.SetActive(false);
            fadeImage.color = new Color(startColor.r, startColor.g, startColor.b, 0f); // reset para 0 transpar�ncia outra vez
        }

    }

    public IEnumerator SetPlayerDeadBackground() {
        yield return new WaitForSeconds(2f); // esperamos 2 segundos por causa da imagem preta
        // a imagem est� na gameInterface ent�o s� a desativamos depois da imagem ficar nice
        gameInterface.SetActive(false);
        gameOverInterface.SetActive(true);

        // meter o texto no topo do ecr�
        Transform gameOverText = gameOverInterface.transform.Find("GameOverText").transform;
        Vector3 pos = gameOverText.position;
        pos.y = 350;
        gameOverText.position = pos;

        gameOverInterface.transform.Find("MainMenu").gameObject.SetActive(true);
        gameOverInterface.transform.Find("QUIT").gameObject.SetActive(true);

        if (GameManager.Instance.isSinglePlayer == false) {
            // meter a c�mara do jogador que morreu no outro jogador para dar spectate
            GameObject otherPlayer = CustomNetworkManager.Instance.FindOtherPlayer(localPlayerReference.gameObject);
            Camera otherPlayerCamera = otherPlayer.GetComponentInChildren<CameraScript>().gameObject.GetComponent<Camera>();

            localPlayerReference.playerCamera.transform.SetParent(otherPlayerCamera.transform.parent);
            // n�o basta apenas met�-la no transform certo, temos de resetar a posi��o tbm e aproveitamos e fazemos para a rota��o tbm
            // juntamente com a camara o componente especial e o script (para n�o podermos mexer na c�mara)
            // mas deixamos o audio lilstener para tamb�m podermos ouvir
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
            SceneManager.LoadScene(SceneManager.GetActiveScene().name); // damos reload da scene para o jogo come�ar do zero

        } else
            StartCoroutine(CoroutineBackToMenu(2)); // 2 segundos porque a imagem preta demora 2 segundos para vir
    }

    private IEnumerator CoroutineBackToMenu(int time) {
        yield return new WaitForSeconds(time);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name); // damos reload da scene para o jogo come�ar do zero
    }

    // isto � chamado de um bloco de if(isServer)
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
