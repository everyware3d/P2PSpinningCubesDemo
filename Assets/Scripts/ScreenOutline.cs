using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.LightTransport;

[DisallowMultipleComponent]
public class ScreenOutline : MonoBehaviour
{
    static public ScreenOutline Instance;
    public ScreenOutline()
    {
        Instance = this;
    }
    public enum ProjectionMode { Screen = 0, World };
    public ProjectionMode projectionMode = ProjectionMode.Screen;

    public Camera mainCamera;
    private float swidth = 0, sheight = 0;

    void Start()
    {
        if (projectionMode == ProjectionMode.Screen)
        {
            gameObject.transform.localPosition = Vector3.zero;
            gameObject.transform.localRotation = Quaternion.identity;
            gameObject.transform.localScale = Vector3.one;
        }
    }
    static float getCoordValue(int c, float sz, float bufferSize)
    {
        switch (c)
        {
            case 0:
                return 0.0f;
            case 1:
                return 1.0f;
            case 2:
                return bufferSize / sz;
            case 3:
                return (sz - bufferSize) / sz;
        }
        return 0;
    }
    void Update()
    {
        if (projectionMode == ProjectionMode.World)
        {
            generateBufferMesh(24, 12, 0.3f);
        }
        else
        {
            if (swidth != mainCamera.pixelWidth || sheight != mainCamera.pixelHeight)
            {
                swidth = mainCamera.pixelWidth;
                sheight = mainCamera.pixelHeight;
                int maxDim = (int)Math.Max(swidth, sheight);
                float bufferSize = maxDim / 30;
                gameObject.transform.localPosition = Vector3.zero;
                gameObject.transform.localRotation = Quaternion.identity;
                gameObject.transform.localScale = Vector3.one;
                generateBufferMesh(swidth, sheight, bufferSize);
            }
        }
    }
    void generateBufferMesh(float width, float height, float bufferSize)
    {
        Mesh mesh = gameObject.GetComponent<MeshFilter>().mesh;
        mesh.Clear();
        int[] coords = {
                    0, 0,
                    0, 1,
                    2, 1,
                    2, 0,

                    2, 0,
                    2, 2,
                    1, 2,
                    1, 0,

                    2, 3,
                    2, 1,
                    1, 1,
                    1, 3,

                    3, 2,
                    3, 3,
                    1, 3,
                    1, 2
            };
        int coords_len = coords.Length;
        int nverts = coords_len / 2;
        int nidx = 6 * (nverts / 4);
        Vector3[] list = new Vector3[nverts];

        int[] tris = new int[nidx];
        for (int i = 0, i3 = 0; i3 < coords_len; i++, i3 += 2)
        {
            float cx = getCoordValue(coords[i3], width, bufferSize);
            float cy = getCoordValue(coords[i3 + 1], height, bufferSize);
            Vector3 screenPos = new Vector3(width * (cx - .5f), height * (cy - .5f), 0);
            list[i] = screenPos;
        }
        for (int i = 0, ri = 0; i < nidx; i += 6, ri += 4)
        {
            tris[i] = ri;
            tris[i + 1] = ri + 1;
            tris[i + 2] = ri + 2;
            tris[i + 3] = ri;
            tris[i + 4] = ri + 2;
            tris[i + 5] = ri + 3;
        }

        mesh.vertices = list;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
    }
}
