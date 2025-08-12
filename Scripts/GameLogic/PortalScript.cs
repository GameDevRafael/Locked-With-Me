using Mirror;
using UnityEngine;

public class PortalScript : NetworkBehaviour {

    // ele começava com setactive a true embora na cena esteja a false, isto é porque o server instancia tudo e mete a ativo
    // então damos override deste método e metemos se active a falso
    public override void OnStartClient() {
        gameObject.SetActive(false);
    }
}