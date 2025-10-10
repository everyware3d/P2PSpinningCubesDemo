
#if OCULUS_SDK
using Oculus.Interaction;
using UnityEngine;

public class XRSharedCubePickable : MonoBehaviour
{
    Color origColor;
    bool isHovering = false;
    bool isSelected = false;
    public void OnHoverEnter() { 
        SharedCube cube = GetComponent<SharedCube>();
        if (!cube.isLocal)
        { // for now, only allow local cubes to be highlighted on hover
            return;
        }
        if (cube != null) {
            Renderer rend = cube.GetComponent<Renderer>();
            if (rend == null) return;
            origColor = rend.material.color;
            isHovering = true;
            rend.material.color = Color.yellow;
        }
    }
    public void OnHoverExit() {
        if (!isHovering)
        {
            return;
        }
        SharedCube cube = GetComponent<SharedCube>();
        if (cube != null && origColor != null)
        {
            Renderer rend = cube?.GetComponent<Renderer>();
            if (rend == null) return;
            rend.material.color = origColor;
        }
    }
    
    RayInteractor getRayInteractor()
    {
        RayInteractable rayInteractable = GetComponent<RayInteractable>();
        if (rayInteractable != null)
        {
            foreach (var view in rayInteractable.InteractorViews)
            {
                if (view.Data is RayInteractor ray)
                {
                    return ray;
                }
            }
        }
        return null;
    }
    Ray getRay()
    {
        RayInteractor rayInteractor = getRayInteractor();
        if (rayInteractor != null)
        {
            return new Ray(rayInteractor.Origin, rayInteractor.Forward);
        }
        return new Ray(Vector3.zero, Vector3.forward);
    }

    float pressedTime;
    public void OnSelectEnter() {
        SharedCube cube = GetComponent<SharedCube>();
        if (cube != null)
        {
            Renderer rend = cube.GetComponent<Renderer>();
            if (rend == null) return;

            if (!cube.isLocal) // for now, only allow local cubes to be selected
                return;
            Ray ray = getRay();
            rend.material.color = Color.red;
            isSelected = true;
            pressedTime = Time.time;
        }

    }
    public void OnSelectExit() {
        if (!isSelected)
        {
            return;
        }
        SharedCube cube = GetComponent<SharedCube>();
        if (cube != null)
        {
            Renderer rend = cube?.GetComponent<Renderer>();
            if (rend == null) return;
            rend.material.color = origColor;
        }
        isSelected = false;
        if (Time.time - pressedTime < .5f){
            // delete this cube if it was a quick select/deselect
            SharedCube.allSharedCubes.Remove(cube.uniqueID);
            cube.Delete(); // deletes from p2p to remove from distribution
            Destroy(gameObject);
        }
    }
    public void Update()
    {
        if (isSelected)
        {
            SharedCube cube = GetComponent<SharedCube>();
            if (cube != null)
            {
                Renderer rend = cube?.GetComponent<Renderer>();
                if (rend == null) return;

                GameObject outlineForColor = P2PSharedCubeInteractionHandler.Instance.outlineForColor;
                if (outlineForColor != null)
                {
                    Ray ray = getRay();
                    Plane plane = new Plane(-outlineForColor.transform.forward, outlineForColor.transform.position);
                    if (plane.Raycast(ray, out float enter))
                    {
                        Vector3 hitPoint = ray.GetPoint(enter);

                        Vector3 localPoint = outlineForColor.transform.InverseTransformPoint(hitPoint);
                        Vector2 normPoint = new Vector2((localPoint.x + .5f), (localPoint.y + .5f));
                        cube.SetTranslation(normPoint);
                        cube.UpdateAllFields();
                    }
                }
            }
        }            
    }
}
#endif
