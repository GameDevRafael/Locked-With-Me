using UnityEngine;
using UnityEngine.UI;

// n�o queremos que esta classe seja singleton porque vamos ter dois jogadores
// se f�sse um singleton ent�o mudar uma c�mara de um jogador implicaria mudar a c�mara de todos os jogadores
public class CameraScript : MonoBehaviour {
    private float rotationSpeedXAxis = 10f;
    private float rotationSpeedYAxis = 5f;
    private float yaw = 0.0f;
    private float pitch = 0.0f;
    private Rect rotationArea = new Rect(0.5f, 0f, 0.5f, 1f); // lado direito do ecr�

    private Camera cameraComponent;

    [HideInInspector] public bool isSpectating;
    [HideInInspector] public bool canRotate = true;
    [HideInInspector] public bool insideChest;

    private Button interactItemButton;


    void Start() {
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
        interactItemButton = UIManager.Instance.interactItemButton.GetComponent<Button>();
    }

    private void Awake() {
        cameraComponent = gameObject.GetComponentInChildren<Camera>();
        Destroy(GameObject.Find("StartView")); // c�mara do main menu
    }

    void LateUpdate() {
        // se estivermos a dar spectate n�o podemos mexer na c�mara
        if (isSpectating) {
            return;
        }

        if (canRotate == false) {
            // isto faz com que a c�mara n�o rodopie quando canRotate � falso devido aos �ltimos valores do pitch e yaw
            transform.rotation = Quaternion.Euler(pitch, yaw, 0.0f); // fica na �ltima rota��o que ficou

            return;
        }

        // para todos os toques no ecr� vamos procurar pelos que ocorrem no lado direito do ecr� e num m�ximo de 60 graus para cima e baixo
        for (int i = 0; i < Input.touchCount; i++) {
            Touch touch = Input.GetTouch(i);

            if (touch.phase == TouchPhase.Moved && IsTouchInsideRotationArea(touch.position)) {
                yaw += touch.deltaPosition.x * Time.deltaTime * rotationSpeedXAxis;
                pitch -= touch.deltaPosition.y * Time.deltaTime * rotationSpeedYAxis;
                pitch = Mathf.Clamp(pitch, -60, 60);
            }
        }

        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f); // apanhar o centro do ecr� (onde a mira est�)
        Ray ray = cameraComponent.ScreenPointToRay(screenCenter);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0.0f);
        transform.rotation = rotation;

        if (insideChest) {
            interactItemButton.interactable = true;

        } else {
            if (Physics.Raycast(ray, out RaycastHit hit, 4f)) {
                string tag = hit.transform.gameObject.tag;
                // se for um dos items que podemos apanhar ent�o podemos dar enable do bot�o sen�o metemos disabled
                bool shouldEnable = tag == "chest" || tag == "door" || tag == "key" || tag == "lock" || tag == "rocketLauncher" || tag == "missile";
                interactItemButton.interactable = shouldEnable;

            } else {
                interactItemButton.interactable = false;
            }

        }


    }


    private bool IsTouchInsideRotationArea(Vector2 touchPosition) {
        Vector3 viewportPosition = cameraComponent.ScreenToViewportPoint(touchPosition);
        return rotationArea.Contains(viewportPosition);
    }
}