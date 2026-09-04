using UnityEngine;

public class VisionProShaderWarmup : MonoBehaviour
{
    void Awake()
    {
        Shader.WarmupAllShaders();
    }
}