#if UNITY_EDITOR_OSX

using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
using UnityEngine;

using System;
using System.IO;
using System.Linq;

public static class OSXPostBuild
{
    private static readonly string DeveloperTeamId =
        Environment.GetEnvironmentVariable("APPLE_DEVELOPER_TEAM_ID");

    private const string OSXBundleName = "DPCoreBundleOSX.bundle";

    private static readonly string[] MetaXRFileNames =
    {
        "libXrApiLayer_METAX_operator.so",
        "libXrApiLayer_METAX_operator.so.meta"
    };

    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.StandaloneOSX)
            return;

        string pbxProjectPath = FindPBXProjectPath(path);
        if (pbxProjectPath == null)
        {
            Debug.LogWarning(
                $"OSXPostBuild: Could not find an Xcode project underneath:\n{path}");
            return;
        }

        PBXProject project = new PBXProject();
        project.ReadFromFile(pbxProjectPath);

        string mainTargetGuid = FindMainTargetGuid(project);

        if (string.IsNullOrEmpty(mainTargetGuid))
        {
            Debug.LogError(
                $"OSXPostBuild: Could not find the main Xcode target. " +
                $"Expected target name '{PlayerSettings.productName}'.");
            return;
        }

        SetSigningTeam(project, mainTargetGuid);
        AddOSXBundle(project, path, mainTargetGuid);

        RemoveMetaReferencesFromProject(
            project,
            path,
            mainTargetGuid);

        AddMetaCleanupBuildPhase(
            project,
            mainTargetGuid);

        project.WriteToFile(pbxProjectPath);

        RemoveMetaFilesFromExport(path);

        Debug.Log("OSXPostBuild: Updated macOS Xcode project.");
    }

    private static string FindPBXProjectPath(string buildPath)
    {
        try
        {
            string[] projects =
                Directory.GetDirectories(
                    buildPath,
                    "*.xcodeproj",
                    SearchOption.TopDirectoryOnly);

            if (projects.Length == 0)
            {
                projects =
                    Directory.GetDirectories(
                        buildPath,
                        "*.xcodeproj",
                        SearchOption.AllDirectories);
            }

            if (projects.Length == 0)
                return null;

            if (projects.Length > 1)
            {
                Debug.LogWarning(
                    $"OSXPostBuild: Found multiple Xcode projects. Using:\n{projects[0]}");
            }

            string pbxProjectPath =
                Path.Combine(projects[0], "project.pbxproj");

            return File.Exists(pbxProjectPath)
                ? pbxProjectPath
                : null;
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                $"OSXPostBuild: Error locating Xcode project: {e.Message}");
            return null;
        }
    }

    private static string FindMainTargetGuid(PBXProject project)
    {
        string targetGuid =
            project.TargetGuidByName(PlayerSettings.productName);

        if (!string.IsNullOrEmpty(targetGuid))
            return targetGuid;

        string[] candidates =
        {
            Path.GetFileNameWithoutExtension(PlayerSettings.productName),
            "UnityPlayer",
            "Unity"
        };

        foreach (string candidate in candidates)
        {
            targetGuid = project.TargetGuidByName(candidate);
            if (!string.IsNullOrEmpty(targetGuid))
                return targetGuid;
        }

        return null;
    }

    private static void SetSigningTeam(
        PBXProject project,
        string targetGuid)
    {
        if (string.IsNullOrEmpty(DeveloperTeamId))
        {
            Debug.LogWarning(
                "OSXPostBuild: APPLE_DEVELOPER_TEAM_ID is not set. " +
                "Leaving Xcode signing team unchanged.");
            return;
        }

        project.SetBuildProperty(
            targetGuid,
            "DEVELOPMENT_TEAM",
            DeveloperTeamId);

        project.SetBuildProperty(
            targetGuid,
            "CODE_SIGN_STYLE",
            "Automatic");

        Debug.Log(
            "OSXPostBuild: Set DEVELOPMENT_TEAM from APPLE_DEVELOPER_TEAM_ID.");
    }

    private static void AddOSXBundle(
        PBXProject project,
        string buildPath,
        string targetGuid)
    {
        string bundlePath =
            FindDirectory(buildPath, OSXBundleName);

        if (bundlePath == null)
        {
            Debug.LogError(
                $"OSXPostBuild: Could not find {OSXBundleName} anywhere underneath:\n" +
                buildPath);
            return;
        }

        string projectRelativePath =
            GetRelativePath(buildPath, bundlePath)
                .Replace("\\", "/");

        string fileGuid =
            project.FindFileGuidByProjectPath(projectRelativePath);

        if (string.IsNullOrEmpty(fileGuid))
        {
            fileGuid = project.AddFile(
                bundlePath,
                projectRelativePath,
                PBXSourceTree.Source);

            Debug.Log(
                $"OSXPostBuild: Added {OSXBundleName} at {projectRelativePath}.");
        }

        project.AddFileToEmbedFrameworks(
            targetGuid,
            fileGuid);

        Debug.Log(
            $"OSXPostBuild: Added/embedded {OSXBundleName}.");
    }

    private static void RemoveMetaReferencesFromProject(
        PBXProject project,
        string buildPath,
        string targetGuid)
    {
        foreach (string fileName in MetaXRFileNames)
        {
            string[] matches;

            try
            {
                matches =
                    Directory.GetFiles(
                        buildPath,
                        fileName,
                        SearchOption.AllDirectories);
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"OSXPostBuild: Could not search Xcode export for {fileName}: {e.Message}");
                continue;
            }

            foreach (string match in matches)
            {
                string relativePath =
                    GetRelativePath(buildPath, match)
                        .Replace("\\", "/");

                string fileGuid =
                    project.FindFileGuidByProjectPath(relativePath);

                if (string.IsNullOrEmpty(fileGuid))
                    continue;

                project.RemoveFileFromBuild(
                    targetGuid,
                    fileGuid);

                project.RemoveFile(fileGuid);

                Debug.Log(
                    $"OSXPostBuild: Removed Meta XR Xcode reference: {relativePath}");
            }
        }

        string[] operatorDirectories;

        try
        {
            operatorDirectories =
                Directory.GetDirectories(
                    buildPath,
                    "MetaXROperator",
                    SearchOption.AllDirectories);
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                $"OSXPostBuild: Could not search for MetaXROperator directories: {e.Message}");
            return;
        }

        foreach (string operatorDirectory in operatorDirectories)
        {
            string[] files;

            try
            {
                files =
                    Directory.GetFiles(
                        operatorDirectory,
                        "*",
                        SearchOption.AllDirectories);
            }
            catch
            {
                continue;
            }

            foreach (string file in files)
            {
                string relativePath =
                    GetRelativePath(buildPath, file)
                        .Replace("\\", "/");

                string fileGuid =
                    project.FindFileGuidByProjectPath(relativePath);

                if (string.IsNullOrEmpty(fileGuid))
                    continue;

                project.RemoveFileFromBuild(
                    targetGuid,
                    fileGuid);

                project.RemoveFile(fileGuid);

                Debug.Log(
                    $"OSXPostBuild: Removed MetaXROperator Xcode reference: {relativePath}");
            }
        }
    }

    private static void AddMetaCleanupBuildPhase(
        PBXProject project,
        string targetGuid)
    {
        string shellScript = @"
set -e

APP_PATH=""${TARGET_BUILD_DIR}/${WRAPPER_NAME}""

echo ""OSXPostBuild: removing Meta XR Linux/OpenXR operator payloads from ${APP_PATH}""

if [ -d ""${APP_PATH}"" ]; then
    find ""${APP_PATH}"" \
        \( -name 'libXrApiLayer_METAX_operator.so' \
        -o -name 'libXrApiLayer_METAX_operator.so.meta' \
        -o -name 'libXrApiLayer_META*.so' \
        -o -name '*METAX*.so' \) \
        -print -delete || true

    find ""${APP_PATH}"" \
        -type d \
        -name 'MetaXROperator' \
        -print \
        -prune \
        -exec rm -rf '{}' \; || true
fi
";

        project.AddShellScriptBuildPhaseBeforeTargetPostprocess(
            targetGuid,
            "Remove Meta XR Plugins",
            "/bin/sh",
            shellScript);

        Debug.Log(
            "OSXPostBuild: Added Xcode build phase to remove Meta XR native plugins before signing.");
    }

    private static void RemoveMetaFilesFromExport(string buildPath)
    {
        foreach (string fileName in MetaXRFileNames)
        {
            try
            {
                string[] matches =
                    Directory.GetFiles(
                        buildPath,
                        fileName,
                        SearchOption.AllDirectories);

                foreach (string match in matches)
                {
                    File.Delete(match);

                    Debug.Log(
                        $"OSXPostBuild: Deleted Meta XR file from exported project:\n{match}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"OSXPostBuild: Error cleaning {fileName}: {e.Message}");
            }
        }

        try
        {
            string[] operatorDirectories =
                Directory.GetDirectories(
                    buildPath,
                    "MetaXROperator",
                    SearchOption.AllDirectories)
                .OrderByDescending(directory => directory.Length)
                .ToArray();

            foreach (string directory in operatorDirectories)
            {
                if (!Directory.Exists(directory))
                    continue;

                Directory.Delete(
                    directory,
                    true);

                Debug.Log(
                    $"OSXPostBuild: Deleted MetaXROperator directory from exported project:\n{directory}");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                $"OSXPostBuild: Error cleaning MetaXROperator directories: {e.Message}");
        }
    }

    private static string FindDirectory(
        string root,
        string directoryName)
    {
        try
        {
            string[] matches =
                Directory.GetDirectories(
                    root,
                    directoryName,
                    SearchOption.AllDirectories);

            if (matches.Length == 0)
                return null;

            if (matches.Length > 1)
            {
                Debug.LogWarning(
                    $"OSXPostBuild: Found multiple copies of {directoryName}. Using:\n" +
                    matches[0]);
            }

            return matches[0];
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                $"OSXPostBuild: Error searching for {directoryName}: {e.Message}");
            return null;
        }
    }

    private static string GetRelativePath(
        string basePath,
        string fullPath)
    {
        Uri baseUri =
            new Uri(
                AppendDirectorySeparatorChar(
                    Path.GetFullPath(basePath)));

        Uri fileUri =
            new Uri(Path.GetFullPath(fullPath));

        return Uri.UnescapeDataString(
            baseUri.MakeRelativeUri(fileUri).ToString());
    }

    private static string AppendDirectorySeparatorChar(string path)
    {
        if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
            return path + Path.DirectorySeparatorChar;

        return path;
    }
}

#endif
