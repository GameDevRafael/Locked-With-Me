using UnityEngine;
using Mirror;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;


/* [Command]: falar com o servidor
 * [ClientRPC]: falar com um cliente espec�fico ou todos os clientes
 * [SyncVars]: vari�veis que podem apenas ser atualizadas pelo host/servidor e n�o pelo cliente, portanto t�m que ser atualizadas com o [Command]
 *  - Hooks: m�todos que permitem fazer l�gica de c�digo, executar a��es etc, em todos os clientes sempre que uma [SyncVar] � atualizada
 *    - estes t�m que ter sempre os par�metros oldValue e newValue nos seus m�todos
 * 
 * Quando usar ClientRPC vs SyncVars?
 * - para estados persistentes conv�m usar SyncVars, pois assim mantemos guardados os estados e atualizamos s� quando necess�rio, s�o otimizadas para enviar
 *   apenas as mudan�as de estado e n�o enviam dados repetidamente para a network se o valor n�o mudar. 
 * - para acontecimentos �nicos ou pontuais que s� ocorrem uma vez conv�m usar [ClientRPC], estas n�o mant�m o estado pois � desnecess�rio.
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

    // C�maras
    private Camera generalCamera;
    [HideInInspector] public Camera playerCamera;
    private Camera generalMinimapCamera;


    // Informa��es das Anima��es
    private Animator animator;
    private float walkSpeed = 7f;
    private float runSpeed = 20f;
    private float gravity = -9.81f;
    private float jumpHeight = 2f;
    private float inputThreshold = 0.1f;
    private float runThreshold = 0.7f;
    private float lastSentAnimSpeed = -1f; // inicializamos com -1 para que n�o tenha uma anima��o por default
    [HideInInspector] public bool isDead;


    // Inform���es da Velocidade
    private float movementMagnitude;
    private float currentSpeed;
    private Vector3 velocity;
    private float horizontal;
    private float vertical;


    // Localiza��o do Jogador
    private bool isInHouse = true;
    [HideInInspector][SyncVar] public bool isInChest;
    private float portalCooldown = 0f;
    private Vector3 spawnPoint;
    [SerializeField] private Sprite P1Icon;
    [SerializeField] private Sprite P2Icon;
    [SerializeField] public SpriteRenderer playerIcon;
    private bool isInteractingWithChest = false;




    // para estarem sincronizadas ao longo dos jogadores, � sincronizada sempre que ocorre uma altera��o nas vari�veis, s� podem ser alteradas pelo servidor/host e n�o pelos clientes
    [SyncVar(hook = nameof(AnimJumpChanged))] private bool networkJumping = false;
    [SyncVar(hook = nameof(AnimSpeedChanged))] private float networkAnimSpeed = 0f;
    [SyncVar(hook = nameof(AnimCrouchChanged))] private bool networkCrouch;
    [SyncVar(hook = nameof(AnimDieChanged))] private bool networkDie;
    [SyncVar(hook = nameof(NeckRotationChanged))] private Quaternion neckRotation;
    [SyncVar(hook = nameof(AnimEquipRocketLauncherChanged))] public bool hasRocketLauncher;
    [SyncVar(hook = nameof(OnGodModeChanged))] public bool isInGodMode = false;




    private void Awake() {
        // precisamos da c�mara e do animator de todos os jogadores para fazer anima��es / desativar componentes da camara
        generalCamera = gameObject.GetComponentInChildren<CameraScript>().gameObject.GetComponent<Camera>();
        animator = gameObject.GetComponent<Animator>();
        generalMinimapCamera = GetComponentInChildren<MinimapPlayerCamera>().gameObject.GetComponent<Camera>();
        playerIcon = GetComponentInChildren<SpriteRenderer>();
    }

    void Start() {
        myController = GetComponent<CharacterController>(); // precisamos do controller de ambos jogadores para podermos fazer o swap bodies

        if (!isLocalPlayer) {
            // se n�o � o jogador local ent�o n�o vai ter a c�mara ligada
            // desativo todos os seus componentes porque preciso do gameobject da c�mara ligado para que a vela apare�a e seja bem rotacionada/posicionada
            generalCamera.enabled = false;
            generalCamera.gameObject.GetComponent<AudioListener>().enabled = false;
            generalCamera.gameObject.GetComponent<Camera>().enabled = false;
            generalCamera.gameObject.GetComponent<UniversalAdditionalCameraData>().enabled = false;
            generalCamera.gameObject.GetComponent<CameraScript>().enabled = false;
            generalMinimapCamera.gameObject.SetActive(false); // s� queremos o mini mapa do jogador local
            return;
        }

        playerIcon.sprite = P1Icon; // como somos o jogador local � porque somos o principal e ent�o somos o jogador 1
        // mas n�o podemos definir j� o �cone do outro jogador porque poder� ainda n�o estar no jogo ent�o fazemos dentro da corrotina
        StartCoroutine(SetupOtherPlayerWhenReady());


        inventory = gameObject.GetComponent<InventoryManager>();
        playerCamera = gameObject.GetComponentInChildren<CameraScript>().gameObject.GetComponent<Camera>();
        playerLives = 3;

        UIManager.Instance.SetLocalPlayer(this);

        jumpButton = UIManager.Instance.jumpButton;
        joystick = UIManager.Instance.joystick;

        // n�o consigo meter action lisiteners no unity porque os scripts que n�o extendem MonoBehaviour n�o s�o reconhecidos
        jumpButton.GetComponent<Button>().onClick.AddListener(Jump);

        spawnPoint = transform.position;

        SoundManager.Instance.PlaySound(SoundManager.Instance.rainSound);
        // carregamos previamente porque a musica fazia o jogo freezar um pouco porque era muito para ser carregado
        SoundManager.Instance.outsideTheme.clip.LoadAudioData();
    }


    private IEnumerator SetupOtherPlayerWhenReady() {
        while (CustomNetworkManager.Instance.FindOtherPlayer(gameObject) == null) {
            yield return new WaitForSeconds(0.1f); // enquanto o outro jogador n�o estiver no jogo esperamos (caso sejamos o host, se formos o cliente n�o esperamos)
        }

        GameObject otherPlayer = CustomNetworkManager.Instance.FindOtherPlayer(gameObject);
        otherPlayer.GetComponent<PlayerMovement>().playerIcon.sprite = P2Icon;
    }


    /*
     * FixedUpdate: quando se trabalha com f�sica usamos este update. 
     * Trabalhamos com a gravidade e adicionamo-la ao jogador e fazemos o mesmo mover-se com o CharacterController que lida com colis�es e movimentos f�sicos.
     */
    void FixedUpdate() {
        if (!isLocalPlayer) {
            return;
        }

        velocity.y += gravity * Time.deltaTime; // gravidade porque n�o estamos a usar rigidbody e sim um character controller e este n�o tem gravidade

        // se o input do joystick for muito fraco vai ser ignorado, portanto vamos permanecer parados
        if (movementMagnitude < inputThreshold) {
            myController.Move(velocity * Time.deltaTime);
            return;
        }

        // calculamos o movimento com base na dire��o da c�mara
        Vector3 movement = CalculateMovementDirection();

        // usamos o controlador para mover o jogador, juntamos a dire��o baseada na c�mara com a velocidade baseada no joystick
        myController.Move(currentSpeed * Time.deltaTime * movement + velocity * Time.deltaTime);
    }


    /*
     * No Update normal coloco o resto do c�digo que n�o faria sentido estar no FixedUpdate, e como n�o trabalho com a c�mara n�o precisamos de um LateUpdate
     */
    void Update() {
        if (!isLocalPlayer || isDead || gameOver) {
            return;
        }

        if (portalCooldown > 0) {
            portalCooldown -= Time.deltaTime;
        }

        float deltaTime = Time.deltaTime;

        // se estiver no ba� n�o se pode mexer
        if (isInChest == false) {
            // apanhamos o verticar e horizontal, isto funciona igual como se tiv�ssemos a usar o Input para o caso do computador
            horizontal = joystick.Horizontal;
            vertical = joystick.Vertical;

            movementMagnitude = new Vector2(horizontal, vertical).magnitude; // precisamos disto para definir se o jogador vai andar ou correr

            // basicamente se o input for acima do limite para correr ele corre, se for abaixo ele anda
            if (movementMagnitude > runThreshold) {
                //currentSpeed = isInHouse ? walkSpeed : runSpeed; // se o jogador estiver dentro de casa s� pode andar
                currentSpeed = walkSpeed;

            } else {
                currentSpeed = walkSpeed;
            }
        }

        // corremos as anim���es e rodamos o pesco�o/corpo do jogador
        UpdateAnimator(deltaTime);
        RotateCharacter(deltaTime);
    }

    /*
     * para atualizar as anima��es s� temos de ver a magnitude do movimento e dependendo do mesmo conseguimos ver se ele est� parado, a correr ou a andar
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

        // como o estado da anima��o dos jogadores � algo que vai ser alterado e temos de manter esse estado, usamos uma [SyncVar] lastSentAnimSpeed
        // sempre que a anima��o anterior � igual � atual vai ser 0 pois 0-0 = 0, 2-2=0, 1-1=0, mas quando s�o diferentes o total j� n�o ser� 0, ent�o se o m�dulo for maior que 0 � porque mudou
        if (Mathf.Abs(animationValue - lastSentAnimSpeed) > 0f) {
            lastSentAnimSpeed = animationValue; // atualizamos a �ltima anima��o usada
            CmdUpdateAnimationSpeed(animationValue); // atualizamos a vari�vel no servidor
        }
    }

    // relembro que s� podemos atualizar [SyncVar] no servidor, ent�o temos de usar o [Command] que serve para comunicar com o servidor
    [Command]
    private void CmdUpdateAnimationSpeed(float speed) {
        networkAnimSpeed = speed; // ao atualizarmos a SyncVar, vai dar trigger no hook em todos os clientes
    }

    // este � o hook que � chamado quando a [SyncVar] � alterada e � chamado em todos os clientes, vamos ignorar o jogador local porque o seu c�digo j� foi efetuado, este � apenas para dizer aos n�o locais
    // que t�m de mostrar a anima��o
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
     * aqui � onde o character do jogador ir� rodar, depende do qu�o o utilizar esteja a girar a c�mara
     * se rodarmos a c�mara menos de 40 graus, ent�o s� giramos o pesco�o do character, sen�o giramos o corpo para simular uma certa realidade e naturalidade
     */
    private void RotateCharacter(float deltaTime) {
        // apanhamos a "frente" da c�mara
        Vector3 cameraForward = generalCamera.transform.forward;

        // se estivermos a mover o boneco ent�o rodamo-lo para nos orientarmos de acordo com a dire��o do movimento e com a rota��o para onde esse movimento est� a "apontar"
        if (movementMagnitude >= inputThreshold) {
            Vector3 moveDirection = CalculateMovementDirection();
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * deltaTime); // para a rota��o ser mais smooth usamos um Slerp que � basicamente um Lerp mas para �ngulos

        } else { // se n�o nos estivermos a mover ent�o rodamos de acordo com a dire��o da c�mara
            cameraForward.Normalize(); // se n�o estiver normalizada o pesco�o n�o roda, geralmente o normalize arranja os problemas porque mete os valores entre 0 e 1
            Vector3 characterForward = transform.forward; // para onde o boneco est� a olhar

            // temos de guardar para onde a c�mara estava a olhar porque podia estar a olhar mais para cima ou para baixo e quando resetarmos o y para 0 vamos perder essa informa��o
            Vector3 cameraForward2 = cameraForward;

            // resetamos para 0 para termos um �ngulo fidedigno onde tanto a c�mara como o corpo est�o a olhar para a frente com os Y resetados
            characterForward.y = 0;
            cameraForward.y = 0;

            float angle = Vector3.SignedAngle(characterForward, cameraForward, Vector3.up); // �ngulo entre a frente da c�mara e a frente do boneco
            Quaternion targetRotation = Quaternion.LookRotation(cameraForward);

            if (Mathf.Abs(angle) > 40.0f) { // se for acima de 40 graus (direita ou esquerda ent�o usamos o valor absoluto em vez de ver maior ou menor que 40) rodamos o corpo
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * deltaTime);
            } else { // sen�o rodamos o pesco�o
                targetRotation = Quaternion.LookRotation(cameraForward2);
                neck.rotation = Quaternion.Slerp(neck.rotation, targetRotation, 5f * deltaTime);
                CmdUpdateNeckRotation(neck.rotation); // o transform reliable no mirror s� atualiza o corpo em si, n�o atualiza mais nada, ent�o manualmente dizemos ao servidor para atualizar o pesco�o
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
     * para calcularmos o movimento com base na dire��o da c�mara s� temos de apanhar a frente e a direita da c�mara com os Y resetados, normalizamos para n�o haver problemas que de vez em quando h�
     * e retornamos o resultado entre a vertical com a c�mara da frente e a horizontal com a c�mara da direita
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
     * para saltarmos usamos a fun��o da f�sica e do movimento e dizemos ao servidor que vamos saltar para todos os jogadores saberem que vamos usar a anima��o de saltar
     *  v^2 = u^2 + 2 * a * s
     *  v^2 = 0 porque � a velocidade final e no topo do salto � 0 porque o jogador p�ra de subir antes de come�ar a cair
     *  u = velocidade inicial e � o que queremos saber para podermos fazer o salto com a velocidade certa
     *  a = acelera��o, gravidade (negativa porque estamos a subir)
     *  s = dist�ncia percorrida (jumpHeight)
     */
    public void Jump() {
        if (!isLocalPlayer || !myController.isGrounded)
            return;

        // como v = 0 ent�o fica u = sqrt(-2 * a * s)
        float jumpVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);
        velocity.y = jumpVelocity;

        animator.SetTrigger("Jump");
        CmdSetJumpState(true);

        StartCoroutine(ResetJumpState());
    }

    private IEnumerator ResetJumpState() {
        yield return new WaitForSeconds(0.25f); // o tempo da anima��o de salto s�o 0.25 segundos (tamb�m daria para ser feito checkando se o controlar est� grounded mas...)
        CmdSetJumpState(false);
    }

    [Command]
    private void CmdSetJumpState(bool jumped) {
        networkJumping = jumped;
    }

    void AnimJumpChanged(bool oldValue, bool newValue) {
        if (!isLocalPlayer && newValue) { // no fim do salto dizemos que a vari�vel fica falsa o que quer dizer que j� n�o est� a saltar ent�o temos de confirmar que ele s� salta quando ela � true
            animator.SetTrigger("Jump");
        }
    }

    // se estiver em god mode ent�o tiramos a tag de Player que faz com que seja visto pelos NPCs e trocamos, sen�o permanece com a tag Player.
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

    // s� queremos que o jogador que entrou na casa oi�a o som ent�a tem que ser o jogador local e usamos o m�todo direto do PlaySound que n�o transmite o som a todos os outros jogadores
    private void OnTriggerEnter(Collider other) {
        if (!isLocalPlayer || isInteractingWithChest)
            return;

        if (other.gameObject.CompareTag("insideHouse1")) {
            isInHouse = true;

            // se o jogador entrar de volta na casa paramos a corrotina da m�sica exterior come�ar
            if (outsideThemeCoroutine != null) {
                StopCoroutine(outsideThemeCoroutine);
                outsideThemeCoroutine = null;
            }

            SoundManager.Instance.StopSound(SoundManager.Instance.outsideTheme);
            SoundManager.Instance.PlaySound(SoundManager.Instance.insideTheme);

            SoundManager.Instance.rainSound.volume = 0.15f;
            //UIManager.Instance.jumpButton.GetComponent<Button>().enabled = true;
            //UIManager.Instance.jumpButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(115f / 255f, 115f / 255f, 115f / 255f); // porque s� vai de 0 a 1 ent�o normalizamos

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

    // mesma l�gica de entrar na casa
    private void OnTriggerExit(Collider other) {
        if (!isLocalPlayer || isInteractingWithChest)
            return;

        if (other.gameObject.CompareTag("insideHouse1") && other.bounds.Contains(transform.position) == false) {
            isInHouse = false;
            SoundManager.Instance.StopSound(SoundManager.Instance.insideTheme);

            // paramos a corrotina se j� existir uma para n�o se sobreporem umas �s outras
            if (outsideThemeCoroutine != null) {
                StopCoroutine(outsideThemeCoroutine);
            }
            outsideThemeCoroutine = StartCoroutine(StartOutsideTheme());

            SoundManager.Instance.rainSound.volume = 0.25f;
            //UIManager.Instance.jumpButton.GetComponent<Button>().enabled = false;
            //UIManager.Instance.jumpButton.GetComponentInChildren<TextMeshProUGUI>().color = Color.white; // porque s� vai de 0 a 1 ent�o normalizamos

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

        outsideThemeCoroutine = null; // no fim da corrotina dizemos que j� n�o existe
    }

    [Command(requiresAuthority = false)]
    public void CmdActivatePortal() {
        GameManager.Instance.RpcActivatePortal();
    }





    /*
     * para escondermos o jogador no ba� fazemos da seguinte forma:
     * primeiro lugar executamos o c�digo localmente, ou seja, apenas nele, meter a anima��o de crouch a true e metemos o jogador na posi��o do ba�
     * no fim notificamos ao servidor que o jogador usou a anima��o de crouch
     * n�o precisamos de manualmente alterar a posi��o dele porque a componente network transform reliable trata disso, o mesmo para sair do ba�
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
     * para o jogador sair do ba� usamos a mesma l�gica, executamos o c�digo locamente, usamos a anima��o de crouch e dizemos que a us�mos ao servidor
     * o jogador volta ao local onde estava quando clicou para entrar no ba�
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

    // se metermos a vari�vel a falso logo asseguir ao c�digo no fim dos m�todos n�o iria resolver o problema da m�sica dar restart
    // porque temos de esperar que a frame acabe e que tudo acabe e usamos o fixed update por causa da f�sica e o end of frame para ter a certeza
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

            // se morrermos dentro do ba� (por exemplo ao entrar) o c�digo ainda vai pensar que estamos dentro do ba� e se clicarmos na m�o ele volta �
            //  posi��o em que estava antes de entrar no mesmo e vai ficar agachado, assim damos reset
            isInChest = false;
            transform.rotation = lookRotation; // o NPC olha para n�s

            UIManager.Instance.hearts[playerLives - 1].GetComponent<Image>().sprite = UIManager.Instance.grayHeartImage;

            playerLives--;

            movementMagnitude = 0; // paramos o jogador, se eu estivesse a correr e morria eu continuava a correr
            animator.SetBool("Die", true);
            isDead = true;
            StartCoroutine(UIManager.Instance.FadeToBlack(2.0f, this));
            GetComponentInChildren<CameraScript>().enabled = false; // temos de desativar o c�digo da c�mara para que ela corre livremente e siga a rota��o do boneco
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
     * agora s� temos de fazer o oposto que a fun��o de morrer
     * voltamos a meter a anima��o de morrer a false, ligamos o audio listener e metemos a c�mara a rodar e transportamos o jogador para o spawn point
     * no fim tiramos a imagem preta
     */
    public void Reborn() {
        if (!isLocalPlayer) return;

        animator.SetBool("Die", false);
        isDead = false;
        GetComponentInChildren<AudioListener>().enabled = true;
        GetComponentInChildren<CameraScript>().enabled = true;

        // o controlador faz bugs ent�o desliga-se momentaneamente
        myController.enabled = false;
        transform.position = spawnPoint;
        myController.enabled = true;

        CmdReborn();

        UIManager.Instance.fadeImage.gameObject.SetActive(false);
        Color startColor = UIManager.Instance.fadeImage.color;
        UIManager.Instance.fadeImage.color = new Color(startColor.r, startColor.g, startColor.b, 0f); // reset para 0 transpar�ncia outra vez

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
     * mudamos o valor da syncvar isOpen que tem o hook que vai ser chamado em todos os clientes, portanto quando interagimos com a porta na verdade s� estamos a trocar se est� aberta 
     * ou fechada, ent�o � fazer o oposto do valor atual da vari�vel
     */
    public void InteractDoor(Transform door) {
        door = door.parent;

        if (isServer) {
            DoorScript doorScript = door.GetComponentInChildren<DoorScript>();
            doorScript.isOpen = !doorScript.isOpen;

        } else {
            CmdInteractWithDoor(door.GetComponent<NetworkIdentity>()); // podemos passar gameobjects mas acho que assim � mais confi�vel
        }
            
    }

    [Command(requiresAuthority = false)] // como � uma porta o cliente n�o tem autoridade sobre ele, ent�o ignoramos isso
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
     * como fizemos no HideInChest, apenas fazemos a mudan�a localmente e deixamos a componente netwokr transform reliable fazer o resto (sincronizar a posi��o para os outros jogadores)
     */
    public void EquipRocketLauncher(GameObject rocketLauncher) {
        animator.SetBool("EquipRocketLauncher", true);
        CmdEquipRocketLauncher(rocketLauncher);

        rocketLauncher.transform.SetParent(rightHand.transform);
        rocketLauncher.transform.localPosition = Vector3.zero;
        rocketLauncher.transform.localRotation = Quaternion.Euler(0, 180, 0);
    }

    // agora dizemos aos outros clientes que o jogador equipou o rocket launcher (anima��o=
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
        // temos de ver se � o jogador local a chamar a fun��o porque o jogador que estiver a andar � o jogador que vai fazer o som, ent�o se for o jogador 1 que estiver a andar
        // tem que ser ele a causar o som
        if (isLocalPlayer) {
            SoundManager.Instance.CmdPlaySound(gameObject);
        }
    }


}