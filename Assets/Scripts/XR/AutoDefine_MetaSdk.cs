// Assets/Editor/AutoDefine_MetaSdk.cs
#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

[InitializeOnLoad]
public static class AutoDefine_MetaSdk
{
    const string SYMBOL = "USING_META_SDK";
    static ListRequest _list;

    static AutoDefine_MetaSdk()
    {
        // Kick off a non-blocking package list request on editor load
        _list = Client.List(true);
        EditorApplication.update += Poll;
    }

    static void Poll()
    {
        if (_list == null || !_list.IsCompleted) return;
        EditorApplication.update -= Poll;

        bool hasMeta =
            _list.Result.Any(p => p.name.StartsWith("com.meta.xr"));

        SetSymbol(BuildTargetGroup.Android, hasMeta);
        SetSymbol(BuildTargetGroup.Standalone, hasMeta);   // add/remove as you like
        // SetSymbol(BuildTargetGroup.iOS, hasMeta);       // if you build for iOS too
    }

    static void SetSymbol(BuildTargetGroup group, bool enable)
    {
        if (group == BuildTargetGroup.Unknown) return;
        var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group)
            .Split(';')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        bool has = defines.Contains(SYMBOL);
        if (enable && !has) { defines.Add(SYMBOL); }
        if (!enable && has) { defines.Remove(SYMBOL); }

        var joined = string.Join(";", defines);
        PlayerSettings.SetScriptingDefineSymbolsForGroup(group, joined);
    }
}

#endif
