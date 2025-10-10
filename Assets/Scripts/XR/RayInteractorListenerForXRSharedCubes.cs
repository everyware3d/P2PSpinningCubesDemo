
#if USING_META_SDK

using UnityEngine;

using Oculus.Interaction;
using UnityEngine;

public class RayInteractorListenerForXRSharedCubes : MonoBehaviour
{
    public RayInteractor rayInteractor;

    private void Awake()
    {
        rayInteractor.WhenStateChanged += OnStateChanged;
    }
    Ray getRay()
    {
        if (rayInteractor != null)
        {
            return new Ray(rayInteractor.Origin, rayInteractor.Forward);
        }
        return new Ray(Vector3.zero, Vector3.forward);
    }
    float pressedTime;
    private void OnStateChanged(InteractorStateChangeArgs args)
    {
        // Detect transition into Select state (trigger pulled)
        if (!rayInteractor.HasSelectedInteractable)
        {
            if (args.NewState == InteractorState.Select)
            {
                // pressed
                pressedTime = Time.time;
            } else if (args.PreviousState == InteractorState.Select && Time.time - pressedTime < .5f)
            {
                // released quickly, add SharedCube at that point
                GameObject outlineForColor = P2PSharedCubeInteractionHandler.Instance.outlineForColor;
                if (outlineForColor != null)
                {
                    Ray ray = getRay();
                    Plane plane = new Plane(-outlineForColor.transform.forward, outlineForColor.transform.position);
                    if (plane.Raycast(ray, out float enter))
                    {
                        Vector3 hitPoint = ray.GetPoint(enter);

                        Vector3 localPoint = outlineForColor.transform.InverseTransformPoint(hitPoint);
                        GameObject newGameObject = SharedCube.spawnNewRemoteObject();
                        SharedCube sharedCube = newGameObject.GetComponent<SharedCube>();
                        Vector2 normPoint = new Vector2((localPoint.x + .5f), (localPoint.y + .5f));
                        sharedCube.SetTranslation(normPoint);
                        sharedCube.Insert();  // inserts into p2p for distribution
                        sharedCube.AfterInsertRemote(); // called explicitly since its only called for remotely created instances
                    }
                }

            }
        }
    }
}

#endif