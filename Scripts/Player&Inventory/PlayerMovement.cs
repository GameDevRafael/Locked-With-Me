using UnityEngine;
using Mirror;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;


/* [Command]: falar com o servidor
 * [ClientRPC]: falar com um cliente específico ou todos os clientes
 * [SyncVars]: variáveis que podem apenas ser atualizadas pelo host/servidor e não pelo cliente, portanto têm que ser atualizadas com o [Command]
 *  - Hooks: métodos que permitem fazer lógica de código, executar ações etc, em todos os clientes sempre que uma [SyncVar] é atualizada
 *    - estes têm que ter sempre os parâmetros oldValue e newValue nos seus métodos
 * 
 * Quando usar ClientRPC vs SyncVars?
 * - para estados persistentes convém usar SyncVars, pois assim mantemos guardados os estados e atualizamos só quando necessário, são otimizadas para enviar
 *   apenas as mudanças de estado e não enviam dados repetidamente para a network se o valor não mudar. 
 * - para acontecimentos únicos ou pontuais que só ocorrem uma vez convém usar [ClientRPC], estas não mantêm o estado pois é desnecessário.
 *   Podem ser usados, por exemplo, para fazer special FX quando personagens morrem
 *   
 */

public class PlayerMovement : NetworkBehaviour {
    private CharacterController myController;
    private FixedJoystick joystick;
    private GameObject jumpButton;
    private Coroutine outsideThemeCoroutine;

    [SerializeField] Transform neck;
    private InventoryManager inventory;
    public GameObject rightHand;

    // Vida do Jogador
    public int playerLives;
    [HideInInspector] public bool gameOver;
    private bool playerHit;

    // Câmaras
    private Camera generalCamera;
    [HideInInspector] public Camera playerCamera;
    private Camera generalMinimapCamera;


    // Informações das Animações
    private Animator animator;
    private float walkSpeed = 7f;
    private float runSpeed = 20f;
    private float gravity = -9.81f;
    private float jumpHeight = 2f;
    private float inputThreshold = 0.1f;
    private float runThreshold = 0.7f;
    private float lastSentAnimSpeed = -1f; // inicializamos com -1 para que não tenha uma animação por default
    [HideInInspector] public bool isDead;


    // Informãções da Velocidade
    private float movementMagnitude;
    private float currentSpeed;
    private Vector3 velocity;
    private float horizontal;
    private float vertical;


    // Localização do Jogador
    private bool isInHouse = true;
    [HideInInspector][SyncVar] public bool isInChest;
    private float portalCooldown = 0f;
    private Vector3 spawnPoint;
    [SerializeField] private Sprite P1Icon;
    [SerializeField] private Sprite P2Icon;
    [SerializeField] public SpriteRenderer playerIcon;
    private bool isInteractingWithChest = false;




    // para estarem sincronizadas ao longo dos jogadores, é sincronizada sempre que ocorre uma alteração nas variáveis, só podem ser alteradas pelo servidor/host e não pelos clientes
    [SyncVar(hook = nameof(AnimJumpChanged))] private bool networkJumping = false;
    [SyncVar(hook = nameof(AnimSpeedChanged))] private float networkAnimSpeed = 0f;
    [SyncVar(hook = nameof(AnimCrouchChanged))] private bool networkCrouch;
    [SyncVar(hook = nameof(AnimDieChanged))] private bool networkDie;
    [SyncVar(hook = nameof(NeckRotationChanged))] private Quaternion neckRotation;
    [SyncVar(hook = nameof(AnimEquipRocketLauncherChanged))] public bool hasRocketLauncher;
    [SyncVar(hook = nameof(OnGodModeChanged))] public bool isInGodMode = false;




    private void Awake() {
        // precisamos da câmara e do animator de todos os jogadores para fazer animações / desativar componentes da camara
        generalCamera = gameObject.GetComponentInChildren<CameraScript>().gameObject.GetComponent<Camera>();
        animator = gameObject.GetComponent<Animator>();
        generalMinimapCamera = GetComponentInChildren<MinimapPlayerCamera>().gameObject.GetComponent<Camera>();
        playerIcon = GetComponentInChildren<SpriteRenderer>();
    }

