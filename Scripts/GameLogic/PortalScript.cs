using Mirror;
using UnityEngine;

public class PortalScript : NetworkBehaviour {

    // ele come�ava com setactive a true embora na cena esteja a false, isto � porque o server instancia tudo e mete a ativo
    // ent�o damos override deste m�todo e metemos se active a falso
    public override void OnStartClient() {
        gameObject.SetActive(false);
    }
}