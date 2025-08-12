using UnityEngine;

public class OceanSoundScript : MonoBehaviour
{
    private void OnTriggerEnter(Collider other) {
        if (other.gameObject.CompareTag("Player") || other.gameObject.CompareTag("PlayerUntagged")) {
            SoundManager.Instance.CmdPlaySound(gameObject);
        }
    }
}
