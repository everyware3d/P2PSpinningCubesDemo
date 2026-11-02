
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

public abstract class MouseAndTouchMonoBehaviour : MonoBehaviour
{
    InputAction _point;
    protected InputAction _press;

    // Track pointer positions.
    protected readonly Dictionary<int, Vector2> _activePointers = new();

    // Pointers whose presses started over UI.
    private readonly HashSet<int> _uiPointers = new();

    // Reuse raycast results to avoid allocations.
    private readonly List<RaycastResult> _uiRaycastResults = new();

    private bool _acceptInput = true;
    private bool _waitingForRelease;

    void Awake()
    {
#if !UNITY_EDITOR
        // EnhancedTouchSupport.Enable();
        // TouchSimulation.Enable();
#endif
    }

    void OnEnable()
    {
#if UNITY_EDITOR
        _point = new InputAction(
            type: InputActionType.PassThrough,
            binding: "<Mouse>/position");

        _press = new InputAction(
            type: InputActionType.PassThrough,
            binding: "<Mouse>/leftButton");
#else
        _point = new InputAction(
            type: InputActionType.PassThrough,
            binding: "<Pointer>/position");

        _press = new InputAction(
            type: InputActionType.PassThrough,
            binding: "<Pointer>/press");
#endif

        _point.performed += OnMoveImpl;
        _press.performed += OnPressImpl;
        _press.canceled += OnReleaseImpl;

        _point.Enable();
        _press.Enable();
    }

    void OnDisable()
    {
        if (_point != null)
        {
            _point.performed -= OnMoveImpl;
            _point.Disable();
            _point.Dispose();
            _point = null;
        }

        if (_press != null)
        {
            _press.performed -= OnPressImpl;
            _press.canceled -= OnReleaseImpl;
            _press.Disable();
            _press.Dispose();
            _press = null;
        }

        _activePointers.Clear();
        _uiPointers.Clear();
        _uiRaycastResults.Clear();
    }

    // Returns true if the screen position hits Canvas UI.
    protected bool IsPointerOverUI(Vector2 screenPosition)
    {
        EventSystem es = EventSystem.current;

        if (es == null)
            return false;

        PointerEventData pointer = new PointerEventData(es)
        {
            position = screenPosition
        };

        _uiRaycastResults.Clear();
        es.RaycastAll(pointer, _uiRaycastResults);

        foreach (RaycastResult result in _uiRaycastResults)
        {
            // Ignore ordinary physics raycasters.
            if (result.module is GraphicRaycaster)
                return true;
        }

        return false;
    }

    public void OnPressImpl(InputAction.CallbackContext ctx)
    {
        try
        {
            if (!_acceptInput || !Application.isFocused)
                return;

            if (_waitingForRelease)
            {
                if (!_press.IsPressed())
                    _waitingForRelease = false;

                return;
            }

            int id = GetPointerId(ctx.control.device);
            Vector2 pos = GetMousePositionOnPress(ctx);

            // PassThrough press actions can receive
            // transitions with different values.
            if (!_press.IsPressed())
            {
                OnReleaseImpl(ctx);
                return;
            }

            // Suppress the complete interaction
            // when the press starts over UI.
            if (IsPointerOverUI(pos))
            {
                _uiPointers.Add(id);
                return;
            }

            _uiPointers.Remove(id);

            OnPress(pos);
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"OnPressImpl exception\n" +
                $"phase={ctx.phase}\n" +
                $"control={ctx.control}\n" +
                $"device={ctx.control?.device}\n" +
                $"{e}");
        }
    }

    public void OnReleaseImpl(InputAction.CallbackContext ctx)
    {
        if (!_acceptInput || !Application.isFocused)
            return;

        if (_waitingForRelease)
        {
            _waitingForRelease = false;
            int waitingId = GetPointerId(ctx.control.device);
            _uiPointers.Remove(waitingId);
            _activePointers.Remove(waitingId);
            return;
        }

        int id = GetPointerId(ctx.control.device);
        Vector2 pos = GetMousePositionOnRelease(ctx);

        // A press that started on UI must not
        // generate a gameplay release.
        if (_uiPointers.Remove(id))
            return;

        // Also ignore releases directly over UI.
        if (IsPointerOverUI(pos))
            return;

        OnRelease(pos);
    }

    public void OnMoveImpl(InputAction.CallbackContext ctx)
    {
        if (!_acceptInput || !Application.isFocused)
            return;

        int id = GetPointerId(ctx.control.device);
        Vector2 pos = GetMousePositionOnMove(ctx);

        // Ignore pointer movement during UI interactions.
        if (_uiPointers.Contains(id))
            return;

        // Ignore movement while hovering over UI.
        if (IsPointerOverUI(pos))
            return;

        OnMove(pos);
    }

    public abstract void OnPress(Vector2 mousePos);
    public abstract void OnRelease(Vector2 mousePos);
    public abstract void OnMove(Vector2 mousePos);

    public static int GetPointerId(InputDevice device)
    {
        if (device is Mouse)
            return -1;

        if (device is Pen)
            return -2;

        if (device is Touchscreen ts)
        {
            foreach (var t in ts.touches)
            {
                if (t.isInProgress)
                    return t.touchId.ReadValue();
            }

            return 0;
        }

        return -999;
    }

    public static Vector2 ReadDevicePosition(InputDevice device)
    {
        var control =
            device.TryGetChildControl<Vector2Control>("position");

        if (control != null)
            return control.ReadValue();

        if (device is Touchscreen ts)
            return ts.primaryTouch.position.ReadValue();

        return Vector2.zero;
    }

    public Tuple<Vector2, int> GetMousePosition(
        InputAction.CallbackContext ctx)
    {
        var device = ctx.control.device;
        int id = GetPointerId(device);

        Vector2 screenPos =
            _activePointers.TryGetValue(id, out var p)
                ? p
                : ReadDevicePosition(device);

        return new Tuple<Vector2, int>(screenPos, id);
    }

    public Vector2 GetMousePositionOnPress(
        InputAction.CallbackContext ctx)
    {
        var device = ctx.control.device;
        int id = GetPointerId(device);

        Vector2 pos = ReadDevicePosition(device);
        _activePointers[id] = pos;

        return pos;
    }

    public Vector2 GetMousePositionOnRelease(
        InputAction.CallbackContext ctx)
    {
        var posPlusId = GetMousePosition(ctx);
        _activePointers.Remove(posPlusId.Item2);

        return posPlusId.Item1;
    }

    public Vector2 GetMousePositionOnMove(
        InputAction.CallbackContext ctx)
    {
        int id = GetPointerId(ctx.control.device);

        // Use the CURRENT position, not the previous one.
        Vector2 screenPos = ctx.ReadValue<Vector2>();

        _activePointers[id] = screenPos;

        return screenPos;
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            _acceptInput = false;
            _waitingForRelease = true;
            _activePointers.Clear();
            _uiPointers.Clear();
        }
        else
        {
            _acceptInput = true;
        }
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            _acceptInput = false;
            _waitingForRelease = true;
            _activePointers.Clear();
            _uiPointers.Clear();
        }
        else
        {
            _acceptInput = true;
        }
    }
}
