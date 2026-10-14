using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CommandLineBuild
{
    private const string ScenePath =
        "Assets/Scenes/P2PSpinningCubesDemo.unity";

    private const string PendingBuildKey =
        "CommandLineBuild.PendingTarget";

    public static void ExportAndroid()
    {
        StartExport(BuildTargetGroup.Android, BuildTarget.Android);
    }

    public static void ExportIOS()
    {
        StartExport(BuildTargetGroup.iOS, BuildTarget.iOS);
    }

    public static void ExportOSX()
    {
        StartExport(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);
    }

    private static void StartExport(
        BuildTargetGroup targetGroup,
        BuildTarget target)
    {
        Debug.Log($"Starting export for {target}");

        // If we're already on the correct platform, just build.
        if (EditorUserBuildSettings.activeBuildTarget == target)
        {
            PerformExport(target);
            return;
        }

        Debug.Log(
            $"Switching build target from " +
            $"{EditorUserBuildSettings.activeBuildTarget} to {target}");

        // This survives the domain reload.
        SessionState.SetString(
            PendingBuildKey,
            target.ToString());

        bool switched =
            EditorUserBuildSettings.SwitchActiveBuildTarget(
                targetGroup,
                target);

        if (!switched)
        {
            SessionState.EraseString(PendingBuildKey);

            throw new Exception(
                $"Failed to switch build target to {target}.");
        }

        // DO NOT try to continue the build here.
        // A domain reload may occur.
    }

    [InitializeOnLoadMethod]
    private static void ContinuePendingBuild()
    {
        string pending =
            SessionState.GetString(PendingBuildKey, "");

        if (string.IsNullOrEmpty(pending))
            return;

        if (!Enum.TryParse(pending, out BuildTarget target))
        {
            SessionState.EraseString(PendingBuildKey);
            return;
        }

        // Unity may still be finishing compilation/import.
        EditorApplication.delayCall += () =>
        {
            ContinueWhenReady(target);
        };
    }

    private static void ContinueWhenReady(BuildTarget target)
    {
        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += () =>
            {
                ContinueWhenReady(target);
            };

            return;
        }

        if (EditorUserBuildSettings.activeBuildTarget != target)
        {
            Debug.Log(
                $"Waiting for {target}. Current target is " +
                $"{EditorUserBuildSettings.activeBuildTarget}");

            EditorApplication.delayCall += () =>
            {
                ContinueWhenReady(target);
            };

            return;
        }

        SessionState.EraseString(PendingBuildKey);

        Debug.Log(
            $"Target switch to {target} completed. Continuing build.");

        PerformExport(target);
    }

    private static void PerformExport(BuildTarget target)
    {
        string projectPath =
            Directory.GetParent(Application.dataPath)!.FullName;

        string outputDirectory;
        BuildOptions buildOptions = BuildOptions.None;

        switch (target)
        {
            case BuildTarget.Android:
                outputDirectory = "../P2PSpinningCubesDemoAndroid";
                EditorUserBuildSettings.exportAsGoogleAndroidProject = true;
                break;

            case BuildTarget.iOS:
                outputDirectory = "P2PSpinningCubesDemoIOS";
                CanAppendBuild canAppend =
                    BuildPipeline.BuildCanBeAppended(target, Path.Combine(projectPath, outputDirectory));
                if (canAppend == CanAppendBuild.Yes)
                {
                    buildOptions |= BuildOptions.AcceptExternalModificationsToPlayer;
                }
                break;

            case BuildTarget.StandaloneOSX:
                outputDirectory = "P2PSpinningCubesDemoOSX";
                break;

            default:
                throw new Exception(
                    $"Unsupported build target: {target}");
        }
        string outputPath =
            Path.Combine(projectPath, outputDirectory);


        Debug.Log($"Exporting {target} to: {outputPath}");

        EditorSceneManager.OpenScene(
            ScenePath,
            OpenSceneMode.Single);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = outputPath,
            target = target,
            options = buildOptions
        };

        BuildReport report =
            BuildPipeline.BuildPlayer(options);

        Debug.Log(
            $"{target} export finished. " +
            $"Result: {report.summary.result}, " +
            $"Warnings: {report.summary.totalWarnings}, " +
            $"Errors: {report.summary.totalErrors}");

        if (report.summary.result != BuildResult.Succeeded ||
            report.summary.totalErrors > 0)
        {
            throw new Exception(
                $"{target} export failed. " +
                $"Result: {report.summary.result}, " +
                $"Warnings: {report.summary.totalWarnings}, " +
                $"Errors: {report.summary.totalErrors}");
        }
        Debug.Log($"{target} export completed successfully.");
    }
}