using System.Collections;
using Mirror;
using UnityEngine;

public class GramophoneScript : NetworkBehaviour {
    private IEnumerator coroutine;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        coroutine = PlaySoundPeriodically();
        StartCoroutine(coroutine);
    }

    private IEnumerator PlaySoundPeriodically() {
        while (true) {
            yield return new WaitForSeconds(60f); // 60 seconds of wait so it's not too repetitive
            SoundManager.Instance.CmdPlaySound(gameObject);
        }
    }
}
