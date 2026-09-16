using System.Collections.Generic;
using UnityEngine;

namespace ChronoLock
{
    public enum TemporalMode { Normal, Freeze, Accelerate, Rewind }
    public enum DeviceKind { Rotor, Reactor, Bridge }

    // Only device state is rewound. Player, objectives and wall-clock timer are not.
    public sealed class TemporalDevice : MonoBehaviour
    {
        public DeviceKind kind;
        public string displayName;
        public Transform movingPart;
        public float state;
        public bool secured;
        public Vector3 restoredPosition = new Vector3(0, 0, 35);
        public Vector3 damagedOffset = new Vector3(0, -5, 0);
        public Vector3 chargedScale = new Vector3(.8f, 2.6f, .8f);
        public Vector3 emptyScale = new Vector3(.8f, .1f, .8f);
        public TemporalMode Mode { get; private set; }
        public int HistoryCount => history.Count;
        public float HistorySeconds => Mathf.Max(0, history.Count - 1) * SampleStep;
        const float SampleStep = 1f / 30f;
        const int Capacity = 241;
        readonly List<float> history = new List<float>(Capacity);
        float accumulator;
        void Awake() { ResetDevice(); }
        public bool Ready => kind == DeviceKind.Reactor ? state >= .999f : kind == DeviceKind.Bridge && state <= .001f;

        public void Initialize(DeviceKind deviceKind, Transform visual, string label)
        {
            kind = deviceKind; movingPart = visual; displayName = label;
            ResetDevice();
        }
        public void ResetDevice()
        {
            secured = false; Mode = TemporalMode.Normal; accumulator = 0; history.Clear();
            state = kind == DeviceKind.Bridge ? 1 : 0;
            // Authored incident memory: the bridge collapsed during the four seconds before arrival.
            if (kind == DeviceKind.Bridge)
                for (int i = 0; i <= 120; i++) history.Add(i / 120f);
            else history.Add(state);
            ApplyVisual();
        }
        public void SetMode(TemporalMode mode) { Mode = secured ? TemporalMode.Normal : mode; accumulator = 0; }
        public void Secure() { secured = true; Mode = TemporalMode.Normal; }
        public void Tick(float dt)
        {
            if (secured || Mode == TemporalMode.Freeze) return;
            accumulator += Mathf.Min(dt, .25f);
            while (accumulator >= SampleStep)
            {
                accumulator -= SampleStep;
                if (Mode == TemporalMode.Rewind)
                {
                    // Consume recorded states; resuming creates a new branch from the restored state.
                    for (int i = 0; i < 2 && history.Count > 1; i++) history.RemoveAt(history.Count - 1);
                    state = history[history.Count - 1];
                }
                else
                {
                    float rate = Mode == TemporalMode.Accelerate ? 4 : 1;
                    if (kind == DeviceKind.Rotor) state += SampleStep * rate * 90;
                    if (kind == DeviceKind.Reactor) state = Mathf.Clamp01(state + SampleStep * rate / 32);
                    // Keep the authored collapse memory until the bridge is first rewound.
                    if (kind != DeviceKind.Bridge)
                    {
                        if (history.Count == Capacity) history.RemoveAt(0);
                        history.Add(state);
                    }
                }
            }
            ApplyVisual();
        }
        public void ApplyVisual()
        {
            if (!movingPart) return;
            if (kind == DeviceKind.Rotor) movingPart.localRotation = Quaternion.Euler(0, 0, state);
            if (kind == DeviceKind.Reactor) movingPart.localScale = Vector3.Lerp(emptyScale, chargedScale, state);
            if (kind == DeviceKind.Bridge) movingPart.localPosition = restoredPosition + damagedOffset * state;
        }
    }
}
