
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CommandLineBuild
{
    private const string ScenePath =
        "Assets/Scenes/P2PSpinningCubesDemo.unity";

    private const string MetaScenePath =
        "Assets/Scenes/MetaP2PSpinningCubesDemo.unity";

    private const string PendingBuildKey =
        "CommandLineBuild.PendingExport";

    // Tracks XR removal across the script/assembly reload caused by UPM.
    private const string PendingXRRemovalKey =
        "CommandLineBuild.PendingXRRemoval";

    private static readonly string[] MetaXRPackageNames =
    {
        "com.meta.xr.sdk.all",
        "com.unity.xr.openxr",
        "com.unity.xr.management"
    };

    private static double xrRemovalWaitStarted = -1;
    private const double XRRemovalTimeoutSeconds = 120;

    private const string OpenXRLoaderName =
        "UnityEngine.XR.OpenXR.OpenXRLoader";

    private enum ExportType
    {
        Android,
        Meta,
        IOS,
        OSX
    }

    // ---------------------------------------------------------
    // One-time Meta package installation
    // ---------------------------------------------------------

    // [MenuItem("Tools/P2P/3) Configure Meta XR")]
    public static void ConfigureMetaXR()
    {
        Debug.Log("Configuring Meta XR after package installation...");
        MetaXRConfiguration.ConfigureMetaXR();
    }

    public static void InstallPackage(string packageName, string packageDescription, Action afterInstallFunction = null)
    {
        var packages = new List<string>();

        if (!HasPackage(packageName))
            packages.Add(packageName);

        if (packages.Count == 0)
        {
            Debug.Log("The " + packageDescription + " is already installed.");
            if (Application.isBatchMode)
                EditorApplication.Exit(0);
            return;
        }

        Debug.Log("Installing: " + string.Join(", ", packages));

        AddAndRemoveRequest packageRequest = Client.AddAndRemove(
            packages.ToArray(),
            Array.Empty<string>());

        EditorApplication.CallbackFunction checkPackageFunction = null;
        checkPackageFunction = () =>
        {
            if (!packageRequest.IsCompleted)
                return;

            EditorApplication.update -= checkPackageFunction;

            CheckPackageInstallImpl(packageRequest, afterInstallFunction);
        };    
        EditorApplication.update += checkPackageFunction;
    }

    // [MenuItem("Tools/P2P/1) Install Meta XR Management")]
    public static void InstallMetaXRManagement()
    {
        InstallPackage(
            "com.unity.xr.management",
            "Meta XR Management package");
    }

    // [MenuItem("Tools/P2P/2) Install OpenXR Package")]
    public static void InstallOpenXR()
    {
        InstallPackage(
            "com.unity.xr.openxr@1.19.0-pre.2",
            "OpenXR package");
    }

    // [MenuItem("Tools/P2P/4) Install Meta XR SDK Package")]
    public static void InstallMetaXRSDK()
    {
        InstallPackage(
            "com.meta.xr.sdk.all",
            "Meta XR All-in-One SDK package");
    }
    
    private static void CheckPackageInstallImpl(AddAndRemoveRequest pr, Action afterInstallAction = null)
    {
        if (pr == null ||
            !pr.IsCompleted)
            return;

        if (pr.Status == StatusCode.Success)
        {
            if (afterInstallAction != null)
            {
                afterInstallAction?.Invoke();
            }
            Debug.Log(
                "Meta package installation completed. " +
                "Reopen Unity if XR settings are not visible.");
        }
        else
        {
            Debug.LogError(
                "Package installation failed: " +
                pr.Error?.message +
                "\nIf the Meta package cannot be resolved, " +
                "install Meta XR All-in-One SDK from the Asset Store.");
        }
        if (Application.isBatchMode)
            EditorApplication.Exit(pr.Status == StatusCode.Success ? 0 : 1);
    }
    private static bool HasPackage(string packageName)
    {
        string name = packageName.Split('@')[0];

        return UnityEditor.PackageManager.PackageInfo
            .GetAllRegisteredPackages()
            .Any(p => p.name == name);

/*        return UnityEditor.PackageManager.PackageInfo
            .GetAllRegisteredPackages()
            .Any(p => p.name == packageName);*/
    }

    private static void VerifyMetaPackages()
    {
        string[] required =
        {
            "com.unity.xr.management",
            "com.unity.xr.openxr",
            "com.meta.xr.sdk.all"
        };

        foreach (string package in required)
        {
            if (!HasPackage(package))
            {
                throw new Exception(
                    $"Meta export requires package {package}. " +
                    "Run Tools/P2P/Install Meta Build Packages " +
                    "in the Unity Editor first.");
            }
        }
    }


    // Returns the packages that are actually present. The names passed
    // to AddAndRemove for removal must NOT include @version.
    private static string[] GetInstalledMetaXRPackages()
    {
        return MetaXRPackageNames.Where(HasPackage).ToArray();
    }

    // [MenuItem("Tools/P2P/5) Uninstall Meta XR")]
    public static void UnInstallMetaXR()
    {
        BeginMetaXRRemoval(null);
    }

    // If resumeExport is non-null, continue the requested non-Meta
    // export after Unity has completed uninstalling and reloading.
    private static void BeginMetaXRRemoval(ExportType? resumeExport)
    {
        string[] packagesToRemove = GetInstalledMetaXRPackages();

        if (packagesToRemove.Length == 0)
        {
            Debug.Log("No Meta XR packages to uninstall.");

            if (resumeExport.HasValue)
            {
                SessionState.EraseString(PendingXRRemovalKey);
                ExportType type = resumeExport.Value;
                EditorApplication.delayCall += () =>
                    ContinueWhenReady(GetBuildTarget(type), type);
            }
            else if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
            return;
        }

        if (resumeExport.HasValue)
        {
            // Both keys survive an Editor assembly/domain reload.
            SessionState.SetString(PendingBuildKey,
                resumeExport.Value.ToString());
            SessionState.SetString(PendingXRRemovalKey, "1");
            xrRemovalWaitStarted = EditorApplication.timeSinceStartup;
        }

        Debug.Log("Uninstalling Meta XR before " +
            (resumeExport.HasValue ? resumeExport.Value.ToString() : "exit") +
            ": " + string.Join(", ", packagesToRemove));

        AddAndRemoveRequest request = Client.AddAndRemove(
            Array.Empty<string>(), packagesToRemove);

        EditorApplication.CallbackFunction checkFunction = null;
        checkFunction = () =>
        {
            if (!request.IsCompleted)
                return;

            EditorApplication.update -= checkFunction;

            if (request.Status != StatusCode.Success)
            {
                FailPendingExport("Meta XR uninstall failed: " +
                    request.Error?.message);
                return;
            }

            Debug.Log("Meta XR package removal request completed.");

            if (resumeExport.HasValue)
            {
                // Wait for package registration/import/compilation to
                // settle before the pending export resumes. If this
                // callback is lost during reload, InitializeOnLoadMethod
                // schedules the same continuation via SessionState.
                ExportType type = resumeExport.Value;
                EditorApplication.delayCall += () =>
                    ContinueWhenReady(GetBuildTarget(type), type);
            }
            else if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        };

        EditorApplication.update += checkFunction;
    }

    private static void FailPendingExport(string message)
    {
        SessionState.EraseString(PendingXRRemovalKey);
        SessionState.EraseString(PendingBuildKey);
        Debug.LogError(message);

        if (Application.isBatchMode)
            EditorApplication.Exit(1);
        else
            throw new Exception(message);
    }

    // ---------------------------------------------------------
    // Export entry points
    // ---------------------------------------------------------

    public static void ExportAndroid()
    {
        StartExport(
            BuildTargetGroup.Android,
            BuildTarget.Android,
            ExportType.Android);
    }

    public static void ExportMeta()
    {
        StartExport(
            BuildTargetGroup.Android,
            BuildTarget.Android,
            ExportType.Meta);
    }

    public static void ExportIOS()
    {
        StartExport(
            BuildTargetGroup.iOS,
            BuildTarget.iOS,
            ExportType.IOS);
    }

    public static void ExportOSX()
    {
        StartExport(
            BuildTargetGroup.Standalone,
            BuildTarget.StandaloneOSX,
            ExportType.OSX);
    }

    private static BuildTarget GetBuildTarget(ExportType type)
    {
        switch (type)
        {
            case ExportType.Android:
            case ExportType.Meta:
                return BuildTarget.Android;

            case ExportType.IOS:
                return BuildTarget.iOS;

            case ExportType.OSX:
                return BuildTarget.StandaloneOSX;

            default:
                throw new Exception(
                    $"Unsupported export type: {type}");
        }
    }

    // ---------------------------------------------------------
    // Platform switching
    // ---------------------------------------------------------

    private static void StartExport(
        BuildTargetGroup targetGroup,
        BuildTarget target,
        ExportType type)
    {
        Debug.Log($"Starting {type} export for {target}");

        // Non-Meta exports must run without Meta/XR packages.
        // Removal is asynchronous, so store the export request and
        // resume it when Unity is ready. Do not use -quit for these
        // three entry points; RunExport() exits batch mode itself.
        if (type != ExportType.Meta)
        {
            string[] xrPackages = GetInstalledMetaXRPackages();
            if (xrPackages.Length > 0)
            {
                SessionState.SetString(PendingBuildKey, type.ToString());
                BeginMetaXRRemoval(type);
                return;
            }
        }

        if (EditorUserBuildSettings.activeBuildTarget == target)
        {
            SessionState.EraseString(PendingBuildKey);
            RunExport(type);
            return;
        }

        Debug.Log(
            $"Switching build target from " +
            $"{EditorUserBuildSettings.activeBuildTarget} to {target}");

        SessionState.SetString(PendingBuildKey, type.ToString());

        bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(
            targetGroup, target);

        if (!switched)
        {
            FailPendingExport($"Failed to switch build target to {target}.");
            return;
        }

        // Some target switches don't trigger a domain reload.
        EditorApplication.delayCall += () => ContinueWhenReady(target, type);
    }

    // Called following a domain reload, either from package removal
    // or from changing the active build target.
    [InitializeOnLoadMethod]
    private static void ContinuePendingBuild()
    {
        string pending = SessionState.GetString(PendingBuildKey, "");
        if (string.IsNullOrEmpty(pending))
            return;

        if (!Enum.TryParse(pending, out ExportType type))
        {
            FailPendingExport("Invalid pending export: " + pending);
            return;
        }

        EditorApplication.delayCall += () =>
            ContinueWhenReady(GetBuildTarget(type), type);
    }

    private static void ContinueWhenReady(BuildTarget target, ExportType type)
    {
        // A previously scheduled callback may still exist after the
        // export has started. Never run the same export twice.
        if (SessionState.GetString(PendingBuildKey, "") != type.ToString())
            return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += () => ContinueWhenReady(target, type);
            return;
        }

        if (type != ExportType.Meta)
        {
            string[] remaining = GetInstalledMetaXRPackages();
            if (remaining.Length > 0)
            {
                // A package-removal operation can reload the scripts,
                // invalidating its callback. Poll registered packages
                // instead of starting a second conflicting operation.
                if (SessionState.GetString(PendingXRRemovalKey, "") == "1")
                {
                    if (xrRemovalWaitStarted < 0)
                        xrRemovalWaitStarted = EditorApplication.timeSinceStartup;

                    if (EditorApplication.timeSinceStartup -
                        xrRemovalWaitStarted > XRRemovalTimeoutSeconds)
                    {
                        FailPendingExport(
                            "XR removal did not finish. Remaining packages: " +
                            string.Join(", ", remaining));
                        return;
                    }

                    EditorApplication.delayCall += () =>
                        ContinueWhenReady(target, type);
                    return;
                }

                // Covers restoration after a platform-switch reload.
                BeginMetaXRRemoval(type);
                return;
            }

            SessionState.EraseString(PendingXRRemovalKey);
            xrRemovalWaitStarted = -1;
        }

        if (EditorUserBuildSettings.activeBuildTarget != target)
        {
            Debug.Log($"Switching to {target} after XR cleanup.");

            bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(
                target == BuildTarget.Android ? BuildTargetGroup.Android :
                target == BuildTarget.iOS ? BuildTargetGroup.iOS :
                BuildTargetGroup.Standalone,
                target);

            if (!switched)
            {
                FailPendingExport($"Failed to switch build target to {target}.");
                return;
            }

            EditorApplication.delayCall += () => ContinueWhenReady(target, type);
            return;
        }

        SessionState.EraseString(PendingBuildKey);
        Debug.Log($"Ready to export {type}; target={target}.");
        RunExport(type);
    }

    private static void RunExport(ExportType type)
    {
        try
        {
            PerformExport(type);

            // Non-Meta exports may have required asynchronous removal,
            // and therefore run without -quit in batch mode.
            if (type != ExportType.Meta && Application.isBatchMode)
                EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            if (type != ExportType.Meta && Application.isBatchMode)
                EditorApplication.Exit(1);
            else
                throw;
        }
    }

    // ---------------------------------------------------------
    // XR configuration
    //
    // Uses reflection so the script still compiles before
    // XR Plug-in Management has been installed.
    // ---------------------------------------------------------

    private static Type FindType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName, false);
            if (type != null)
                return type;
        }

        return null;
    }

    private static void ConfigureAndroidXR(bool meta)
    {
        if (!meta && !HasPackage("com.unity.xr.management"))
            return;

        if (meta)
            VerifyMetaPackages();

        Type perTargetType = FindType(
            "UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget");

        Type generalType = FindType(
            "UnityEngine.XR.Management.XRGeneralSettings");

        Type metadataType = FindType(
            "UnityEditor.XR.Management.Metadata.XRPackageMetadataStore");

        if (perTargetType == null ||
            generalType == null ||
            metadataType == null)
        {
            throw new Exception(
                "XR Management assemblies are not loaded. " +
                "Restart Unity after installing the XR packages.");
        }

        const BindingFlags staticFlags =
            BindingFlags.Public | BindingFlags.Static;

        const BindingFlags instanceFlags =
            BindingFlags.Public | BindingFlags.Instance;

        // Look up the XR settings container registered with
        // Unity's build settings.

        const string settingsKey =
            "com.unity.xr.management.loader_settings";

        UnityEngine.Object containerObject;

        bool found = EditorBuildSettings.TryGetConfigObject(
            settingsKey, out containerObject);

        if (!found || containerObject == null)
        {
            if (!meta)
                return;

            // First-time XR configuration: create the settings asset.
            if (!AssetDatabase.IsValidFolder("Assets/XR"))
                AssetDatabase.CreateFolder("Assets", "XR");

            containerObject =
                ScriptableObject.CreateInstance(perTargetType);

            AssetDatabase.CreateAsset(
                containerObject,
                "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");

            EditorBuildSettings.AddConfigObject(
                settingsKey, containerObject, true);
        }

        if (!perTargetType.IsInstanceOfType(containerObject))
        {
            throw new Exception(
                "Unexpected XR settings container type.");
        }

        MethodInfo getSettings = perTargetType.GetMethod(
            "SettingsForBuildTarget", instanceFlags);

        object generalSettings = getSettings.Invoke(
            containerObject,
            new object[] { BuildTargetGroup.Android });

        if (generalSettings == null && meta)
        {
            perTargetType.GetMethod(
                "CreateDefaultSettingsForBuildTarget",
                instanceFlags).Invoke(
                    containerObject,
                    new object[] { BuildTargetGroup.Android });

            generalSettings = getSettings.Invoke(
                containerObject,
                new object[] { BuildTargetGroup.Android });
        }

        if (generalSettings == null)
            return;

        object manager = generalType.GetProperty(
            "Manager", instanceFlags).GetValue(generalSettings);

        if (manager == null && meta)
        {
            perTargetType.GetMethod(
                "CreateDefaultManagerSettingsForBuildTarget",
                instanceFlags).Invoke(
                    containerObject,
                    new object[] { BuildTargetGroup.Android });

            generalSettings = getSettings.Invoke(
                containerObject,
                new object[] { BuildTargetGroup.Android });

            manager = generalType.GetProperty(
                "Manager", instanceFlags).GetValue(generalSettings);
        }

        if (meta)
        {
            if (manager == null)
                throw new Exception(
                    "Unable to create Android XR Manager Settings.");

            // Enable OpenXR as the Android XR provider.

            bool assigned = (bool)metadataType.GetMethod(
                "IsLoaderAssigned", staticFlags).Invoke(
                    null,
                    new object[]
                    {
                        OpenXRLoaderName,
                        BuildTargetGroup.Android
                    });

            if (!assigned)
            {
                bool success = (bool)metadataType.GetMethod(
                    "AssignLoader", staticFlags).Invoke(
                        null,
                        new object[]
                        {
                            manager,
                            OpenXRLoaderName,
                            BuildTargetGroup.Android
                        });

                if (!success)
                    throw new Exception(
                        "Failed to enable OpenXR for Android.");
            }
        }

        // Set "Initialize XR on Startup" for Android.
        // Disable it for ordinary Android phone exports.

        var serialized = new SerializedObject(
            (UnityEngine.Object)generalSettings);

        SerializedProperty initOnStart =
            serialized.FindProperty("m_InitManagerOnStart");

        if (initOnStart == null)
            throw new Exception(
                "XR Initialize on Startup setting not found.");

        initOnStart.boolValue = meta;
        serialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(
            (UnityEngine.Object)generalSettings);

        if (manager is UnityEngine.Object managerObject)
            EditorUtility.SetDirty(managerObject);

        EditorUtility.SetDirty(containerObject);

        AssetDatabase.SaveAssets();

        Debug.Log(
            $"Android XR configured. " +
            $"Initialize on startup: {meta}, " +
            $"OpenXR requested: {meta}");
    }

    // ---------------------------------------------------------
    // Build/export
    // ---------------------------------------------------------

    // Ensure every export uses exactly one Scene in both the
    // active Build Profile scene list and the open Editor scene setup.
    private static void ConfigureExportScene(string scenePath)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            throw new Exception($"Scene not found: {scenePath}");

        // In Unity 6, EditorBuildSettings.scenes refers to the active
        // Build Profile's scene list (or the global list if not overridden).
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(scenePath, true)
        };

        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
        if (buildScenes.Length != 1 ||
            !buildScenes[0].enabled ||
            buildScenes[0].path != scenePath)
        {
            throw new Exception(
                $"Failed to set single build scene: {scenePath}");
        }

        // Editor scene activation is an Edit-mode operation.
        // Configure exactly one open, loaded, active scene through
        // EditorSceneManager rather than SceneManager.SetActiveScene().
        EditorSceneManager.RestoreSceneManagerSetup(new[]
        {
            new SceneSetup
            {
                path = scenePath,
                isLoaded = true,
                isActive = true
            }
        });

        SceneSetup[] openedScenes = EditorSceneManager.GetSceneManagerSetup();
        if (openedScenes.Length != 1 ||
            openedScenes[0].path != scenePath ||
            !openedScenes[0].isLoaded ||
            !openedScenes[0].isActive)
        {
            throw new Exception(
                $"Failed to set one active Editor scene: {scenePath}. " +
                $"Editor scene count: {openedScenes.Length}");
        }

        Debug.Log($"Scene list: 1 enabled scene ({scenePath})");
        Debug.Log($"Open/active Editor scene: {openedScenes[0].path}");
    }

    private static void PerformExport(ExportType type)
    {
        BuildTarget target = GetBuildTarget(type);

        string projectPath =
            Directory.GetParent(Application.dataPath)!.FullName;

        string outputDirectory;
        string scenePath = ScenePath;

        BuildOptions buildOptions = BuildOptions.None;

        switch (type)
        {
            case ExportType.Android:
                outputDirectory = "../P2PSpinningCubesDemoAndroid";
                EditorUserBuildSettings.exportAsGoogleAndroidProject = true;
                ConfigureAndroidXR(false);
                break;

            case ExportType.Meta:
                outputDirectory = "../P2PSpinningCubesDemoMeta";
                scenePath = MetaScenePath;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = true;

                ConfigureAndroidXR(true);

                // Standard Quest Android requirements.
                PlayerSettings.SetScriptingBackend(
                    UnityEditor.Build.NamedBuildTarget.Android,
                    ScriptingImplementation.IL2CPP);

                PlayerSettings.Android.targetArchitectures =
                    AndroidArchitecture.ARM64;

                break;

            case ExportType.IOS:
                outputDirectory = "P2PSpinningCubesDemoIOS";

                CanAppendBuild canAppend =
                    BuildPipeline.BuildCanBeAppended(
                        target,
                        Path.Combine(projectPath, outputDirectory));

                if (canAppend == CanAppendBuild.Yes)
                {
                    buildOptions |=
                        BuildOptions.AcceptExternalModificationsToPlayer;
                }
                break;

            case ExportType.OSX:
                outputDirectory = "P2PSpinningCubesDemoOSX";
                break;

            default:
                throw new Exception(
                    $"Unsupported export type: {type}");
        }

        string outputPath =
            Path.GetFullPath(
                Path.Combine(projectPath, outputDirectory));

        Debug.Log($"Exporting {type} to: {outputPath}");
        Debug.Log($"Scene: {scenePath}");

        // Make the Editor scene list, the open scene, and the active
        // scene agree before compiling the Player for every platform.
        ConfigureExportScene(scenePath);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            // Also explicitly enforce the single scene in the Player build.
            scenes = new[] { scenePath },
            locationPathName = outputPath,
            target = target,
            options = buildOptions
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result != BuildResult.Succeeded ||
            report.summary.totalErrors > 0)
        {
            throw new Exception(
                $"{type} export failed. " +
                $"Result: {report.summary.result}, " +
                $"Warnings: {report.summary.totalWarnings}, " +
                $"Errors: {report.summary.totalErrors}");
        }

        if (type == ExportType.Meta)
            FixMetaOpenXRPackaging(outputPath);

        Debug.Log(
            $"{type} export finished successfully. " +
            $"Warnings: {report.summary.totalWarnings}, " +
            $"Errors: {report.summary.totalErrors}");
    }

    private static void FixMetaOpenXRPackaging(string outputPath)
    {
        string gradlePath = Path.Combine(
            outputPath, "launcher", "build.gradle");

        if (!File.Exists(gradlePath))
            throw new Exception(
                "Meta launcher Gradle file not found: " + gradlePath);

        string contents = File.ReadAllText(gradlePath);

        const string rule =
            "pickFirsts += ['**/libopenxr_loader.so']";

        if (contents.Contains(rule))
            return;

        // Gradle allows multiple android { } blocks.
        string addition = @"

    android {
        packaging {
            jniLibs {
                pickFirsts += ['**/libopenxr_loader.so']
            }
        }
    }
    ";

        File.AppendAllText(gradlePath, addition);

        Debug.Log("Added Meta OpenXR packaging fix: " + gradlePath);
    }

}
