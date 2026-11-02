
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.EnhancedTouch;
using System.Collections.Generic;
using System;
using System.Runtime.InteropServices.WindowsRuntime;

public abstract class XRMouseAndTouchMonoBehaviour : MonoBehaviour
{
    public enum HandIndex
    {
        LEFT = 0,
        RIGHT = 1
    };

    public abstract void OnPress(
        HandIndex idx, Vector2 mousePos, Ray ray);

    public abstract void OnRelease(
        HandIndex idx, Vector2 mousePos, Ray ray);

    public abstract void OnMove(
        HandIndex idx, Vector2 mousePos, Ray ray);

    public Transform leftControllerTransform;
    public Transform rightControllerTransform;

    // Screen-stabilized object showing the user's cube color.
    public GameObject outlineForColor;

#if P2P_META_XR
    private bool _rightIsPressed = false;
    private bool _leftIsPressed = false;
#endif

    // True if a controller's current press began over UI.
    private readonly bool[] _uiPressCaptured = new bool[2];

    // True if a controller's current press began in the scene.
    private readonly bool[] _scenePressStarted = new bool[2];

    private readonly List<Canvas> _canvases = new();

    void Awake()
    {
#if UNITY_VISIONOS || P2P_META_XR
        Application.SetStackTraceLogType(
            LogType.Log, StackTraceLogType.None);
#endif
    }

    void OnDisable()
    {
        _uiPressCaptured[0] = false;
        _uiPressCaptured[1] = false;

        _scenePressStarted[0] = false;
        _scenePressStarted[1] = false;

#if P2P_META_XR
        _leftIsPressed = false;
        _rightIsPressed = false;
#endif
    }

    public virtual Vector2 WorldToScreenPoint(Vector3 worldPos)
    {
        Vector3 localPoint =
            outlineForColor.transform.InverseTransformPoint(worldPos);

        return new Vector2(
            localPoint.x + 0.5f,
            localPoint.y + 0.5f);
    }

    public virtual bool GetMousePosition(
        HandIndex controller,
        out Vector2 mousePos,
        out Ray controllerRay)
    {
        mousePos = Vector2.zero;
        controllerRay = new Ray(Vector3.zero, Vector3.forward);

        Transform controllerTransform =
            controller == HandIndex.LEFT
                ? leftControllerTransform
                : rightControllerTransform;

        if (controllerTransform == null || outlineForColor == null)
            return false;

        Vector3 controllerPosition = controllerTransform.position;
        Vector3 controllerForward = controllerTransform.forward;

        controllerRay = new Ray(
            controllerPosition, controllerForward);

        Plane outlinePlane = new Plane(
            outlineForColor.transform.forward,
            outlineForColor.transform.position);

        bool intersects =
            outlinePlane.Raycast(controllerRay, out float enter);

        if (!intersects || enter < 0f)
            return false;

        Vector3 worldPoint = controllerRay.GetPoint(enter);

        mousePos = WorldToScreenPoint(worldPoint);
        return true;
    }

    // Check whether a controller ray intersects
    // any active, raycastable World Space Canvas graphic.
    protected virtual bool IsPointerOverUI(Ray ray)
    {
        _canvases.Clear();
        _canvases.AddRange(
            UnityEngine.Object.FindObjectsByType<Canvas>()
        );
        foreach (Canvas canvas in _canvases)
        {
            if (canvas == null ||
                !canvas.isActiveAndEnabled ||
                canvas.renderMode != RenderMode.WorldSpace)
                continue;

            IList<Graphic> graphics =
                GraphicRegistry.GetGraphicsForCanvas(canvas);

            foreach (Graphic graphic in graphics)
            {
                if (graphic == null ||
                    !graphic.isActiveAndEnabled ||
                    !graphic.raycastTarget ||
                    graphic.depth == -1)
                    continue;

                RectTransform rect = graphic.rectTransform;

                if (rect == null)
                    continue;

                // Intersect controller ray with UI graphic plane.
                Plane plane = new Plane(
                    rect.forward, rect.position);

                if (!plane.Raycast(ray, out float distance) ||
                    distance < 0f)
                    continue;

                Vector3 hitPoint = ray.GetPoint(distance);
                Vector3 localPoint =
                    rect.InverseTransformPoint(hitPoint);

                // Check if hit lies inside the UI rectangle.
                if (!rect.rect.Contains(
                    new Vector2(localPoint.x, localPoint.y)))
                    continue;

                // Respect CanvasGroup interaction blocking.
                if (IsBlockedByCanvasGroup(graphic.transform))
                    continue;

                return true;
            }
        }

        return false;
    }

