using UnityEngine;
using Mirror;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;


/* [Command]: talking with the server
 * [ClientRPC]: talking to a specific client or all of them
 * [SyncVars]: variables that can only be updated by the host / server and not by the client (unless it's in a [Command] block)
 *  - Hooks: these methods let us perform code on all clients everytime a syncvar variable is updated
 *    - these must always have the params oldValue and newValue
 * 
 * When should i use ClientRPC vs SyncVars?
 * - for persistent states i should use SyncVars so i can store states and updated only when needed, they're optimized to send only the state changes and
 * don't send data repeatedly to the network if the values don't change
 * - for single occurrences or punctual that only happen once, i should use [ClientRPC], these don't store state because it's unnecessary
 * can be used for making special FX when characters die for example.
 */

public class PlayerMovement : NetworkBehaviour {
    private CharacterController myController;
    private FixedJoystick joystick;
    private GameObject jumpButton;
    private Coroutine outsideThemeCoroutine;

    [SerializeField] Transform neck;
    private InventoryManager inventory;
    public GameObject rightHand;

    // player's health
    public int playerLives;
    [HideInInspector] public bool gameOver;
    private bool playerHit;

    // cameras
    private Camera generalCamera;
    [HideInInspector] public Camera playerCamera;
    private Camera generalMinimapCamera;


    // animations' informations
    private Animator animator;
    private float walkSpeed = 7f;
    private float runSpeed = 20f;
    private float gravity = -9.81f;
    private float jumpHeight = 2f;
    private float inputThreshold = 0.1f;
    private float runThreshold = 0.7f;
    private float lastSentAnimSpeed = -1f; // initialize with -1 so it doesn't have an animation by default
    [HideInInspector] public bool isDead;


    // velocity's information
    private float movementMagnitude;
    private float currentSpeed;
    private Vector3 velocity;
    private float horizontal;
    private float vertical;


    // player's location
    private bool isInHouse = true;
    [HideInInspector][SyncVar] public bool isInChest;
    private float portalCooldown = 0f;
    private Vector3 spawnPoint;
    [SerializeField] private Sprite P1Icon;
    [SerializeField] private Sprite P2Icon;
    [SerializeField] public SpriteRenderer playerIcon;
    private bool isInteractingWithChest = false;




    // everytime one of these is updated all players get the new values and the hook's code action
    [SyncVar(hook = nameof(AnimJumpChanged))] private bool networkJumping = false;
    [SyncVar(hook = nameof(AnimSpeedChanged))] private float networkAnimSpeed = 0f;
    [SyncVar(hook = nameof(AnimCrouchChanged))] private bool networkCrouch;
    [SyncVar(hook = nameof(AnimDieChanged))] private bool networkDie;
    [SyncVar(hook = nameof(NeckRotationChanged))] private Quaternion neckRotation;
    [SyncVar(hook = nameof(AnimEquipRocketLauncherChanged))] public bool hasRocketLauncher;
    [SyncVar(hook = nameof(OnGodModeChanged))] public bool isInGodMode = false;




    private void Awake() {
        // i need the camera and animator of all the player to make animations and deactivate the camera's components
        generalCamera = gameObject.GetComponentInChildren<CameraScript>().gameObject.GetComponent<Camera>();
        animator = gameObject.GetComponent<Animator>();
        generalMinimapCamera = GetComponentInChildren<MinimapPlayerCamera>().gameObject.GetComponent<Camera>();
        playerIcon = GetComponentInChildren<SpriteRenderer>();
    }

