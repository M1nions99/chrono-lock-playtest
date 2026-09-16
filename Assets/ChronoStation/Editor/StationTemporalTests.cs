using System;
using System.Collections.Generic;
using ChronoStation;
using UnityEngine;

// Real component tests on temporary objects; no scene, asset or global clock changes.
public static class StationTemporalTests
{
    public static List<string> Run()
    {
        var errors = new List<string>();
        Test(errors,"initial / stop / rates / reset", o =>
        {
            o.kind=StationDeviceKind.Rotor;o.baseRate=.15f;o.Initialize();
            Need(o.HistoryCount==1&&!o.CanRewind&&!o.Apply(StationAbility.Rewind),"Initial state has no invented rewind history");
            Step(o,30);float normal=o.parameter;Quaternion rotated=o.movingPart.localRotation;
            Need(normal>.14f&&Quaternion.Angle(Quaternion.identity,rotated)>40,"Normal rotor advances");
            o.Apply(StationAbility.Stop);Step(o,30);Need(o.parameter==normal&&Quaternion.Angle(rotated,o.movingPart.localRotation)<.001f,"Stop holds exact pose");
            o.ResetDevice();o.Apply(StationAbility.Slow);Step(o,30);Need(Mathf.Abs(o.parameter-normal*.18f)<.0001f,"Slow multiplier");
            o.ResetDevice();o.Apply(StationAbility.Accelerate);Step(o,30);Need(Mathf.Abs(o.parameter-normal*4)<.0001f,"Acceleration multiplier");
            o.ResetDevice();Need(o.parameter==0&&o.HistoryCount==1&&!o.HasUsed(StationAbility.Accelerate)&&!o.ActiveAbility.HasValue,"Reset clears usage, mode and history");
            Step(o,30);Need(o.Apply(StationAbility.Rewind),"Recorded rotation can rewind");Step(o,15);
            Need(Quaternion.Angle(o.movingPart.localRotation,Quaternion.identity)<.001f&&o.HistoryCount==1&&!o.ActiveAbility.HasValue,"2x rewind restores exact initial rotation and auto releases");
        });
        Test(errors,"position / future branch / bounded history", o =>
        {
            o.kind=StationDeviceKind.Cargo;o.endpointA=Vector3.zero;o.endpointB=new Vector3(4,2,1);o.baseRate=.3f;o.Initialize();
            Step(o,30);Vector3 halfway=o.movingPart.localPosition;Step(o,30);
            o.Apply(StationAbility.Rewind);Step(o,15);Need(Vector3.Distance(halfway,o.movingPart.localPosition)<.0001f,"Rewind restores actual position");
            int rewound=o.HistoryCount;o.Release();Step(o,15);Need(o.HistoryCount==rewound+15&&o.HistoryCount<61,"Forward branch replaces discarded future");
            o.ResetDevice();o.kind=StationDeviceKind.Rotor;o.Apply(StationAbility.Stop);Step(o,420);Need(o.HistoryCount==361&&Mathf.Abs(o.HistorySeconds-12)<.001f,"General device stationary history remains capped at twelve seconds");
            Vector3 secured=o.movingPart.localPosition;o.Secure();Step(o,30);Need(o.movingPart.localPosition==secured&&!o.Apply(StationAbility.Accelerate),"Secured device holds pose and rejects abilities");
        });
        Test(errors,"cargo endpoint waiting preserves actual journey", o =>
        {
            o.kind=StationDeviceKind.Cargo;o.endpointA=Vector3.zero;o.endpointB=new Vector3(6,0,2);o.baseRate=.15f;o.Initialize();
            Step(o,60);int heldCount=o.HistoryCount;o.Apply(StationAbility.Stop);Step(o,900);
            Need(o.HistoryCount==heldCount&&o.HasInitialHistory,"Thirty seconds of mid-journey Stop preserve actual cargo movement history");
            o.Release();Step(o,180);Need(o.Progress==1,"Cargo reaches destination");int arrivalCount=o.HistoryCount;
            Step(o,900);Need(o.CanRewind&&o.HistoryCount==arrivalCount,"Thirty seconds of endpoint waiting do not erase movement history");
            Need(o.Apply(StationAbility.Rewind),"Waiting cargo accepts rewind");
            int guard=400;while(o.ActiveAbility==StationAbility.Rewind&&guard-->0)Step(o,1);
            Need(guard>0&&o.HistoryCount==1&&o.Progress==0&&o.movingPart.localPosition==Vector3.zero,"Cargo rewinds all the way to its actual initial pose");
        });
        Test(errors,"platform reflected direction / growth scale", o =>
        {
            o.kind=StationDeviceKind.MovingPlatform;o.endpointB=Vector3.right*3;o.baseRate=1;o.Initialize();Step(o,42);
            o.Apply(StationAbility.Rewind);Step(o,6);o.Release();float old=o.parameter;Step(o,1);Need(o.parameter<old,"Rewound ping-pong direction remains descending");
        });
        Test(errors,"growth scale snapshot", o =>
        {
            o.kind=StationDeviceKind.Growth;o.movingPart.localScale=new Vector3(1,2,3);o.baseRate=.5f;o.Initialize();Step(o,30);Vector3 scale=o.movingPart.localScale;Step(o,30);
            o.Apply(StationAbility.Rewind);Step(o,15);Need(Vector3.Distance(scale,o.movingPart.localScale)<.0001f,"Growth rewind restores scale");
            o.ResetDevice();Need(o.movingPart.localScale==new Vector3(1,2,3),"Reset restores authored scale");
        });
        foreach(string error in errors)Debug.LogError("STATION_TEMPORAL_FAIL "+error);
        if(errors.Count==0)Debug.Log("STATION_TEMPORAL_TESTS_PASSED");return errors;
    }
    static void Step(StationTemporalObject o,int count){for(int i=0;i<count;i++)o.Tick(1f/30f);}
    static void Need(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Test(List<string> errors,string name,Action<StationTemporalObject> action)
    {
        var go=new GameObject("Temporary temporal test");
        try{var o=go.AddComponent<StationTemporalObject>();o.movingPart=go.transform;action(o);}
        catch(Exception e){errors.Add(name+": "+e.Message);}
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
}
