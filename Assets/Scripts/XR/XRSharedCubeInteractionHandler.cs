using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.EnhancedTouch;
using P2PPlugin.Network;
using P2PPlugin.Utils;

[DisallowMultipleComponent]
public class XRSharedCubeInteractionHandler : XRMouseAndTouchMonoBehaviour, P2PInteractionHandler
{
    XRSharedCubeInteractionHandler()
    {
        P2PInteractionHandler.Instance = this;
    }
    public GameObject parentOfSpawnedGOs; // parent GameObject to hold all spawned SharedCube instances
    public GameObject prefabToSpawn;     // prefab GameObject created when clicked on an empty space, has SharedCube component
    // public GameObject outlineForColor;   // actually in XRMouseAndTouchMonoBehaviour

    public GameObject getParentOfSpawnedGOs()
    {
        return parentOfSpawnedGOs;
    }
    public GameObject getPrefabToSpawn()
    {
        return prefabToSpawn;
    }
    public GameObject getOutlineForColor()
    {
        return outlineForColor;
    }

    /* Click and dragging SharedCube states */
    private bool[] isDragging = { false, false };   // if an owned cube has been pressed on, the user can drag
    private bool[] pressedOnObject = { false, false };  // whether the user pressed on an object
    private bool[] hasMovedSincePressed = { false, false };  // used for removing object on release, if the object hasn't moved
    private Plane[] dragPlane = { new Plane(), new Plane() };  // plane to drag the object on, based on the outlineForColor transform
    private Vector2[] pressedPoint = { new Vector2(), new Vector2() };  // the point where the user pressed down, used to determine if the user has moved enough to be considered a drag
    private GameObject[] draggingGameObject = { null, null };
    private SharedCube[] draggingSharedCube = { null, null };
    private Vector3[] offsetObjectToHitPoint = { new Vector3(), new Vector3() };

    private float _movementThresholdInPixels = 3f;

    void Start()
    {
        _movementThresholdInPixels = Mathf.Max(Camera.main.pixelWidth, Camera.main.pixelHeight) * 0.03f;  // 1% of the larger dimension
    }
    /* OnPress - If a cube is pressed, then start dragging it around
     *         - If no cube is pressed, keep track of pressedPoint in 
     *             case its a click (detected OnRelease) to add a cube
    */
    override public void OnPress(HandIndex idxarg, Vector2 mouseTouchPos, Ray ray) {
        RaycastHit hit;
        int idx = (int)idxarg;
        pressedPoint[idx] = mouseTouchPos;
        hasMovedSincePressed[idx] = false;
        if (Physics.Raycast(ray, out hit)) {  // if click hits an object/cube
            draggingSharedCube[idx] = hit.transform.gameObject.GetComponent<SharedCube>();
            pressedOnObject[idx] = true;
            if (draggingSharedCube[idx].isLocal) { // restrict cubes that aren't owned by this node (for now)
                isDragging[idx] = true;
                draggingGameObject[idx] = hit.transform.gameObject;
                dragPlane[idx] = new Plane(
                    outlineForColor.transform.forward,
                    outlineForColor.transform.position
                );
                if (dragPlane[idx].Raycast(ray, out float enter)) {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    offsetObjectToHitPoint[idx] = draggingGameObject[idx].transform.position - hitPoint;
                }
            }
            else {
                draggingSharedCube[idx] = null;
            }
        }
    }
    override public void OnRelease(HandIndex idxarg, Vector2 mouseTouchPos, Ray ray) {
        int idx = (int)idxarg;
        if (draggingGameObject[idx] == null && !pressedOnObject[idx] && !hasMovedSincePressed[idx]) {
            if (Utils.IsOnNormalCanvas(mouseTouchPos)) {
                /* Spawn GameObject, set values on SharedCube component and Insert into P2P Plugin for distribution */
                GameObject newGameObject = SharedCube.spawnNewRemoteObject();
                SharedCube sharedCube = newGameObject.GetComponent<SharedCube>();
                if (sharedCube != null) {
                    sharedCube.SetTranslation(Utils.ScreenToNormalized(mouseTouchPos));
                    sharedCube.Insert();  // inserts into p2p for distribution
                    sharedCube.AfterInsertRemote(); // called explicitly since its only called for remotely created instances
                }
            }
        }
        else if (isDragging[idx]) {
            if (draggingSharedCube[idx] != null && !hasMovedSincePressed[idx]) {  // if not moved, treat like a click and delete
                if (draggingSharedCube[idx].isLocal) {
                    SharedCube.allSharedCubes.Remove(draggingSharedCube[idx].uniqueID);
                    draggingSharedCube[idx].Delete(); // deletes from p2p to remove from distribution
                    Destroy(draggingGameObject[idx]);
                }
                else {
                    Debug.Log("Cannot delete Shared Cube that was not created by this user");
                }
            }
            isDragging[idx] = false;
            draggingSharedCube[idx] = null;
            draggingGameObject[idx] = null;
        }
        pressedOnObject[idx] = false;
    }
    override public void OnMove(HandIndex idxarg, Vector2 mouseTouchPos, Ray ray) {
        int idx = (int)idxarg;
        // var world = Camera.main.ScreenToWorldPoint(new Vector3(mouseTouchPos.x, mouseTouchPos.y, 0f));
        if (isDragging[idx]) {
            if (dragPlane[idx].Raycast(ray, out float enter)) {
                Vector3 pos = ray.GetPoint(enter) + offsetObjectToHitPoint[idx];
                Vector3 diff = draggingGameObject[idx].transform.position - pos;
                if (diff.magnitude > 0.0001) {
                    draggingSharedCube[idx].SetTranslation(Utils.ScreenToNormalized(WorldToScreenPoint(pos)));
                    draggingSharedCube[idx].UpdateAllFields();
                }
            }
        }

        float dist = (pressedPoint[idx] - mouseTouchPos).magnitude;
        if (!hasMovedSincePressed[idx] && pressedPoint != null && dist > _movementThresholdInPixels) {
            hasMovedSincePressed[idx] = true;  // if moved, then it shouldn't be deleted on release
        }
    }
}
