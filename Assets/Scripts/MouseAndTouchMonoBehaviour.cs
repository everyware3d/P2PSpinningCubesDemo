using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.EnhancedTouch;
using System.Collections.Generic;

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

        _point.performed += OnMove;      // fires for mouse move, touch move, pen move
        _press.performed += OnPressImpl;      // down
        _press.canceled += OnRelease;    // up

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
            OnRelease(ctx);
        }
        else
        {
            OnPress(ctx);
        }  
    }
    public abstract void OnPress(InputAction.CallbackContext ctx);
    public abstract void OnRelease(InputAction.CallbackContext ctx);
    public abstract void OnMove(InputAction.CallbackContext ctx);

    public static int GetPointerId(InputDevice device)
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
    public static Vector2 ReadDevicePosition(InputDevice device)
    {
        var control = device.TryGetChildControl<Vector2Control>("position");
        return control != null ? control.ReadValue() : Vector2.zero;
    }
}