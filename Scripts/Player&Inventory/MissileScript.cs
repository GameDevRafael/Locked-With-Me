using System.Collections;
using Mirror;
using UnityEngine;

public class MissileScript : NetworkBehaviour
{
    private ParticleSystem particleSystem;
    [SyncVar] public bool wasShot = false;
    [SyncVar] public bool firstHit = false;
    private NPCScript hitNPC;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        particleSystem = GetComponentInChildren<ParticleSystem>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private IEnumerator WaitForSystemEnd() {
        yield return new WaitForSeconds(3); // tempo que o sistema de partículas tem de vida

        if (isServer){
            if (hitNPC != null) {
                hitNPC.Destroy();
                hitNPC = null;
            }

            NetworkSpawner.Instance.Destroy(this.gameObject, true);
        }

    }

    [ClientRpc]
    void RpcHideMesh() {
        gameObject.GetComponent<MeshRenderer>().enabled = false;
    }

    [Server]
    private void MissileHit() {
        firstHit = true;

        SoundManager.Instance.RpcPlaySound(gameObject);

        RpcHideMesh();

        RpcShowPS();

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 2.5f);

        foreach (Collider hitCollider in hitColliders) {
            if (hitCollider.gameObject == gameObject) continue; // o collider do próprio míssil n tava a deixar este collider funcionar bem

            // o código não estava a apanhar o script por algum motivo então procura-se por ele no gajo inteiro
            // acho eu
            // n tenho a certeza, mas quando fiz isto funcionou logo
            // mas acho que foi coincidência
            NPCScript hitNPC = hitCollider.GetComponent<NPCScript>();
            EnemyHealth enemyHealth = hitCollider.GetComponent<EnemyHealth>();
            DamHealth damHealth = hitCollider.GetComponent<DamHealth>();

            // primeiro corremos a animação dele morrer e fazemos com que ele não faça mais barulho e depois dos 3 segundos para o sistema de partículas se destruir
            // é que também destruímos o NPC
            if (hitNPC != null) {
                if (enemyHealth != null) {
                    enemyHealth.CmdTakeDamage(25);

                } else {
                    hitNPC.CmdDie();
                    hitNPC.StopAllCoroutines(); // parar corrotinas tipo a do random growl
                }
            }

            if(damHealth != null) {
                damHealth.CmdTakeDamage(20);
            }
        }

        StartCoroutine(WaitForSystemEnd());
    }


    private void OnCollisionEnter(Collision collision) {
        if (!isServer) return;

        // temos de ver se já foi disparado senão ele dá destroy assim que começa o jogo porque inicia com uma colisão
        if (wasShot && collision.transform.CompareTag("rocketLauncher") == false && collision.transform.name != "rocketLauncher" 
            && !firstHit && collision.transform.gameObject.CompareTag("Player") == false) {

            Debug.Log("missil bateu em " + collision.transform.name);

            MissileHit();
        }

    }

    [ClientRpc]
    void RpcShowPS() {
        particleSystem.Play();
    }

    [Command(requiresAuthority = false)]
    void CmdShowPS() {
        RpcShowPS();
    }
}
