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

    // assim clicamos no script com o bot�o direito e chamamos a fun��o diretamente, muito f�cil de dar debug sem ter de jogar o jogo constantemente!!
    [ContextMenu("Destroy Dam Now")]
    public void CallDestroyDam() {
        StartCoroutine(DestroyDAM());
    }


    private IEnumerator DestroyDAM() {
        if (isServer) {
            yield return new WaitForSeconds(1.5f); // esperamos at� a explos�o acabar e destruimos a barragem
            UIManager.Instance.SetYouWinInterface(); // o jogo acabou ent�o vamos mostrar you win interface
            NetworkSpawner.Instance.Destroy(gameObject, true); // e destru�mos a barragem
        }
    }


    [Command(requiresAuthority = false)]
    public void CmdTakeDamage(float amount) {
        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth); // para ter a certeza que fica entre um m�nimo de vida 0 e m�x de vida 100

        if (currentHealth == 0) {
            SoundManager.Instance.RpcPlaySound(gameObject);
            isDestroyed = true;

        }
    }




    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        particleSystem = GetComponentInChildren<ParticleSystem>(); // como vai ser chamado em todos os clientes n�o usamos isLocalPlayer por causa do hook isDestroyed

    }


}
