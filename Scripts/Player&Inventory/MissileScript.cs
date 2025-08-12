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
        yield return new WaitForSeconds(3); // tempo que o sistema de part�culas tem de vida

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
            if (hitCollider.gameObject == gameObject) continue; // o collider do pr�prio m�ssil n tava a deixar este collider funcionar bem

            // o c�digo n�o estava a apanhar o script por algum motivo ent�o procura-se por ele no gajo inteiro
            // acho eu
            // n tenho a certeza, mas quando fiz isto funcionou logo
            // mas acho que foi coincid�ncia
            NPCScript hitNPC = hitCollider.GetComponent<NPCScript>();
            EnemyHealth enemyHealth = hitCollider.GetComponent<EnemyHealth>();
            DamHealth damHealth = hitCollider.GetComponent<DamHealth>();

            // primeiro corremos a anima��o dele morrer e fazemos com que ele n�o fa�a mais barulho e depois dos 3 segundos para o sistema de part�culas se destruir
            // � que tamb�m destru�mos o NPC
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

        // temos de ver se j� foi disparado sen�o ele d� destroy assim que come�a o jogo porque inicia com uma colis�o
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
