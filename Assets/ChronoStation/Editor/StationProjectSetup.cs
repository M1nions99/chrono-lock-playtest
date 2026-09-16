using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StationProjectSetup
{
    // Activate the validated authored scene; never regenerate it while deploying to the project.
    public static void Activate()
    {
        const string path = "Assets/ChronoStation/Scenes/ChronoStationDemo.unity";
        if (!File.Exists(path)) throw new FileNotFoundException("The validated scene is missing", path);
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(path);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
        PlayerSettings.productName = "CHRONO STATION";
        PlayerSettings.companyName = "5team";
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
        Debug.Log("STATION_PROJECT_ACTIVATED " + path);
    }
}
