using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class SoundManager : NetworkBehaviour {
    public static SoundManager Instance { get; set; }

    [Header("Music")]
    public AudioSource insideTheme;
    public AudioSource outsideTheme;

    [Header("Sound Effects")]
    public AudioSource rainSound;
    public AudioSource grabItemSound;

    public AudioClip openDoorSound;
    public AudioClip closeDoorSound;
    public AudioClip zombieAttackSound;
    public AudioClip zombieGrowl1Sound;
    public AudioClip zombieGrowl2Sound;
    public AudioClip zombieMoanSound;
    public AudioClip HouseFootstepAudio;
    public AudioClip GrassFootstepAudio;



    // singleton class, i only want one sound manager
    void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void PlaySound(AudioSource audio) {
        if (!audio.isPlaying) {
            audio.Play();
        } else {
            audio.Stop();
            audio.Play();
        }
    }

    public void StopSound(AudioSource audio) {
        audio.Stop();
    }

    // we cant pass transforms through the network, but we can pass their gameObject
    // we have to tell the server to ignore authority because the clients don't have authority of objects that don't belong to them (aren't attached to them)
    [Command(requiresAuthority = false)]
    public void CmdPlaySound(GameObject soundObject) {
        RpcPlaySound(soundObject);
    }


    [ClientRpc] // the servers tells all the clients
    public void RpcPlaySound(GameObject soundObject) {
        AudioSource audio = soundObject.GetComponent<AudioSource>();
        PlaySound(audio);
    }

    [ClientRpc]
    public void RpcPlayMissileShootSound(GameObject soundObject) {
        AudioSource audio = soundObject.transform.Find("ShootMissileSound").gameObject.GetComponent<AudioSource>();
        PlaySound(audio);
    }


    [ClientRpc] // NPCs' footsteps 
    public void RpcPlaySound(GameObject soundObject, bool houseNPC) {
        AudioSource audio = soundObject.GetComponent<AudioSource>();

        if (houseNPC) {
            audio.clip = HouseFootstepAudio;
        } else {
            audio.clip = GrassFootstepAudio;
        }

        PlaySound(audio);
    }

    public void PlayDoorSound(GameObject soundObject, bool openDoor) {
        AudioSource audio = soundObject.GetComponent<AudioSource>();
        audio.clip = openDoor ? openDoorSound : closeDoorSound;
        PlaySound(audio);
    }

    [ClientRpc]
    public void RpcPlayZombieSoundFX(string audioName, GameObject audioObject) {
        AudioSource audioSource = audioObject.GetComponentInChildren<AudioSource>();

        switch (audioName) {
            case "attack":
                audioSource.clip = zombieAttackSound;
                break;
            case "growl1":
                audioSource.clip = zombieGrowl1Sound;
                break;
            case "growl2":
                audioSource.clip = zombieGrowl2Sound;
                break;
            case "moan":
                audioSource.clip = zombieMoanSound;
                break;
        }
        PlaySound(audioSource);
    }

    [Command(requiresAuthority = false)]
    public void CmdPlayZombieSoundFX(string audioName, GameObject audioObject) {
        RpcPlayZombieSoundFX(audioName, audioObject);
    }
}