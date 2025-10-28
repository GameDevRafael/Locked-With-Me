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
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth); // to make sure it stays between a minimum of 0 heatlh and 100 max health

        if (currentHealth == 0) {
            nPC.animator.SetBool("die", true);
            nPC.CmdDie();
            nPC.StopAllCoroutines(); // to stop corroutines like random growl

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
