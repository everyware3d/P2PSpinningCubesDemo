// #if UNITY_VISIONOS
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.EnhancedTouch;
using P2PPlugin.Network;
using P2PPlugin.Utils;

[DisallowMultipleComponent]
public class VisionSharedCubeInteractionHandler : XRMouseAndTouchMonoBehaviour, P2PInteractionHandler
{
    VisionSharedCubeInteractionHandler()
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

#if UNITY_VISIONOS
    /* Click and dragging SharedCube states */
    private bool[] isDragging = { false, false };   // if an owned cube has been pressed on, the user can drag
    private bool[] pressedOnObject = { false, false };  // whether the user pressed on an object
    private bool[] hasMovedSincePressed = { false, false };  // used for removing object on release, if the object hasn't moved
    private float[] timeWhenLastPressed = { 0.0f, 0.0f };  // used for removing object on release, if the object hasn't moved
    private Plane[] dragPlane = { new Plane(), new Plane() };  // plane to drag the object on, based on the outlineForColor transform
    private Vector2[] pressedPoint = { new Vector2(), new Vector2() };  // the point where the user pressed down, used to determine if the user has moved enough to be considered a drag
    private GameObject[] draggingGameObject = { null, null };
    private SharedCube[] draggingSharedCube = { null, null };
    private Vector3[] offsetObjectToHitPoint = { new Vector3(), new Vector3() };

    private float _movementThresholdInPixels = 0.3f;
#endif
    void Start()
    {
    }
    /* OnPress - If a cube is pressed, then start dragging it around
     *         - If no cube is pressed, keep track of pressedPoint in 
     *             case its a click (detected OnRelease) to add a cube
    */
    override public void OnPress(HandIndex idxarg, Vector2 mouseTouchPos, Ray ray) {
#if UNITY_VISIONOS
        RaycastHit hit;
        int idx = (int)idxarg;
        pressedPoint[idx] = mouseTouchPos;
        hasMovedSincePressed[idx] = false;
        timeWhenLastPressed[idx] = Time.time;
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
#endif
    }
    override public void OnRelease(HandIndex idxarg, Vector2 mouseTouchPos, Ray ray) {
#if UNITY_VISIONOS
        int idx = (int)idxarg;
        float timeSincePressed = Time.time - timeWhenLastPressed[idx];
        if (draggingGameObject[idx] == null && !pressedOnObject[idx] && timeSincePressed < 0.3f){ // !hasMovedSincePressed[idx]) {
            if (Utils.IsOnNormalCanvas(mouseTouchPos)) {
                /* Spawn GameObject, set values on SharedCube component and Insert into P2P Plugin for distribution */
                GameObject newGameObject = SharedCube.spawnNewRemoteObject();
                SharedCube sharedCube = newGameObject.GetComponent<SharedCube>();
                if (sharedCube != null) {
                    sharedCube.SetTranslation(mouseTouchPos);
                    // sharedCube.SetTranslation(Utils.ScreenToNormalized(mouseTouchPos));
                    sharedCube.Insert();  // inserts into p2p for distribution
                    sharedCube.AfterInsertRemote(); // called explicitly since its only called for remotely created instances
                }
            }
        }
        else if (isDragging[idx]) {
            if (draggingSharedCube[idx] != null && timeSincePressed < 0.3f) {// } && !hasMovedSincePressed[idx]) {  // if not moved, treat like a click and delete
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
#endif
    }
    override public void OnMove(HandIndex idxarg, Vector2 mouseTouchPos, Ray ray) {
#if UNITY_VISIONOS
        int idx = (int)idxarg;
        if (isDragging[idx]) {
            if (dragPlane[idx].Raycast(ray, out float enter)) {
                Vector3 pos = ray.GetPoint(enter) + offsetObjectToHitPoint[idx];
                Vector3 diff = draggingGameObject[idx].transform.position - pos;
                if (diff.magnitude > 0.0001) {
                    // draggingSharedCube[idx].SetTranslation(pos);
                    draggingSharedCube[idx].SetTranslation(WorldToScreenPoint(pos));
                    draggingSharedCube[idx].UpdateAllFields();
                }
            }
        }
        float dist = (pressedPoint[idx] - mouseTouchPos).magnitude;
        if (!hasMovedSincePressed[idx] && pressedPoint != null && dist > _movementThresholdInPixels) {
            hasMovedSincePressed[idx] = true;  // if moved, then it shouldn't be deleted on release
        }
#endif
    }
    public Vector2 WorldToScreenPoint(Vector3 worldPos)
    {
        Vector3 localPoint = outlineForColor.transform.InverseTransformPoint(worldPos);
        return new Vector2((localPoint.x + 0.5f), (localPoint.y + 0.5f));
    }

    public bool GetMousePosition(HandIndex handIndex, out Vector2 mousePos, out Ray controllerRay)
    {
        mousePos = Vector2.zero;
        Transform controllerTransform = handIndex == HandIndex.LEFT ? leftControllerTransform : rightControllerTransform;
        Vector3 controllerPosition = controllerTransform.position;
        Vector3 controllerForward = controllerTransform.forward;
        controllerRay = new Ray(controllerPosition, controllerForward);
        Plane outlinePlane = new Plane(
            outlineForColor.transform.forward,
            outlineForColor.transform.position
        );
        bool intersects = outlinePlane.Raycast(controllerRay, out float enter);
        if (!intersects)
            return false;
        if (enter < 0f)
            return false;
        Vector3 worldPoint = controllerRay.GetPoint(enter);
        mousePos = WorldToScreenPoint(worldPoint);
        return true;
    }

    public void Update()
    {
#if UNITY_VISIONOS
        var hands = VisionProFingerTips.Instance;
        if (hands == null)
            return;

        if (hands.RightPressed)
        {
            Debug.Log("RightPressed at position: " + hands.RightPinchPosition);
            if (GetMousePosition(HandIndex.RIGHT, out Vector2 mousePos, out Ray ray))
                OnPress(HandIndex.RIGHT, mousePos, ray);
        }

        if (hands.RightPressing)
        {
            if (GetMousePosition(HandIndex.RIGHT, out Vector2 mousePos, out Ray ray))
                OnMove(HandIndex.RIGHT, mousePos, ray);
        }

        if (hands.RightReleased)
        {
            Debug.Log("RightReleased at position: " + hands.RightPinchPosition);
            if (GetMousePosition(HandIndex.RIGHT, out Vector2 mousePos, out Ray ray))
                OnRelease(HandIndex.RIGHT, mousePos, ray);
        }

        if (hands.LeftPressed)
        {
            Debug.Log("LeftPressed at position: " + hands.LeftPinchPosition);
            if (GetMousePosition(HandIndex.LEFT, out Vector2 mousePos, out Ray ray))
                OnPress(HandIndex.LEFT, mousePos, ray);
        }

        if (hands.LeftPressing)
        {
            if (GetMousePosition(HandIndex.LEFT, out Vector2 mousePos, out Ray ray))
                OnMove(HandIndex.LEFT, mousePos, ray);
        }

        if (hands.LeftReleased)
        {
            Debug.Log("LeftReleased at position: " + hands.LeftPinchPosition);
            if (GetMousePosition(HandIndex.LEFT, out Vector2 mousePos, out Ray ray))
                OnRelease(HandIndex.LEFT, mousePos, ray);
        }
        /*if (GetMousePosition(HandIndex.RIGHT, out Vector2 mousePos2, out Ray ray2))
        {
            Debug.Log("Right hand mouse position: " + mousePos2);
        }*/
#endif
    }
}
