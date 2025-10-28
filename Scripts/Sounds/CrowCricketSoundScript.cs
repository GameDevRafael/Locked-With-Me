using System.Collections;
using Mirror;
using UnityEngine;

public class CrowCricketSoundScript : NetworkBehaviour { // RpcClient needs a gameObject with identity
    private bool canPlay = true;

    public void OnTriggerEnter(Collider other) {
        if (canPlay) {
            StartCoroutine(PlayAgainAfterTime(20f));
        }
    }

    // like this the sound only plays at a minimum interval of 20 seconds
    // even if the player enters and leaves repeatedly it won't hear it if hasn't passed 20 seconds yet
    private IEnumerator PlayAgainAfterTime(float time) {
        canPlay = false;
        SoundManager.Instance.CmdPlaySound(gameObject);
        yield return new WaitForSeconds(time);
        canPlay = true;
    }
}
