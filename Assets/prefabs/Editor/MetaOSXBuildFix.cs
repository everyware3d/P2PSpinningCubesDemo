#if UNITY_EDITOR && UNITY_STANDALONE_OSX

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/* This is only needed for exporting the OSX build to an XCode project. 
   The Meta MRUK package has a dependency on the Oculus package, 
   which is not compatible with macOS. This script patches the asmdef
   files in the Meta MRUK package to exclude macOS from the build. */

public static class MetaOSXBuildFix
{
    private const string ExcludedPlatform = "macOSStandalone";

    [MenuItem("Tools/P2P/Exclude Meta MRUK From macOS")]
    public static void Apply()
    {
        int changed = 0;

        changed += PatchMatchingFile(
            "Library/PackageCache",
            "meta.xr.mrutilitykit.asmdef");

        changed += PatchMatchingFile(
            "Library/PackageCache",
            "Meta.XR.BuildingBlocks.AIBlocks.asmdef");

        changed += PatchMatchingFile(
            "Library/PackageCache",
            "Meta.XR.MultiplayerBlocks.Shared.asmdef");

        AssetDatabase.Refresh();

        Debug.Log(
            $"MetaOSXBuildFix: Patched {changed} asmdef file(s) " +
            $"to exclude {ExcludedPlatform}.");
    }

    private static int PatchMatchingFile(
        string root,
        string fileName)
    {
        string[] matches =
            Directory.GetFiles(
                root,
                fileName,
                SearchOption.AllDirectories);

        int changed = 0;

        foreach (string path in matches)
        {
            string json = File.ReadAllText(path);

            const string emptyExclude =
                "\"excludePlatforms\": []";

            if (!json.Contains(emptyExclude))
            {
                Debug.LogWarning(
                    $"MetaOSXBuildFix: {path} does not have an empty " +
                    "excludePlatforms array. Leaving unchanged.");

                continue;
            }

            json = json.Replace(
                emptyExclude,
                "\"excludePlatforms\": [\n" +
                "        \"macOSStandalone\"\n" +
                "    ]");

            File.WriteAllText(
                path,
                json);

            Debug.Log(
                $"MetaOSXBuildFix: Excluded macOS from {path}");

            changed++;
        }

        return changed;
    }
}

#endif
