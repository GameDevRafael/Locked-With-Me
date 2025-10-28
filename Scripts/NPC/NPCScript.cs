using System.Collections;
using System.Collections.Generic;
using System.Net;
using Mirror;
using Mirror.Examples.MultipleMatch;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

public class NPCScript : NetworkBehaviour {

    [HideInInspector] public int currentTarget; // current target's waypoint
    [HideInInspector] public bool hasAttacked = false; // doesn't exceute the attack code multiple times, only the animation
    [HideInInspector] public Transform soundTriggerHeard; // the gameobject that makes the noise when the player steps on it

    [HideInInspector][SyncVar(hook = nameof(OnPlayerToChaseChanged))] public GameObject playerToChase;
    // the box collider serves as a field of view
    [HideInInspector]public SyncList<GameObject> playersInFOV = new SyncList<GameObject>();
    [SyncVar(hook = nameof(OnSawPlayerChanged))] private bool sawPlayer = false;
    [SyncVar(hook = nameof(OnDistanceToPlayerChanged))] private float distanceToPlayer;
    [SyncVar(hook = nameof(OnHeardNoiseChanged))] public bool heardNoise = false;
    [SyncVar(hook = nameof(AnimDieChanged))] private bool networkDie;
    [SyncVar(hook = nameof(OnCurrentWaypointChanged))] public Vector3 currentWaypointPosition;
    [SyncVar(hook = nameof(OnDistanceToWPChanged))] public float networkDistanceToWP = 999f;
    [SyncVar(hook = nameof(OnTimeRestChanged))] public float restTime = 0f;

    [Header("Waypoints e Navegação")]
    public Animator animator;
    public Transform[] wayPoints;
    public NavMeshAgent navMeshAgent;
    [HideInInspector] public List<Transform> wayPointsRandom;
    public Transform spawnPoint;

    [Header("Ataque e Investigação")]
    public Transform attackPoint;
    public Transform headPoint;
    private float attackRange = 1f;
    public Transform[] soundTriggers;
    private Vector3 offsetHeadPlayer = Vector3.up * 4f;

    [Header("Informações de Jogadores")]
    public LayerMask player1Layer;
    public LayerMask player2Layer;

    // if it's the NPC inside the house then the footstep is more accurate for in house steps, also checks the NPC's velocity
    public bool houseNPC;

    void Start() {
        navMeshAgent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        wayPointsRandom = new List<Transform>();

        StartCoroutine(RandomGrowlCoroutine());
    }

    private void OnPlayerToChaseChanged(GameObject oldPlayer, GameObject newPlayer) {
        playerToChase = newPlayer;
    }

    private void OnSawPlayerChanged(bool oldValue, bool newValue) {
        animator.SetBool("sawPlayer", newValue);
    }

    private void OnHeardNoiseChanged(bool oldValue, bool newValue) {
        animator.SetBool("heardNoise", newValue);
    }

    private void OnDistanceToPlayerChanged(float oldValue, float newValue) {
        animator.SetFloat("distanceToPlayer", newValue);
    }

    private void OnCurrentWaypointChanged(Vector3 oldPos, Vector3 newPos) {
        currentWaypointPosition = newPos;
    }

    private void OnDistanceToWPChanged(float oldDist, float newDist) {
        animator.SetFloat("distanceToWP", newDist);
    }

    private void OnTimeRestChanged(float oldTime, float newTime) {
        animator.SetFloat("restTime", newTime);
    }


    /*
     * the sound triggers are small colliders that when the players step on them they'll make a sound simulating a creak on the wood
     * if the NPC is in range of listening then it'll start chasing the sound to its origin
     */
    public bool CanHearAudioSource() {
        float maxHearingDistance = 0;
        float distance = 0;

        foreach (Transform soundAlarm in soundTriggers) {
            AudioSource audioSource = soundAlarm.GetComponent<AudioSource>();
            if (audioSource.isPlaying) {
                distance = Vector3.Distance(transform.position, audioSource.transform.position);
                maxHearingDistance = audioSource.maxDistance;

                if (distance <= maxHearingDistance) {
                    soundTriggerHeard = soundAlarm;
                }
            }
        }

        return distance < maxHearingDistance;
    }

    void AnimDieChanged(bool oldValue, bool newValue) {
        animator.SetBool("die", newValue);
        StartCoroutine(DestroyNPC(2f));
    }

