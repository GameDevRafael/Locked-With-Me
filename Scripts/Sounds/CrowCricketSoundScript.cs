using System.Collections;
using Mirror;
using UnityEngine;

public class CrowCricketSoundScript : NetworkBehaviour { // o RpcClient precisa de um gameobject com identity
    private bool canPlay = true;

    public void OnTriggerEnter(Collider other) {
        if (canPlay) {
            StartCoroutine(PlayAgainAfterTime(20f));
        }
    }


    // assim o som s� toca num m�nimo intervalo de 20 segundos, mesmo se o jogador entrar e sair repetidamente n�o vai ouvir se n�o tiverem passado 20 segundos
    private IEnumerator PlayAgainAfterTime(float time) {
        canPlay = false;
        SoundManager.Instance.CmdPlaySound(gameObject);
        yield return new WaitForSeconds(time);
        canPlay = true;
    }
}