    void Start() {
        myController = GetComponent<CharacterController>(); // precisamos do controller de ambos jogadores para podermos fazer o swap bodies

        if (!isLocalPlayer) {
            // se não é o jogador local então não vai ter a câmara ligada
            // desativo todos os seus componentes porque preciso do gameobject da câmara ligado para que a vela apareça e seja bem rotacionada/posicionada
            generalCamera.enabled = false;
            generalCamera.gameObject.GetComponent<AudioListener>().enabled = false;
            generalCamera.gameObject.GetComponent<Camera>().enabled = false;
            generalCamera.gameObject.GetComponent<UniversalAdditionalCameraData>().enabled = false;
            generalCamera.gameObject.GetComponent<CameraScript>().enabled = false;
            generalMinimapCamera.gameObject.SetActive(false); // só queremos o mini mapa do jogador local
            return;
        }

        playerIcon.sprite = P1Icon; // como somos o jogador local é porque somos o principal e então somos o jogador 1
        // mas não podemos definir já o ícone do outro jogador porque poderá ainda não estar no jogo então fazemos dentro da corrotina
        StartCoroutine(SetupOtherPlayerWhenReady());


        inventory = gameObject.GetComponent<InventoryManager>();
        playerCamera = gameObject.GetComponentInChildren<CameraScript>().gameObject.GetComponent<Camera>();
        playerLives = 3;

        UIManager.Instance.SetLocalPlayer(this);

        jumpButton = UIManager.Instance.jumpButton;
        joystick = UIManager.Instance.joystick;

        // não consigo meter action lisiteners no unity porque os scripts que não extendem MonoBehaviour não são reconhecidos
        jumpButton.GetComponent<Button>().onClick.AddListener(Jump);

        spawnPoint = transform.position;

        SoundManager.Instance.PlaySound(SoundManager.Instance.rainSound);
        // carregamos previamente porque a musica fazia o jogo freezar um pouco porque era muito para ser carregado
        SoundManager.Instance.outsideTheme.clip.LoadAudioData();
    }


    private IEnumerator SetupOtherPlayerWhenReady() {
        while (CustomNetworkManager.Instance.FindOtherPlayer(gameObject) == null) {
            yield return new WaitForSeconds(0.1f); // enquanto o outro jogador não estiver no jogo esperamos (caso sejamos o host, se formos o cliente não esperamos)
        }

        GameObject otherPlayer = CustomNetworkManager.Instance.FindOtherPlayer(gameObject);
        otherPlayer.GetComponent<PlayerMovement>().playerIcon.sprite = P2Icon;
    }


    /*
     * FixedUpdate: quando se trabalha com física usamos este update. 
     * Trabalhamos com a gravidade e adicionamo-la ao jogador e fazemos o mesmo mover-se com o CharacterController que lida com colisões e movimentos físicos.
     */
    void FixedUpdate() {
        if (!isLocalPlayer) {
            return;
        }

        velocity.y += gravity * Time.deltaTime; // gravidade porque não estamos a usar rigidbody e sim um character controller e este não tem gravidade

        // se o input do joystick for muito fraco vai ser ignorado, portanto vamos permanecer parados
        if (movementMagnitude < inputThreshold) {
            myController.Move(velocity * Time.deltaTime);
            return;
        }

        // calculamos o movimento com base na direção da câmara
        Vector3 movement = CalculateMovementDirection();

        // usamos o controlador para mover o jogador, juntamos a direção baseada na câmara com a velocidade baseada no joystick
        myController.Move(currentSpeed * Time.deltaTime * movement + velocity * Time.deltaTime);
    }