    void Start() {
        myController = GetComponent<CharacterController>();

        if (!isLocalPlayer) {
            // if it isn't the local player then it doesn't need its camera on
            // i deactivate all of its components because i still need the camera's gameobject on so the candle appears and be well rotationed/positioned
            generalCamera.enabled = false;
            generalCamera.gameObject.GetComponent<AudioListener>().enabled = false;
            generalCamera.gameObject.GetComponent<Camera>().enabled = false;
            generalCamera.gameObject.GetComponent<UniversalAdditionalCameraData>().enabled = false;
            generalCamera.gameObject.GetComponent<CameraScript>().enabled = false;
            generalMinimapCamera.gameObject.SetActive(false); // i only want the local player's mini map
            return;
        }

        // this is the local player so it's the player 1
        playerIcon.sprite = P1Icon;
        // but i cant define the other player's icon already because he may not be in the game yet, so i do it inside the corrotine
        StartCoroutine(SetupOtherPlayerWhenReady());


        inventory = gameObject.GetComponent<InventoryManager>();
        playerCamera = gameObject.GetComponentInChildren<CameraScript>().gameObject.GetComponent<Camera>();
        playerLives = 3;

        UIManager.Instance.SetLocalPlayer(this);

        jumpButton = UIManager.Instance.jumpButton;
        joystick = UIManager.Instance.joystick;

        // i cant place action listeners in unity because the scripts that dont extend MonoBehaviour aren't recognized (i think)
        jumpButton.GetComponent<Button>().onClick.AddListener(Jump);

        spawnPoint = transform.position;

        SoundManager.Instance.PlaySound(SoundManager.Instance.rainSound);
        // i loaded the music ahead of time so the game stopped freezing a bit (it was too much to be loaded at once)
        SoundManager.Instance.outsideTheme.clip.LoadAudioData();
    }


    private IEnumerator SetupOtherPlayerWhenReady() {
        while (CustomNetworkManager.Instance.FindOtherPlayer(gameObject) == null) {
            yield return new WaitForSeconds(0.1f); // while the other player isn't in the game i need to wait (in case it's the host, if i'm the client i don't wait)
        }

        GameObject otherPlayer = CustomNetworkManager.Instance.FindOtherPlayer(gameObject);
        otherPlayer.GetComponent<PlayerMovement>().playerIcon.sprite = P2Icon;
    }


    /*
     * FixedUpdate: used when working with pyshics
     * here I add the gravity to the player and make him move with character controller that deals with collisions and physical movements 
     */
    void FixedUpdate() {
        if (!isLocalPlayer) {
            return;
        }
        
        velocity.y += gravity * Time.deltaTime; // gravity because I'm not using a rigidBody but I'm using a character controller and this one doesn't have gravity

        // if the joystick's input is too weak it'll be ignored so he'll remain stopped
        if (movementMagnitude < inputThreshold) {
            myController.Move(velocity * Time.deltaTime);
            return;
        }

        // calculate the movement based on the camera's direction
        Vector3 movement = CalculateMovementDirection();

        // the controller moves the player based on the camera's direction and the joystick's velocity
        myController.Move(currentSpeed * Time.deltaTime * movement + velocity * Time.deltaTime);
    }


    void Update() {
        if (!isLocalPlayer || isDead || gameOver) {
            return;
        }

        if (portalCooldown > 0) {
            portalCooldown -= Time.deltaTime;
        }

        float deltaTime = Time.deltaTime;

        // if the player is inside the chest he can't move
        if (isInChest == false) {
            // i grab the vertical and horizontal because it's the same as if i were using the input like in a computer game
            horizontal = joystick.Horizontal;
            vertical = joystick.Vertical;

            movementMagnitude = new Vector2(horizontal, vertical).magnitude; // this is important to define whether the player is going to walk or run

            // basically if the input is above the limit to run, then he runs
            if (movementMagnitude > runThreshold) {
                //currentSpeed = isInHouse ? walkSpeed : runSpeed; // if the player is inside the house he can only walk
                currentSpeed = walkSpeed;

            } else {
                currentSpeed = walkSpeed;
            }
        }

        // this runs the animations and rotates the neck/body of the player
        UpdateAnimator(deltaTime);
        RotateCharacter(deltaTime);
    }

