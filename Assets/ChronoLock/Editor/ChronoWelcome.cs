using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// One-time onboarding only for a clean default scene; never replaces an unsaved scene.
[InitializeOnLoad]
public static class ChronoWelcome
{
    const string ScenePath = "Assets/ChronoLock/Scenes/ChronoLab.unity";
    static ChronoWelcome() { EditorApplication.delayCall += Welcome; }
    static void Welcome()
    {
        if(Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if(EditorApplication.isCompiling || EditorApplication.isUpdating) {EditorApplication.delayCall+=Welcome;return;}
        string key="ChronoLock.PolishWelcome."+Application.dataPath;
        if(EditorPrefs.GetBool(key,false)) return;
        var active=SceneManager.GetActiveScene();
        if(active.isDirty || (active.name!="SampleScene" && !string.IsNullOrEmpty(active.path))) return;
        if(!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)) return;
        Open(); EditorPrefs.SetBool(key,true);
    }
    [MenuItem("ChronoLock/Open Research Station",priority=0)]
    public static void Open()
    {
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        EditorSceneManager.OpenScene(ScenePath);
        if(SceneView.lastActiveSceneView)
            SceneView.lastActiveSceneView.LookAt(new Vector3(0,2.3f,9),Quaternion.identity,2.6f,false,true);
    }
}
