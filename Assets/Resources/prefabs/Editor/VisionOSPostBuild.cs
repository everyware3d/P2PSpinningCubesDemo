#if UNITY_EDITOR && UNITY_VISIONOS

#define WRITE_MUTLICAST_ENTITLEMENT

using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
using UnityEngine;

using System;
using System.IO;
using System.Linq;
using System.Xml;

public static class VisionOSPostBuild
{
    // IMPORTANT:
    // Set the environment variable APPLE_DEVELOPER_TEAM_ID to your
    // Apple Developer Team ID in your Unity Editor's environment.
    private static readonly string DeveloperTeamId =
        Environment.GetEnvironmentVariable("APPLE_DEVELOPER_TEAM_ID");

    private const string VisionFrameworkName = "DPCoreBundleVision.xcframework";
    private const string IOSFrameworkName    = "DPCoreBundleIOS.xcframework";

    private const string EntitlementsFileName = "Unity-VisionOS.entitlements";

    // Run fairly late so this happens after Unity has generated the Xcode project.
    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.VisionOS)
            return;

        UpdateInfoPlist(path);
        UpdateXcodeProject(path);
        UpdateXcodeScheme(path);
    }

    private static void UpdateInfoPlist(string buildPath)
    {
        string plistPath = Path.Combine(buildPath, "Info.plist");

        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        plist.root.SetString(
            "NSHandsTrackingUsageDescription",
            "Hand tracking is used to interact with virtual objects.");

        plist.root.SetString(
            "NSWorldSensingUsageDescription",
            "World sensing is used to place and interact with virtual objects in your environment.");

        plist.root.SetString(
            "NSLocalNetworkUsageDescription",
            "Local network access is used to discover and communicate with nearby devices.");

        File.WriteAllText(
            plistPath,
            plist.WriteToString());

        Debug.Log(
            "VisionOSPostBuild: Updated Info.plist.");
    }

    private static void UpdateXcodeProject(string buildPath)
    {
        string projectPath =
            PBXProject.GetPBXProjectPath(buildPath);

        PBXProject project =
            new PBXProject();

        project.ReadFromFile(projectPath);

        string mainTargetGuid =
            project.GetUnityMainTargetGuid();

        string unityFrameworkTargetGuid =
            project.GetUnityFrameworkTargetGuid();

        //
        // 1. Set signing team
        //
        SetSigningTeam(
            project,
            mainTargetGuid,
            unityFrameworkTargetGuid);

        //
        // 2. Configure multicast entitlement
        //
#if WRITE_MUTLICAST_ENTITLEMENT
        UpdateEntitlements(
            project,
            buildPath,
            mainTargetGuid);
#endif
        //
        // 3. Remove DPCoreBundleIOS.framework
        //
        RemoveFramework(
            project,
            buildPath,
            mainTargetGuid,
            unityFrameworkTargetGuid,
            IOSFrameworkName);

        //
        // 4. Link the XCFramework in UnityFramework only.
        //
        AddVisionFramework(
            project,
            buildPath,
            mainTargetGuid,
            unityFrameworkTargetGuid);

        // 5. Embed the already-processed dynamic .framework in the app.
        // Do NOT add the .xcframework to the app target's Frameworks phase:
        // Xcode may otherwise run ProcessXCFramework twice for one output.
        AddVisionFrameworkEmbeddingPhase(project, mainTargetGuid);

        project.WriteToFile(projectPath);

        Debug.Log(
            "VisionOSPostBuild: Updated Xcode project.");
    }

    private static void UpdateEntitlements(
        PBXProject project,
        string buildPath,
        string mainTargetGuid)
    {
        string entitlementsPath =
            Path.Combine(
                buildPath,
                EntitlementsFileName);

        PlistDocument entitlements =
            new PlistDocument();

        if (File.Exists(entitlementsPath))
        {
            entitlements.ReadFromFile(
                entitlementsPath);
        }

        //
        // Allow multicast / UDP broadcast networking.
        //
        entitlements.root.SetBoolean(
            "com.apple.developer.networking.multicast",
            true);

        File.WriteAllText(
            entitlementsPath,
            entitlements.WriteToString());

        //
        // Make sure the entitlements file is part of the Xcode project.
        //
        string fileGuid =
            project.FindFileGuidByProjectPath(
                EntitlementsFileName);

        if (string.IsNullOrEmpty(fileGuid))
        {
            project.AddFile(
                entitlementsPath,
                EntitlementsFileName,
                PBXSourceTree.Source);
        }

        //
        // Tell Xcode to use this entitlement file when signing
        // the main application target.
        //
        project.SetBuildProperty(
            mainTargetGuid,
            "CODE_SIGN_ENTITLEMENTS",
            EntitlementsFileName);

        Debug.Log(
            $"VisionOSPostBuild: Added multicast entitlement " +
            $"and configured CODE_SIGN_ENTITLEMENTS={EntitlementsFileName}.");
    }

    private static void UpdateXcodeScheme(string buildPath)
    {
        string projectPath =
            PBXProject.GetPBXProjectPath(buildPath);

        string projectDirectory =
            Path.GetDirectoryName(projectPath);

        if (string.IsNullOrEmpty(projectDirectory))
        {
            Debug.LogWarning(
                $"VisionOSPostBuild: Could not determine Xcode project directory from:\n{projectPath}");

            return;
        }

        string schemeDirectory =
            Path.Combine(
                projectDirectory,
                "xcshareddata",
                "xcschemes");

        if (!Directory.Exists(schemeDirectory))
        {
            Debug.LogWarning(
                $"VisionOSPostBuild: Xcode scheme directory not found:\n{schemeDirectory}");

            return;
        }

        string[] schemeFiles =
            Directory.GetFiles(
                schemeDirectory,
                "*.xcscheme",
                SearchOption.TopDirectoryOnly);

        if (schemeFiles.Length == 0)
        {
            Debug.LogWarning(
                $"VisionOSPostBuild: No shared Xcode schemes found in:\n{schemeDirectory}");

            return;
        }

        foreach (string schemePath in schemeFiles)
        {
            //
            // Turn off:
            //
            // Scheme > Run > Info > Debug executable
            //
            // This launches the app from Xcode without attaching LLDB.
            //
            XcScheme scheme =
                new XcScheme();

            scheme.ReadFromFile(
                schemePath);

            scheme.SetDebugExecutable(
                false);

            scheme.WriteToFile(
                schemePath);

            //
            // Add:
            //
            // IDELogRedirectionPolicy=stdioToOSLog
            //
            XmlDocument document =
                new XmlDocument();

            document.PreserveWhitespace =
                true;

            document.Load(
                schemePath);

            XmlNode launchAction =
                document.SelectSingleNode(
                    "/Scheme/LaunchAction");

            if (launchAction == null)
                continue;

            XmlNode environmentVariables =
                launchAction.SelectSingleNode(
                    "EnvironmentVariables");

            if (environmentVariables == null)
            {
                environmentVariables =
                    document.CreateElement(
                        "EnvironmentVariables");

                launchAction.AppendChild(
                    environmentVariables);
            }

            XmlElement existingVariable =
                null;

            foreach (XmlNode node in environmentVariables.ChildNodes)
            {
                if (node is XmlElement element &&
                    element.Name == "EnvironmentVariable" &&
                    element.GetAttribute("key") ==
                    "IDELogRedirectionPolicy")
                {
                    existingVariable =
                        element;

                    break;
                }
            }

            if (existingVariable == null)
            {
                existingVariable =
                    document.CreateElement(
                        "EnvironmentVariable");

                environmentVariables.AppendChild(
                    existingVariable);
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

            document.Save(
                schemePath);

            Debug.Log(
                $"VisionOSPostBuild: Disabled Debug executable and set " +
                $"IDELogRedirectionPolicy=stdioToOSLog in " +
                $"{Path.GetFileName(schemePath)}.");
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
                "VisionOSPostBuild: DeveloperTeamId has not been configured.");

            return;
        }

        Debug.Log(
            $"VisionOSPostBuild: mainTargetGuid={mainTargetGuid}, " +
            $"unityFrameworkTargetGuid={unityFrameworkTargetGuid}");

        //
        // Do not call PBXProject.SetTeamId() here.
        //
        // In some Unity/visionOS generated projects it can throw an
        // ArgumentException while constructing its internal
        // target/configuration dictionary.
        //
        // DEVELOPMENT_TEAM is just an Xcode build setting, so setting it
        // directly is sufficient and avoids that Unity Xcode API issue.
        //
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
            $"VisionOSPostBuild: Set development team to {DeveloperTeamId}.");
    }

    // Unity sometimes leaves an XCFramework reference in the generated Xcode
    // project without copying the actual directory (or an incremental export
    // can preserve an old reference). Ensure the physical asset is present.
    private const string VisionFrameworkXcodePath =
        "Frameworks/P2PPlugin/Plugins/visionOS/DPCoreBundleVision.xcframework";

    private static string EnsureVisionFrameworkCopied(string buildPath)
    {
        string destination = Path.Combine(
            buildPath,
            VisionFrameworkXcodePath.Replace('/', Path.DirectorySeparatorChar));

        // An XCFramework is a directory bundle, not a single binary file.
        if (File.Exists(Path.Combine(destination, "Info.plist")))
            return destination;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string packageRoot = Path.Combine(
            projectRoot, "Packages", "com.everyware3d.p2pplugin");

        // Search the embedded plugin package first, then Assets in case the
        // framework was imported directly into this project.
        string source = FindFramework(packageRoot, VisionFrameworkName);
        if (source == null)
            source = FindFramework(Application.dataPath, VisionFrameworkName);

        if (source == null || !File.Exists(Path.Combine(source, "Info.plist")))
            throw new FileNotFoundException(
                $"Could not find a valid {VisionFrameworkName} in " +
                $"{packageRoot} or {Application.dataPath}. " +
                "Check the plugin package and native XCFramework build.");

        if (Directory.Exists(destination))
            Directory.Delete(destination, true);  // Replace incomplete export.

        CopyFrameworkDirectory(source, destination);

        if (!File.Exists(Path.Combine(destination, "Info.plist")))
            throw new IOException(
                $"XCFramework copy failed: {destination}");

        Debug.Log($"VisionOSPostBuild: Copied XCFramework from {source} " +
                  $"to {destination}");
        return destination;
    }

    private static void CopyFrameworkDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (string file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);

        foreach (string subdirectory in Directory.GetDirectories(source))
            CopyFrameworkDirectory(
                subdirectory,
                Path.Combine(destination, Path.GetFileName(subdirectory)));
    }

    private static void AddVisionFramework(
        PBXProject project,
        string buildPath,
        string mainTargetGuid,
        string unityFrameworkTargetGuid)
    {
        // Ensure the complete XCFramework exists in the exported Xcode project.
        string frameworkPath = EnsureVisionFrameworkCopied(buildPath);

        // Unity may use either the assembly/package name or the UPM package
        // name when it creates a project reference.
        string[] candidatePaths =
        {
            VisionFrameworkXcodePath,
            "Frameworks/com.everyware3d.p2pplugin/Plugins/visionOS/" + VisionFrameworkName,
            "Frameworks/Plugins/visionOS/" + VisionFrameworkName,
            "Frameworks/" + VisionFrameworkName
        };

        foreach (string candidate in candidatePaths)
        {
            string existingGuid = project.FindFileGuidByProjectPath(candidate);
            if (!string.IsNullOrEmpty(existingGuid))
            {
                Debug.Log("VisionOSPostBuild: Existing Xcode reference at " +
                          candidate + "; not adding another build phase.");
                return;
            }
        }

        // If Xcode already mentions the framework under an unfamiliar path,
        // avoid creating a second ProcessXCFramework task. That would recreate
        // the previous 'Multiple commands produce' build failure.
        string pbxprojPath = PBXProject.GetPBXProjectPath(buildPath);
        if (File.ReadAllText(pbxprojPath).Contains(VisionFrameworkName))
        {
            Debug.LogWarning(
                "VisionOSPostBuild: The Xcode project already mentions " +
                VisionFrameworkName + " under another path. Skipping a duplicate " +
                "registration. Check its PBXFileReference and file path if " +
                "Xcode still reports a missing framework.");
            return;
        }

        if (string.IsNullOrEmpty(unityFrameworkTargetGuid))
            throw new Exception(
                "Cannot register " + VisionFrameworkName +
                ": UnityFramework target was not found.");

        // Unity omitted the XCFramework entirely. Add a *single* link/build
        // reference to UnityFramework, where the native P/Invoke symbols are
        // needed. Do not also use AddFileToEmbedFrameworks(mainTargetGuid):
        // that would add another ProcessXCFramework task and can collide.
        string fileGuid = project.AddFile(
            frameworkPath,
            VisionFrameworkXcodePath,
            PBXSourceTree.Source);

        project.AddFileToBuild(unityFrameworkTargetGuid, fileGuid);

        Debug.Log(
            "VisionOSPostBuild: Added missing XCFramework reference to " +
            "UnityFramework only: " + VisionFrameworkXcodePath);

    }

    // UnityFramework links DPCoreBundleVision.xcframework. That causes Xcode to
    // select its xros-arm64 (or simulator) slice and place the resulting dynamic
    // framework in BUILT_PRODUCTS_DIR. Copy that *processed framework*, rather
    // than making the main app target process the XCFramework a second time.
    //
    // This is intentionally a shell build phase, NOT AddFileToEmbedFrameworks:
    // the latter also adds a link step to the main target and previously caused
    // the Xcode "Multiple commands produce" failure.
    private static void AddVisionFrameworkEmbeddingPhase(
        PBXProject project,
        string mainTargetGuid)
    {
        if (string.IsNullOrEmpty(mainTargetGuid))
            throw new Exception("visionOS main app target not found.");

        const string script = @"set -eu

SOURCE=""${BUILT_PRODUCTS_DIR}/DPCoreBundleVision.framework""
DEST=""${TARGET_BUILD_DIR}/${FRAMEWORKS_FOLDER_PATH}/DPCoreBundleVision.framework""

if [ ! -f ""${SOURCE}/DPCoreBundleVision"" ]; then
    echo ""error: Built DPCoreBundleVision.framework is missing: ${SOURCE}"" >&2
    echo ""error: Verify the XCFramework is linked to UnityFramework."" >&2
    exit 1
fi

mkdir -p ""$(dirname ""${DEST}"")""
rm -rf ""${DEST}""
/usr/bin/ditto ""${SOURCE}"" ""${DEST}""

if [ ""${CODE_SIGNING_ALLOWED:-NO}"" = ""YES"" ] && \
   [ -n ""${EXPANDED_CODE_SIGN_IDENTITY:-}"" ]; then
    /usr/bin/codesign --force --sign ""${EXPANDED_CODE_SIGN_IDENTITY}"" \
        --timestamp=none ""${DEST}""
fi

echo ""Embedded DPCoreBundleVision.framework at ${DEST}""
";

        // AddShellScriptBuildPhase is idempotent for an identical script/name
        // and adds the phase at the end of the app target's build phases.
        project.AddShellScriptBuildPhase(
            mainTargetGuid,
            "Embed and Sign DPCoreBundleVision",
            "/bin/sh",
            script);

        Debug.Log("VisionOSPostBuild: Added dynamic framework embed/sign phase.");
    }

    private static void RemoveFramework(
        PBXProject project,
        string buildPath,
        string mainTargetGuid,
        string unityFrameworkTargetGuid,
        string frameworkName)
    {
        string frameworkPath =
            FindFramework(
                buildPath,
                frameworkName);

        /*
         * We know where Unity normally copies your existing iOS plugin,
         * so also check the expected project path even if the directory
         * can't be found on disk.
         */
        string[] candidateProjectPaths =
        {
            $"Frameworks/com.everyware3d.p2pplugin/Plugins/iOS/{frameworkName}",
            $"Frameworks/P2PPlugin/Plugins/iOS/{frameworkName}",
            $"Frameworks/Plugins/iOS/{frameworkName}",
            $"Frameworks/{frameworkName}"
        };

        //
        // If it exists physically, its actual project-relative path
        // is our best candidate.
        //
        if (frameworkPath != null)
        {
            string relative =
                GetRelativePath(
                    buildPath,
                    frameworkPath)
                .Replace(
                    "\\",
                    "/");

            candidateProjectPaths =
                new[] { relative }
                .Concat(candidateProjectPaths)
                .Distinct()
                .ToArray();
        }

        foreach (string projectPath in candidateProjectPaths)
        {
            string fileGuid =
                project.FindFileGuidByProjectPath(
                    projectPath);

            if (string.IsNullOrEmpty(fileGuid))
                continue;

            //
            // Remove from every unique target it might have been linked into.
            //
            foreach (string targetGuid in GetUniqueTargetGuids(
                         mainTargetGuid,
                         unityFrameworkTargetGuid))
            {
                project.RemoveFileFromBuild(
                    targetGuid,
                    fileGuid);
            }

            //
            // Remove the PBX file reference entirely.
            //
            project.RemoveFile(
                fileGuid);

            Debug.Log(
                $"VisionOSPostBuild: Removed {frameworkName} " +
                $"from Xcode project ({projectPath}).");
        }
    }

    private static string[] GetUniqueTargetGuids(
        params string[] targetGuids)
    {
        return targetGuids
            .Where(
                guid => !string.IsNullOrEmpty(guid))
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
                    $"VisionOSPostBuild: Found multiple copies of " +
                    $"{frameworkName}. Using:\n{matches[0]}");
            }

            return matches[0];
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                $"VisionOSPostBuild: Error searching for " +
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
            new Uri(
                Path.GetFullPath(fullPath));

        return Uri.UnescapeDataString(
            baseUri.MakeRelativeUri(
                fileUri).ToString());
    }

    private static string AppendDirectorySeparatorChar(
        string path)
    {
        if (!path.EndsWith(
                Path.DirectorySeparatorChar.ToString()))
        {
            return path +
                   Path.DirectorySeparatorChar;
        }

        return path;
    }
}

#endif
