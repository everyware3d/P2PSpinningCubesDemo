using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

public class VisionProFingerTips : MonoBehaviour
{
    public static VisionProFingerTips Instance { get; private set; }

    [Header("Finger Tip Targets")]
    [SerializeField]
    private Transform leftIndexTip;

    [SerializeField]
    private Transform rightIndexTip;

    [Header("Index Proximal Targets")]
    [SerializeField]
    private Transform leftIndexProximal;

    [SerializeField]
    private Transform rightIndexProximal;

    [Header("Position Smoothing")]
    [SerializeField]
    private float positionSmoothing = 10.0f;

    [Header("Finger Rotation")]
    [SerializeField]
    private XRHandJointID directionBaseJoint =
        XRHandJointID.IndexProximal;

    [SerializeField]
    private float rotationSmoothing = 20.0f;

    [Header("Pinch")]
    [SerializeField]
    private float pinchStartDistance = 0.010f;   // 1.0 cm

    [SerializeField]
    private float pinchReleaseDistance = 0.020f; // 2.0 cm

    private XRHandSubsystem handSubsystem;

    //
    // Pinch state
    //

    private bool leftPressing;
    private bool rightPressing;

    public bool LeftPressing => leftPressing;
    public bool RightPressing => rightPressing;

    public bool LeftPressed { get; private set; }
    public bool RightPressed { get; private set; }

    public bool LeftReleased { get; private set; }
    public bool RightReleased { get; private set; }

    public Vector3 LeftPinchPosition { get; private set; }
    public Vector3 RightPinchPosition { get; private set; }

    public float LeftPinchDistance { get; private set; }
    public float RightPinchDistance { get; private set; }

    //
    // Fingertip smoothing state
    //

    private Quaternion leftSmoothedRotation;
    private Quaternion rightSmoothedRotation;

    private bool leftRotationInitialized;
    private bool rightRotationInitialized;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);

        if (subsystems.Count > 0)
        {
            handSubsystem = subsystems[0];
        }
        else
        {
            Debug.LogError("No XRHandSubsystem found.");
        }
    }

    void Update()
    {
        //
        // Reset one-frame events.
        //

        LeftPressed = false;
        RightPressed = false;

        LeftReleased = false;
        RightReleased = false;

        if (handSubsystem == null ||
            !handSubsystem.running)
        {
            return;
        }

        UpdateHand(
            handSubsystem.leftHand,
            leftIndexTip,
            leftIndexProximal,
            true,
            ref leftPressing,
            ref leftSmoothedRotation,
            ref leftRotationInitialized);

        UpdateHand(
            handSubsystem.rightHand,
            rightIndexTip,
            rightIndexProximal,
            false,
            ref rightPressing,
            ref rightSmoothedRotation,
            ref rightRotationInitialized);
    }

    private void UpdateHand(
        XRHand hand,
        Transform fingerTarget,
        Transform proximalTarget,
        bool left,
        ref bool pressing,
        ref Quaternion smoothedRotation,
        ref bool rotationInitialized)
    {
        if (!hand.isTracked)
        {
            HandleTrackingLost(
                left,
                ref pressing);

            rotationInitialized = false;
            return;
        }

        XRHandJoint indexTipJoint =
            hand.GetJoint(
                XRHandJointID.IndexTip);

        XRHandJoint thumbTipJoint =
            hand.GetJoint(
                XRHandJointID.ThumbTip);

        XRHandJoint indexProximalJoint =
            hand.GetJoint(
                XRHandJointID.IndexProximal);

        XRHandJoint baseJoint =
            hand.GetJoint(
                directionBaseJoint);

        if (!indexTipJoint.TryGetPose(
            out Pose indexPose))
        {
            return;
        }

        //
        // Finger tip position
        //

        float positionT =
            1.0f -
            Mathf.Exp(
                -positionSmoothing *
                Time.deltaTime);

        if (fingerTarget != null)
        {
            fingerTarget.localPosition =
                Vector3.Lerp(
                    fingerTarget.localPosition,
                    indexPose.position,
                    positionT);
        }

        //
        // Index proximal joint pose
        //

        if (proximalTarget != null &&
            indexProximalJoint.TryGetPose(
                out Pose proximalPose))
        {
            proximalTarget.localPosition =
                Vector3.Lerp(
                    proximalTarget.localPosition,
                    proximalPose.position,
                    positionT);

            proximalTarget.localRotation =
                Quaternion.Slerp(
                    proximalTarget.localRotation,
                    proximalPose.rotation,
                    positionT);
        }

        //
        // Finger tip rotation
        //

        if (fingerTarget != null &&
            baseJoint.TryGetPose(
                out Pose basePose))
        {
            Vector3 direction =
                indexPose.position -
                basePose.position;

            if (direction.sqrMagnitude >
                0.000001f)
            {
                direction.Normalize();

                Vector3 up =
                    fingerTarget.localRotation *
                    Vector3.up;

                if (Mathf.Abs(
                        Vector3.Dot(
                            direction,
                            up)) > 0.98f)
                {
                    up = Vector3.up;
                }

                Quaternion desiredRotation =
                    Quaternion.LookRotation(
                        direction,
                        up);

                if (!rotationInitialized)
                {
                    smoothedRotation =
                        desiredRotation;

                    rotationInitialized = true;
                }
                else
                {
                    float rotationT =
                        1.0f -
                        Mathf.Exp(
                            -rotationSmoothing *
                            Time.deltaTime);

                    smoothedRotation =
                        Quaternion.Slerp(
                            smoothedRotation,
                            desiredRotation,
                            rotationT);
                }

                fingerTarget.localRotation =
                    smoothedRotation;
            }
        }

        //
        // Pinch detection
        //

        if (!thumbTipJoint.TryGetPose(
            out Pose thumbPose))
        {
            return;
        }

        float pinchDistance =
            Vector3.Distance(
                indexPose.position,
                thumbPose.position);

        if (left)
        {
            LeftPinchDistance =
                pinchDistance;
        }
        else
        {
            RightPinchDistance =
                pinchDistance;
        }

        bool wasPressing =
            pressing;

        //
        // Pinch hysteresis
        //

        if (!pressing)
        {
            if (pinchDistance <=
                pinchStartDistance)
            {
                pressing = true;
            }
        }
        else
        {
            if (pinchDistance >=
                pinchReleaseDistance)
            {
                pressing = false;
            }
        }

        //
        // Pinch position
        //

        Vector3 rawPinchPosition =
            (
                indexPose.position +
                thumbPose.position
            ) * 0.5f;

        Vector3 currentPinchPosition =
            left
                ? LeftPinchPosition
                : RightPinchPosition;

        if (currentPinchPosition ==
            Vector3.zero)
        {
            currentPinchPosition =
                rawPinchPosition;
        }

        Vector3 smoothedPinchPosition =
            Vector3.Lerp(
                currentPinchPosition,
                rawPinchPosition,
                positionT);

        if (left)
        {
            LeftPinchPosition =
                smoothedPinchPosition;
        }
        else
        {
            RightPinchPosition =
                smoothedPinchPosition;
        }

        //
        // Press event
        //

        if (!wasPressing &&
            pressing)
        {
            if (left)
                LeftPressed = true;
            else
                RightPressed = true;
        }

        //
        // Release event
        //

        if (wasPressing &&
            !pressing)
        {
            if (left)
                LeftReleased = true;
            else
                RightReleased = true;
        }
    }

    private void HandleTrackingLost(
        bool left,
        ref bool pressing)
    {
        if (!pressing)
            return;

        pressing = false;

        if (left)
            LeftReleased = true;
        else
            RightReleased = true;
    }
}