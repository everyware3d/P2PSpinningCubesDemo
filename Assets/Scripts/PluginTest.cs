using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class PluginTest : MonoBehaviour
{
    const string pluginName = "com.example.unityplugin.UnityPlugin";

    static AndroidJavaClass _pluginClass;
    static AndroidJavaObject _pluginInstance;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public static AndroidJavaClass PluginClass
    {
        get
        {
            if (_pluginClass == null)
            {
                _pluginClass = new AndroidJavaClass(pluginName);
            }
            return _pluginClass;
        }
    }
    public static AndroidJavaObject PluginInstance
    {
        get
        {
            if (_pluginInstance==null)
            {
                _pluginInstance = PluginClass.CallStatic<AndroidJavaObject>("getInstance");
            }
            return _pluginInstance;
        }
    }

    void Start()
    {
        Debug.Log("Elapsed Time: " + getElapsedTime());
    }

    float elapsedTime = 0;

    // Update is called once per frame
    void Update()
    {
        elapsedTime += Time.deltaTime;
        if (elapsedTime >= 5)
        {
            elapsedTime -= 5;
            Debug.Log("Tick: " + getElapsedTime());
        }
    }

    double getElapsedTime()
    {
        if (Application.platform == RuntimePlatform.Android)
            return PluginInstance.Call<double>("getElapsedTime");
        Debug.LogWarning("Wrong platform");
        return 0;
    }
}