    /*
     * No Update normal coloco o resto do código que não faria sentido estar no FixedUpdate, e como não trabalho com a câmara não precisamos de um LateUpdate
     */
    void Update() {
        if (!isLocalPlayer || isDead || gameOver) {
            return;
        }

        if (portalCooldown > 0) {
            portalCooldown -= Time.deltaTime;
        }

        float deltaTime = Time.deltaTime;

        // se estiver no baú não se pode mexer
        if (isInChest == false) {
            // apanhamos o verticar e horizontal, isto funciona igual como se tivéssemos a usar o Input para o caso do computador
            horizontal = joystick.Horizontal;
            vertical = joystick.Vertical;

            movementMagnitude = new Vector2(horizontal, vertical).magnitude; // precisamos disto para definir se o jogador vai andar ou correr

            // basicamente se o input for acima do limite para correr ele corre, se for abaixo ele anda
            if (movementMagnitude > runThreshold) {
                //currentSpeed = isInHouse ? walkSpeed : runSpeed; // se o jogador estiver dentro de casa só pode andar
                currentSpeed = walkSpeed;

            } else {
                currentSpeed = walkSpeed;
            }
        }

        // corremos as animãções e rodamos o pescoço/corpo do jogador
        UpdateAnimator(deltaTime);
        RotateCharacter(deltaTime);
    }

    /*
     * para atualizar as animações só temos de ver a magnitude do movimento e dependendo do mesmo conseguimos ver se ele está parado, a correr ou a andar
     */
    private void UpdateAnimator(float deltaTime) {
        float animationValue;

        if (movementMagnitude < inputThreshold) {
            animationValue = 0f; // idle
        } else if (movementMagnitude > runThreshold) {
            animationValue = isInHouse ? 1f : 2f; // walk or run
        } else {
            animationValue = 1f; // walk
        }

        animator.SetFloat("Speed", animationValue, 0.1f, deltaTime);

        // como o estado da animação dos jogadores é algo que vai ser alterado e temos de manter esse estado, usamos uma [SyncVar] lastSentAnimSpeed
        // sempre que a animação anterior é igual à atual vai ser 0 pois 0-0 = 0, 2-2=0, 1-1=0, mas quando são diferentes o total já não será 0, então se o módulo for maior que 0 é porque mudou
        if (Mathf.Abs(animationValue - lastSentAnimSpeed) > 0f) {
            lastSentAnimSpeed = animationValue; // atualizamos a última animação usada
            CmdUpdateAnimationSpeed(animationValue); // atualizamos a variável no servidor
        }
    }

    // relembro que só podemos atualizar [SyncVar] no servidor, então temos de usar o [Command] que serve para comunicar com o servidor
    [Command]
    private void CmdUpdateAnimationSpeed(float speed) {
        networkAnimSpeed = speed; // ao atualizarmos a SyncVar, vai dar trigger no hook em todos os clientes
    }

    // este é o hook que é chamado quando a [SyncVar] é alterada e é chamado em todos os clientes, vamos ignorar o jogador local porque o seu código já foi efetuado, este é apenas para dizer aos não locais
    // que têm de mostrar a animação
    void AnimSpeedChanged(float oldValue, float newValue) {
        if (!isLocalPlayer) {
            animator.SetFloat("Speed", newValue);
        }
    }

    void AnimCrouchChanged(bool oldValue, bool newValue) {
        if (!isLocalPlayer) {
            animator.SetBool("Crouch", newValue);
        }
    }

    void AnimDieChanged(bool oldValue, bool newValue) {
        if (!isLocalPlayer) {
            animator.SetBool("Die", newValue);
        }
    }




