using System.Collections.Generic;
using UnityEngine;

namespace ChronoLock
{
    public sealed class TemporalGlow : MonoBehaviour
    {
        public ChronoGame game;
        TemporalDevice device;
        readonly List<Renderer> indicators = new List<Renderer>();
        MaterialPropertyBlock properties;
        void Start()
        {
            device=GetComponent<TemporalDevice>();properties=new MaterialPropertyBlock();
            foreach(var r in GetComponentsInChildren<Renderer>())
                if(r.sharedMaterial && (r.sharedMaterial.name.Contains("Teal") || r.name.Contains("Display glass") || r.name.Contains("center indicator"))) indicators.Add(r);
        }
        void Update()
        {
            if(!device || !game || game.Paused || game.Completed)return;
            var mode=device.Mode;
            Color c=mode==TemporalMode.Accelerate?new Color(1,.5f,.09f):mode==TemporalMode.Rewind?new Color(.68f,.32f,1):device.kind==DeviceKind.Bridge?new Color(.68f,.32f,1):new Color(.04f,.78f,.88f);
            if(device.secured)c=new Color(.18f,.95f,.6f);
            float intensity=game.CurrentTarget==device?1.15f:1;
            if(mode!=TemporalMode.Normal)intensity+=Mathf.Sin(Time.time*5)*.10f;
            foreach(var r in indicators)
            {
                float charge=device.kind==DeviceKind.Reactor && r.name.Contains("Energy column")?Mathf.Lerp(.09f,1,device.state):1;
                properties.SetColor("_BaseColor",c*intensity*charge);r.SetPropertyBlock(properties);
            }
        }
    }
}
