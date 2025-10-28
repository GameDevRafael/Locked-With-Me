using UnityEngine;
using UnityEngine.UI;

// não queremos que esta classe seja singleton porque vamos ter dois jogadores
// i dont want this class to be singleton because there'll be two players each with different informations
public class CameraScript : MonoBehaviour {
    private float rotationSpeedXAxis = 10f;
    private float rotationSpeedYAxis = 5f;
    private float yaw = 0.0f;
    private float pitch = 0.0f;
    private Rect rotationArea = new Rect(0.5f, 0f, 0.5f, 1f); // right side of the screen

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
        Destroy(GameObject.Find("StartView")); // main menu's camera
    }

    void LateUpdate() {
        // if spectating then i cant move the camera
        if (isSpectating) {
            return;
        }

        if (canRotate == false) {
            // this makes so that the camera wont rotate random when canRotate is false due to the last values of pitch and yaw
            transform.rotation = Quaternion.Euler(pitch, yaw, 0.0f); // remains in the last rotation it had

            return;
        }

        // for every touch on the screen i'll find the ones that ocurred on the right side of the screen and set a max of 60 degrees upwards and downwards
        for (int i = 0; i < Input.touchCount; i++) {
            Touch touch = Input.GetTouch(i);

            if (touch.phase == TouchPhase.Moved && IsTouchInsideRotationArea(touch.position)) {
                yaw += touch.deltaPosition.x * Time.deltaTime * rotationSpeedXAxis;
                pitch -= touch.deltaPosition.y * Time.deltaTime * rotationSpeedYAxis;
                pitch = Mathf.Clamp(pitch, -60, 60);
            }
        }

        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f); // center of the screen is where the aim is
        Ray ray = cameraComponent.ScreenPointToRay(screenCenter);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0.0f);
        transform.rotation = rotation;

        if (insideChest) {
            interactItemButton.interactable = true;

        } else {
            if (Physics.Raycast(ray, out RaycastHit hit, 4f)) {
                string tag = hit.transform.gameObject.tag;
                // if it's one of the items that i can grab then i can enable the button
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