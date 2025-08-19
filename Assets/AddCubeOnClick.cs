using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
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
    private bool hasMovedSincePressed = false;
    private Vector2 pressedPoint;
    private GameObject draggingGameObject;
    private SharedCube draggingSharedCube;
    private Vector3 offset;

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

    InputAction _point;   // <Pointer>/position (Vector2)
    InputAction _press;   // <Pointer>/press    (Button)
    // Track multiple concurrent pointers (mouse id = -1, fingers >= 0)
    readonly Dictionary<int, Vector2> _activePointers = new();

    void OnEnable()
    {
        _point = new InputAction(type: InputActionType.PassThrough, binding: "<Pointer>/position");
        _press = new InputAction(type: InputActionType.PassThrough, binding: "<Pointer>/press");

        _point.performed += OnMove;      // fires for mouse move, touch move, pen move
        _press.performed += OnPress;      // down
        _press.canceled  += OnRelease;    // up

        _point.Enable();
        _press.Enable();

        // Optional: enable Touch → Mouse simulation in Editor
        UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable();
        UnityEngine.InputSystem.EnhancedTouch.TouchSimulation.Enable();
    }
    void OnDisable()
    {
        _point.Disable(); _press.Disable();
        UnityEngine.InputSystem.EnhancedTouch.TouchSimulation.Disable();
        UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Disable();
    }

    void OnRelease(InputAction.CallbackContext ctx)
    {
        var device = ctx.control.device;
        int id = GetPointerId(device);
        // Debug.Log($"Up id:{id}");
        _activePointers.Remove(id);

        var mousePos = _activePointers.TryGetValue(id, out var p) ? p : ReadDevicePosition(device);

        // Debug.Log("OnRelease: id: " + id + " hasMovedSincePressed: " + hasMovedSincePressed);
        if (draggingGameObject == null && !pressedOnObject && !hasMovedSincePressed)  //} && !hasMovedSincePressed)
        {
            // Get mouse position in screen coordinates
            // Vector2 mousePos = Mouse.current.position.ReadValue();

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
            if (!hasMovedSincePressed && draggingSharedCube != null)
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
    static int GetPointerId(InputDevice device)
    {
        if (device is Mouse) return -1; // canonical mouse id
        if (device is Pen)   return -2;
        if (device is Touchscreen ts)
        {
            // Prefer active touch id if available
            foreach (var t in ts.touches)
                if (t.isInProgress) return t.touchId.ReadValue();
            return 0;
        }
        return -999; // fallback
    }
    void OnMove(InputAction.CallbackContext ctx)
    {
        // Determine which pointer moved
        var device = ctx.control.device;
        int id = GetPointerId(device);

        var mousePos = _activePointers.TryGetValue(id, out var p) ? p : ReadDevicePosition(device);

        var screenPos = ctx.ReadValue<Vector2>();
        _activePointers[id] = screenPos;

        // Example: convert to world pos (2D)
        var world = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));

        // TODO: drag handling per pointer id
        // Debug.Log($"Move id:{id} pos:{screenPos} world:{world}");

        if (isDragging)
        {
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            if (dragPlane.Raycast(ray, out float enter))
            {
                Vector3 pos = ray.GetPoint(enter) + offset;
                Vector3 diff = draggingGameObject.transform.position - pos;
                if (diff.magnitude > 0.0001)
                {
                    draggingSharedCube.translation = pos;  // good for owned instance, since it will replicate (but using TCP)
                    draggingSharedCube.UpdateAllFields();
                }
            }
        }
        float dist = (pressedPoint - mousePos).magnitude;
        // Debug.Log("pressedPoint: " + pressedPoint + " mousePos: " + mousePos + " dist: " + dist);
        if (!hasMovedSincePressed && pressedPoint != null && dist > 3)
        {
            hasMovedSincePressed = true;
        }
    }
    void OnPress(InputAction.CallbackContext ctx)
    {
        var device = ctx.control.device;
        int id = GetPointerId(device);
        var mousePos = _activePointers.TryGetValue(id, out var p) ? p : ReadDevicePosition(device);

        if (!_press.IsPressed())
        {
            OnRelease(ctx);
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        RaycastHit hit;

        pressedPoint = mousePos;
        hasMovedSincePressed = false;

        // Debug.Log("pressedPoint: " + pressedPoint);

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
                dragPlane = new Plane(-mainCamera.transform.forward, draggingGameObject.transform.position);
                if (dragPlane.Raycast(ray, out float enter))
                {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    offset = draggingGameObject.transform.position - hitPoint;
                    // Vector3 pos = hitPoint + offset;
                }
            }
        }
        // Your shared handler here (raycast, spawn, select, etc.)
    }

    static Vector2 ReadDevicePosition(InputDevice device)
    {
        var control = device.TryGetChildControl<Vector2Control>("position");
        return control != null ? control.ReadValue() : Vector2.zero;
    }

    // Update is called once per frame
    void Update()
    {
    }
}