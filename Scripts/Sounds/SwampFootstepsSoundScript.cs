using System.Collections;
using UnityEngine;

public class SwampScript : MonoBehaviour {
    private bool isCoroutineRunning = false;
    [SerializeField] private AudioSource swampSoundFX;
    private Coroutine soundCoroutine;

    private void OnTriggerEnter(Collider other) {
        if (other.gameObject.CompareTag("Player") || other.gameObject.CompareTag("PlayerUntagged")) {
            if (!isCoroutineRunning) {
                soundCoroutine = StartCoroutine(PlaySoundPeriodically());
                isCoroutineRunning = true;
            }
        }
    }

    private void OnTriggerExit(Collider other) {
        if (other.gameObject.CompareTag("Player") || other.gameObject.CompareTag("PlayerUntagged")) {
            if (isCoroutineRunning && soundCoroutine != null) {
                StopCoroutine(soundCoroutine);
                isCoroutineRunning = false;
                soundCoroutine = null;
            }
        }
    }

    private IEnumerator PlaySoundPeriodically() {
        while (true) {
            yield return new WaitForSeconds(60f);
            SoundManager.Instance.PlaySound(swampSoundFX);
        }
    }
}