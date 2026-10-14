using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class BuildQuest
{
    [MenuItem("Tools/Minimarket/Build Quest APK")]
    public static void BuildFromMenu()
    {
        Build();
    }

    static void Build()
    {
        string[] scenes = { "Assets/Scenes/MinimarketXR_Full.unity" };
        string outputPath = "Builds/Quest/MiniMarketVR.apk";

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };

        // Asegurar IL2CPP + ARM64 para Quest.
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        var report = BuildPipeline.BuildPlayer(options);

        // Unity a veces falla la primera vez tras cambiar settings de OpenXR
        // ("Settings found in project but not yet loaded. Please build again").
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.LogWarning("[BuildQuest] Primer intento falló, reintentando...");
            report = BuildPipeline.BuildPlayer(options);
        }

        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.LogError("[BuildQuest] FALLÓ: " + report.summary.result +
                " errores=" + report.summary.totalErrors);
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
            return;
        }

        Debug.Log("[BuildQuest] OK: " + outputPath +
            " tamaño=" + (report.summary.totalSize / (1024 * 1024)) + " MB");
        if (Application.isBatchMode)
            EditorApplication.Exit(0);
    }
}
