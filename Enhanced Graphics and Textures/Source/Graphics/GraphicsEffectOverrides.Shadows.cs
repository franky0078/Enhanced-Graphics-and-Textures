using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using Game.Rendering;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Advanced_Graphics
{
    // Shadow distance and resolution, applied every frame because the game resets both.
    internal sealed partial class GraphicsEffectOverrides
    {
        private static readonly FieldInfo s_StoredShadows = typeof(CameraUpdateSystem).GetField(
            "m_StoredShadowSplitsAndDistance", BindingFlags.Instance | BindingFlags.NonPublic);

        private CameraUpdateSystem m_CameraUpdate;
        private HDAdditionalLightData m_ResolutionSun;
        private int m_ResolutionCap = -1, m_LastResolution;

        // Current shadow range of the game (follows what the camera sees).
        public float GameShadowDistance { get; private set; } = -1f;
        public int ShadowResolutionCap => m_ResolutionCap;

        private bool ApplyShadowDistance(Setting s)
        {
            var gw = GameComponent<HDShadowSettings>(c => c.maxShadowDistance);
            GameShadowDistance = gw != null ? gw.maxShadowDistance.value : -1f;
            return s.ShadowDistancePercent != 100 && GameShadowDistance > 0f;
        }

        // The asset limits the sun's shadow map (HDRP reads it once at start); higher values have no effect.
        private int ResolutionCap()
        {
            if (m_ResolutionCap >= 0) return m_ResolutionCap;
            m_ResolutionCap = 0;
            try
            {
                if (GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset asset)
                    m_ResolutionCap = asset.currentPlatformRenderPipelineSettings.hdShadowInitParams.maxDirectionalShadowMapResolution;
            }
            catch (Exception) { }
            Mod.LogLater("Graphics shadow resolution limit of the render pipeline: " + (m_ResolutionCap > 0 ? m_ResolutionCap.ToString() : "unknown") + ".");
            return m_ResolutionCap;
        }

        private void ApplyShadowResolution(Setting s, HDAdditionalLightData sun)
        {
            int cap = ResolutionCap();
            if (s.ShadowResolution <= 0 || sun == null)
            {
                m_ResolutionSun = null;
                return;
            }
            int resolution = Mathf.Clamp(s.ShadowResolution, 512, 8192);
            if (cap > 0) resolution = Mathf.Min(resolution, cap);
            var value = sun.shadowResolution;
            if (value == null) return;
            if (sun == m_ResolutionSun && value.useOverride && value.@override == resolution) return;
            sun.SetShadowResolutionOverride(true);
            sun.SetShadowResolution(resolution);
            if (sun != m_ResolutionSun || resolution != m_LastResolution)
                Mod.LogLater("Graphics sun shadow map " + resolution + " (setting " + s.ShadowResolution + ", limit " + (cap > 0 ? cap.ToString() : "?") + ").");
            m_ResolutionSun = sun;
            m_LastResolution = resolution;
        }

        private float m_NextReport;

        // Debug mode: the values HDRP actually renders with, every 10 s.
        private void DebugReport(Setting s, HDAdditionalLightData sun, HDAdditionalCameraData camera)
        {
            if (!Mod.DebugBackendEnabled || Time.unscaledTime < m_NextReport || camera == null) return;
            m_NextReport = Time.unscaledTime + 10f;
            var hd = HDCamera.GetOrCreate(camera.GetComponent<Camera>());
            var stack = hd?.volumeStack;
            if (stack == null) return;
            var exposure = stack.GetComponent<Exposure>();
            var shadows = stack.GetComponent<HDShadowSettings>();
            var ao = stack.GetComponent<ScreenSpaceAmbientOcclusion>();
            var fog = stack.GetComponent<Fog>();
            var contact = stack.GetComponent<ContactShadows>();
            var text = new StringBuilder("Graphics effects (rendered): ");
            text.AppendFormat(CultureInfo.InvariantCulture,
                "exposure {0} comp {1:0.00} limits {2:0.00}..{3:0.00}; shadows {4:0} m splits {5:0.000}/{6:0.000}/{7:0.000} count {8}; " +
                "AO {9:0.00} r {10:0.00}; fog {11} {12:0} m; contact {13} {14:0.00} m to {15:0} m, sun {16}, frame {17}; stored {18}; volumes: ",
                exposure.mode.value, exposure.compensation.value, exposure.limitMin.value, exposure.limitMax.value,
                shadows.maxShadowDistance.value, shadows.cascadeShadowSplit0.value, shadows.cascadeShadowSplit1.value,
                shadows.cascadeShadowSplit2.value, shadows.cascadeShadowSplitCount.value,
                ao.intensity.value, ao.radius.value, fog.enabled.value, fog.meanFreePath.value,
                contact.enable.value, contact.length.value, contact.maxDistance.value,
                sun != null && sun.useContactShadow != null ? sun.useContactShadow.useOverride + "/" + sun.useContactShadow.@override : "?",
                camera.customRenderingSettings && camera.renderingPathCustomFrameSettings.IsEnabled(FrameSettingsField.ContactShadows),
                m_CameraUpdate != null && s_StoredShadows != null ? ((float4)s_StoredShadows.GetValue(m_CameraUpdate)).ToString() : "?");
            foreach (var volume in m_GameVolumes)
                if (volume != null)
                    text.Append(volume.gameObject.name).Append(" (").Append(volume.priority.ToString(CultureInfo.InvariantCulture))
                        .Append(volume.enabled ? "" : ", off").Append(", layer ").Append(volume.gameObject.layer).Append("); ");
            text.Append("own ").Append(m_Volume != null && m_Volume.enabled ? "on" : "off")
                .Append(", camera mask ").Append(camera.volumeLayerMask.value);
            Mod.LogLater(text.ToString());
        }
    }
}
