using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ChronoOpeningAssetImport
{
    public static void Prepare()
    {
        const string folder="Assets/ChronoLock/Materials/Opening/";
        Directory.CreateDirectory(folder);Directory.CreateDirectory("Assets/ChronoLock/Resources");AssetDatabase.Refresh();
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChronoLock/Art/Opening/PalmAnchor.fbx");
        if(!asset)throw new InvalidOperationException("Blender PalmAnchor.fbx is required.");
        var root=new GameObject("PalmAnchor");
        try
        {
            var source=(GameObject)PrefabUtility.InstantiatePrefab(asset);source.transform.SetParent(root.transform,false);
            foreach(var c in root.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    string n=materials[i]?materials[i].name:"CL_Dark";
                    bool glow=n.Contains("Teal");
                    string key=glow?"PalmTeal":n.Contains("Ceramic")?"PalmCeramic":n.Contains("Glove")?"PalmGlove":n.Contains("Metal")?"PalmMetal":"PalmDark";
                    Color color=glow?new Color(.04f,.85f,.8f):key=="PalmCeramic"?new Color(.62f,.73f,.76f):key=="PalmGlove"?new Color(.24f,.34f,.38f):key=="PalmMetal"?new Color(.13f,.22f,.26f):new Color(.025f,.045f,.055f);
                    string path=folder+key+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(!m){m=new Material(Shader.Find(glow?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
                    m.SetColor("_BaseColor",color);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",.45f);EditorUtility.SetDirty(m);materials[i]=m;
                }
                renderer.sharedMaterials=materials;
            }
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/ChronoLock/Resources/PalmAnchor.prefab");
            Debug.Log("CHRONO_PALM_PREFAB_CREATED preserving source FBX transform");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
        AssetDatabase.SaveAssets();
    }
    public static void PrepareAndUpgrade(){Prepare();ChronoOpeningBuilder.Upgrade();}
}