    /*
     * aqui é onde o character do jogador irá rodar, depende do quão o utilizar esteja a girar a câmara
     * se rodarmos a câmara menos de 40 graus, então só giramos o pescoço do character, senão giramos o corpo para simular uma certa realidade e naturalidade
     */
    private void RotateCharacter(float deltaTime) {
        // apanhamos a "frente" da câmara
        Vector3 cameraForward = generalCamera.transform.forward;

        // se estivermos a mover o boneco então rodamo-lo para nos orientarmos de acordo com a direção do movimento e com a rotação para onde esse movimento está a "apontar"
        if (movementMagnitude >= inputThreshold) {
            Vector3 moveDirection = CalculateMovementDirection();
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * deltaTime); // para a rotação ser mais smooth usamos um Slerp que é basicamente um Lerp mas para ângulos

        } else { // se não nos estivermos a mover então rodamos de acordo com a direção da câmara
            cameraForward.Normalize(); // se não estiver normalizada o pescoço não roda, geralmente o normalize arranja os problemas porque mete os valores entre 0 e 1
            Vector3 characterForward = transform.forward; // para onde o boneco está a olhar

            // temos de guardar para onde a câmara estava a olhar porque podia estar a olhar mais para cima ou para baixo e quando resetarmos o y para 0 vamos perder essa informação
            Vector3 cameraForward2 = cameraForward;

            // resetamos para 0 para termos um ângulo fidedigno onde tanto a câmara como o corpo estão a olhar para a frente com os Y resetados
            characterForward.y = 0;
            cameraForward.y = 0;

            float angle = Vector3.SignedAngle(characterForward, cameraForward, Vector3.up); // ângulo entre a frente da câmara e a frente do boneco
            Quaternion targetRotation = Quaternion.LookRotation(cameraForward);

            if (Mathf.Abs(angle) > 40.0f) { // se for acima de 40 graus (direita ou esquerda então usamos o valor absoluto em vez de ver maior ou menor que 40) rodamos o corpo
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * deltaTime);
            } else { // senão rodamos o pescoço
                targetRotation = Quaternion.LookRotation(cameraForward2);
                neck.rotation = Quaternion.Slerp(neck.rotation, targetRotation, 5f * deltaTime);
                CmdUpdateNeckRotation(neck.rotation); // o transform reliable no mirror só atualiza o corpo em si, não atualiza mais nada, então manualmente dizemos ao servidor para atualizar o pescoço
            }
        }
    }

    [Command]
    private void CmdUpdateNeckRotation(Quaternion rotation) {
        neckRotation = rotation;
    }

    private void NeckRotationChanged(Quaternion oldRotation, Quaternion newRotation) {
        if (!isLocalPlayer) {
            neck.rotation = newRotation;
        }
    }

    /*
     * para calcularmos o movimento com base na direção da câmara só temos de apanhar a frente e a direita da câmara com os Y resetados, normalizamos para não haver problemas que de vez em quando há
     * e retornamos o resultado entre a vertical com a câmara da frente e a horizontal com a câmara da direita
     */
    private Vector3 CalculateMovementDirection() {
        Vector3 cameraForward = generalCamera.transform.forward;
        Vector3 cameraRight = generalCamera.transform.right;

        cameraForward.y = 0;
        cameraRight.y = 0;

        cameraForward.Normalize();
        cameraRight.Normalize();

        return cameraForward * vertical + cameraRight * horizontal;
    }

    /*
     * para saltarmos usamos a função da física e do movimento e dizemos ao servidor que vamos saltar para todos os jogadores saberem que vamos usar a animação de saltar
     *  v^2 = u^2 + 2 * a * s
     *  v^2 = 0 porque é a velocidade final e no topo do salto é 0 porque o jogador pára de subir antes de começar a cair
     *  u = velocidade inicial e é o que queremos saber para podermos fazer o salto com a velocidade certa
     *  a = aceleração, gravidade (negativa porque estamos a subir)
     *  s = distância percorrida (jumpHeight)
     */
    public void Jump() {
        if (!isLocalPlayer || !myController.isGrounded)
            return;

        // como v = 0 então fica u = sqrt(-2 * a * s)
        float jumpVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);
        velocity.y = jumpVelocity;

        animator.SetTrigger("Jump");
        CmdSetJumpState(true);

        StartCoroutine(ResetJumpState());
    }

    private IEnumerator ResetJumpState() {
        yield return new WaitForSeconds(0.25f); // o tempo da animação de salto são 0.25 segundos (também daria para ser feito checkando se o controlar está grounded mas...)
        CmdSetJumpState(false);
    }

    [Command]
    private void CmdSetJumpState(bool jumped) {
        networkJumping = jumped;
    }

    void AnimJumpChanged(bool oldValue, bool newValue) {
        if (!isLocalPlayer && newValue) { // no fim do salto dizemos que a variável fica falsa o que quer dizer que já não está a saltar então temos de confirmar que ele só salta quando ela é true
            animator.SetTrigger("Jump");
        }
    }

    // se estiver em god mode então tiramos a tag de Player que faz com que seja visto pelos NPCs e trocamos, senão permanece com a tag Player.
    private void OnGodModeChanged(bool oldValue, bool newValue) {
        if (newValue) {
            gameObject.tag = "PlayerUntagged";

        } else {
            gameObject.tag = "Player";
        }
    }

    [Command]
    public void CmdSetGodMode(bool godModeEnabled) {
        isInGodMode = godModeEnabled;
    }

    // só queremos que o jogador que entrou na casa oiça o som entõa tem que ser o jogador local e usamos o método direto do PlaySound que não transmite o som a todos os outros jogadores
    private void OnTriggerEnter(Collider other) {
        if (!isLocalPlayer || isInteractingWithChest)
            return;

        if (other.gameObject.CompareTag("insideHouse1")) {
            isInHouse = true;

            // se o jogador entrar de volta na casa paramos a corrotina da música exterior começar
            if (outsideThemeCoroutine != null) {
                StopCoroutine(outsideThemeCoroutine);
                outsideThemeCoroutine = null;
            }

            SoundManager.Instance.StopSound(SoundManager.Instance.outsideTheme);
            SoundManager.Instance.PlaySound(SoundManager.Instance.insideTheme);

            SoundManager.Instance.rainSound.volume = 0.15f;
            //UIManager.Instance.jumpButton.GetComponent<Button>().enabled = true;
            //UIManager.Instance.jumpButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(115f / 255f, 115f / 255f, 115f / 255f); // porque só vai de 0 a 1 então normalizamos

        } 
        
        if(other.gameObject.CompareTag("portal")) {
            if(portalCooldown <= 0) {
                myController.enabled = false;
                transform.position = GameManager.Instance.portal2.transform.position;
                myController.enabled = true;
                portalCooldown = 30f;
            } else {
                UIManager.Instance.ShowCaptionOnce("Cooldown: " + Mathf.CeilToInt(portalCooldown), 0); // ceiliing porque se for 0.4 vai dizer 0 e o jogador fica confuso
            }
        }
        
        if (other.gameObject.CompareTag("portal2")) {
            if (portalCooldown <= 0) {
                myController.enabled = false;
                transform.position = GameManager.Instance.portal.transform.position;
                myController.enabled = true;
                portalCooldown = 30f;
            } else {
                UIManager.Instance.ShowCaptionOnce("Cooldown: " + Mathf.CeilToInt(portalCooldown), 0); // ceiliing porque se for 0.4 vai dizer 0 e o jogador fica confuso
            }
        }
    }

    // mesma lógica de entrar na casa
    private void OnTriggerExit(Collider other) {
        if (!isLocalPlayer || isInteractingWithChest)
            return;

        if (other.gameObject.CompareTag("insideHouse1") && other.bounds.Contains(transform.position) == false) {
            isInHouse = false;
            SoundManager.Instance.StopSound(SoundManager.Instance.insideTheme);

            // paramos a corrotina se já existir uma para não se sobreporem umas às outras
            if (outsideThemeCoroutine != null) {
                StopCoroutine(outsideThemeCoroutine);
            }
            outsideThemeCoroutine = StartCoroutine(StartOutsideTheme());

            SoundManager.Instance.rainSound.volume = 0.25f;
            //UIManager.Instance.jumpButton.GetComponent<Button>().enabled = false;
            //UIManager.Instance.jumpButton.GetComponentInChildren<TextMeshProUGUI>().color = Color.white; // porque só vai de 0 a 1 então normalizamos

            if(hasRocketLauncher == false) {
                UIManager.Instance.ShowCaptionOnce("You must grab the Rocket Launcher...", 2);
            }
        }
    }

    private IEnumerator StartOutsideTheme() {
        yield return new WaitForSeconds(10f);

        if (!isInHouse) {
            SoundManager.Instance.PlaySound(SoundManager.Instance.outsideTheme); // o jogador pode decidir voltar a entrar na casa
        }

        outsideThemeCoroutine = null; // no fim da corrotina dizemos que já não existe
    }

    [Command(requiresAuthority = false)]
    public void CmdActivatePortal() {
        GameManager.Instance.RpcActivatePortal();
    }





    /*
     * para escondermos o jogador no baú fazemos da seguinte forma:
     * primeiro lugar executamos o código localmente, ou seja, apenas nele, meter a animação de crouch a true e metemos o jogador na posição do baú
     * no fim notificamos ao servidor que o jogador usou a animação de crouch
     * não precisamos de manualmente alterar a posição dele porque a componente network transform reliable trata disso, o mesmo para sair do baú
     */
    public void HideInChest(Vector3 position) {
        if (!isLocalPlayer) return;

        isInteractingWithChest = true;
        movementMagnitude = 0;

        animator.SetBool("Crouch", true);

        myController.enabled = false;
        transform.position = position;
        myController.enabled = true;

        CmdHideInChest();
        StartCoroutine(ResetChestInteractionFlag());
    }


    [Command]
    private void CmdHideInChest() {
        isInChest = true;
        networkCrouch = true;
    }

    /*
     * para o jogador sair do baú usamos a mesma lógica, executamos o código locamente, usamos a animação de crouch e dizemos que a usámos ao servidor
     * o jogador volta ao local onde estava quando clicou para entrar no baú
     */
    public void LeaveChest(Vector3 position) {
        if (!isLocalPlayer) return;

        isInteractingWithChest = true;
        animator.SetBool("Crouch", false);

        myController.enabled = false;
        transform.position = position;
        myController.enabled = true;

        CmdLeaveChest();
        StartCoroutine(ResetChestInteractionFlag());
    }

    // se metermos a variável a falso logo asseguir ao código no fim dos métodos não iria resolver o problema da música dar restart
    // porque temos de esperar que a frame acabe e que tudo acabe e usamos o fixed update por causa da física e o end of frame para ter a certeza
    private IEnumerator ResetChestInteractionFlag() {
        yield return new WaitForFixedUpdate();
        yield return new WaitForEndOfFrame();
        isInteractingWithChest = false;
    }

    [Command]
    private void CmdLeaveChest() {
        isInChest = false;
        networkCrouch = false;
    }


    public void Die(NPCScript nPC) {
        if (isLocalPlayer && !playerHit) {
            playerHit = true;

            Quaternion lookRotation = Quaternion.LookRotation(nPC.headPoint.position);

            // se morrermos dentro do baú (por exemplo ao entrar) o código ainda vai pensar que estamos dentro do baú e se clicarmos na mão ele volta à
            //  posição em que estava antes de entrar no mesmo e vai ficar agachado, assim damos reset
            isInChest = false;
            transform.rotation = lookRotation; // o NPC olha para nós

            UIManager.Instance.hearts[playerLives - 1].GetComponent<Image>().sprite = UIManager.Instance.grayHeartImage;

            playerLives--;

            movementMagnitude = 0; // paramos o jogador, se eu estivesse a correr e morria eu continuava a correr
            animator.SetBool("Die", true);
            isDead = true;
            StartCoroutine(UIManager.Instance.FadeToBlack(2.0f, this));
            GetComponentInChildren<CameraScript>().enabled = false; // temos de desativar o código da câmara para que ela corre livremente e siga a rotação do boneco
            CmdDie();

            Debug.Log(playerLives + "lives remaining");

            if (playerLives == 0) {
                gameOver = true;
                StartCoroutine(UIManager.Instance.SetPlayerDeadBackground());

            }
        }
    }

    [Command]
    public void CmdDestroyPlayer() {
        NetworkServer.Destroy(gameObject);
    }


    [Command]
    private void CmdDie() {
        networkDie = true;
    }

    /*
     * agora só temos de fazer o oposto que a função de morrer
     * voltamos a meter a animação de morrer a false, ligamos o audio listener e metemos a câmara a rodar e transportamos o jogador para o spawn point
     * no fim tiramos a imagem preta
     */
    public void Reborn() {
        if (!isLocalPlayer) return;

        animator.SetBool("Die", false);
        isDead = false;
        GetComponentInChildren<AudioListener>().enabled = true;
        GetComponentInChildren<CameraScript>().enabled = true;

        // o controlador faz bugs então desliga-se momentaneamente
        myController.enabled = false;
        transform.position = spawnPoint;
        myController.enabled = true;

        CmdReborn();

        UIManager.Instance.fadeImage.gameObject.SetActive(false);
        Color startColor = UIManager.Instance.fadeImage.color;
        UIManager.Instance.fadeImage.color = new Color(startColor.r, startColor.g, startColor.b, 0f); // reset para 0 transparência outra vez

        playerHit = false;
    }

    [Command]
    private void CmdReborn() {
        networkDie = false;
        if (networkCrouch) {
            animator.SetBool("Crouch", false);
            networkCrouch = false;

        }
    }


    /*
     * mudamos o valor da syncvar isOpen que tem o hook que vai ser chamado em todos os clientes, portanto quando interagimos com a porta na verdade só estamos a trocar se está aberta 
     * ou fechada, então é fazer o oposto do valor atual da variável
     */
    public void InteractDoor(Transform door) {
        door = door.parent;

        if (isServer) {
            DoorScript doorScript = door.GetComponentInChildren<DoorScript>();
            doorScript.isOpen = !doorScript.isOpen;

        } else {
            CmdInteractWithDoor(door.GetComponent<NetworkIdentity>()); // podemos passar gameobjects mas acho que assim é mais confiável
        }
            
    }

    [Command(requiresAuthority = false)] // como é uma porta o cliente não tem autoridade sobre ele, então ignoramos isso
    private void CmdInteractWithDoor(NetworkIdentity doorNetId) {
        DoorScript doorScript = doorNetId.GetComponentInChildren<DoorScript>();
        doorScript.isOpen = !doorScript.isOpen;
    }


    [Command]
    public void CmdDestroyObject(GameObject objectToDestroy) {
        NetworkServer.Destroy(objectToDestroy);
    }

    public bool OpenLock(Transform lockItem) {
        return inventory.HasKey(lockItem.name);
    }

    [Command]
    public void CmdLockFall(GameObject lockObj) {
        Rigidbody rb = lockObj.GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;

        RpcOnLockFall(lockObj);
    }

    [ClientRpc]
    void RpcOnLockFall(GameObject lockObj) {
        Rigidbody rb = lockObj.GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;
    }

    /*
     * como fizemos no HideInChest, apenas fazemos a mudança localmente e deixamos a componente netwokr transform reliable fazer o resto (sincronizar a posição para os outros jogadores)
     */
    public void EquipRocketLauncher(GameObject rocketLauncher) {
        animator.SetBool("EquipRocketLauncher", true);
        CmdEquipRocketLauncher(rocketLauncher);

        rocketLauncher.transform.SetParent(rightHand.transform);
        rocketLauncher.transform.localPosition = Vector3.zero;
        rocketLauncher.transform.localRotation = Quaternion.Euler(0, 180, 0);
    }

    // agora dizemos aos outros clientes que o jogador equipou o rocket launcher (animação=
    private void AnimEquipRocketLauncherChanged(bool oldValue, bool newValue) {
        if (!isLocalPlayer) {
            animator.SetBool("EquipRocketLauncher", newValue);
        }
    }

    [Command(requiresAuthority = false)]
    private void CmdEquipRocketLauncher(GameObject rocketLauncher) {
        hasRocketLauncher = true;
        RpcEquipRocketLauncher(rocketLauncher);
    }

    [ClientRpc]
    private void RpcEquipRocketLauncher(GameObject rocketLauncher) {
        rocketLauncher.transform.SetParent(rightHand.transform);
        rocketLauncher.transform.localPosition = Vector3.zero;
        rocketLauncher.transform.localRotation = Quaternion.Euler(0, 180, 0);
    }

    public void PlayFootstepSound() {
        // temos de ver se é o jogador local a chamar a função porque o jogador que estiver a andar é o jogador que vai fazer o som, então se for o jogador 1 que estiver a andar
        // tem que ser ele a causar o som
        if (isLocalPlayer) {
            SoundManager.Instance.CmdPlaySound(gameObject);
        }
    }


}