    /*
     * to update the animations i only need to check the movement's magnitude and depending on it i can check whether he's stopped, running or walking
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

        // i have to keep the animation's state because it's something that's occasionally changed, for that i used a syncVar lastSentAnimSpeed
        // everytime the previous animation is the same as the current one it'll be 0 because 0-0=0, 2-2=0, 1-1=0, but when different the total won't be 0 anymore
        // so if the absolute value is bigger than 0 it's because it changed
        if (Mathf.Abs(animationValue - lastSentAnimSpeed) > 0f) {
            lastSentAnimSpeed = animationValue; // update the last animation used
            CmdUpdateAnimationSpeed(animationValue); // update the variable on the server
        }
    }

    // i can only update the sync var on the server so it needs to be done on a command block (in case this is the client)
    [Command]
    private void CmdUpdateAnimationSpeed(float speed) {
        networkAnimSpeed = speed; // when updating it, it'll trigger the hook on all clients
    }

    // this hook is called ignores the local player because its code was already executed
    // this one is only to inform for the non local players they have to show the animation
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
     * this is where the player's character will rotate depending on how much the user is rotating the camera
     * if i rotate the camera less than 40 degrees then the player's neck will be rotated, else it rotates his entire body
     */
    private void RotateCharacter(float deltaTime) {
        Vector3 cameraForward = generalCamera.transform.forward; // the front of the camera

        // if i'm moving the character then it'll rotate it to follow the direction of the movement and the rotation to where that movement is "Pointing" to
        if (movementMagnitude >= inputThreshold) {
            Vector3 moveDirection = CalculateMovementDirection();
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            // for the rotation to be smoother i used a slerp that's basically a lerp but for angles
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * deltaTime);

        } else { // if i'm not moving then it rotates according to the camera's direction
            cameraForward.Normalize(); // normalize fixes everything
            Vector3 characterForward = transform.forward; // to where the character is looking at

            // i have to keep where the camera was looking at because it could be higher or lower and when resetting the y axis to 0 i lose that information
            Vector3 cameraForward2 = cameraForward;

            // reset the y axis to 0 to have a more clean and precise angle where both the camera and body are looking ahead with y reset
            characterForward.y = 0;
            cameraForward.y = 0;

            float angle = Vector3.SignedAngle(characterForward, cameraForward, Vector3.up);
            Quaternion targetRotation = Quaternion.LookRotation(cameraForward);

            if (Mathf.Abs(angle) > 40.0f) {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * deltaTime);
            } else {
                targetRotation = Quaternion.LookRotation(cameraForward2);
                neck.rotation = Quaternion.Slerp(neck.rotation, targetRotation, 5f * deltaTime);
                // the transform reliable only updates the body itself, so i have to manually tell the server to update the neck
                CmdUpdateNeckRotation(neck.rotation);
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

    private Vector3 CalculateMovementDirection() {
        Vector3 cameraForward = generalCamera.transform.forward;
        Vector3 cameraRight = generalCamera.transform.right;

        cameraForward.y = 0;
        cameraRight.y = 0;

        cameraForward.Normalize();
        cameraRight.Normalize();

        return cameraForward * vertical + cameraRight * horizontal;
    }

    private IEnumerator ResetJumpState() {
        // the animation's just time is 0.25 seconds
        yield return new WaitForSeconds(0.25f);
        CmdSetJumpState(false);
    }

    [Command]
    private void CmdSetJumpState(bool jumped) {
        networkJumping = jumped;
    }

    void AnimJumpChanged(bool oldValue, bool newValue) {
        if (!isLocalPlayer && newValue) {
            animator.SetTrigger("Jump");
        }
    }

    // if god mode is on then the player loses its tag that makes him be seen by the NPCs
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

    // i only want the player that entered the house to hear the sound so it has to be the local sound
    private void OnTriggerEnter(Collider other) {
        if (!isLocalPlayer || isInteractingWithChest)
            return;

        if (other.gameObject.CompareTag("insideHouse1")) {
            isInHouse = true;

            // if the player gets inside the house again i stop the corrotine of the outside theme music
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
                // ceiling because if it's 0.4 then it'll say it remains 0 seconds and the player gets confused
                UIManager.Instance.ShowCaptionOnce("Cooldown: " + Mathf.CeilToInt(portalCooldown), 0);
            }
        }
        
        if (other.gameObject.CompareTag("portal2")) {
            if (portalCooldown <= 0) {
                myController.enabled = false;
                transform.position = GameManager.Instance.portal.transform.position;
                myController.enabled = true;
                portalCooldown = 30f;
            } else {
                // ceiling because if it's 0.4 then it'll say it remains 0 seconds and the player gets confused
                UIManager.Instance.ShowCaptionOnce("Cooldown: " + Mathf.CeilToInt(portalCooldown), 0);
            }
        }
    }