    private static bool IsBlockedByCanvasGroup(Transform target)
    {
        Transform current = target;

        while (current != null)
        {
            CanvasGroup group =
                current.GetComponent<CanvasGroup>();

            if (group != null && group.enabled)
            {
                if (!group.blocksRaycasts)
                    return true;

                if (group.ignoreParentGroups)
                    break;
            }

            current = current.parent;
        }

        return false;
    }

    // Call this when a controller trigger is pressed.
    protected void ProcessPress(HandIndex idx)
    {
        int hand = (int)idx;

        _uiPressCaptured[hand] = false;
        _scenePressStarted[hand] = false;

        bool gotMousePos = GetMousePosition(
            idx, out Vector2 mousePos, out Ray ray);

        // Check UI even when the controller does not
        // intersect the screen-stabilized outline.
        if (IsPointerOverUI(ray))
        {
            _uiPressCaptured[hand] = true;
            return;
        }

        if (!gotMousePos)
            return;

        _scenePressStarted[hand] = true;
        OnPress(idx, mousePos, ray);
    }

    // Call this when the trigger is released.
    protected void ProcessRelease(HandIndex idx)
    {
        int hand = (int)idx;

        bool uiCaptured = _uiPressCaptured[hand];
        bool sceneStarted = _scenePressStarted[hand];

        _uiPressCaptured[hand] = false;
        _scenePressStarted[hand] = false;

        if (uiCaptured || !sceneStarted)
            return;

        bool gotMousePos = GetMousePosition(
            idx, out Vector2 mousePos, out Ray ray);

        // Always release an interaction that began
        // in the scene, even if now pointing at UI.
        if (gotMousePos)
            OnRelease(idx, mousePos, ray);
    }

    // Call this while a controller trigger is held.
    protected void ProcessMove(HandIndex idx)
    {
        int hand = (int)idx;

        if (_uiPressCaptured[hand])
            return;

        bool gotMousePos = GetMousePosition(
            idx, out Vector2 mousePos, out Ray ray);

        if (!gotMousePos)
            return;

        if (IsPointerOverUI(ray))
            return;

        OnMove(idx, mousePos, ray);
    }

    private void Update()
    {
#if P2P_META_XR
        if (OVRInput.GetDown(
                OVRInput.Button.PrimaryIndexTrigger,
                OVRInput.Controller.LTouch))
        {
            _leftIsPressed = true;
            ProcessPress(HandIndex.LEFT);
        }

        if (OVRInput.GetUp(
                OVRInput.Button.PrimaryIndexTrigger,
                OVRInput.Controller.LTouch))
        {
            ProcessRelease(HandIndex.LEFT);
            _leftIsPressed = false;
        }

        if (OVRInput.GetDown(
                OVRInput.Button.PrimaryIndexTrigger,
                OVRInput.Controller.RTouch))
        {
            _rightIsPressed = true;
            ProcessPress(HandIndex.RIGHT);
        }

        if (OVRInput.GetUp(
                OVRInput.Button.PrimaryIndexTrigger,
                OVRInput.Controller.RTouch))
        {
            ProcessRelease(HandIndex.RIGHT);
            _rightIsPressed = false;
        }

        if (_leftIsPressed)
            ProcessMove(HandIndex.LEFT);

        if (_rightIsPressed)
            ProcessMove(HandIndex.RIGHT);
#endif
    }
}
