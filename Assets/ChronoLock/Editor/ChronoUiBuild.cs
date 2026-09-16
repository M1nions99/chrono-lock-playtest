using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class ChronoUiBuild
{
    public static void Build()
    {
        PlayerSettings.productName = "CHRONO LOCK";
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.defaultIsNativeResolution = false;
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/ChronoLock/Scenes/ChronoLab.unity" },
            locationPathName = "Builds/ChronoLockOpening/ChronoLock.exe",
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
        });
        if(result.summary.result != BuildResult.Succeeded) throw new Exception("UI build failed: " + result.summary.result);
        Debug.Log("CHRONO_UI_BUILD_PASSED");
    }
}


