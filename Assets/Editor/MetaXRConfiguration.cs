
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class MetaXRConfiguration
{
    private const string SettingsKey =
        "com.unity.xr.management.loader_settings";

    private const string OpenXRLoaderName =
        "UnityEngine.XR.OpenXR.OpenXRLoader";

    private static readonly BuildTargetGroup Android =
        BuildTargetGroup.Android;

    // Find a type without a compile-time dependency.
    private static Type FindType(string fullName)
    {
        foreach (Assembly assembly in
                 AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName, false);
            if (type != null)
                return type;
        }

        return null;
    }

    private static object Invoke(
        object instance,
        string methodName,
        params object[] args)
    {
        MethodInfo method = instance.GetType().GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Instance);

        if (method == null)
            throw new Exception(
                $"XR method not found: {methodName}");

        return method.Invoke(instance, args);
    }

    private static object GetProperty(
        object instance,
        string propertyName)
    {
        PropertyInfo property =
            instance.GetType().GetProperty(propertyName);

        if (property == null)
            throw new Exception(
                $"XR property not found: {propertyName}");

        return property.GetValue(instance);
    }

    private static ScriptableObject GetOrCreateSettings(
        Type containerType)
    {
        // Use the actual XR type for the generic lookup.
        MethodInfo getConfig =
            typeof(EditorBuildSettings)
                .GetMethods(BindingFlags.Public |
                            BindingFlags.Static)
                .First(m =>
                    m.Name == "TryGetConfigObject" &&
                    m.IsGenericMethodDefinition)
                .MakeGenericMethod(containerType);

        object[] args = { SettingsKey, null };

        bool found = (bool)getConfig.Invoke(null, args);

        if (found && args[1] is ScriptableObject existing)
            return existing;

        // Search for an existing asset before creating one.
        string[] guids =
            AssetDatabase.FindAssets(
                "t:XRGeneralSettingsPerBuildTarget");

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            var asset =
                AssetDatabase.LoadAssetAtPath(
                    path, containerType) as ScriptableObject;

            if (asset != null)
            {
                EditorBuildSettings.AddConfigObject(
                    SettingsKey, asset, true);

                return asset;
            }
        }

        // No XR settings asset exists yet.
        Debug.Log("Creating XR Management settings asset.");

        if (!AssetDatabase.IsValidFolder("Assets/XR"))
            AssetDatabase.CreateFolder("Assets", "XR");

        string assetPath =
            "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";

        var settings =
            AssetDatabase.LoadAssetAtPath(
                assetPath, containerType) as ScriptableObject;

        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance(
                containerType);

            AssetDatabase.CreateAsset(
                settings, assetPath);

            AssetDatabase.SaveAssets();
        }

        EditorBuildSettings.AddConfigObject(
            SettingsKey, settings, true);

        return settings;
    }

    // Can be called directly via -executeMethod.
    public static void ConfigureMetaXR()
    {
        Debug.Log("Configuring Meta XR for Android...");

        Type perTargetType = FindType(
            "UnityEditor.XR.Management." +
            "XRGeneralSettingsPerBuildTarget");

        Type generalSettingsType = FindType(
            "UnityEngine.XR.Management.XRGeneralSettings");

        Type metadataType = FindType(
            "UnityEditor.XR.Management.Metadata." +
            "XRPackageMetadataStore");

        if (perTargetType == null ||
            generalSettingsType == null ||
            metadataType == null)
        {
            throw new Exception(
                "XR Plug-in Management is not installed " +
                "or its assemblies are not loaded. " +
                "Install com.unity.xr.management and " +
                "restart Unity.");
        }

        if (FindType(OpenXRLoaderName) == null)
        {
            throw new Exception(
                "OpenXR is not installed or loaded. " +
                "Install com.unity.xr.openxr and " +
                "restart Unity.");
        }

        // Obtain the per-platform XR settings container.
        ScriptableObject container =
            GetOrCreateSettings(perTargetType);

        // Get existing Android settings, if any.
        object xrSettings = Invoke(
            container,
            "SettingsForBuildTarget",
            Android);

        object manager = xrSettings == null
            ? null
            : GetProperty(xrSettings, "Manager");

        // Only create defaults when needed.
        // Do not overwrite existing Android XR settings.
        if (manager == null)
        {
            Debug.Log(
                "Creating default Android XR Manager settings.");

            Invoke(
                container,
                "CreateDefaultManagerSettingsForBuildTarget",
                Android);

            xrSettings = Invoke(
                container,
                "SettingsForBuildTarget",
                Android);

            manager = GetProperty(xrSettings, "Manager");
        }

        if (xrSettings == null || manager == null)
            throw new Exception(
                "Unable to initialize Android XR settings.");

        // ------------------------------------------
        // 1. Initialize XR on Startup = enabled
        // ------------------------------------------

        PropertyInfo initProperty =
            generalSettingsType.GetProperty(
                "InitManagerOnStart");

        if (initProperty == null || !initProperty.CanWrite)
            throw new Exception(
                "Cannot set Initialize XR on Startup.");

        initProperty.SetValue(xrSettings, true);

        Debug.Log(
            "Initialize XR on Startup = true");

        // ------------------------------------------
        // 2. OpenXR provider = enabled
        // ------------------------------------------

        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.Static;

        MethodInfo isAssigned =
            metadataType.GetMethod(
                "IsLoaderAssigned", flags);

        MethodInfo assignLoader =
            metadataType.GetMethod(
                "AssignLoader", flags);

        if (isAssigned == null || assignLoader == null)
            throw new Exception(
                "XR loader metadata APIs not found.");

        object[] checkArgs =
        {
            OpenXRLoaderName,
            Android
        };

        bool alreadyAssigned =
            (bool)isAssigned.Invoke(null, checkArgs);

        if (!alreadyAssigned)
        {
            bool success = (bool)assignLoader.Invoke(
                null,
                new object[]
                {
                    manager,
                    OpenXRLoaderName,
                    Android
                });

            if (!success)
                throw new Exception(
                    "Failed to enable OpenXR for Android.");
        }

        // Save all modified settings.
        EditorUtility.SetDirty(
            (UnityEngine.Object)xrSettings);

        EditorUtility.SetDirty(
            (UnityEngine.Object)manager);

        EditorUtility.SetDirty(container);

        AssetDatabase.SaveAssets();

        // Verify both settings.
        bool initEnabled =
            (bool)initProperty.GetValue(xrSettings);

        bool openXREnabled =
            (bool)isAssigned.Invoke(null, checkArgs);

        if (!initEnabled || !openXREnabled)
            throw new Exception(
                "Meta XR configuration verification failed.");

        Debug.Log(
            "Meta XR configuration completed successfully. " +
            "Initialize XR on Startup = true; " +
            "OpenXR = enabled.");
    }
}
