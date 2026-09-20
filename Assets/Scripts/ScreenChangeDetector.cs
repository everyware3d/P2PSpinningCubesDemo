using UnityEngine;

public class ScreenChangeDetector : MonoBehaviour
{
    int lastWidth;
    int lastHeight;

    void Start()
    {
        lastWidth = Screen.width;
        lastHeight = Screen.height;
    }

    void Update()
    {
        if (Screen.width != lastWidth ||
            Screen.height != lastHeight)
        {
            int oldWidth = lastWidth;
            int oldHeight = lastHeight;

            lastWidth = Screen.width;
            lastHeight = Screen.height;

            OnScreenChanged(oldWidth, oldHeight,
                            lastWidth, lastHeight);
        }
    }

    void OnScreenChanged(
        int oldWidth, int oldHeight,
        int newWidth, int newHeight)
    {
        SharedCube[] allCubes = FindObjectsOfType<SharedCube>();
        Debug.Log(
            $"Screen changed: {oldWidth}x{oldHeight} -> " +
            $"{newWidth}x{newHeight}   #cubes={allCubes.Length}");
        foreach (SharedCube cube in allCubes)
        {
            cube.Refresh();
        }
    }
}