//using System.Numerics;
using UnityEditor;
using UnityEngine;

public class ScreenCanvasScript : MonoBehaviour
{
    
    public Camera mainCamera;
    public Vector3 screenPixels = new Vector3(0, 0, 0);

    public float depth = 5f;
    public bool shouldSet = true;
    private GameObject scaleObject;
    /*private GameObject parentObject;*/
    void Start()
    {
        /*
        float width = mainCamera.pixelWidth;
        float height = mainCamera.pixelHeight;
        screenPoint.y = -height;
        screenPoint.x = 50f;        */
    }


    // Update is called once per frame
    void Update()
    {
        if (scaleObject == null)// } || parentObject == null)
        {
            if (scaleObject == null && transform.childCount > 0)
            {
                scaleObject = transform.GetChild(0).gameObject;
            }
/*            if (parentObject == null && scaleObject != null && scaleObject.transform.childCount > 0)
            {
                parentObject = scaleObject.transform.GetChild(0).gameObject;
            }*/
        }
        if (scaleObject != null && shouldSet)
        {
            float width = mainCamera.pixelWidth;
            float height = mainCamera.pixelHeight;
            Debug.Log("width: " + width + " height: " + height);
            scaleObject.transform.localPosition = new Vector3(width / 2, -height / 2, 0.0f);
            shouldSet = false;
        }
        //transform. = mainCamera.worldToCameraMatrix;
        //transform.position = mainCamera.ScreenToWorldPoint(screenPoint);

        Vector3 screenPos = new Vector3(screenPixels.x, screenPixels.y, depth);
        transform.position = mainCamera.ScreenToWorldPoint(screenPos);

        // Optional: scale so that 1 unit = 1 pixel at that depth
        float pixelHeightAtDepth = 2f * depth * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float worldUnitsPerPixel = pixelHeightAtDepth / Screen.height;
        transform.localScale = new Vector3(1,-1,1) * worldUnitsPerPixel;
    }
}
