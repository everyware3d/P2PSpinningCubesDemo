//using System.Numerics;
using UnityEditor;
using UnityEngine;

public class ScreenCanvasScript : MonoBehaviour
{
    
    public Camera mainCamera;
    private float swidth = 0, sheight = 0;

    public float depth = 5f;

    void Start()
    {
    }

    void Update()
    {
        if (swidth != mainCamera.pixelWidth || sheight != mainCamera.pixelHeight)
        {
            swidth = mainCamera.pixelWidth;
            sheight = mainCamera.pixelHeight;
            Debug.Log("ScreenCanvasScript: screen size changed to " + swidth + " x " + sheight);
            Vector3 screenPos = new Vector3(swidth / 2f, sheight / 2f, depth);
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(screenPos);
            Vector3 pos = mainCamera.transform.InverseTransformPoint(worldPos);
            gameObject.transform.localPosition = pos;

            // scale so that 1 unit = 1 pixel at that depth
            float pixelHeightAtDepth = 2f * depth * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float worldUnitsPerPixel = pixelHeightAtDepth / sheight;
            gameObject.transform.localScale = new Vector3(1, 1, 1) * worldUnitsPerPixel;
        }
    }
}
