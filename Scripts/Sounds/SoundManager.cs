using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class SoundManager : NetworkBehaviour {
    public static SoundManager Instance { get; set; }

    // tudo sons 2D, os 3D são passados por parâmetros
    // music
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



    // singleton classe, se voltar a usar este script então destruímo-lo para termos a certeza que só existe um SoundManager
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


    // não podemos passar objetos pela network então passo o nome do objeto e depois apanhamo-lo diretamente
    // PS: afinal podemos, não podemos passar o transform mas podemos passar o gameObject
    [Command(requiresAuthority = false)] // dizemos ao server, os clientes não têm autoridade sobre objetos que não lhe pertencem (que não estão attached ao jogador) então temos de ignorar autoridade
    public void CmdPlaySound(GameObject soundObject) {
        RpcPlaySound(soundObject);
    }


    [ClientRpc] // dizemos do server para todos os clientes
    public void RpcPlaySound(GameObject soundObject) {
        AudioSource audio = soundObject.GetComponent<AudioSource>();
        PlaySound(audio);
    }

    [ClientRpc]
    public void RpcPlayMissileShootSound(GameObject soundObject) {
        AudioSource audio = soundObject.transform.Find("ShootMissileSound").gameObject.GetComponent<AudioSource>();
        PlaySound(audio);
    }


    [ClientRpc] // footsteps dos NPCs
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

    [ClientRpc] // dizemos do server para todos os clientes
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