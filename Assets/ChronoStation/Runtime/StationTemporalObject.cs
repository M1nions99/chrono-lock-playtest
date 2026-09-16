using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChronoStation
{
    /// <summary>
    /// Object-local temporal simulation. The owner calls Tick only while gameplay is running.
    /// No Update, global clock changes, synthetic prehistory, or dependencies on game state.
    /// Platform/cargo endpoints use movingPart's parent space; author its initial pose at
    /// Lerp(endpointA, endpointB, initial parameter) for a continuous first movement.
    /// Growth doubles authored scale between parameters 0 and 1 (relative to the initial value).
    /// Cargo excludes stationary waiting from its movement history, preserving its recorded journey.
    /// </summary>
    public sealed class StationTemporalObject : MonoBehaviour
    {
        public StationDeviceKind kind;
        public Transform movingPart;
        public Vector3 endpointA, endpointB;
        public float baseRate = .15f;
        public float parameter;
        public string displayName;
        public bool pingPong = true;

        public StationAbility? ActiveAbility { get; private set; }
        public bool Secured { get; private set; }
        public bool CanRewind => !Secured && history.Count > 1;
        public int HistoryCount => history.Count;
        public float HistorySeconds => Mathf.Max(0, history.Count - 1) * SampleStep;
        public float Progress => parameter;
        public bool HasInitialHistory => history.Count > 0 && Mathf.Abs(history[0].parameter - initialParameter) < .00001f;

        const float SampleStep = 1f / 30f;
        const int HistoryCapacity = 361; // Initial sample plus twelve seconds of 30 Hz states.
        readonly List<Snapshot> history = new List<Snapshot>(HistoryCapacity);
        readonly HashSet<StationAbility> used = new HashSet<StationAbility>();
        bool initialized;
        Vector3 initialPosition, initialScale;
        Quaternion initialRotation;
        float initialParameter;
        int direction = 1;
        double accumulator;

        struct Snapshot
        {
            public Vector3 position, scale;
            public Quaternion rotation;
            public float parameter;
            public int direction;
        }

        public void Initialize()
        {
            if (initialized) return; // Never replace reset data with a modified live pose.
            if (!movingPart) movingPart = transform;
            if (!Finite(parameter) || !Finite(baseRate))
                throw new InvalidOperationException("Temporal parameter and rate must be finite.");
            initialPosition = movingPart.localPosition;
            initialRotation = movingPart.localRotation;
            initialScale = movingPart.localScale;
            initialParameter = parameter;
            initialized = true;
            ResetDevice();
        }

        public void ResetDevice()
        {
            if (!initialized) { Initialize(); return; }
            ActiveAbility = null;
            Secured = false;
            used.Clear();
            accumulator = 0;
            direction = 1;
            parameter = initialParameter;
            movingPart.localPosition = initialPosition;
            movingPart.localRotation = initialRotation;
            movingPart.localScale = initialScale;
            history.Clear();
            Record(); // Only the actual authored initial state, never an invented past.
        }

        public bool HasUsed(StationAbility ability) => used.Contains(ability);

        public bool Apply(StationAbility ability)
        {
            Initialize();
            if (Secured || !Enum.IsDefined(typeof(StationAbility), ability)) return false;
            if (ability == StationAbility.Rewind && !CanRewind) return false;
            if (ActiveAbility != ability) accumulator = 0;
            ActiveAbility = ability;
            used.Add(ability);
            return true;
        }

        public void Release()
        {
            ActiveAbility = null;
            accumulator = 0;
        }

        public void Secure()
        {
            Initialize();
            Release();
            Secured = true; // Keep this exact transform; do not snap to an endpoint.
        }

        public void Tick(float dt)
        {
            Initialize();
            if (Secured || !movingPart || !Finite(dt) || dt <= 0) return;
            if (!Finite(baseRate)) throw new InvalidOperationException("Temporal rate must be finite.");
            bool rewind = ActiveAbility == StationAbility.Rewind;
            accumulator += rewind ? (double)dt * 2 : dt;
            // Retain fractional time and consume all elapsed samples, independent of frame rate.
            while (accumulator + 1e-9 >= SampleStep)
            {
                accumulator -= SampleStep;
                if (rewind)
                {
                    if (history.Count <= 1) { Release(); break; }
                    history.RemoveAt(history.Count - 1);
                    Restore(history[history.Count - 1]);
                    if (history.Count == 1) { Release(); break; }
                }
                else
                {
                    // The one-shot cargo puzzle records motion; waiting or Stop must not erase it.
                    // Other devices retain ordinary elapsed-time history, including held states.
                    bool cargoMovementHistory = kind == StationDeviceKind.Cargo;
                    Vector3 beforePosition = movingPart.localPosition;
                    Quaternion beforeRotation = movingPart.localRotation;
                    Vector3 beforeScale = movingPart.localScale;
                    float multiplier = ActiveAbility == StationAbility.Stop ? 0
                        : ActiveAbility == StationAbility.Slow ? .18f
                        : ActiveAbility == StationAbility.Accelerate ? 4 : 1;
                    if (multiplier > 0) Advance(SampleStep * multiplier);
                    if (cargoMovementHistory && beforePosition.Equals(movingPart.localPosition)
                        && beforeRotation.Equals(movingPart.localRotation)
                        && beforeScale.Equals(movingPart.localScale)) continue;
                    Record(); // A held state is genuine elapsed history, also bounded to twelve seconds.
                }
            }
        }

        void Advance(float dt)
        {
            float change = Mathf.Max(0, baseRate) * dt;
            switch (kind)
            {
                case StationDeviceKind.Rotor:
                    parameter += change;
                    movingPart.localRotation = initialRotation * Quaternion.Euler(0, 0, (parameter - initialParameter) * 360);
                    break;
                case StationDeviceKind.Generator:
                    parameter = Mathf.Clamp01(parameter + change);
                    movingPart.localRotation = initialRotation * Quaternion.Euler(0, (parameter - initialParameter) * 360, 0);
                    break;
                case StationDeviceKind.Growth:
                    parameter = Mathf.Clamp01(parameter + change);
                    movingPart.localScale = initialScale * ((1 + parameter) / (1 + Mathf.Clamp01(initialParameter)));
                    break;
                case StationDeviceKind.MovingPlatform:
                    if (pingPong)
                    {
                        // Reflect arbitrary overshoot without losing its remaining travel distance.
                        float phase = direction > 0 ? parameter : 2 - parameter;
                        phase = Mathf.Repeat(phase + change, 2);
                        direction = phase < 1 ? 1 : -1;
                        parameter = phase <= 1 ? phase : 2 - phase;
                    }
                    else parameter = Mathf.Clamp01(parameter + change);
                    movingPart.localPosition = Vector3.Lerp(endpointA, endpointB, parameter);
                    break;
                case StationDeviceKind.Cargo:
                    parameter = Mathf.Clamp01(parameter + change);
                    movingPart.localPosition = Vector3.Lerp(endpointA, endpointB, parameter);
                    break;
            }
        }

        void Record()
        {
            if (history.Count == HistoryCapacity) history.RemoveAt(0);
            history.Add(new Snapshot { position = movingPart.localPosition, rotation = movingPart.localRotation,
                scale = movingPart.localScale, parameter = parameter, direction = direction });
        }
        void Restore(Snapshot state)
        {
            movingPart.localPosition = state.position;
            movingPart.localRotation = state.rotation;
            movingPart.localScale = state.scale;
            parameter = state.parameter;
            direction = state.direction;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
