using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

[DisallowMultipleComponent]
public class AddCubeOnClick : MonoBehaviour
{
    static public AddCubeOnClick Instance;
    AddCubeOnClick()
    {
        Instance = this;
    }
    [SerializeField]

    public Camera mainCamera;
    [SerializeField]
    public GameObject prefabToSpawn;

    public GameObject outlineForColor;

    private bool isDragging = false;
    private bool pressedOnObject = false;
    private Plane dragPlane;
    private bool hasDragged = false;
    private GameObject draggingGameObject;
    private SharedCube draggingSharedCube;
    private Vector3 offset;

    private bool added = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        EnhancedTouchSupport.Enable();
        TouchSimulation.Enable(); // now Mouse.current.leftButton mirrors the first touch
    }
    void Start()
    {
        P2PNetworkedObject.addPeerChangeListener((addOrRemove, peerID) =>
        {
            SharedCube.reloadAssignedColors();
        });
        SharedCube.reloadAssignedColors();
    }

    // Update is called once per frame
    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                draggingGameObject = hit.transform.gameObject;
                draggingSharedCube = draggingGameObject.GetComponent<SharedCube>();
                pressedOnObject = true;
                if (!draggingSharedCube.isLocal)
                {
                    // can't move cubes that aren't owned by this node
                    draggingGameObject = null;
                    draggingSharedCube = null;
                }
                else
                {
                    isDragging = true;
                    hasDragged = false;
                    dragPlane = new Plane(-mainCamera.transform.forward, draggingGameObject.transform.position);
                    if (dragPlane.Raycast(ray, out float enter))
                    {
                        Vector3 hitPoint = ray.GetPoint(enter);
                        offset = draggingGameObject.transform.position - hitPoint;
                        Vector3 pos = hitPoint + offset;
                    }
                }
            }
        }
        else if (isDragging && Mouse.current.leftButton.isPressed)
        {
            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (dragPlane.Raycast(ray, out float enter))
            {
                Vector3 pos = ray.GetPoint(enter) + offset;
                Vector3 diff = draggingGameObject.transform.position - pos;
                if (diff.magnitude > 0.0001)
                {
                    draggingSharedCube.translation = pos;  // good for owned instance, since it will replicate (but using TCP)
                    hasDragged = true;
                    draggingSharedCube.UpdateAllFields();
                }
            }
        }
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            if (draggingGameObject == null && !pressedOnObject)
            {
                // Get mouse position in screen coordinates
                Vector2 mousePos = Mouse.current.position.ReadValue();

                // Convert to world position
                Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mainCamera.nearClipPlane + 5f));

                // Spawn object
                GameObject newGameObject = Instantiate(prefabToSpawn, worldPos, Quaternion.identity);
                newGameObject.transform.SetParent(mainCamera.transform);
                newGameObject.SetActive(true);
                /* Set SharedCube values and Insert() */
                SharedCube sharedCube = newGameObject.GetComponent<SharedCube>();
                if (sharedCube != null)
                {
                    sharedCube.translation = worldPos;
                    sharedCube.Insert();
                    SharedCube.allSharedCubes.Add(sharedCube.uniqueID, sharedCube);
                    SharedCube.setAssignedColor(sharedCube);
                }
            }
            else if (isDragging) // if hasn't dragged, delete
            {
                // SharedCube sharedCube = draggingGameObject.GetComponent<SharedCube>();
                if (!hasDragged && draggingSharedCube != null)
                {
                    if (draggingSharedCube.isLocal)
                    {
                        SharedCube.allSharedCubes.Remove(draggingSharedCube.uniqueID);
                        draggingSharedCube.Delete();
                        Destroy(draggingGameObject);
                    }
                    else
                    {
                        Debug.Log("Cannot delete Shared Cube that was not created by this user");
                    }
                }
                isDragging = false;
                draggingSharedCube = null;
                draggingGameObject = null;
            }
            pressedOnObject = false;
        }
    }
}