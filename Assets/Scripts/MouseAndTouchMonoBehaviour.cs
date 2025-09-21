using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.EnhancedTouch;
using System.Collections.Generic;
using System;
using System.Runtime.InteropServices.WindowsRuntime;

public abstract class MouseAndTouchMonoBehaviour : MonoBehaviour
{

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        EnhancedTouchSupport.Enable();
        TouchSimulation.Enable(); // now Mouse.current.leftButton mirrors the first touch
    }
    InputAction _point;   // <Pointer>/position (Vector2)
    protected InputAction _press;   // <Pointer>/press    (Button)
    // Track multiple concurrent pointers (mouse id = -1, fingers >= 0)
    protected readonly Dictionary<int, Vector2> _activePointers = new();

    void OnEnable()
    {
        _point = new InputAction(type: InputActionType.PassThrough, binding: "<Pointer>/position");
        _press = new InputAction(type: InputActionType.PassThrough, binding: "<Pointer>/press");

        _point.performed += OnMoveImpl;      // fires for mouse move, touch move, pen move
        _press.performed += OnPressImpl;      // down
        _press.canceled += OnReleaseImpl;    // up

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

    public void OnPressImpl(InputAction.CallbackContext ctx)
    {
        if (!_press.IsPressed())
        {
            OnReleaseImpl(ctx);
        }
        else
        {
            OnPress(GetMousePositionOnPress(ctx));
        }
    }
    public void OnReleaseImpl(InputAction.CallbackContext ctx)
    {
        OnRelease(GetMousePositionOnRelease(ctx));
    }
    public void OnMoveImpl(InputAction.CallbackContext ctx)
    {
        Tuple<Vector2,Vector2> mouseScreenPos = GetMousePositionOnMove(ctx);
        OnMove(mouseScreenPos.Item1, mouseScreenPos.Item2);
    }

    public abstract void OnPress(Vector2 mousePos);
    public abstract void OnRelease(Vector2 mousePos);
    public abstract void OnMove(Vector2 mousePos, Vector2 screenPos);

    public static int GetPointerId(InputDevice device)
    {
        if (device is Mouse) return -1; // canonical mouse id
        if (device is Pen) return -2;
        if (device is Touchscreen ts)
        {
            // Prefer active touch id if available
            foreach (var t in ts.touches)
                if (t.isInProgress) return t.touchId.ReadValue();
            return 0;
        }
        return -999; // fallback
    }
    public static Vector2 ReadDevicePosition(InputDevice device)
    {
        var control = device.TryGetChildControl<Vector2Control>("position");
        return control != null ? control.ReadValue() : Vector2.zero;
    }

    public Tuple<Vector2, int> GetMousePosition(InputAction.CallbackContext ctx)
    {
        var device = ctx.control.device;
        int id = GetPointerId(device);
        var screenPos = _activePointers.TryGetValue(id, out var p) ? p : ReadDevicePosition(device);
        return new Tuple<Vector2, int>(screenPos, id);
    }
    public Vector2 GetMousePositionOnPress(InputAction.CallbackContext ctx)
    {
        var posPlusId = GetMousePosition(ctx);
        return posPlusId.Item1;
    }
    public Vector2 GetMousePositionOnRelease(InputAction.CallbackContext ctx)
    {
        var posPlusId = GetMousePosition(ctx);
        _activePointers.Remove(posPlusId.Item2);
        return posPlusId.Item1;
    }
    public Tuple<Vector2,Vector2> GetMousePositionOnMove(InputAction.CallbackContext ctx)
    {
        var posPlusId = GetMousePosition(ctx);
        var screenPos = ctx.ReadValue<Vector2>();
        _activePointers[posPlusId.Item2] = screenPos;
        return new Tuple<Vector2,Vector2>(posPlusId.Item1, screenPos);
    }
}