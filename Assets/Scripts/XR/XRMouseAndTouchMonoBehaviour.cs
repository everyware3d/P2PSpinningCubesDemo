using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.EnhancedTouch;
using System.Collections.Generic;
using System;
using System.Runtime.InteropServices.WindowsRuntime;

public abstract class XRMouseAndTouchMonoBehaviour : MonoBehaviour
{

    public abstract void OnPress(short idx, Vector2 mousePos, Ray ray);
    public abstract void OnRelease(short idx, Vector2 mousePos, Ray ray);
    public abstract void OnMove(short idx, Vector2 mousePos, Ray ray);

    public Transform leftControllerTransform;
    public Transform rightControllerTransform;
    public GameObject outlineForColor;   // screen stabilized object that shows the current user's color for cubes

    private bool _rightIsPressed = false;
    private bool _leftIsPressed = false;
    void Awake()
    {
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
    }
    public Vector2 WorldToScreenPoint(Vector3 worldPos)
    {
        Vector3 localPoint = outlineForColor.transform.InverseTransformPoint(worldPos);
        return new Vector2(Camera.main.pixelWidth * (localPoint.x + 0.5f), Camera.main.pixelHeight * (localPoint.y + 0.5f));
    }
    public bool GetMousePosition(OVRInput.Controller controller, out Vector2 mousePos, out Ray controllerRay)
    {
        mousePos = Vector2.zero;
        Transform controllerTransform = controller == OVRInput.Controller.LTouch ? leftControllerTransform : rightControllerTransform;
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
    private void Update()
    {
        if (OVRInput.GetDown(
                OVRInput.Button.PrimaryIndexTrigger,
                OVRInput.Controller.LTouch))
        {
            bool gotMousePos = GetMousePosition(OVRInput.Controller.LTouch, out Vector2 mousePos, out Ray ray);
            if (gotMousePos)
                OnPress(0, mousePos, ray);
            _leftIsPressed = true;
        }

        if (OVRInput.GetUp(
                OVRInput.Button.PrimaryIndexTrigger,
                OVRInput.Controller.LTouch))
        {
            bool gotMousePos = GetMousePosition(OVRInput.Controller.LTouch, out Vector2 mousePos, out Ray ray);
            if (gotMousePos)
                OnRelease(0, mousePos, ray);
            _leftIsPressed = false;
        }

        if (OVRInput.GetDown(
                OVRInput.Button.PrimaryIndexTrigger,
                OVRInput.Controller.RTouch))
        {
            bool gotMousePos = GetMousePosition(OVRInput.Controller.RTouch, out Vector2 mousePos, out Ray ray);
            if (gotMousePos)
                OnPress(1, mousePos, ray);
            _rightIsPressed = true;
        }

        if (OVRInput.GetUp(
                OVRInput.Button.PrimaryIndexTrigger,
                OVRInput.Controller.RTouch))
        {
            bool gotMousePos = GetMousePosition(OVRInput.Controller.RTouch, out Vector2 mousePos, out Ray ray);
            if (gotMousePos)
                OnRelease(1, mousePos, ray);
            _rightIsPressed = false;
        }
        if (_leftIsPressed)
        {
            bool gotMousePos = GetMousePosition(OVRInput.Controller.LTouch, out Vector2 mousePos, out Ray ray);
            if (gotMousePos)
                OnMove(0, mousePos, ray);
        }
        if (_rightIsPressed)
        {
            bool gotMousePos = GetMousePosition(OVRInput.Controller.RTouch, out Vector2 mousePos, out Ray ray);
            if (gotMousePos)
                OnMove(1, mousePos, ray);
        }
    }


}