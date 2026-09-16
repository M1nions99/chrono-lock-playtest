using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Run inside the intended Unity project. Source prefab/meshes are never written.
public static class ChronoFirstPersonArmBuilder
{
    const string Source="Assets/ChronoLock/Resources/Protagonist.prefab";
    const string Output="Assets/ChronoLock/Resources/ProtagonistRightArm.prefab";
    const string Folder="Assets/ChronoLock/Art/Character/FirstPerson/";
    [Serializable] sealed class BonePoint { public string name; public Vector3 rootLocal; }
    [Serializable] sealed class Part { public string renderer,mesh; public int vertices,triangles; }
    [Serializable] sealed class Report
    {
        public string source=Source,output=Output;
        public int preservedTransforms,preservedBones,rendererCount,vertexCount,triangleCount;
        public float elbowAllowanceMeters=.08f;
        public List<BonePoint> bones=new List<BonePoint>();
        public List<Part> parts=new List<Part>();
    }
    [MenuItem("ChronoLock/Build First Person Right Arm")]
    public static void Build()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Source);
        if(!prefab)throw new FileNotFoundException("Build the rigged Protagonist prefab first: "+Source);
        var actor=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if(!actor)throw new InvalidOperationException("Unable to instantiate source protagonist.");
        try
        {
            PrefabUtility.UnpackPrefabInstance(actor,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            actor.name="ProtagonistRightArm";
            var allTransforms=actor.GetComponentsInChildren<Transform>(true);
            var elbow=Find(allTransforms,"lowerarm_r");var hand=Find(allTransforms,"hand_r");
            if(!elbow||!hand)throw new InvalidOperationException("Required lowerarm_r / hand_r bones are missing.");
            Vector3 axis=(hand.position-elbow.position).normalized;
            if(axis.sqrMagnitude<.99f)throw new InvalidOperationException("Elbow and wrist positions coincide.");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var report=new Report{preservedTransforms=allTransforms.Length};
            foreach(string name in new[]{"hand_r","middle_01_r","index_01_r","pinky_01_r"})
            {
                var bone=Find(allTransforms,name);if(!bone)throw new InvalidOperationException("Required report bone missing: "+name);
                report.bones.Add(new BonePoint{name=name,rootLocal=actor.transform.InverseTransformPoint(bone.position)});
            }
            var skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var uniqueBones=new HashSet<Transform>();foreach(var skin in skins)foreach(var bone in skin.bones)if(bone)uniqueBones.Add(bone);report.preservedBones=uniqueBones.Count;
            foreach(var renderer in skins)
            {
                Mesh source=renderer.sharedMesh;
                if(!source){UnityEngine.Object.DestroyImmediate(renderer);continue;}
                var baked=new Mesh();Mesh result=null;
                try
                {
                    renderer.BakeMesh(baked,false);
                    var positions=baked.vertices;
                    if(positions.Length!=source.vertexCount)throw new InvalidOperationException("Baked/source vertex mismatch: "+renderer.name);
                    bool[] eligible=Eligible(source,renderer,positions,elbow.position,axis);
                    result=Extract(source,eligible,out int triangles);
                    if(!result){UnityEngine.Object.DestroyImmediate(renderer);continue;}
                    string identity=PathOf(renderer.transform,actor.transform)+"#"+Array.IndexOf(skins,renderer);
                    string name="RightArm_"+Hash128.Compute(identity).ToString();
                    string path=Folder+name+".asset";result.name=name;
                    var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(existing){EditorUtility.CopySerialized(result,existing);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(result);result=null;renderer.sharedMesh=existing;}
                    else{AssetDatabase.CreateAsset(result,path);renderer.sharedMesh=result;result=null;}
                    renderer.enabled=true;renderer.gameObject.SetActive(true);renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.updateWhenOffscreen=true;
                    // Baking is only used for classification/bounds. Stored vertices remain in original bind space.
                    var acceptedBounds=new Bounds();bool first=true;
                    for(int i=0;i<positions.Length;i++)if(eligible[i]){if(first){acceptedBounds=new Bounds(positions[i],Vector3.zero);first=false;}else acceptedBounds.Encapsulate(positions[i]);}
                    acceptedBounds.Expand(.08f);renderer.localBounds=acceptedBounds;
                    report.parts.Add(new Part{renderer=identity,mesh=path,vertices=renderer.sharedMesh.vertexCount,triangles=triangles});
                    report.vertexCount+=renderer.sharedMesh.vertexCount;report.triangleCount+=triangles;report.rendererCount++;
                }
                finally{UnityEngine.Object.DestroyImmediate(baked);if(result)UnityEngine.Object.DestroyImmediate(result);}
            }
            // Keep every bone/transform, even where its old body renderer was removed.
            foreach(var renderer in actor.GetComponentsInChildren<Renderer>(true))if(!(renderer is SkinnedMeshRenderer))UnityEngine.Object.DestroyImmediate(renderer);
            foreach(var collider in actor.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
            foreach(var animator in actor.GetComponentsInChildren<Animator>(true))UnityEngine.Object.DestroyImmediate(animator);
            foreach(var animation in actor.GetComponentsInChildren<Animation>(true))UnityEngine.Object.DestroyImmediate(animation);
            foreach(var script in actor.GetComponentsInChildren<MonoBehaviour>(true))if(script)UnityEngine.Object.DestroyImmediate(script);
            foreach(var transform in allTransforms)transform.gameObject.layer=2;
            if(report.rendererCount==0||report.triangleCount==0)throw new InvalidOperationException("No right forearm/hand triangles survived. Verify bone naming and bind pose.");
            if(actor.GetComponentsInChildren<Transform>(true).Length!=report.preservedTransforms)throw new InvalidOperationException("Rig transform hierarchy was altered.");
            foreach(var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                foreach(var material in renderer.sharedMaterials)if(!material)throw new InvalidOperationException("Retained arm renderer has a missing material: "+renderer.name);
            AssetDatabase.SaveAssets();
            if(!PrefabUtility.SaveAsPrefabAsset(actor,Output))throw new IOException("Could not save right arm prefab.");
            File.WriteAllText(Folder+"RightArmReport.json",JsonUtility.ToJson(report,true));AssetDatabase.Refresh();
            Debug.Log("CHRONO_RIGHT_ARM_BUILT "+JsonUtility.ToJson(report));
        }
        finally{UnityEngine.Object.DestroyImmediate(actor);}
    }
    static string Clean(string n){int colon=n.LastIndexOf(':');return n.Substring(colon+1).ToLowerInvariant();}
    static Transform Find(Transform[] all,string name){foreach(var t in all)if(Clean(t.name)==name)return t;return null;}
    static bool Allowed(string raw)
    {
        string n=Clean(raw);if(!n.EndsWith("_r"))return false;
        return n.StartsWith("upperarm")||n.StartsWith("lowerarm")||n=="hand_r"||n.StartsWith("index")||n.StartsWith("middle")||n.StartsWith("ring")||n.StartsWith("pinky")||n.StartsWith("thumb");
    }
    static bool[] Eligible(Mesh mesh,SkinnedMeshRenderer renderer,Vector3[] posed,Vector3 elbow,Vector3 axis)
    {
        var counts=mesh.GetBonesPerVertex();var weights=mesh.GetAllBoneWeights();
        try
        {
            if(counts.Length!=mesh.vertexCount)throw new InvalidOperationException("Skin weights missing on "+renderer.name);
            var result=new bool[mesh.vertexCount];int cursor=0;
            for(int v=0;v<result.Length;v++)
            {
                float arm=0;
                for(int j=0;j<counts[v];j++)
                {
                    var weight=weights[cursor++];
                    if(weight.boneIndex<0||weight.boneIndex>=renderer.bones.Length)throw new InvalidOperationException("Out of range bone index.");
                    var bone=renderer.bones[weight.boneIndex];
                    if(bone && Allowed(bone.name))arm+=weight.weight;
                }
                result[v]=arm>=.45f && Vector3.Dot(renderer.transform.TransformPoint(posed[v])-elbow,axis)>=-.08f;
            }
            return result;
        }
        finally{/* Mesh-provided NativeArrays are non-owning views; do not dispose them. */}
    }
    static Mesh Extract(Mesh source,bool[] eligible,out int triangleCount)
    {
        triangleCount=0;var triangles=new List<int>[source.subMeshCount];var oldIndices=new List<int>();var remap=new Dictionary<int,int>();
        for(int s=0;s<source.subMeshCount;s++)
        {
            if(source.GetTopology(s)!=MeshTopology.Triangles)throw new InvalidOperationException("Expected triangle topology: "+source.name);
            triangles[s]=new List<int>();var indices=source.GetTriangles(s);
            for(int i=0;i<indices.Length;i+=3)
            {
                if(!eligible[indices[i]]||!eligible[indices[i+1]]||!eligible[indices[i+2]])continue;
                for(int j=0;j<3;j++){int old=indices[i+j];if(!remap.TryGetValue(old,out int next)){next=oldIndices.Count;remap.Add(old,next);oldIndices.Add(old);}triangles[s].Add(next);}triangleCount++;
            }
        }
        if(triangleCount==0)return null;
        var mesh=new Mesh{indexFormat=oldIndices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
        mesh.vertices=Remap(source.vertices,oldIndices);if(source.normals.Length>0)mesh.normals=Remap(source.normals,oldIndices);if(source.tangents.Length>0)mesh.tangents=Remap(source.tangents,oldIndices);if(source.colors.Length>0)mesh.colors=Remap(source.colors,oldIndices);
        for(int channel=0;channel<8;channel++){var uv=new List<Vector4>();source.GetUVs(channel,uv);if(uv.Count>0){var values=new List<Vector4>();foreach(int old in oldIndices)values.Add(uv[old]);mesh.SetUVs(channel,values);}}
        mesh.bindposes=source.bindposes;
        var counts=source.GetBonesPerVertex();var weights=source.GetAllBoneWeights();
        try
        {
            var starts=new int[counts.Length];int cursor=0;for(int i=0;i<starts.Length;i++){starts[i]=cursor;cursor+=counts[i];}
            var newCounts=new NativeArray<byte>(oldIndices.Count,Allocator.Temp);var newWeights=new List<BoneWeight1>();
            try{for(int i=0;i<oldIndices.Count;i++){int old=oldIndices[i];newCounts[i]=counts[old];for(int k=0;k<counts[old];k++)newWeights.Add(weights[starts[old]+k]);}using(var nativeWeights=new NativeArray<BoneWeight1>(newWeights.ToArray(),Allocator.Temp))mesh.SetBoneWeights(newCounts,nativeWeights);}
            finally{newCounts.Dispose();}
        }
        finally{/* Mesh-provided NativeArrays are non-owning views; do not dispose them. */}
        mesh.subMeshCount=source.subMeshCount;for(int i=0;i<triangles.Length;i++)mesh.SetTriangles(triangles[i],i,false);
        for(int b=0;b<source.blendShapeCount;b++)for(int f=0;f<source.GetBlendShapeFrameCount(b);f++)
        {var dv=new Vector3[source.vertexCount];var dn=new Vector3[source.vertexCount];var dt=new Vector3[source.vertexCount];source.GetBlendShapeFrameVertices(b,f,dv,dn,dt);mesh.AddBlendShapeFrame(source.GetBlendShapeName(b),source.GetBlendShapeFrameWeight(b,f),Remap(dv,oldIndices),Remap(dn,oldIndices),Remap(dt,oldIndices));}
        mesh.RecalculateBounds();return mesh;
    }
    static T[] Remap<T>(T[] values,List<int> indices){var result=new T[indices.Count];for(int i=0;i<result.Length;i++)result[i]=values[indices[i]];return result;}
    static string PathOf(Transform t,Transform root){var path=t.name;while(t.parent && t.parent!=root){t=t.parent;path=t.name+"/"+path;}return path;}
}

