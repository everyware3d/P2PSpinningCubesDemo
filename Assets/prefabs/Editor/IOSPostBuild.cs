#if UNITY_EDITOR && UNITY_IOS

using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
using UnityEngine;

using System;
using System.IO;
using System.Linq;
using System.Xml;

public static class IOSPostBuild
{
    // IMPORTANT:
    // Set the environment variable APPLE_DEVELOPER_TEAM_ID to your 
    // Apple Developer Team ID in your Unity Editor's environment.
    private static readonly string DeveloperTeamId =
        Environment.GetEnvironmentVariable("APPLE_DEVELOPER_TEAM_ID");

    private const string VisionFrameworkName = "DPCoreBundleVision.xcframework";
    private const string IOSFrameworkName    = "DPCoreBundleIOS.xcframework";

    // Run fairly late so this happens after Unity has generated the Xcode project.
    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS)
            return;

        UpdateXcodeProject(path);
        UpdateXcodeScheme(path);
    }

    private static void UpdateXcodeProject(string buildPath)
    {
        string projectPath = PBXProject.GetPBXProjectPath(buildPath);

        PBXProject project = new PBXProject();
        project.ReadFromFile(projectPath);

        string mainTargetGuid = project.GetUnityMainTargetGuid();
        string unityFrameworkTargetGuid = project.GetUnityFrameworkTargetGuid();

        //
        // 1. Set signing team
        //
        SetSigningTeam(
            project,
            mainTargetGuid,
            unityFrameworkTargetGuid);

        //
        // 2. Remove DPCoreBundleVision.framework
        //
        RemoveFramework(
            project,
            buildPath,
            mainTargetGuid,
            unityFrameworkTargetGuid,
            VisionFrameworkName);

        //
        // 3. Add + embed DPCoreBundleIOS.framework
        //
        AddIOSFramework(
            project,
            buildPath,
            mainTargetGuid,
            unityFrameworkTargetGuid);

        project.WriteToFile(projectPath);

        Debug.Log("IOSPostBuild: Updated Xcode project.");
    }


    private static void UpdateXcodeScheme(string buildPath)
    {
        string projectPath = PBXProject.GetPBXProjectPath(buildPath);
        string projectDirectory = Path.GetDirectoryName(projectPath);

        if (string.IsNullOrEmpty(projectDirectory))
        {
            Debug.LogWarning(
                $"IOSPostBuild: Could not determine Xcode project directory from:\n{projectPath}");
            return;
        }

        string schemeDirectory = Path.Combine(
            projectDirectory,
            "xcshareddata",
            "xcschemes");

        if (!Directory.Exists(schemeDirectory))
        {
            Debug.LogWarning(
                $"IOSPostBuild: Xcode scheme directory not found:\n{schemeDirectory}");
            return;
        }

        string[] schemeFiles = Directory.GetFiles(
            schemeDirectory,
            "*.xcscheme",
            SearchOption.TopDirectoryOnly);

        if (schemeFiles.Length == 0)
        {
            Debug.LogWarning(
                $"IOSPostBuild: No shared Xcode schemes found in:\n{schemeDirectory}");
            return;
        }

        foreach (string schemePath in schemeFiles)
        {
            XmlDocument document = new XmlDocument();
            document.PreserveWhitespace = true;
            document.Load(schemePath);

            XmlNode launchAction =
                document.SelectSingleNode("/Scheme/LaunchAction");

            if (launchAction == null)
                continue;

            XmlNode environmentVariables =
                launchAction.SelectSingleNode("EnvironmentVariables");

            if (environmentVariables == null)
            {
                environmentVariables =
                    document.CreateElement("EnvironmentVariables");

                launchAction.AppendChild(environmentVariables);
            }

            XmlElement existingVariable = null;

            foreach (XmlNode node in environmentVariables.ChildNodes)
            {
                if (node is XmlElement element &&
                    element.Name == "EnvironmentVariable" &&
                    element.GetAttribute("key") == "IDELogRedirectionPolicy")
                {
                    existingVariable = element;
                    break;
                }
            }

            if (existingVariable == null)
            {
                existingVariable =
                    document.CreateElement("EnvironmentVariable");

                environmentVariables.AppendChild(existingVariable);
            }

            existingVariable.SetAttribute(
                "key",
                "IDELogRedirectionPolicy");

            existingVariable.SetAttribute(
                "value",
                "stdioToOSLog");

            existingVariable.SetAttribute(
                "isEnabled",
                "YES");

            document.Save(schemePath);

            Debug.Log(
                $"IOSPostBuild: Set IDELogRedirectionPolicy=stdioToOSLog in {Path.GetFileName(schemePath)}.");
        }
    }

    private static void SetSigningTeam(
        PBXProject project,
        string mainTargetGuid,
        string unityFrameworkTargetGuid)
    {
        if (string.IsNullOrEmpty(DeveloperTeamId) ||
            DeveloperTeamId == "YOUR_TEAM_ID")
        {
            Debug.LogWarning(
                "IOSPostBuild: DeveloperTeamId has not been configured.");
            return;
        }

        Debug.Log(
            $"IOSPostBuild: mainTargetGuid={mainTargetGuid}, " +
            $"unityFrameworkTargetGuid={unityFrameworkTargetGuid}");

        // Do not call PBXProject.SetTeamId() here. In some Unity-generated
        // projects it can throw an ArgumentException while
        // constructing its internal target/configuration dictionary.
        // DEVELOPMENT_TEAM is just an Xcode build setting, so setting it
        // directly is sufficient and avoids that Unity Xcode API issue.
        foreach (string targetGuid in GetUniqueTargetGuids(
                     mainTargetGuid,
                     unityFrameworkTargetGuid))
        {
            project.SetBuildProperty(
                targetGuid,
                "DEVELOPMENT_TEAM",
                DeveloperTeamId);

            project.SetBuildProperty(
                targetGuid,
                "CODE_SIGN_STYLE",
                "Automatic");
        }

        Debug.Log(
            $"IOSPostBuild: Set development team to {DeveloperTeamId}.");
    }

    private static void AddIOSFramework(
        PBXProject project,
        string buildPath,
        string mainTargetGuid,
        string unityFrameworkTargetGuid)
    {
        string frameworkPath =
            FindFramework(buildPath, IOSFrameworkName);

        if (frameworkPath == null)
        {
            Debug.LogError(
                $"IOSPostBuild: Could not find {IOSFrameworkName} " +
                $"anywhere underneath:\n{buildPath}");

            return;
        }

        string projectRelativePath =
            GetRelativePath(buildPath, frameworkPath);

        // Xcode wants forward slashes in project paths.
        projectRelativePath =
            projectRelativePath.Replace("\\", "/");

        string fileGuid =
            project.FindFileGuidByProjectPath(projectRelativePath);

        if (string.IsNullOrEmpty(fileGuid))
        {
            fileGuid = project.AddFile(
                frameworkPath,
                projectRelativePath,
                PBXSourceTree.Source);

            Debug.Log(
                $"IOSPostBuild: Added {IOSFrameworkName} " +
                $"at {projectRelativePath}.");
        }

        /*
         * Native plugin symbols are generally needed by UnityFramework,
         * so link the framework there.
         */
        if (!string.IsNullOrEmpty(unityFrameworkTargetGuid))
        {
            project.AddFileToBuild(
                unityFrameworkTargetGuid,
                fileGuid);
        }

        /*
         * Embed it in the application target.
         *
         * AddFileToEmbedFrameworks also adds the framework to that
         * target's linked frameworks.
         */
        project.AddFileToEmbedFrameworks(
            mainTargetGuid,
            fileGuid);

        Debug.Log(
            $"IOSPostBuild: Linked and embedded {IOSFrameworkName}.");
    }

    private static void RemoveFramework(
        PBXProject project,
        string buildPath,
        string mainTargetGuid,
        string unityFrameworkTargetGuid,
        string frameworkName)
    {
        string frameworkPath =
            FindFramework(buildPath, frameworkName);

        /*
         * We know where Unity normally copies your existing iOS plugin,
         * so also check the expected project path even if the directory
         * can't be found on disk.
         */
        string[] candidateProjectPaths =
        {
            $"Frameworks/P2PPlugin/Plugins/iOS/{frameworkName}",
            $"Frameworks/Plugins/iOS/{frameworkName}",
            $"Frameworks/{frameworkName}"
        };

        // If it exists physically, its actual project-relative path
        // is our best candidate.
        if (frameworkPath != null)
        {
            string relative =
                GetRelativePath(buildPath, frameworkPath)
                    .Replace("\\", "/");

            candidateProjectPaths =
                new[] { relative }
                .Concat(candidateProjectPaths)
                .Distinct()
                .ToArray();
        }

        foreach (string projectPath in candidateProjectPaths)
        {
            string fileGuid =
                project.FindFileGuidByProjectPath(projectPath);

            if (string.IsNullOrEmpty(fileGuid))
                continue;

            // Remove from every unique target it might have been linked into.
            foreach (string targetGuid in GetUniqueTargetGuids(
                         mainTargetGuid,
                         unityFrameworkTargetGuid))
            {
                project.RemoveFileFromBuild(
                    targetGuid,
                    fileGuid);
            }

            // Remove the PBX file reference entirely.
            project.RemoveFile(fileGuid);

            Debug.Log(
                $"IOSPostBuild: Removed {frameworkName} " +
                $"from Xcode project ({projectPath}).");
        }
    }


    private static string[] GetUniqueTargetGuids(params string[] targetGuids)
    {
        return targetGuids
            .Where(guid => !string.IsNullOrEmpty(guid))
            .Distinct()
            .ToArray();
    }

    private static string FindFramework(
        string root,
        string frameworkName)
    {
        try
        {
            string[] matches =
                Directory.GetDirectories(
                    root,
                    frameworkName,
                    SearchOption.AllDirectories);

            if (matches.Length == 0)
                return null;

            if (matches.Length > 1)
            {
                Debug.LogWarning(
                    $"IOSPostBuild: Found multiple copies of " +
                    $"{frameworkName}. Using:\n{matches[0]}");
            }

            return matches[0];
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                $"IOSPostBuild: Error searching for " +
                $"{frameworkName}: {e.Message}");

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