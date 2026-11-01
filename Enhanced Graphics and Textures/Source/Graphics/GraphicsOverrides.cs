using System;
using Game.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Advanced_Graphics
{
    //Applies only graphics settings that extend the game's options.
    internal sealed class GraphicsOverrides
    {
        private readonly GraphicsLightingOverrides m_Lighting = new GraphicsLightingOverrides();
        private float m_BaseDetail;
        private int m_BaseCache;
        private int m_BaseTessellation;
        private int m_BasePatchSize;
        private int m_BaseShadowResolution;
        private int m_DefaultShadowResolution;
        private float m_BaseShadowCullingHeight, m_BaseShadowCullingVolume;
        private bool m_Captured;
        private bool m_TouchedDetail, m_TouchedCache, m_TouchedTessellation,
            m_TouchedPatch, m_TouchedShadowResolution, m_TouchedShadowCulling;
        private float m_LastAppliedDetail;
        private int m_LastAppliedCache;
        private float m_ObservedDetail;
        private int m_ObservedCache, m_ObservedTessellation, m_ObservedPatch, m_ObservedShadowResolution;
        private HDAdditionalCameraData m_TaaCamera;
        private float m_BaseTaaSharpen, m_LastTaaSharpen;
        private float m_BaseTaaAntiFlicker;
        private float m_LastTaaAntiFlicker;
        private bool m_TouchedTaaStability;
        private bool m_TouchedTaa;
        private GameObject m_ShadowHolder;
        private Volume m_ShadowVolume;
        private VolumeProfile m_ShadowProfile;
        private HDShadowSettings m_ShadowSettings;
        private int m_LastShadowDistance;

        public string BaseDetail => m_Captured ? m_BaseDetail.ToString("0.00") : "?";
        public string BaseCache => m_Captured ? m_BaseCache.ToString() : "?";
        public string BaseTessellation => m_Captured ? m_BaseTessellation.ToString() : "?";
        public string BasePatchSize => m_Captured ? m_BasePatchSize.ToString() : "?";
        public string BaseShadowResolution => m_Captured ? m_BaseShadowResolution.ToString() : "?";
        public string BaseShadowCulling => m_Captured ?
            $"{m_BaseShadowCullingHeight:0.##} / {m_BaseShadowCullingVolume:0.##}" : "?";
        public int? DefaultCache => m_Captured ? m_BaseCache : (int?)null;
        public int? DefaultTessellation => m_Captured ? m_BaseTessellation : (int?)null;
        public int? DefaultPatchSize => m_Captured ? m_BasePatchSize : (int?)null;
        public int? DefaultShadowResolution => m_Captured ? m_DefaultShadowResolution : (int?)null;

        public void Capture()
        {
            if (m_Captured) return;
            var graphics = SharedSettings.instance?.graphics;
            var lod = graphics?.GetQualitySetting<LevelOfDetailQualitySettings>();
            var terrain = graphics?.GetQualitySetting<TerrainQualitySettings>();
            var shadows = graphics?.GetQualitySetting<ShadowsQualitySettings>();
            if (lod == null || terrain == null || shadows == null) return;
            m_BaseDetail = Math.Max(0.1f, Math.Min(1f, lod.levelOfDetail));
            m_BaseCache = lod.meshMemoryBudget;
            m_BaseTessellation = terrain.finalTessellation;
            m_BasePatchSize = terrain.targetPatchSize;
            m_BaseShadowResolution = shadows.directionalShadowResolution;
            m_DefaultShadowResolution = GetDefaultShadowResolution(shadows);
            m_BaseShadowCullingHeight = shadows.shadowCullingThresholdHeight;
            m_BaseShadowCullingVolume = shadows.shadowCullingThresholdVolume;
            m_Captured = true;
            ObserveGameValues();
            Mod.log.Info($"Graphics baseline: detail {m_BaseDetail:0.00}, cache {m_BaseCache} MB, " +
                $"terrain {m_BaseTessellation}/{m_BasePatchSize}, shadow resolution {m_BaseShadowResolution}");
        }

        public void Apply(Setting options)
        {
            m_Lighting.ApplyVolume(options);
            Capture();
            if (!m_Captured) return;
            var graphics = SharedSettings.instance?.graphics;
            if (graphics == null) return;

            var lod = graphics.GetQualitySetting<LevelOfDetailQualitySettings>();
            if (lod != null)
            {
                var percent = Math.Max(10, Math.Min(300, options.GlobalDetailScalePercent));

                if (m_TouchedDetail && Math.Abs(lod.levelOfDetail - m_LastAppliedDetail) > 0.0001f)
                {
                    m_BaseDetail = Math.Max(0.1f, Math.Min(1f, lod.levelOfDetail));
                    m_TouchedDetail = false;
                }
                if (!m_TouchedDetail && percent == 100)
                    m_BaseDetail = Math.Max(0.1f, Math.Min(1f, lod.levelOfDetail));

                if (percent == 100 && lod.levelOfDetail > 3f)
                {
                    lod.levelOfDetail = m_BaseDetail;
                    lod.Apply();
                    Mod.log.Info($"Recovered out-of-range graphics LOD to {m_BaseDetail:0.00}");
                }

                if (percent != 100 || m_TouchedDetail)
                {
                    var detail = percent == 100 ? m_BaseDetail : m_BaseDetail * percent / 100f;
                    if (Math.Abs(lod.levelOfDetail - detail) > 0.0001f)
                    {
                        lod.levelOfDetail = detail;
                        lod.Apply();
                        Mod.log.Info($"Graphics LOD {detail:0.00} (base {m_BaseDetail:0.00}, {percent}%)");
                    }
                    m_LastAppliedDetail = detail;
                    m_TouchedDetail = percent != 100;
                }

                var cache = Math.Max(128, Math.Min(16384, options.GeometryCacheSizeMB));
                if (m_TouchedCache && lod.meshMemoryBudget != m_LastAppliedCache)
                {
                    m_BaseCache = lod.meshMemoryBudget;
                    m_TouchedCache = false;
                }
                if (!m_TouchedCache && cache == Setting.DefaultCacheMB)
                    m_BaseCache = lod.meshMemoryBudget;
                if (cache != Setting.DefaultCacheMB || m_TouchedCache)
                {
                    var target = cache == Setting.DefaultCacheMB ? m_BaseCache : cache;
                    if (lod.meshMemoryBudget != target)
                    {
                        lod.meshMemoryBudget = target;
                        lod.Apply();
                        Mod.log.Info($"Graphics geometry cache {target} MB");
                    }
                    m_LastAppliedCache = target;
                    m_TouchedCache = cache != Setting.DefaultCacheMB;
                }
            }

            var terrain = graphics.GetQualitySetting<TerrainQualitySettings>();
            if (terrain != null)
            {
                var tessellation = Math.Max(1, Math.Min(16, options.TerrainTessellationLevel));
                var patch = Math.Max(4, Math.Min(128, options.TerrainPatchSizeUnits));
                if (terrain.finalTessellation != tessellation || terrain.targetPatchSize != patch)
                {
                    terrain.finalTessellation = tessellation;
                    terrain.targetPatchSize = patch;
                    terrain.Apply();
                    Mod.log.Info($"Graphics terrain tessellation {tessellation}, patch size {patch}");
                }
                m_TouchedTessellation |= tessellation != m_BaseTessellation;
                m_TouchedPatch |= patch != m_BasePatchSize;
            }

            var shadows = graphics.GetQualitySetting<ShadowsQualitySettings>();
            if (shadows != null)
            {
                var threshold = Math.Max(0, Math.Min(10, options.SmallShadowCasterThreshold));
                var height = threshold == 0 ? m_BaseShadowCullingHeight : threshold;
                var volume = threshold == 0 ? m_BaseShadowCullingVolume : threshold;
                if (shadows.shadowCullingThresholdHeight != height ||
                    shadows.shadowCullingThresholdVolume != volume)
                {
                    shadows.shadowCullingThresholdHeight = height;
                    shadows.shadowCullingThresholdVolume = volume;
                    shadows.Apply();
                    Mod.log.Info($"Graphics shadow caster culling height {height:0.##}, volume {volume:0.##}");
                }
                m_TouchedShadowCulling |= threshold > 0;
                var resolution = options.ShadowResolution == 0 ? m_DefaultShadowResolution :
                    Math.Max(512, Math.Min(8192, options.ShadowResolution));
                if (shadows.directionalShadowResolution != resolution)
                {
                    shadows.directionalShadowResolution = resolution;
                    shadows.Apply();
                    // Some older builds did not pass resolution through Apply().
                    var sun = GameObject.FindGameObjectWithTag("SunLight");
                    var light = sun != null ? sun.GetComponent<HDAdditionalLightData>() : null;
                    if (light != null && resolution > 0)
                    {
                        light.SetShadowResolutionOverride(true);
                        light.SetShadowResolution(resolution);
                    }
                    Mod.log.Info($"Graphics shadow resolution {resolution}");
                }
                m_TouchedShadowResolution |= resolution != m_BaseShadowResolution;
            }

            ApplyShadowDistance(options.ShadowDistanceMeters);
            ApplyCamera(options);
            ObserveGameValues();
        }

        public void SyncFromGame(Setting options)
        {
            if (options == null) return;
            Capture();
            if (!m_Captured) return;
            var graphics = SharedSettings.instance?.graphics;
            var lod = graphics?.GetQualitySetting<LevelOfDetailQualitySettings>();
            var terrain = graphics?.GetQualitySetting<TerrainQualitySettings>();
            var shadows = graphics?.GetQualitySetting<ShadowsQualitySettings>();
            if (lod == null || terrain == null || shadows == null) return;

            bool changed = false;
            if (Math.Abs(lod.levelOfDetail - m_ObservedDetail) > 0.0001f)
            {
                m_BaseDetail = Math.Max(0.1f, Math.Min(1f, lod.levelOfDetail));
                m_TouchedDetail = false;
                options.GlobalDetailScalePercent = 100;
                changed = true;
            }
            if (lod.meshMemoryBudget != m_ObservedCache)
            {
                m_BaseCache = lod.meshMemoryBudget;
                m_TouchedCache = false;
                options.GeometryCacheSizeMB = lod.meshMemoryBudget;
                changed = true;
            }
            if (terrain.finalTessellation != m_ObservedTessellation)
            {
                m_BaseTessellation = terrain.finalTessellation;
                options.TerrainTessellationLevel = terrain.finalTessellation;
                changed = true;
            }
            if (terrain.targetPatchSize != m_ObservedPatch)
            {
                m_BasePatchSize = terrain.targetPatchSize;
                options.TerrainPatchSizeUnits = terrain.targetPatchSize;
                changed = true;
            }
            if (shadows.directionalShadowResolution != m_ObservedShadowResolution)
            {
                m_BaseShadowResolution = shadows.directionalShadowResolution;
                m_DefaultShadowResolution = GetDefaultShadowResolution(shadows);
                options.ShadowResolution = shadows.directionalShadowResolution;
                changed = true;
            }
            if (changed)
            {
                ObserveGameValues();
                options.ApplyAndSave();
                Mod.log.Info("Advanced Graphics synchronized changes from the game graphics menu");
            }
        }

        private static int GetDefaultShadowResolution(ShadowsQualitySettings shadows)
        {
            var defaults = DefaultSettingsProvider.CreateShadowDefaults(shadows.GetLevel());
            // A custom/off preset has no positive resolution default.
            if (defaults == null || defaults.directionalShadowResolution <= 0)
                defaults = DefaultSettingsProvider.CreateShadowDefaults(QualitySetting.Level.High);
            return defaults.directionalShadowResolution;
        }

        private void ObserveGameValues()
        {
            var graphics = SharedSettings.instance?.graphics;
            var lod = graphics?.GetQualitySetting<LevelOfDetailQualitySettings>();
            var terrain = graphics?.GetQualitySetting<TerrainQualitySettings>();
            var shadows = graphics?.GetQualitySetting<ShadowsQualitySettings>();
            if (lod != null)
            {
                m_ObservedDetail = lod.levelOfDetail;
                m_ObservedCache = lod.meshMemoryBudget;
            }
            if (terrain != null)
            {
                m_ObservedTessellation = terrain.finalTessellation;
                m_ObservedPatch = terrain.targetPatchSize;
            }
            if (shadows != null) m_ObservedShadowResolution = shadows.directionalShadowResolution;
        }

        public void ApplyCamera(Setting options)
        {
            if (options == null) return;
            var camera = Camera.main;
            var data = camera != null ? camera.GetComponent<HDAdditionalCameraData>() : null;
            if (data != m_TaaCamera)
            {
                RestoreTaa();
                m_TaaCamera = data;
                if (data != null)
                {
                    m_BaseTaaSharpen = data.taaSharpenStrength;
                    CaptureTaaStability(data);
                }
            }
            if (data == null) return;
            if (!m_TouchedTaa || !Mathf.Approximately(data.taaSharpenStrength, m_LastTaaSharpen))
                m_BaseTaaSharpen = data.taaSharpenStrength;
            if (options.TaaSharpenStrength <= 0)
            {
                RestoreTaaSharpen();
            }
            else
            {
                var strength = Math.Max(0.05f, Math.Min(2f, options.TaaSharpenStrength));
                if (data.taaSharpenStrength != strength) data.taaSharpenStrength = strength;
                m_LastTaaSharpen = data.taaSharpenStrength;
                m_TouchedTaa = true;
            }

            // Do not change the game's AA method or take over DLSS/FSR2.
            var graphics = SharedSettings.instance?.graphics;
            if (!options.TaaStabilityEnabled ||
                data.antialiasing != HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing ||
                (graphics != null && (graphics.isDlssActive || graphics.isFsr2Active)))
            {
                RestoreTaaStability();
                return;
            }
            if (!m_TouchedTaaStability) CaptureTaaStability(data);
            else
            {
                if (!Mathf.Approximately(data.taaAntiFlicker, m_LastTaaAntiFlicker)) m_BaseTaaAntiFlicker = data.taaAntiFlicker;
            }
            // Use the actual HDRP value; leave quality, history sharpening and
            // ringing protection at the game's values so the slider isolates one parameter.
            var value = options.TaaStabilityStrength;
            data.taaAntiFlicker = float.IsNaN(value) || float.IsInfinity(value) ? 0.5f : Mathf.Clamp01(value);
            m_LastTaaAntiFlicker = data.taaAntiFlicker;
            m_TouchedTaaStability = true;
        }

        private void CaptureTaaStability(HDAdditionalCameraData data)
        {
            m_BaseTaaAntiFlicker = data.taaAntiFlicker;
        }

        private void RestoreTaaSharpen()
        {
            if (m_TaaCamera != null && m_TouchedTaa && Mathf.Approximately(m_TaaCamera.taaSharpenStrength, m_LastTaaSharpen))
                m_TaaCamera.taaSharpenStrength = m_BaseTaaSharpen;
            m_TouchedTaa = false;
        }

        private void RestoreTaaStability()
        {
            var data = m_TaaCamera;
            if (data != null && m_TouchedTaaStability)
            {
                if (Mathf.Approximately(data.taaAntiFlicker, m_LastTaaAntiFlicker)) data.taaAntiFlicker = m_BaseTaaAntiFlicker;
            }
            m_TouchedTaaStability = false;
        }

        private void RestoreTaa()
        {
            RestoreTaaSharpen();
            RestoreTaaStability();
        }

        private void ApplyShadowDistance(int meters)
        {
            if (meters <= 0)
            {
                if (m_ShadowVolume != null) m_ShadowVolume.enabled = false;
                m_LastShadowDistance = 0;
                return;
            }
            if (m_ShadowVolume == null)
            {
                m_ShadowHolder = new GameObject("Advanced Graphics shadow reach") { hideFlags = HideFlags.HideAndDontSave };
                UnityEngine.Object.DontDestroyOnLoad(m_ShadowHolder);
                m_ShadowVolume = m_ShadowHolder.AddComponent<Volume>();
                m_ShadowVolume.isGlobal = true;
                m_ShadowVolume.priority = 10000f;
                m_ShadowVolume.weight = 1f;
                m_ShadowProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                m_ShadowProfile.hideFlags = HideFlags.HideAndDontSave;
                m_ShadowVolume.sharedProfile = m_ShadowProfile;
                m_ShadowSettings = m_ShadowProfile.Add<HDShadowSettings>();
            }
            var distance = Math.Max(50, Math.Min(2000, meters));
            if (m_LastShadowDistance != distance)
            {
                m_ShadowSettings.maxShadowDistance.Override(distance);
                Mod.log.Info($"Graphics shadow distance {distance} m");
                m_LastShadowDistance = distance;
            }
            m_ShadowVolume.enabled = true;
        }

        public void ApplyLighting(Setting options, HDAdditionalLightData sun)
        {
            m_Lighting.ApplyVolume(options);
            m_Lighting.ApplySun(options, sun);
        }

        public void Restore()
        {
            m_Lighting.Restore();
            RestoreTaa();
            m_TaaCamera = null;
            if (m_ShadowHolder != null) UnityEngine.Object.Destroy(m_ShadowHolder);
            if (m_ShadowProfile != null) UnityEngine.Object.Destroy(m_ShadowProfile);
            m_ShadowHolder = null;
            m_ShadowVolume = null;
            m_ShadowProfile = null;
            m_ShadowSettings = null;
            if (!m_Captured) return;
            var graphics = SharedSettings.instance?.graphics;
            var lod = graphics?.GetQualitySetting<LevelOfDetailQualitySettings>();
            if (lod != null && (m_TouchedDetail || m_TouchedCache))
            {
                if (m_TouchedDetail) lod.levelOfDetail = m_BaseDetail;
                if (m_TouchedCache) lod.meshMemoryBudget = m_BaseCache;
                lod.Apply();
            }
            var terrain = graphics?.GetQualitySetting<TerrainQualitySettings>();
            if (terrain != null && (m_TouchedTessellation || m_TouchedPatch))
            {
                if (m_TouchedTessellation) terrain.finalTessellation = m_BaseTessellation;
                if (m_TouchedPatch) terrain.targetPatchSize = m_BasePatchSize;
                terrain.Apply();
            }
            var shadows = graphics?.GetQualitySetting<ShadowsQualitySettings>();
            if (shadows != null && (m_TouchedShadowResolution || m_TouchedShadowCulling))
            {
                if (m_TouchedShadowCulling)
                {
                    shadows.shadowCullingThresholdHeight = m_BaseShadowCullingHeight;
                    shadows.shadowCullingThresholdVolume = m_BaseShadowCullingVolume;
                }
                if (m_TouchedShadowResolution)
                    shadows.directionalShadowResolution = m_BaseShadowResolution;
                shadows.Apply();
                if (m_TouchedShadowResolution)
                {
                    var sun = GameObject.FindGameObjectWithTag("SunLight");
                    var light = sun != null ? sun.GetComponent<HDAdditionalLightData>() : null;
                    if (light != null && m_BaseShadowResolution > 0)
                    {
                        light.SetShadowResolutionOverride(true);
                        light.SetShadowResolution(m_BaseShadowResolution);
                    }
                }
            }
        }
    }
}



