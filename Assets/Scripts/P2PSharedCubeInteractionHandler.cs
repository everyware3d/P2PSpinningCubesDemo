using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.EnhancedTouch;
using P2PPlugin.Network;

[DisallowMultipleComponent]
public class P2PSharedCubeInteractionHandler : MouseAndTouchMonoBehaviour
{
    static public P2PSharedCubeInteractionHandler Instance;
    P2PSharedCubeInteractionHandler()
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

    /* OnPress - If a cube is pressed, then start dragging it around
     *         - If no cube is pressed, keep track of pressedPoint in 
     *             case its a click (detected OnRelease) to add a cube
    */
    override public void OnPress(Vector2 mouseTouchPos) {
        Ray ray = mainCamera.ScreenPointToRay(mouseTouchPos);
        RaycastHit hit;
        pressedPoint = mouseTouchPos;
        hasMovedSincePressed = false;
        if (Physics.Raycast(ray, out hit)) {  // if click hits an object/cube
            draggingSharedCube = hit.transform.gameObject.GetComponent<SharedCube>();
            pressedOnObject = true;
            if (draggingSharedCube.isLocal) { // restrict cubes that aren't owned by this node (for now)
                isDragging = true;
                draggingGameObject = hit.transform.gameObject;
                dragPlane = new Plane(-mainCamera.transform.forward, draggingGameObject.transform.position);
                if (dragPlane.Raycast(ray, out float enter)) {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    offsetObjectToHitPoint = draggingGameObject.transform.position - hitPoint;
                }
            } else {
                draggingSharedCube = null;
            }
        }
    }
    override public void OnRelease(Vector2 mouseTouchPos) {
        if (draggingGameObject == null && !pressedOnObject && !hasMovedSincePressed) {
            /* Spawn GameObject, set values on SharedCube component and Insert into P2P Plugin for distribution */
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(mouseTouchPos.x, mouseTouchPos.y, mainCamera.nearClipPlane + 5f));
            GameObject newGameObject = Instantiate(prefabToSpawn, worldPos, Quaternion.identity);
            newGameObject.transform.SetParent(mainCamera.transform);
            newGameObject.SetActive(true);
            SharedCube sharedCube = newGameObject.GetComponent<SharedCube>();
            if (sharedCube != null) { // prefabToSpawn should have SharedCube component defined
                sharedCube.SetTranslation(worldPos);
                sharedCube.Insert();  // inserts into p2p for distribution
                SharedCube.allSharedCubes.Add(sharedCube.uniqueID, sharedCube);
                SharedCube.setAssignedColorToCube(sharedCube);
            }
        } else if (isDragging) {
            if (draggingSharedCube != null && !hasMovedSincePressed) {  // if not moved, treat like a click and delete
                if (draggingSharedCube.isLocal) {
                    SharedCube.allSharedCubes.Remove(draggingSharedCube.uniqueID);
                    draggingSharedCube.Delete(); // deletes from p2p to remove from distribution
                    Destroy(draggingGameObject);
                } else {
                    Debug.Log("Cannot delete Shared Cube that was not created by this user");
                }
            }
            isDragging = false;
            draggingSharedCube = null;
            draggingGameObject = null;
        }
        pressedOnObject = false;
    }
    override public void OnMove(Vector2 mouseTouchPos) {
        var world = Camera.main.ScreenToWorldPoint(new Vector3(mouseTouchPos.x, mouseTouchPos.y, 0f));
        if (isDragging) {
            Ray ray = mainCamera.ScreenPointToRay(mouseTouchPos);
            if (dragPlane.Raycast(ray, out float enter)) {
                Vector3 pos = ray.GetPoint(enter) + offsetObjectToHitPoint;
                Vector3 diff = draggingGameObject.transform.position - pos;
                if (diff.magnitude > 0.0001) {
                    draggingSharedCube.SetTranslation(pos);  // good for owned instance, since it will replicate (but using TCP)
                    draggingSharedCube.UpdateAllFields();
                }
            }
        }
        float dist = (pressedPoint - mouseTouchPos).magnitude;
        if (!hasMovedSincePressed && pressedPoint != null && dist > 3) {
            hasMovedSincePressed = true;  // if moved, then it shouldn't be deleted on release
        }
    }
}