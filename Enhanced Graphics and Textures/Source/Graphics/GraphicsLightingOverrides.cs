using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Advanced_Graphics
{
    // Own only the requested parameters. Removing the volume releases all overrides.
    internal sealed class GraphicsLightingOverrides
    {
        private GameObject m_Holder;
        private Volume m_Volume;
        private VolumeProfile m_Profile;
        private Bloom m_Bloom;
        private PhysicallyBasedSky m_Sky;
        private Volume m_GameLightingVolume;
        private int m_LightingLookupFrames;
        private HDAdditionalLightData m_Sun;
        private Color m_BaseFlareTint, m_LastFlareTint;
        private float m_BaseFlareSize, m_LastFlareSize;
        private float m_BaseFlareFalloff, m_LastFlareFalloff;
        private bool m_TouchedSun;

        public void ApplyVolume(Setting options)
        {
            if (options == null) return;
            bool bloom = options.BloomMode != Setting.BloomControl.GameDefault ||
                !Mathf.Approximately(options.BloomIntensity, 0.2f);
            bool sky = !Mathf.Approximately(options.SkyExposure, 0f);
            if (!bloom && !sky)
            {
                if (m_Volume != null) m_Volume.enabled = false;
                return;
            }
            if (m_Volume == null)
            {
                m_Holder = new GameObject("Enhanced Graphics lighting") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(m_Holder);
                m_Volume = m_Holder.AddComponent<Volume>();
                m_Volume.isGlobal = true;
                m_Volume.priority = 10000f;
                m_Volume.weight = 1f;
                m_Profile = ScriptableObject.CreateInstance<VolumeProfile>();
                m_Profile.hideFlags = HideFlags.HideAndDontSave;
                m_Volume.sharedProfile = m_Profile;
                m_Bloom = m_Profile.Add<Bloom>();
                m_Sky = m_Profile.Add<PhysicallyBasedSky>();
            }
            m_Bloom.active = bloom;
            m_Bloom.intensity.Override(options.BloomMode == Setting.BloomControl.Disabled ?
                0f : FiniteClamp(options.BloomIntensity, 0f, 1f, 0.2f));

            // Read the game's current exposure after LightingSystem updates it, so
            // the correction follows the original day/night lighting instead of freezing it.
            m_Sky.active = false;
            if (sky)
            {
                if (m_GameLightingVolume == null && m_LightingLookupFrames-- <= 0)
                {
                    m_LightingLookupFrames = 60;
                    var holder = GameObject.Find("LightingPostProcessVolume");
                    m_GameLightingVolume = holder != null ? holder.GetComponent<Volume>() : null;
                }
                var profile = m_GameLightingVolume != null ? m_GameLightingVolume.profile : null;
                if (profile != null && profile.TryGet<PhysicallyBasedSky>(out var original))
                {
                    m_Sky.exposure.Override(original.exposure.value +
                        FiniteClamp(options.SkyExposure, -5f, 5f, 0f));
                    m_Sky.active = true;
                }
            }
            m_Volume.enabled = bloom || m_Sky.active;
        }

        public void ApplySun(Setting options, HDAdditionalLightData sun)
        {
            if (sun != m_Sun)
            {
                RestoreSun();
                m_Sun = sun;
                if (sun != null)
                {
                    m_BaseFlareTint = sun.flareTint;
                    m_BaseFlareSize = sun.flareSize;
                    m_BaseFlareFalloff = sun.flareFalloff;
                }
            }
            if (sun == null || options == null) return;
            // Adopt externally changed values, rather than multiplying our previous result.
            if (!m_TouchedSun || sun.flareTint != m_LastFlareTint)
                m_BaseFlareTint = sun.flareTint;
            if (!m_TouchedSun || !Mathf.Approximately(sun.flareSize, m_LastFlareSize))
                m_BaseFlareSize = sun.flareSize;
            if (!m_TouchedSun || !Mathf.Approximately(sun.flareFalloff, m_LastFlareFalloff))
                m_BaseFlareFalloff = sun.flareFalloff;
            var strength = FiniteClamp(options.SunBloomStrength, 0f, 5f, 1f);
            if (Mathf.Approximately(strength, 1f))
            {
                RestoreSun();
                return;
            }
            var radius = m_BaseFlareSize > 0f ? m_BaseFlareSize : 2f;
            // A large exponent can confine the entire halo to the sun disk.
            var size = Mathf.Clamp(Mathf.Max(radius * strength, 2f * (strength - 1f)), 0f, 90f);
            var falloff = strength > 0f ? Mathf.Clamp(
                FiniteClamp(m_BaseFlareFalloff, 1f, 64f, 4f) / (strength * strength), 1f, 64f) : 4f;
            var tint = m_BaseFlareTint;
            if (Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b)) <= 0.0001f)
                tint = Color.white;
            var gain = strength > 1f ? strength * strength : strength;
            tint.r *= gain;
            tint.g *= gain;
            tint.b *= gain;
            if (sun.flareTint != tint) sun.flareTint = tint;
            if (!Mathf.Approximately(sun.flareSize, size)) sun.flareSize = size;
            if (!Mathf.Approximately(sun.flareFalloff, falloff)) sun.flareFalloff = falloff;
            m_LastFlareFalloff = sun.flareFalloff;
            m_LastFlareTint = sun.flareTint;
            m_LastFlareSize = sun.flareSize;
            m_TouchedSun = true;
        }

        private static float FiniteClamp(float value, float min, float max, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
        }

        private void RestoreSun()
        {
            if (m_Sun != null && m_TouchedSun && m_Sun.flareTint == m_LastFlareTint)
                m_Sun.flareTint = m_BaseFlareTint;
            if (m_Sun != null && m_TouchedSun && Mathf.Approximately(m_Sun.flareSize, m_LastFlareSize))
                m_Sun.flareSize = m_BaseFlareSize;
            if (m_Sun != null && m_TouchedSun && Mathf.Approximately(m_Sun.flareFalloff, m_LastFlareFalloff))
                m_Sun.flareFalloff = m_BaseFlareFalloff;
            m_TouchedSun = false;
        }

        public void Restore()
        {
            RestoreSun();
            m_Sun = null;
            if (m_Holder != null) Object.Destroy(m_Holder);
            if (m_Profile != null)
            {
                foreach (var component in m_Profile.components)
                    if (component != null) Object.Destroy(component);
                Object.Destroy(m_Profile);
            }
            m_Holder = null;
            m_Volume = null;
            m_Profile = null;
            m_Bloom = null;
            m_Sky = null;
            m_GameLightingVolume = null;
            m_LightingLookupFrames = 0;
        }
    }
}
