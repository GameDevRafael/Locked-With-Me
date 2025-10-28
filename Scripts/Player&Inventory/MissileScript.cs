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
        yield return new WaitForSeconds(3); // partycle system's time of life

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
            if (hitCollider.gameObject == gameObject) continue; // the missile's collider wasn't letting this collider work well

            // i dont think the code was catching the script for some reason so i tried to find in on the entirety of the character, im not sure, but it worked
            NPCScript hitNPC = hitCollider.GetComponent<NPCScript>();
            EnemyHealth enemyHealth = hitCollider.GetComponent<EnemyHealth>();
            DamHealth damHealth = hitCollider.GetComponent<DamHealth>();

            // first i run the animation of him dying and then make it so he doesn't make any noise and then after the 3 seconds i also destroy the character
            if (hitNPC != null) {
                if (enemyHealth != null) {
                    enemyHealth.CmdTakeDamage(25);

                } else {
                    hitNPC.CmdDie();
                    hitNPC.StopAllCoroutines(); // stop corroutines like random growl
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

        // i have to check if it's been already fired or else it destroys right when the game starts because it starts with a collision
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
