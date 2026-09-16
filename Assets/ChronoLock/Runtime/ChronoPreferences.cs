using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChronoLock
{
    [Serializable]
    public sealed class ChronoSettingsData
    {
        public float masterVolume = .8f, effectsVolume = .8f, sensitivity = 1f, fov = 78f;
        public bool invertY = false, cameraMotion = true, showTimer = true;
        public int displayMode = 0, quality = 1, frameLimit = 1;
        public bool vsync = true;
        public ChronoSettingsData Copy() => (ChronoSettingsData)MemberwiseClone();
    }

    public static class ChronoPreferences
    {
        public const string StorageKey = "ChronoLock.Options.v1";
        static ChronoSettingsData current;
        static UniversalRenderPipelineAsset runtimePipeline;
        static RenderPipelineAsset previousPipelineOverride;
        // Public snapshots cannot accidentally change live settings while editing a menu.
        public static ChronoSettingsData Current => Runtime.Copy();
        internal static ChronoSettingsData Runtime
        {
            get { if (current == null) Load(); return current; }
        }
        public static float EffectsVolume => Runtime.effectsVolume;
        public static ChronoSettingsData Defaults() => new ChronoSettingsData();
        public static ChronoSettingsData Clone() => Current;

        public static ChronoSettingsData Load()
        {
            ChronoSettingsData data = Defaults();
            try
            {
                string json = PlayerPrefs.GetString(StorageKey, "");
                if (!string.IsNullOrWhiteSpace(json))
                {
                    string trimmed = json.Trim();
                    if (!trimmed.StartsWith("{") || !trimmed.EndsWith("}"))
                        throw new FormatException("Settings must contain a JSON object.");
                    // Missing fields retain their version-one defaults.
                    JsonUtility.FromJsonOverwrite(trimmed, data);
                }
            }
            catch (Exception) { data = Defaults(); Debug.LogWarning("Chrono settings were unreadable; using defaults."); }
            Apply(data);
            return Current;
        }

        // Apply changes the session only. Save is the explicit persistent commit.
        public static void Apply(ChronoSettingsData data)
        {
            current = Sanitize(data);
            AudioListener.volume = current.masterVolume;
            QualitySettings.vSyncCount = current.vsync ? 1 : 0;
            Application.targetFrameRate = current.frameLimit == 0 ? 60 : current.frameLimit == 1 ? 120 : -1;
            // vSync takes precedence on desktop. The frame cap applies with vSync disabled.
            if (Application.isPlaying)
                Screen.fullScreenMode = current.displayMode == 0 ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
            ApplyQuality(current.quality);
        }
        public static void Save(ChronoSettingsData data)
        {
            Apply(data);
            PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(current));
            PlayerPrefs.Save();
        }
        static ChronoSettingsData Sanitize(ChronoSettingsData value)
        {
            var data = value != null ? value.Copy() : Defaults();
            data.masterVolume = FiniteClamp(data.masterVolume, 0, 1, .8f);
            data.effectsVolume = FiniteClamp(data.effectsVolume, 0, 1, .8f);
            data.sensitivity = FiniteClamp(data.sensitivity, .2f, 3, 1);
            data.fov = FiniteClamp(data.fov, 60, 100, 78);
            data.displayMode = Mathf.Clamp(data.displayMode, 0, 1);
            data.quality = Mathf.Clamp(data.quality, 0, 2);
            data.frameLimit = Mathf.Clamp(data.frameLimit, 0, 2);
            return data;
        }
        static float FiniteClamp(float value, float minimum, float maximum, float fallback)
            => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, minimum, maximum);

        static void ApplyQuality(int level)
        {
            if (!Application.isPlaying) return;
            int samples = level == 0 ? 1 : level == 1 ? 2 : 4;
            float distance = level == 0 ? 20 : level == 1 ? 35 : 55;
            // URP ignores several legacy QualitySettings fields, so tune its actual asset.
            // Clone it once in Play Mode: never write settings back into an authored asset.
            var pipeline = (QualitySettings.renderPipeline ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            if (pipeline)
            {
                if (!Application.isPlaying) return;
                if (!runtimePipeline)
                {
                    previousPipelineOverride = QualitySettings.renderPipeline;
                    runtimePipeline = UnityEngine.Object.Instantiate(pipeline);
                    runtimePipeline.name = "Chrono runtime quality";
                    runtimePipeline.hideFlags = HideFlags.DontSave;
                    QualitySettings.renderPipeline = runtimePipeline;
                    var lifetime = new GameObject("Chrono preferences lifetime");
                    lifetime.hideFlags = HideFlags.HideInHierarchy;
                    lifetime.AddComponent<ChronoPreferencesLifetime>();
                    UnityEngine.Object.DontDestroyOnLoad(lifetime);
                }
                runtimePipeline.shadowDistance = distance;
                runtimePipeline.mainLightShadowmapResolution = level == 0 ? 512 : level == 1 ? 1024 : 2048;
                runtimePipeline.msaaSampleCount = samples;
                runtimePipeline.renderScale = level == 0 ? .8f : 1;
            }
            else
            {
                QualitySettings.shadowDistance = distance;
                QualitySettings.shadowResolution = level == 0 ? UnityEngine.ShadowResolution.Low : level == 1 ? UnityEngine.ShadowResolution.Medium : UnityEngine.ShadowResolution.High;
                QualitySettings.antiAliasing = samples == 1 ? 0 : samples;
            }
        }
        internal static void ReleaseRuntimeQuality()
        {
            if (!runtimePipeline) return;
            if (QualitySettings.renderPipeline == runtimePipeline) QualitySettings.renderPipeline = previousPipelineOverride;
            UnityEngine.Object.Destroy(runtimePipeline);
            runtimePipeline = null; previousPipelineOverride = null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() { current = null; runtimePipeline = null; previousPipelineOverride = null; }
    }

    // Restores the authored quality asset when Play Mode or the application ends.
    public sealed class ChronoPreferencesLifetime : MonoBehaviour
    {
        void OnDestroy() { ChronoPreferences.ReleaseRuntimeQuality(); }
    }
}
