using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChronoStation
{
    // One skinned right arm, derived from the protagonist, serves the opening and gameplay.
    [DefaultExecutionOrder(100)]
    public sealed class StationHandView : MonoBehaviour
    {
        const int ArmLayer=30;
        public StationGame game;
        public bool IsVisible => presentation && presentation.gameObject.activeInHierarchy;
        public bool IsOpeningPresentation => IsVisible && game && game.Opening;
        public Transform Palm => palm;
        public Transform Presentation => presentation;
        public Camera ArmCamera => armCamera;
        public Color GlyphColor { get; private set; }
        Transform presentation,alignment,palm;
        Camera view,armCamera;
        UniversalAdditionalCameraData baseData;
        readonly List<Finger> fingers=new List<Finger>();
        readonly List<Renderer> glyphs=new List<Renderer>();
        MaterialPropertyBlock glyphBlock;
        int previousMask;
        bool maskChanged;
        float gesture,castPulse,releasePulse,motionClock;
        bool hadActive;
        int lastCastSerial;
        sealed class Finger { public Transform bone;public Quaternion rest;public Vector3 axis;public float amount; }

        void Start()
        {

            if(!game)game=FindFirstObjectByType<StationGame>();
            view=GetComponent<Camera>();
            if(!view&&game&&game.Player!=null)view=game.Player.View;
            if(!view||!game){Debug.LogError("Chrono right arm requires the player camera and game.");enabled=false;return;}
            var source=Resources.Load<GameObject>("ProtagonistRightArm");
            if(!source){Debug.LogError("ProtagonistRightArm is missing. Run ChronoLock/Build First Person Arm.");enabled=false;return;}
            presentation=new GameObject("Protagonist right arm presentation").transform;
            presentation.SetParent(view.transform,false);
            alignment=new GameObject("Anatomical palm alignment").transform;alignment.SetParent(presentation,false);
            var model=Instantiate(source,alignment,false);model.name="Protagonist right arm";
            var bones=new Dictionary<string,Transform>();
            foreach(var t in model.GetComponentsInChildren<Transform>(true)){t.gameObject.layer=ArmLayer;bones[t.name]=t;}
            palm=Required(bones,"hand_r");
            var middle=Required(bones,"middle_01_r");var index=Required(bones,"index_01_r");var pinky=Required(bones,"pinky_01_r");
            Vector3 fingerDirection=presentation.InverseTransformDirection(middle.position-palm.position).normalized;
            Vector3 thumbDirection=presentation.InverseTransformDirection(index.position-pinky.position).normalized;
            thumbDirection=Vector3.ProjectOnPlane(thumbDirection,fingerDirection).normalized;
            Quaternion frame=Quaternion.LookRotation(Vector3.Cross(thumbDirection,fingerDirection),fingerDirection);
            Vector3 center=presentation.InverseTransformPoint(Vector3.Lerp(palm.position,middle.position,.52f));
            alignment.localRotation=Quaternion.Inverse(frame);alignment.localPosition=-(alignment.localRotation*center);
            foreach(var pair in bones)
            {
                string n=pair.Key;
                if(!n.EndsWith("_r",StringComparison.Ordinal)||!(n.StartsWith("index_")||n.StartsWith("middle_")||n.StartsWith("ring_")||n.StartsWith("pinky_")||n.StartsWith("thumb_")))continue;
                fingers.Add(new Finger{bone=pair.Value,rest=pair.Value.localRotation,axis=pair.Value.InverseTransformDirection(presentation.right).normalized,amount=n.StartsWith("thumb_")?.22f:n.Contains("_01_")?1:n.Contains("_02_")?.8f:.45f});
            }
            foreach(var collider in model.GetComponentsInChildren<Collider>(true)){collider.enabled=false;Destroy(collider);}
            foreach(var animator in model.GetComponentsInChildren<Animator>(true))animator.enabled=false;
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                if(renderer is SkinnedMeshRenderer skin)skin.updateWhenOffscreen=true;
                foreach(var material in renderer.sharedMaterials)if(material&&material.name.Contains("Glyph")){glyphs.Add(renderer);break;}
            }
            glyphBlock=new MaterialPropertyBlock();
            previousMask=view.cullingMask;view.cullingMask&=~(1<<ArmLayer);maskChanged=true;
            baseData=view.GetUniversalAdditionalCameraData();
            armCamera=new GameObject("First person arm camera").AddComponent<Camera>();
            armCamera.transform.SetParent(view.transform,false);armCamera.cullingMask=1<<ArmLayer;
            armCamera.clearFlags=CameraClearFlags.Depth;armCamera.nearClipPlane=.01f;armCamera.farClipPlane=3;
            armCamera.allowHDR=view.allowHDR;armCamera.allowMSAA=true;
            // A new URP camera defaults to clearing depth; clearDepth is a read-only API.
            var armData=armCamera.GetUniversalAdditionalCameraData();armData.renderType=CameraRenderType.Overlay;armData.renderPostProcessing=false;armData.renderShadows=false;
            baseData.cameraStack.Add(armCamera);
            var fill=new GameObject("Right arm soft fill").AddComponent<Light>();fill.transform.SetParent(presentation,false);
            fill.transform.localPosition=new Vector3(-.15f,.2f,-.15f);fill.type=LightType.Point;fill.color=new Color(.9f,.94f,1);fill.intensity=.035f;fill.range=1.2f;fill.shadows=LightShadows.None;fill.cullingMask=1<<ArmLayer;
            presentation.gameObject.SetActive(false);armCamera.enabled=false;
        }
        static Transform Required(Dictionary<string,Transform> bones,string name)
        { if(bones.TryGetValue(name,out var bone))return bone;throw new InvalidOperationException("Protagonist right arm bone missing: "+name); }

        void LateUpdate()
        {
            if(!presentation||!armCamera||!palm||!game||!view)return;
            bool opening=game.Opening;
            bool hud=game.Page==StationPage.HUD;
            bool openingVisible=opening&&hud&&game.OpeningElapsed>=2.7f&&game.OpeningElapsed<7.8f;
            bool playing=game.IsPlaying&&!opening&&hud;
            bool visible=openingVisible||playing;
            presentation.gameObject.SetActive(visible);armCamera.enabled=visible;
            if(!visible)
            {
                gesture=castPulse=releasePulse=0;hadActive=false;
                lastCastSerial=game.CastSerial;return;
            }
            armCamera.fieldOfView=view.fieldOfView;armCamera.aspect=view.aspect;
            bool motion=ChronoLock.ChronoPreferences.Runtime.cameraMotion;
            float dt=Mathf.Min(Time.deltaTime,.05f);
            motionClock+=dt;
            bool active=game.SelfActive.HasValue||game.ActiveObject!=null;
            if(game.CastSerial!=lastCastSerial)castPulse=1;
            if(!active&&hadActive)releasePulse=1;
            lastCastSerial=game.CastSerial;hadActive=active;
            castPulse=Mathf.MoveTowards(castPulse,0,dt*2.8f);releasePulse=Mathf.MoveTowards(releasePulse,0,dt*3.5f);
            float desired=active||castPulse>.05f?1:0;
            gesture=motion?Mathf.MoveTowards(gesture,desired,dt*4.5f):desired;
            Vector3 position;Quaternion rotation;float curl;
            if(openingVisible)
            {
                float t=game.OpeningElapsed;
                float reveal=motion?Mathf.SmoothStep(0,1,Mathf.Clamp01((t-2.7f)/.75f)):1;
                float lower=motion?Mathf.SmoothStep(0,1,Mathf.Clamp01((t-7.1f)/.7f)):0;
                position=Vector3.Lerp(new Vector3(.16f,-.51f,.44f),new Vector3(.12f,-.075f,.44f),reveal*(1-lower));
                rotation=Quaternion.Euler(8,-12,12);
                curl=motion?Mathf.Lerp(16,3,Mathf.SmoothStep(0,1,Mathf.Clamp01((t-3.25f)/1.1f))):3;
                gesture=0;
            }
            else
            {
                position=Vector3.Lerp(new Vector3(.28f,-.31f,.44f),new Vector3(.23f,-.15f,.46f),gesture);
                rotation=Quaternion.Slerp(Quaternion.Euler(90,-40,-25),Quaternion.Euler(28,-32,-18),gesture);
                curl=Mathf.Lerp(12,5,gesture);
                if(motion)
                {
                    position+=Vector3.up*Mathf.Sin(motionClock*1.8f)*.0015f;
                    position+=new Vector3(0,-.009f,-.014f)*castPulse;
                    curl+=releasePulse*8;
                }
            }
            // Compensate for the player's FOV so the arm never grows across the aiming reticle.
            float fovScale=Mathf.Tan(view.fieldOfView*Mathf.Deg2Rad*.5f)/Mathf.Tan(78*Mathf.Deg2Rad*.5f);
            presentation.localScale=Vector3.one*fovScale;
            presentation.localPosition=new Vector3(position.x*fovScale,position.y*fovScale,position.z);
            presentation.localRotation=rotation;
            foreach(var finger in fingers)finger.bone.localRotation=finger.rest*Quaternion.AngleAxis(-curl*finger.amount,finger.axis);
            GlyphColor=openingVisible?AbilityColor(StationAbility.Stop):AbilityColor(game.Selected);
            glyphBlock.SetColor("_BaseColor",GlyphColor);glyphBlock.SetColor("_EmissionColor",GlyphColor*(active?1.3f:.55f));
            foreach(var glyph in glyphs)glyph.SetPropertyBlock(glyphBlock);
        }
        public static Color AbilityColor(StationAbility ability)
        {
            switch(ability)
            {
                case StationAbility.Accelerate:return new Color(1,.40f,.055f);
                case StationAbility.Slow:return new Color(.12f,.38f,1);
                case StationAbility.Rewind:return new Color(.62f,.25f,1);
                default:return new Color(.035f,.8f,1);
            }
        }
        void OnDisable()
        {
            if(presentation)presentation.gameObject.SetActive(false);
            if(armCamera)armCamera.enabled=false;
        }
        void OnDestroy()
        {
            if(baseData&&armCamera)baseData.cameraStack.Remove(armCamera);
            if(view&&maskChanged)view.cullingMask=previousMask;
            if(armCamera)Destroy(armCamera.gameObject);
            if(presentation)Destroy(presentation.gameObject);
        }
    }
}

