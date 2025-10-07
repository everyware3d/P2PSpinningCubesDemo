using UnityEngine;


public class Utils
{
    public static bool IsOnCanvas(Vector2 mouseTouchPos)
    {
        return mouseTouchPos.x > 0 && mouseTouchPos.x < Camera.main.pixelWidth &&
               mouseTouchPos.y > 0 && mouseTouchPos.y < Camera.main.pixelHeight;
    }
    public static Vector3 NormalizedToScreen(Vector2 norm)
    {   // converts normalized coordinates (0.0 to 1.0) to screen coordinates with (0,0) at center of screen
        if (ScreenOutline.Instance.projectionMode == ScreenOutline.ProjectionMode.Screen)
        {
            return new Vector3((norm.x - 0.5f) * Camera.main.pixelWidth, (norm.y - 0.5f) * Camera.main.pixelHeight, 0.0f);
        } else
        {
            return new Vector3(norm.x - 0.5f, norm.y - 0.5f, 0.0f);
        }
    }
    public static Vector2 ScreenToNormalized(Vector3 screen)
    {   // WorldToScreenPoint result already is normalized between 0.0 and pixel dimensions
        return new Vector2((screen.x / Camera.main.pixelWidth), (screen.y / Camera.main.pixelHeight));
    }
}