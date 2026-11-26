using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Game.Rendering;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Advanced_Graphics
{
    // HDRP effects relative to the game's current volume values; only changed parameters are overridden.
    internal sealed partial class GraphicsEffectOverrides
    {
        private const string HolderName = "Enhanced Graphics effects";
        private const int VolumeRefreshFrames = 120;

        private GameObject m_Holder;
        private Volume m_Volume;
        private VolumeProfile m_Profile;
        private ContactShadows m_Contact;
        private ScreenSpaceAmbientOcclusion m_Ao;
        private HDShadowSettings m_Cascades;
        private Exposure m_Exposure;
        private Fog m_Fog;
        private readonly List<Volume> m_GameVolumes = new List<Volume>();
        private readonly List<bool> m_CameraVolumes = new List<bool>();
        private bool m_FromCamera;
        private int m_VolumeRefresh;
        private int m_LastLog;

        private HDAdditionalLightData m_ContactSun;
        private bool m_SunTouched, m_SunBaseUseOverride, m_SunBaseOverride;
        private HDAdditionalCameraData m_FrameCamera;
        private bool m_FrameTouched, m_FrameBaseCustom, m_FrameBaseMask, m_FrameBaseValue;

        // Values the game currently uses (-1 = not found / off), shown in the graphics window.
        public float GameAoIntensity { get; private set; } = -1f;
        public float GameAoRadius { get; private set; } = -1f;
        public float GameFogDistance { get; private set; } = -1f;
        public float GameFirstCascade { get; private set; } = -1f;

        public static bool IsActive(Setting s) =>
            s != null && (s.ContactShadowsEnabled || s.AmbientOcclusionStrengthPercent != 100 ||
            s.AmbientOcclusionRadiusPercent != 100 || s.ShadowCascadeFocus > 0 ||
            Math.Abs(s.ExposureCompensation) > 0.001f || s.ExposureAdaptationPercent != 100 ||
            s.FogDensityPercent != 100);

        public void Apply(Setting s, HDAdditionalLightData sun, HDAdditionalCameraData camera, CameraUpdateSystem cameraUpdate)
        {
            if (cameraUpdate != null) m_CameraUpdate = cameraUpdate;
            if (s == null || !s.GraphicsEnabled)
            {
                Release();
                return;
            }
            if (--m_VolumeRefresh <= 0) RefreshGameVolumes();
            ReadGameValues();
            bool distance = ApplyShadowDistance(s);
            ApplyShadowResolution(s, sun);
            DebugReport(s, sun, camera);
            if (!IsActive(s) && !distance)
            {
                Release();
                return;
            }
            EnsureVolume();
            ApplyContactShadows(s, sun, camera);
            ApplyAmbientOcclusion(s);
            ApplyCascades(s, distance);
            ApplyExposure(s);
            ApplyFog(s);
            m_Volume.enabled = true;
            LogChange(s);
        }

        private void EnsureVolume()
        {
            if (m_Volume != null) return;
            m_Holder = new GameObject(HolderName) { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(m_Holder);
            m_Volume = m_Holder.AddComponent<Volume>();
            m_Volume.isGlobal = true;
            m_Volume.priority = 10000f;
            m_Volume.weight = 1f;
            m_Profile = ScriptableObject.CreateInstance<VolumeProfile>();
            m_Profile.hideFlags = HideFlags.HideAndDontSave;
            m_Volume.sharedProfile = m_Profile;
            m_Contact = m_Profile.Add<ContactShadows>();
            m_Ao = m_Profile.Add<ScreenSpaceAmbientOcclusion>();
            m_Cascades = m_Profile.Add<HDShadowSettings>();
            m_Exposure = m_Profile.Add<Exposure>();
            m_Fog = m_Profile.Add<Fog>();
            m_VolumeRefresh = 0;
        }

        // The mod's own volumes are left out, so values are always relative to the game (or another mod).
        private void RefreshGameVolumes()
        {
            m_VolumeRefresh = VolumeRefreshFrames;
            m_GameVolumes.Clear();
            m_CameraVolumes.Clear();
            Volume[] volumes;
            try { volumes = VolumeManager.instance.GetVolumes(~0); }
            catch (Exception) { return; }
            float highest = float.MinValue;
            foreach (var volume in volumes)
            {
                if (volume == null || !volume.isGlobal) continue;
                var name = volume.gameObject.name;
                if (name.StartsWith("Enhanced Graphics", StringComparison.Ordinal) ||
                    name.StartsWith("Advanced Graphics", StringComparison.Ordinal)) continue;
                m_GameVolumes.Add(volume);
                highest = Mathf.Max(highest, volume.priority);
                // The game's camera controller rewrites shadow distance and cascades every frame.
                m_CameraVolumes.Add(name == "CameraControllerVolume");
            }
            // Other mods (e.g. Lumina) bring their own volumes: stay above all of them.
            if (m_Volume != null) m_Volume.priority = Mathf.Max(10000f, highest + 1f);
        }

        // Highest-priority active global volume that overrides the parameter; null = HDRP default.
        private T GameComponent<T>(Func<T, VolumeParameter> parameter, bool skipCamera = false) where T : VolumeComponent
        {
            m_FromCamera = false;
            for (int i = m_GameVolumes.Count - 1; i >= 0; i--)
            {
                if (skipCamera && m_CameraVolumes[i]) continue;
                var volume = m_GameVolumes[i];
                if (volume == null || !volume.enabled || volume.weight <= 0f || !volume.gameObject.activeInHierarchy) continue;
                var profile = volume.HasInstantiatedProfile() ? volume.profile : volume.sharedProfile;
                if (profile == null || !profile.TryGet<T>(out var component) || component == null || !component.active) continue;
                if (!parameter(component).overrideState) continue;
                m_FromCamera = m_CameraVolumes[i];
                return component;
            }
            return null;
        }

        private void ReadGameValues()
        {
            var ao = GameComponent<ScreenSpaceAmbientOcclusion>(c => c.intensity);
            GameAoIntensity = ao != null ? ao.intensity.value : -1f;
            var radius = GameComponent<ScreenSpaceAmbientOcclusion>(c => c.radius);
            GameAoRadius = radius != null ? radius.radius.value : -1f;
            var fogOn = GameComponent<Fog>(c => c.enabled);
            var fog = GameComponent<Fog>(c => c.meanFreePath);
            GameFogDistance = fogOn != null && fogOn.enabled.value ? (fog != null ? fog.meanFreePath.value : 400f) : -1f;
            var split = GameComponent<HDShadowSettings>(c => c.cascadeShadowSplit0);
            GameFirstCascade = split != null ? split.cascadeShadowSplit0.value : -1f;
        }

        private static void Set(VolumeParameter<float> parameter, bool use, float value)
        {
            if (use) parameter.Override(value);
            else parameter.overrideState = false;
        }

        private void ApplyContactShadows(Setting s, HDAdditionalLightData sun, HDAdditionalCameraData camera)
        {
            bool on = s.ContactShadowsEnabled;
            m_Contact.active = on;
            if (on)
            {
                float distance = Mathf.Clamp(s.ContactShadowDistance, 10f, 2000f);
                m_Contact.enable.Override(true);
                m_Contact.length.Override(Mathf.Clamp(s.ContactShadowLength, 0.01f, 1f));
                m_Contact.opacity.Override(Mathf.Clamp01(s.ContactShadowOpacityPercent / 100f));
                m_Contact.maxDistance.Override(distance);
                m_Contact.fadeDistance.Override(Mathf.Max(5f, distance * 0.1f));
            }
            ApplySunContact(on ? sun : null);
            ApplyCameraContact(on ? camera : null);
        }

        // The sun only casts contact shadows with its own flag set.
        private void ApplySunContact(HDAdditionalLightData sun)
        {
            if (sun != m_ContactSun) RestoreSunContact();
            m_ContactSun = sun;
            if (sun == null) return;
            var flag = sun.useContactShadow;
            if (flag == null) return;
            if (!m_SunTouched)
            {
                m_SunBaseUseOverride = flag.useOverride;
                m_SunBaseOverride = flag.@override;
                m_SunTouched = true;
            }
            if (!flag.useOverride) flag.useOverride = true;
            if (!flag.@override) flag.@override = true;
        }

        private void RestoreSunContact()
        {
            if (m_ContactSun != null && m_SunTouched)
            {
                var flag = m_ContactSun.useContactShadow;
                if (flag != null)
                {
                    flag.useOverride = m_SunBaseUseOverride;
                    flag.@override = m_SunBaseOverride;
                }
            }
            m_SunTouched = false;
            m_ContactSun = null;
        }

        // Enables only the contact-shadow frame setting of the camera; all other frame settings stay untouched.
        private void ApplyCameraContact(HDAdditionalCameraData camera)
        {
            if (camera != m_FrameCamera) RestoreCameraContact();
            m_FrameCamera = camera;
            if (camera == null) return;
            const uint field = (uint)FrameSettingsField.ContactShadows;
            if (!m_FrameTouched)
            {
                m_FrameBaseCustom = camera.customRenderingSettings;
                m_FrameBaseMask = camera.renderingPathCustomFrameSettingsOverrideMask.mask[field];
                m_FrameBaseValue = camera.renderingPathCustomFrameSettings.IsEnabled(FrameSettingsField.ContactShadows);
                m_FrameTouched = true;
            }
            if (!camera.customRenderingSettings) camera.customRenderingSettings = true;
            if (!camera.renderingPathCustomFrameSettingsOverrideMask.mask[field])
                camera.renderingPathCustomFrameSettingsOverrideMask.mask[field] = true;
            if (!camera.renderingPathCustomFrameSettings.IsEnabled(FrameSettingsField.ContactShadows))
                camera.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.ContactShadows, true);
        }

        private void RestoreCameraContact()
        {
            var camera = m_FrameCamera;
            if (camera != null && m_FrameTouched)
            {
                const uint field = (uint)FrameSettingsField.ContactShadows;
                camera.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.ContactShadows, m_FrameBaseValue);
                camera.renderingPathCustomFrameSettingsOverrideMask.mask[field] = m_FrameBaseMask;
                camera.customRenderingSettings = m_FrameBaseCustom;
            }
            m_FrameTouched = false;
            m_FrameCamera = null;
        }

        // Scales the game's AO; with AO off in the game (intensity 0) it stays off.
        private void ApplyAmbientOcclusion(Setting s)
        {
            bool strength = s.AmbientOcclusionStrengthPercent != 100;
            bool radius = s.AmbientOcclusionRadiusPercent != 100;
            m_Ao.active = strength || radius;
            if (!m_Ao.active) return;
            float baseIntensity = GameAoIntensity >= 0f ? GameAoIntensity : 0f;
            float baseRadius = GameAoRadius > 0f ? GameAoRadius : 2f;
            // Game value not found: leave it to the game instead of switching AO off.
            Set(m_Ao.intensity, strength && GameAoIntensity >= 0f, Mathf.Clamp(baseIntensity * s.AmbientOcclusionStrengthPercent / 100f, 0f, 4f));
            Set(m_Ao.radius, radius && GameAoRadius > 0f, Mathf.Clamp(baseRadius * s.AmbientOcclusionRadiusPercent / 100f, 0.25f, 5f));
        }

        // The game's camera rewrites distance and cascades every frame; keep its cascade metres, move only the far end.
        private void ApplyCascades(Setting s, bool distance)
        {
            int focus = Mathf.Clamp(s.ShadowCascadeFocus, 0, 100);
            m_Cascades.active = focus > 0 || distance;
            var gw = GameComponent<HDShadowSettings>(c => c.maxShadowDistance);
            float gameDistance = gw != null ? gw.maxShadowDistance.value : 500f;
            float target = distance ? Mathf.Max(50f, gameDistance * Mathf.Clamp(s.ShadowDistancePercent, 25, 200) / 100f) : gameDistance;
            Set(m_Cascades.maxShadowDistance, distance, target);
            if (!m_Cascades.active || target <= 0f)
            {
                m_Cascades.cascadeShadowSplit0.overrideState = false;
                m_Cascades.cascadeShadowSplit1.overrideState = false;
                m_Cascades.cascadeShadowSplit2.overrideState = false;
                return;
            }
            var g0 = GameComponent<HDShadowSettings>(c => c.cascadeShadowSplit0);
            var g1 = GameComponent<HDShadowSettings>(c => c.cascadeShadowSplit1);
            var g2 = GameComponent<HDShadowSettings>(c => c.cascadeShadowSplit2);
            float scale = (1f - 0.75f * focus / 100f) * gameDistance / target;
            float a = Mathf.Min((g0 != null ? g0.cascadeShadowSplit0.value : 0.05f) * scale, 0.97f);
            float b = Mathf.Min(Mathf.Max((g1 != null ? g1.cascadeShadowSplit1.value : 0.15f) * scale, a + 0.0001f), 0.98f);
            float c2 = Mathf.Min(Mathf.Max((g2 != null ? g2.cascadeShadowSplit2.value : 0.3f) * scale, b + 0.0001f), 0.99f);
            // The split values clamp against their neighbours, so set them in both directions.
            for (int i = 0; i < 2; i++)
            {
                m_Cascades.cascadeShadowSplit2.Override(c2);
                m_Cascades.cascadeShadowSplit1.Override(b);
                m_Cascades.cascadeShadowSplit0.Override(a);
                m_Cascades.cascadeShadowSplit1.Override(b);
                m_Cascades.cascadeShadowSplit2.Override(c2);
            }
        }

        private void ApplyExposure(Setting s)
        {
            bool compensation = Math.Abs(s.ExposureCompensation) > 0.001f;
            bool adaptation = s.ExposureAdaptationPercent != 100;
            m_Exposure.active = compensation || adaptation;
            if (!m_Exposure.active) return;
            var gc = GameComponent<Exposure>(c => c.compensation);
            var gd = GameComponent<Exposure>(c => c.adaptationSpeedDarkToLight);
            var gl = GameComponent<Exposure>(c => c.adaptationSpeedLightToDark);
            // Positive values brighten the image (HDRP subtracts the compensation from EV100).
            float ev = Mathf.Clamp(s.ExposureCompensation, -3f, 3f);
            Set(m_Exposure.compensation, compensation, (gc != null ? gc.compensation.value : 0f) + ev);
            // Automatic exposure clamps after the compensation; the game's day/night limits would swallow it.
            var gmin = GameComponent<Exposure>(c => c.limitMin);
            var gmax = GameComponent<Exposure>(c => c.limitMax);
            Set(m_Exposure.limitMin, compensation, (gmin != null ? gmin.limitMin.value : -1f) - ev);
            Set(m_Exposure.limitMax, compensation, (gmax != null ? gmax.limitMax.value : 14f) - ev);
            float speed = Mathf.Clamp(s.ExposureAdaptationPercent, 10, 500) / 100f;
            Set(m_Exposure.adaptationSpeedDarkToLight, adaptation,
                Mathf.Max(0.001f, (gd != null ? gd.adaptationSpeedDarkToLight.value : 3f) * speed));
            Set(m_Exposure.adaptationSpeedLightToDark, adaptation,
                Mathf.Max(0.001f, (gl != null ? gl.adaptationSpeedLightToDark.value : 1f) * speed));
        }

        // Density relative to the game's fog: 200 % halves the visibility distance, 0 % removes the fog.
        private void ApplyFog(Setting s)
        {
            int percent = Mathf.Clamp(s.FogDensityPercent, 0, 400);
            m_Fog.active = percent != 100;
            if (!m_Fog.active) return;
            if (percent == 0)
            {
                m_Fog.enabled.Override(false);
                m_Fog.meanFreePath.overrideState = false;
                return;
            }
            m_Fog.enabled.overrideState = false;
            float distance = GameFogDistance > 0f ? GameFogDistance : 400f;
            m_Fog.meanFreePath.Override(Mathf.Max(1f, distance * 100f / percent));
        }

        private void LogChange(Setting s)
        {
            // Only setting changes are logged; the game's values (fog, exposure) change with time and weather.
            int key;
            unchecked
            {
                key = s.ContactShadowsEnabled ? 1 : 2;
                key = key * 31 + Mathf.RoundToInt(s.ContactShadowLength * 100f);
                key = key * 31 + s.ContactShadowOpacityPercent;
                key = key * 31 + s.ContactShadowDistance;
                key = key * 31 + s.AmbientOcclusionStrengthPercent;
                key = key * 31 + s.AmbientOcclusionRadiusPercent;
                key = key * 31 + s.ShadowCascadeFocus;
                key = key * 31 + Mathf.RoundToInt(s.ExposureCompensation * 100f);
                key = key * 31 + s.ExposureAdaptationPercent;
                key = key * 31 + s.FogDensityPercent;
            }
            if (key == m_LastLog) return;
            m_LastLog = key;
            // Runs in the rendering phase: the game's logger must not be called directly here.
            Mod.LogLater(string.Format(CultureInfo.InvariantCulture,
                "Graphics effects: contact {0} ({1:0.00} m, {2} %, {3} m), AO {4} %/{5} % (game {6:0.##}/{7:0.##}), " +
                "cascade focus {8} (game {9:0.###}), exposure {10:+0.00;-0.00;0} EV / {11} %, fog {12} % (game {13:0} m)",
                s.ContactShadowsEnabled ? "on" : "off", s.ContactShadowLength, s.ContactShadowOpacityPercent, s.ContactShadowDistance,
                s.AmbientOcclusionStrengthPercent, s.AmbientOcclusionRadiusPercent, GameAoIntensity, GameAoRadius,
                s.ShadowCascadeFocus, GameFirstCascade, s.ExposureCompensation, s.ExposureAdaptationPercent,
                s.FogDensityPercent, GameFogDistance));
        }

        // Effects off: release the overrides but keep the volume for the next change.
        private void Release()
        {
            if (m_Volume != null) m_Volume.enabled = false;
            RestoreSunContact();
            RestoreCameraContact();
            m_LastLog = 0;
        }

        public void Restore()
        {
            Release();
            if (m_Holder != null) UnityEngine.Object.Destroy(m_Holder);
            if (m_Profile != null)
            {
                foreach (var component in m_Profile.components)
                    if (component != null) UnityEngine.Object.Destroy(component);
                UnityEngine.Object.Destroy(m_Profile);
            }
            m_Holder = null;
            m_Volume = null;
            m_Profile = null;
            m_Contact = null;
            m_Ao = null;
            m_Cascades = null;
            m_Exposure = null;
            m_Fog = null;
            m_GameVolumes.Clear();
            m_VolumeRefresh = 0;
        }
    }
}
