using System.Collections;
using Mirror;
using Mirror.Examples.BenchmarkIdle;
using UnityEngine;
using UnityEngine.UI;

public class DamHealth : NetworkBehaviour {

    private ParticleSystem particleSystem;
    [HideInInspector] public float maxHealth = 100f;
    public Slider healthSlider;


    [SyncVar(hook = nameof(OnHealthChanged))] private float currentHealth = 100;
    [SyncVar(hook = nameof(OnDeathStateChanged))] private bool isDestroyed = false;


    private void OnHealthChanged(float oldHealth, float newHealth) {
        healthSlider.value = newHealth;
    }

    private void OnDeathStateChanged(bool oldValue, bool newValue) {
        particleSystem.Play();
        StartCoroutine(DestroyDAM());

    }

    // with this i can click on the script with the right button of the mouse and call the method directly, really easy to debug without having to play game untill the end!!
    [ContextMenu("Destroy Dam Now")]
    public void CallDestroyDam() {
        StartCoroutine(DestroyDAM());
    }


    private IEnumerator DestroyDAM() {
        if (isServer) {
            yield return new WaitForSeconds(1.5f); // it waits for the explosion to end and then destroy the dam
            UIManager.Instance.SetYouWinInterface(); // the game ended so it shows the you win interface
            NetworkSpawner.Instance.Destroy(gameObject, true);
        }
    }


    [Command(requiresAuthority = false)]
    public void CmdTakeDamage(float amount) {
        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth); // just to make sure that it says between a minimum health of 0 and a max of 100

        if (currentHealth == 0) {
            SoundManager.Instance.RpcPlaySound(gameObject);
            isDestroyed = true;

        }
    }




    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        particleSystem = GetComponentInChildren<ParticleSystem>(); // because it'll be called on every client i dont have to use isLocalPlayer here because of the hook isDestroyed

    }


}