    private IEnumerator DestroyNPC(float time) {
        yield return new WaitForSeconds(time);
        if(isServer)
            NetworkSpawner.Instance.Destroy(gameObject, true);
    }

    public IEnumerator MoveToSpawn() {
        yield return new WaitForSeconds(2f);
        // directly changing its position wasnt working correctly because of the nav mesh's setup, this fixed it
        navMeshAgent.Warp(spawnPoint.position);
        hasAttacked = false;
        heardNoise = false;
    }

    private bool IsPlayerValidTarget(GameObject player) {
        if (player == null) return false;

        PlayerMovement pm = player.GetComponent<PlayerMovement>();

        return !pm.isDead && !pm.isInChest;
    }

    private GameObject GetValidTargetPlayer() {
        if (playersInFOV.Count == 0)
            return null;

        // if im playing singleplayer then the NPC grabs the player that's on the its FOV (if it's valid, meaning outside a chest or alive)
        if (GameManager.Instance.isSinglePlayer) {
            GameObject player = playersInFOV[0];
            if (IsPlayerValidTarget(player)) {
                this.distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
                return player;
            }
            return null;

        } else {
            GameObject closestVisiblePlayer = null;
            // this variable is so that i know when there's a smaller distance than the current one (this one)
            float distanceToPlayer = float.MaxValue; // it won't cause bad optimization because all floats occupy 4 bytes


            for (int i = 0; i < playersInFOV.Count; i++) {
                GameObject currentPlayer = playersInFOV[i];

                if (IsPlayerValidTarget(currentPlayer)) {
                    float dist = Vector3.Distance(transform.position, currentPlayer.transform.position);

                    if (dist < distanceToPlayer) {
                        closestVisiblePlayer = currentPlayer;
                        distanceToPlayer = dist;
                        this.distanceToPlayer = dist; // it updated the sync var with the closest distance

                    }
                }
            }

            return IsPlayerValidTarget(closestVisiblePlayer) ? closestVisiblePlayer : null;
        }
    }


    void Update() {
        // it should run on the server because it follows the same mechanisms on all clients
        if (isServer == false) {
            return;
        }

        // when the singeplayer player goes back to the beginning, the NPC will keep trying to gtab the player to chase variable but it'll be null
        if (networkDie) {
            if (playerToChase == null) {
                animator.enabled = false;
            }
            navMeshAgent.isStopped = true;

            // so it doesn't activate any animation
            sawPlayer = false;
            heardNoise = false;
            distanceToPlayer = 999f;
            return;
        }

        if (CanHearAudioSource()) {
            heardNoise = true;
        }

        // grabs the player that's valid
        playerToChase = GetValidTargetPlayer();

        if (playerToChase == null) {
            if (sawPlayer == true)
                sawPlayer = false;
            return;
        }

        RaycastHit hit;
        Vector3 playerHeadPosition = playerToChase.transform.position + offsetHeadPlayer;
        float distanceToPlayerHead = Vector3.Distance(headPoint.position, playerHeadPosition);
        // normalize is necessary for the raycast to work well
        Vector3 directionToPlayer = (playerHeadPosition - headPoint.position).normalized;

        if (Physics.Raycast(headPoint.position, directionToPlayer, out hit, distanceToPlayerHead)) {
            if (hit.transform.CompareTag("Player")) {
                PlayerMovement pm = hit.transform.GetComponent<PlayerMovement>();
                sawPlayer = !pm.isDead && !pm.isInChest; // it only sees him if he's valid

            } else {
                sawPlayer = false;
            }

        } else {
            sawPlayer = false;
        }
    }

    public void ChangeHeardNoise(bool value) {
        if (isServer)
            heardNoise = value;
    }


    // updated its list to include the new player that joined the game (it stops being singleplayer)
    public void OnPlayerConnected() {
        if (GameManager.Instance.activePlayers.Count > 0) {
            GameManager.Instance.activePlayers[0] = GameManager.Instance.activePlayers[0];
            GameManager.Instance.activePlayers[1] = GameManager.Instance.activePlayers[1];
        }
    }

    // because someone left the game, it has to update its list so there won't be any problems with nulls (and now it's singleplayer)
    public void OnPlayerDisconnected() {
        if (GameManager.Instance.activePlayers.Count > 0) {
            GameManager.Instance.activePlayers[0] = GameManager.Instance.activePlayers[0];
            GameManager.Instance.activePlayers[1] = null;
        }
    }

