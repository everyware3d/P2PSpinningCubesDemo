using System.Collections.Generic;
using UnityEngine;
#if UNITY_VISIONOS
using UnityEngine.XR;
using UnityEngine.XR.Hands;
#endif
public class VisionProFingerTips : MonoBehaviour
{
    public static VisionProFingerTips Instance { get; private set; }

    [Header("Finger Tip Targets")]
    [SerializeField]
    private Transform leftIndexTip;

    [SerializeField]
    private Transform rightIndexTip;

    [Header("Pointer Targets")]
    [SerializeField]
    private Transform leftPointer;

    [SerializeField]
    private Transform rightPointer;

    [Header("Position Smoothing")]
    [SerializeField]
    private float positionSmoothing = 10.0f;

#if UNITY_VISIONOS
    [Header("Finger Rotation")]
    [SerializeField]
    private XRHandJointID directionBaseJoint =
        XRHandJointID.IndexProximal;
#endif
    [SerializeField]
    private float rotationSmoothing = 20.0f;

    [Header("Pinch")]
    [SerializeField]
    private float pinchStartDistance = 0.020f;   // 2.0 cm

    [SerializeField]
    private float pinchReleaseDistance = 0.030f; // 3.0 cm

    [SerializeField]
    private float pinchDistanceSmoothing = 25.0f;

    [SerializeField]
    private float pinchStartDelay = 0.040f;      // 40 ms

    [SerializeField]
    private float pinchReleaseDelay = 0.060f;    // 60 ms

#if UNITY_VISIONOS
    private XRHandSubsystem handSubsystem;
#endif
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

    public float LeftPinchStrength { get; private set; }
    public float RightPinchStrength { get; private set; }

    private float leftSmoothedPinchDistance;
    private float rightSmoothedPinchDistance;

    private bool leftPinchDistanceInitialized;
    private bool rightPinchDistanceInitialized;

    private float leftPinchStartTimer;
    private float rightPinchStartTimer;

    private float leftPinchReleaseTimer;
    private float rightPinchReleaseTimer;

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
#if UNITY_VISIONOS
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
#endif
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

#if UNITY_VISIONOS
        if (handSubsystem == null ||
            !handSubsystem.running)
        {
            return;
        }
        UpdateHand(
            handSubsystem.leftHand,
            leftIndexTip,
            leftPointer,
            true,
            ref leftPressing,
            ref leftSmoothedRotation,
            ref leftRotationInitialized,
            ref leftSmoothedPinchDistance,
            ref leftPinchDistanceInitialized,
            ref leftPinchStartTimer,
            ref leftPinchReleaseTimer);

        UpdateHand(
            handSubsystem.rightHand,
            rightIndexTip,
            rightPointer,
            false,
            ref rightPressing,
            ref rightSmoothedRotation,
            ref rightRotationInitialized,
            ref rightSmoothedPinchDistance,
            ref rightPinchDistanceInitialized,
            ref rightPinchStartTimer,
            ref rightPinchReleaseTimer);
#endif
    }

#if UNITY_VISIONOS
    private void UpdateHand(
        XRHand hand,
        Transform fingerTarget,
        Transform pointerTarget,
        bool left,
        ref bool pressing,
        ref Quaternion smoothedRotation,
        ref bool rotationInitialized,
        ref float smoothedPinchDistance,
        ref bool pinchDistanceInitialized,
        ref float pinchStartTimer,
        ref float pinchReleaseTimer)
    {
        if (!hand.isTracked)
        {
            HandleTrackingLost(
                left,
                ref pressing);

            rotationInitialized = false;
            pinchDistanceInitialized = false;
            pinchStartTimer = 0.0f;
            pinchReleaseTimer = 0.0f;
            return;
        }

        XRHandJoint indexTipJoint =
            hand.GetJoint(
                XRHandJointID.IndexTip);

        XRHandJoint thumbTipJoint =
            hand.GetJoint(
                XRHandJointID.ThumbTip);

        XRHandJoint indexDistalJoint =
            hand.GetJoint(
                XRHandJointID.IndexDistal);

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
        // Pointer position follows the index distal joint.
        // Pointer rotation follows the index proximal joint.
        //

        if (pointerTarget != null &&
            indexDistalJoint.TryGetPose(
                out Pose distalPose) &&
            indexProximalJoint.TryGetPose(
                out Pose proximalPose))
        {
            pointerTarget.localPosition =
                Vector3.Lerp(
                    pointerTarget.localPosition,
                    distalPose.position,
                    positionT);

            pointerTarget.localRotation =
                Quaternion.Slerp(
                    pointerTarget.localRotation,
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

        float rawPinchDistance =
            Vector3.Distance(
                indexPose.position,
                thumbPose.position);

        //
        // Smooth the measured thumb/index distance before deciding
        // whether the pinch is pressed or released.
        //

        if (!pinchDistanceInitialized)
        {
            smoothedPinchDistance = rawPinchDistance;
            pinchDistanceInitialized = true;
        }
        else
        {
            float pinchT =
                1.0f -
                Mathf.Exp(
                    -pinchDistanceSmoothing *
                    Time.deltaTime);

            smoothedPinchDistance =
                Mathf.Lerp(
                    smoothedPinchDistance,
                    rawPinchDistance,
                    pinchT);
        }

        if (left)
            LeftPinchDistance = smoothedPinchDistance;
        else
            RightPinchDistance = smoothedPinchDistance;

        //
        // Continuous pinch strength: 0 = open, 1 = pinched.
        //

        float pinchStrength =
            Mathf.Clamp01(
                Mathf.InverseLerp(
                    pinchReleaseDistance,
                    pinchStartDistance,
                    smoothedPinchDistance));

        if (left)
            LeftPinchStrength = pinchStrength;
        else
            RightPinchStrength = pinchStrength;

        bool wasPressing = pressing;

        //
        // Debounced hysteresis. The threshold must remain satisfied
        // briefly before the press/release state changes.
        //

        if (!pressing)
        {
            pinchReleaseTimer = 0.0f;

            if (smoothedPinchDistance <= pinchStartDistance)
            {
                pinchStartTimer += Time.deltaTime;

                if (pinchStartTimer >= pinchStartDelay)
                {
                    pressing = true;
                    pinchStartTimer = 0.0f;
                }
            }
            else
            {
                pinchStartTimer = 0.0f;
            }
        }
        else
        {
            pinchStartTimer = 0.0f;

            if (smoothedPinchDistance >= pinchReleaseDistance)
            {
                pinchReleaseTimer += Time.deltaTime;

                if (pinchReleaseTimer >= pinchReleaseDelay)
                {
                    pressing = false;
                    pinchReleaseTimer = 0.0f;
                }
            }
            else
            {
                pinchReleaseTimer = 0.0f;
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
#endif
}
