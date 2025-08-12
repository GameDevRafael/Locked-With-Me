using System.Collections;
using System.Collections.Generic;
using System.Net;
using Mirror;
using Mirror.Examples.MultipleMatch;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

public class NPCScript : NetworkBehaviour {

    [HideInInspector] public int currentTarget; // waypoint alvo atual
    [HideInInspector] public bool hasAttacked = false; // não executa o código de ataque multiplas vezes, só a animação
    [HideInInspector] public Transform soundTriggerHeard; // o gameobject que faz o barulho que o jogador faz

    [HideInInspector][SyncVar(hook = nameof(OnPlayerToChaseChanged))] public GameObject playerToChase;
    // players dentro do box collider do NPC que serve como campo de visão
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

    public bool houseNPC; // se for da casa ent o footstep é som de casa senão é som de relva e para ver a velocidade dele

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
     * os sound triggers são pequenos colliders que quando o jogador entra neles irá fazer um som, simula o efeito de pisar
     * em madeira que faz barulho 
     * se o NPC estiver no alcance desse som então começa a dar chase para o local que fez o barulho
     *
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
        // trocar diretamente a posição dele não estava a funcioanr corretamente por causa do setup do nav mesh, mas esta ação é própria para lidar com isso
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

        // se estivermos a jogar singleplayer então o NPC apanha o jogador que estiver no seu FOV (se houver e for válido -> se não estiver escondido ou morto)
        if (GameManager.Instance.isSinglePlayer) {
            GameObject player = playersInFOV[0];
            if (IsPlayerValidTarget(player)) {
                this.distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
                return player;
            }
            return null;

        } else {
            GameObject closestVisiblePlayer = null;
            // esta variável é só para sabermos quando apanhamos uma distância mais pequena do que a atual (esta)
            float distanceToPlayer = float.MaxValue; // não mete em causa a otimização porque todos os floats ocupam 4 bytes

            for (int i = 0; i < playersInFOV.Count; i++) {
                GameObject currentPlayer = playersInFOV[i];

                if (IsPlayerValidTarget(currentPlayer)) {
                    float dist = Vector3.Distance(transform.position, currentPlayer.transform.position);

                    if (dist < distanceToPlayer) {
                        closestVisiblePlayer = currentPlayer;
                        distanceToPlayer = dist;
                        this.distanceToPlayer = dist; // aqui atualizamos a syncVar com a distância mais próxima

                    }
                }
            }

            return IsPlayerValidTarget(closestVisiblePlayer) ? closestVisiblePlayer : null;
        }
    }


    void Update() {
        // só precisamos do server a controlar o server porque ele segue o mesmo mecanismo em todos os clientes
        if (isServer == false) {
            return;
        }

        // quando o jogador no singleplayer volta ao inicio o NPC vai continuar a tentar apanhar a variable playerToCHase mas vai ser nula
        if (networkDie) {
            if (playerToChase == null) {
                animator.enabled = false;
            }
            navMeshAgent.isStopped = true;

            // faz com que não ative nenhuma animation
            sawPlayer = false;
            heardNoise = false;
            distanceToPlayer = 999f;
            return;
        }

        if (CanHearAudioSource()) {
            heardNoise = true;
        }

        // apanhamos o jogador que for válido (single ou multiplayer) que não esteja nem morto nem escondido
        playerToChase = GetValidTargetPlayer();

        if (playerToChase == null) {
            if (sawPlayer == true)
                sawPlayer = false;
            return;
        }

        RaycastHit hit;
        Vector3 playerHeadPosition = playerToChase.transform.position + offsetHeadPlayer;
        float distanceToPlayerHead = Vector3.Distance(headPoint.position, playerHeadPosition);
        // normalized necessário para o raycast funcionar bem (normalizar sempre um vetor)
        Vector3 directionToPlayer = (playerHeadPosition - headPoint.position).normalized;

        if (Physics.Raycast(headPoint.position, directionToPlayer, out hit, distanceToPlayerHead)) {
            if (hit.transform.CompareTag("Player")) {
                PlayerMovement pm = hit.transform.GetComponent<PlayerMovement>();
                sawPlayer = !pm.isDead && !pm.isInChest; // só o vemos se for válido (não estiver nem escondido nem morto)

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


    // atualizamos a lista para incluir o novo jogador que entrou no jogo e dizer que já não estamos a jogar singleplayer
    public void OnPlayerConnected() {
        if (GameManager.Instance.activePlayers.Count > 0) {
            GameManager.Instance.activePlayers[0] = GameManager.Instance.activePlayers[0];
            GameManager.Instance.activePlayers[1] = GameManager.Instance.activePlayers[1];
        }
    }

    // como um jogador saiu do jogo temos de atualizar a lista para não haver problemas de nulos e dizer que estamos a jogar singleplayer
    public void OnPlayerDisconnected() {
        if (GameManager.Instance.activePlayers.Count > 0) {
            GameManager.Instance.activePlayers[0] = GameManager.Instance.activePlayers[0];
            GameManager.Instance.activePlayers[1] = null;
        }
    }

    // escolhemos um waypoint ao calhas que não seja outra vez o way point atual
    public void MoveToNextWayPoint() {
        wayPointsRandom.Clear();

        for (int i = 0; i < wayPoints.Length; i++) {
            if (wayPoints[i] != wayPoints[currentTarget]) {
                wayPointsRandom.Add(wayPoints[i]);
            }
        }

        currentTarget = Random.Range(0, wayPointsRandom.Count);
        navMeshAgent.SetDestination(wayPointsRandom[currentTarget].position);

        // atualizamos a syncvar e a animação de andar
        currentWaypointPosition = wayPointsRandom[currentTarget].position;
    }

    // não precisamos de meter a dar o som se for o cliente porque isto ocorre independentemente deles, ou seja, o NPC é exatamente igual em ambos os jogos
    public void PlayFootstepSound() {
        // os NPCs só dão trigger do som do passo a partir do servidor porque não são "donos" de nenhum cliente
        if (isServer && animator.GetBool("die") == false) {
            // não precisamos de passar por um método [Command] porque estamos a chamar diretamente pelo servidor
            SoundManager.Instance.RpcPlaySound(gameObject, houseNPC);
        }
    }

    public void RotateTowardPlayer() {
        if (playerToChase == null) return;

        // apanhamos a direção em que queremos olhar apanhando um vetor 3D entre o jogador e nós
        Vector3 direction = playerToChase.transform.position - transform.position;
        direction.y = 0; // só fazemos a rotação a nível horizontal
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * navMeshAgent.angularSpeed);
    }

    // não precisams de meter a dar o som caso seja o cliente porque isto não é dependente de um deles, vai ocorrer independentemente
    private IEnumerator RandomGrowlCoroutine() {
        while (animator.GetBool("die") == false) {
            float waitTime = Random.Range(10f, 20f);
            yield return new WaitForSeconds(waitTime);

            string[] growls = { "growl1", "growl2", "moan" };
            string selectedGrowl = growls[Random.Range(0, growls.Length)];

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0); // isto serve para vermos qual é o comportamento atual do NPC
            if (stateInfo.IsName("Rest") || stateInfo.IsName("Walk")) {
                if (isServer && animator.GetBool("die") == false)
                    SoundManager.Instance.RpcPlayZombieSoundFX(selectedGrowl, gameObject);
            }
        }
    }


    // fazemos o jogador que foi acertado pelo NPC morrer e concluímos o ataque do NPC
    private void ProcessPlayerHit(Collider playerCollider) {
        PlayerMovement playerMovement = playerCollider.GetComponent<PlayerMovement>();
        if (playerMovement != null) {
            playerMovement.Die(this);
            hasAttacked = true;
        }
    }

    /*
     * o NPC vai atacar o jogador, só pode atacar se não estiver já a atacar (a animação corre à mesma mas não faz o código)
     * fazmoes o som de ataque e usamos um OverlapSphere para sabermos onde a mão do NPC (que ataca) 
     * passou e se um dos hits for o jogador então é porque lhe acertou independentemente se for single ou multiplayer
     * usamos overlapSphere para ser mais realista porque estamos a usar a mão do NPC e assim usamos uma esfera
     * se fosse uma bala já não era preciso esta maneira
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

    // nós primeiro fazemos o npc morrer com a animação e paramos as corrotinas
    // e esperamos 3 segundos para o míssil desaparecer e depois chamamos isto para ele ser destruído
    // isto vem de um call dentro de um bloco de if(isServer)
    public void Destroy() {
        NetworkSpawner.Instance.Destroy(gameObject, true);
    }


    [Command(requiresAuthority = false)]
    public void CmdDie() {
        networkDie = true;
    }

}
