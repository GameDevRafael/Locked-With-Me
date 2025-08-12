using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class EnemyHealth : NetworkBehaviour {
    [HideInInspector] public float maxHealth = 100f;
    [SerializeField] private NPCScript nPC;

    public Slider healthSlider;

    [SyncVar(hook = nameof(OnHealthChanged))] private float currentHealth = 100;

    private void OnHealthChanged(float oldHealth, float newHealth) {
        healthSlider.value = newHealth;
    }


    [Command(requiresAuthority = false)]
    public void CmdTakeDamage(float amount) {
        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth); // para ter a certeza que fica entre um mínimo de vida 0 e máx de vida 100

        if (currentHealth == 0) {
            nPC.animator.SetBool("die", true);
            nPC.CmdDie();
            nPC.StopAllCoroutines(); // parar corrotinas tipo a do random growl

            RpcShowDAM();
        }
    }

    [ClientRpc]
    private void RpcShowDAM() {
        GameManager.Instance.DAM.transform.Find("Canvas").gameObject.SetActive(true);
        GameManager.Instance.DAM.transform.GetComponent<DamHealth>().enabled = true;
        UIManager.Instance.ShowCaptionOnce("Great! Now destroy the dam!", 2);
    }

}
