using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.EnhancedTouch;
using P2PPlugin.Network;

[DisallowMultipleComponent]
public class AddCubeOnClick : MouseAndTouchMonoBehaviour
{
    static public AddCubeOnClick Instance;
    AddCubeOnClick()
    {
        Instance = this;
    }
    public Camera mainCamera;
    public GameObject prefabToSpawn;     // prefab GameObject created when clicked on an empty space, has SharedCube component
    public GameObject outlineForColor;   // screen stabilized object that shows the current user's color for cubes

    /* Click and dragging SharedCube states */
    private bool isDragging = false;   // if an owned cube has been pressed on, the user can drag
    private bool pressedOnObject = false;  // whether the user pressed on an object
    private bool hasMovedSincePressed = false;  // used for removing object on release, if the object hasn't moved

    private Plane dragPlane;
    private Vector2 pressedPoint;
    private GameObject draggingGameObject;
    private SharedCube draggingSharedCube;
    private Vector3 offsetObjectToHitPoint;

    private P2PComputer localComputer = null;
    private bool inserted = false;
    void Start()
    {
        localComputer = new P2PComputer();
        P2PComputer.addP2PChangeListener((addOrRemove, p2pIns) =>
        {
            SharedCube.reloadAssignedColors();
        });
    }

    public void Update()
    {
        if (P2PObject.instantiated && !inserted)
        {
            localComputer.Insert();
            localComputer.AfterInsertRemote();
            inserted = true;
        }
    }
    override public void OnRelease(InputAction.CallbackContext ctx)
    {
        var device = ctx.control.device;
        int id = GetPointerId(device);
        _activePointers.Remove(id);

        var mousePos = _activePointers.TryGetValue(id, out var p) ? p : ReadDevicePosition(device);

        if (draggingGameObject == null && !pressedOnObject && !hasMovedSincePressed)
        {
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
            if (draggingSharedCube != null && !hasMovedSincePressed)
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
    override public void OnMove(InputAction.CallbackContext ctx)
    {
        // Determine which pointer moved
        var device = ctx.control.device;
        int id = GetPointerId(device);
        var mousePos = _activePointers.TryGetValue(id, out var p) ? p : ReadDevicePosition(device);
        var screenPos = ctx.ReadValue<Vector2>();
        _activePointers[id] = screenPos;

        // Example: convert to world pos (2D)
        var world = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        if (isDragging)
        {
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            if (dragPlane.Raycast(ray, out float enter))
            {
                Vector3 pos = ray.GetPoint(enter) + offsetObjectToHitPoint;
                Vector3 diff = draggingGameObject.transform.position - pos;
                if (diff.magnitude > 0.0001)
                {
                    draggingSharedCube.translation = pos;  // good for owned instance, since it will replicate (but using TCP)
                    draggingSharedCube.UpdateAllFields();
                }
            }
        }
        float dist = (pressedPoint - mousePos).magnitude;
        if (!hasMovedSincePressed && pressedPoint != null && dist > 3)
        {
            hasMovedSincePressed = true;
        }
    }

    /* OnPress - If a cube is pressed, then start dragging it around
     *         - If no cube is pressed, keep track of pressedPoint in 
     *             case its a click (detected OnRelease) to add a cube
    */
    override public void OnPress(InputAction.CallbackContext ctx)
    {
        var device = ctx.control.device;
        int id = GetPointerId(device);

        var mousePos = _activePointers.TryGetValue(id, out var p) ? p : ReadDevicePosition(device);
        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        RaycastHit hit;
        pressedPoint = mousePos;
        hasMovedSincePressed = false;
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
                    offsetObjectToHitPoint = draggingGameObject.transform.position - hitPoint;
                }
            }
        }
    }
}