    private void OnTriggerExit(Collider other) {
        if (!isLocalPlayer || isInteractingWithChest)
            return;

        if (other.gameObject.CompareTag("insideHouse1") && other.bounds.Contains(transform.position) == false) {
            isInHouse = false;
            SoundManager.Instance.StopSound(SoundManager.Instance.insideTheme);

            // stop the corrotine if there's already one for zero overlapping
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
            SoundManager.Instance.PlaySound(SoundManager.Instance.outsideTheme);
        }

        outsideThemeCoroutine = null;
    }

    [Command(requiresAuthority = false)]
    public void CmdActivatePortal() {
        GameManager.Instance.RpcActivatePortal();
    }





    /*
     * first the code is ran locally so we grab the player crouch him and place him inside the chest's position and in the end i notify the server that the player is in crouch animation
     * i dont have to manually change his position because the component network transform reliable already handles that
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

    // if i set the variable to false right after the code at the of the methods it wouldn't fix the problem of the music restarting when leaving entering chests
    // because i have to wait for the frame to end and for everything to finish and then use the fixed update because of the physics and end of frame just to be sure
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

            // if inside the chest the code will still think im inside the chest and if i click on the hand icon on the UI it'll come back to the position the player was before
            // entering the chest and he'll be crouch, so i have to reset
            isInChest = false;
            transform.rotation = lookRotation; // NPC looks at us

            UIManager.Instance.hearts[playerLives - 1].GetComponent<Image>().sprite = UIManager.Instance.grayHeartImage;

            playerLives--;

            movementMagnitude = 0;// the player is stopped
            animator.SetBool("Die", true);
            isDead = true;
            StartCoroutine(UIManager.Instance.FadeToBlack(2.0f, this));
            GetComponentInChildren<CameraScript>().enabled = false; // deactivate the camera's code so it runs freely and follows the character's dying animation
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

    // opposite of dying method
    public void Reborn() {
        if (!isLocalPlayer) return;

        animator.SetBool("Die", false);
        isDead = false;
        GetComponentInChildren<AudioListener>().enabled = true;
        GetComponentInChildren<CameraScript>().enabled = true;

        // the controller was causing bugs so it's turned off
        myController.enabled = false;
        transform.position = spawnPoint;
        myController.enabled = true;

        CmdReborn();

        UIManager.Instance.fadeImage.gameObject.SetActive(false);
        Color startColor = UIManager.Instance.fadeImage.color;
        UIManager.Instance.fadeImage.color = new Color(startColor.r, startColor.g, startColor.b, 0f); // reset to 0 alpha again

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
     * this changes the syncvar isOpen value that has the hook that'll be called on all clients so when i interact with the door im only changing if it's opened or closed
     * so it's the opposite of the current value
     */
    public void InteractDoor(Transform door) {
        door = door.parent;

        if (isServer) {
            DoorScript doorScript = door.GetComponentInChildren<DoorScript>();
            doorScript.isOpen = !doorScript.isOpen;

        } else {
            CmdInteractWithDoor(door.GetComponent<NetworkIdentity>());
        }
            
    }

    [Command(requiresAuthority = false)] // because it's a door the client doesn't have authority over it
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

    // same logic as in hide in chest method
    public void EquipRocketLauncher(GameObject rocketLauncher) {
        animator.SetBool("EquipRocketLauncher", true);
        CmdEquipRocketLauncher(rocketLauncher);

        rocketLauncher.transform.SetParent(rightHand.transform);
        rocketLauncher.transform.localPosition = Vector3.zero;
        rocketLauncher.transform.localRotation = Quaternion.Euler(0, 180, 0);
    }

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
        // i need to check if it's the local player calling the method because th player that's walking is the player that'll make the sound
        if (isLocalPlayer) {
            SoundManager.Instance.CmdPlaySound(gameObject);
        }
    }


}