    // random way point that's not the current one
    public void MoveToNextWayPoint() {
        wayPointsRandom.Clear();

        for (int i = 0; i < wayPoints.Length; i++) {
            if (wayPoints[i] != wayPoints[currentTarget]) {
                wayPointsRandom.Add(wayPoints[i]);
            }
        }

        currentTarget = Random.Range(0, wayPointsRandom.Count);
        navMeshAgent.SetDestination(wayPointsRandom[currentTarget].position);

        // update the syncvar and the walking animation
        currentWaypointPosition = wayPointsRandom[currentTarget].position;
    }

    // i dont need to activate the sound if it's the client because this will occur independently because the NPC is the same on both players
    public void PlayFootstepSound() {
        // NPCs only trigger the footstep sound through the server because they aren't the owners of any client
        if (isServer && animator.GetBool("die") == false) {
            //i dont need to pass a command block because we are inside the server
            SoundManager.Instance.RpcPlaySound(gameObject, houseNPC);
        }
    }

    public void RotateTowardPlayer() {
        if (playerToChase == null) return;

        // grabs the direction i want to look at by gatting a vector between the player and the NPC
        Vector3 direction = playerToChase.transform.position - transform.position;
        direction.y = 0; // só fazemos a rotação a nível horizontal
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * navMeshAgent.angularSpeed);
    }

    // not needed to activate the sound if it's the client because it doesn't matter, it'll happen on both players anyways
    private IEnumerator RandomGrowlCoroutine() {
        while (animator.GetBool("die") == false) {
            float waitTime = Random.Range(10f, 20f);
            yield return new WaitForSeconds(waitTime);

            string[] growls = { "growl1", "growl2", "moan" };
            string selectedGrowl = growls[Random.Range(0, growls.Length)];

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0); // this shows the current animation of the NCP
            if (stateInfo.IsName("Rest") || stateInfo.IsName("Walk")) {
                if (isServer && animator.GetBool("die") == false)
                    SoundManager.Instance.RpcPlayZombieSoundFX(selectedGrowl, gameObject);
            }
        }
    }


    // it makes the player that was hit die and conclude the NPC's attack
    private void ProcessPlayerHit(Collider playerCollider) {
        PlayerMovement playerMovement = playerCollider.GetComponent<PlayerMovement>();
        if (playerMovement != null) {
            playerMovement.Die(this);
            hasAttacked = true;
        }
    }

    /*
     * the NPC is going to attack the player, it can only attack if it's not already attacking
     * it makes the sound of the attack and i use an overlap sphere to know where the NPC's hand (attack point) passes through
     * and if one of the hits is the player then it hit him
     * also, overlaph sphere is more realistic because im using the hand to attack
     */
    public void AttackMove() {
        if (!hasAttacked) {
            bool hitPlayer = false;

            if (isServer)
                SoundManager.Instance.RpcPlayZombieSoundFX("attack", gameObject);
            else
                SoundManager.Instance.CmdPlayZombieSoundFX("attack", gameObject);


            if (GameManager.Instance.isSinglePlayer) {
                Collider[] hitPlayer1 = Physics.OverlapSphere(attackPoint.position, attackRange, player1Layer);

                foreach (Collider player in hitPlayer1) {
                    ProcessPlayerHit(player);
                    hitPlayer = true;
                }

            } else {
                Collider[] hitPlayer1 = Physics.OverlapSphere(attackPoint.position, attackRange, player1Layer);
                Collider[] hitPlayer2 = Physics.OverlapSphere(attackPoint.position, attackRange, player2Layer);

                foreach (Collider player in hitPlayer1) {
                    ProcessPlayerHit(player);
                    hitPlayer = true;
                }

                foreach (Collider player in hitPlayer2) {
                    ProcessPlayerHit(player);
                    hitPlayer = true;
                }
            }

            if (hitPlayer)
                StartCoroutine(MoveToSpawn());
        }


    }

    // first the NPC dies with the animation and stopp the corroutines, then wait 3 seconds for the missile to dissappear and then call this so it's destoryed
    // this comes from an if(isServer) block
    public void Destroy() {
        NetworkSpawner.Instance.Destroy(gameObject, true);
    }


    [Command(requiresAuthority = false)]
    public void CmdDie() {
        networkDie = true;
    }

}
