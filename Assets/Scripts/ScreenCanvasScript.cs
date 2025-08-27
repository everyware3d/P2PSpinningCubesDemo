//using System.Numerics;
using UnityEditor;
using UnityEngine;

public class ScreenCanvasScript : MonoBehaviour
{
    
    public Camera mainCamera;
    public Vector3 screenPixels = new Vector3(0, 0, 0);

    public float depth = 5f;
    
    /*private GameObject parentObject;*/
    void Start()
    {
    }


    // Update is called once per frame
    void Update()
    {
        float width = mainCamera.pixelWidth;
        float height = mainCamera.pixelHeight;
        Debug.Log("width: " + width + " height: " + height);
        transform.localPosition = new Vector3(width / 2, -height / 2, 0.0f);

        Vector3 screenPos = new Vector3(screenPixels.x, screenPixels.y, depth);
        gameObject.transform.parent.transform.position = mainCamera.ScreenToWorldPoint(screenPos);

        // Optional: scale so that 1 unit = 1 pixel at that depth
        float pixelHeightAtDepth = 2f * depth * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float worldUnitsPerPixel = pixelHeightAtDepth / Screen.height;
        gameObject.transform.parent.transform.localScale = new Vector3(1,-1,1) * worldUnitsPerPixel;
    }
}
