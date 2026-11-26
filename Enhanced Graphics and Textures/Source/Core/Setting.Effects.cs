using System;
using Game.Settings;

namespace Advanced_Graphics
{
    // HDRP effects, edited in the in-game graphics window. 100 % / 0 = game value.
    public sealed partial class Setting
    {
        [SettingsUIHidden]
        [SettingsUISection(MainSection, ShadowsGroup)]
        public bool ContactShadowsEnabled { get; set; }

        private float m_ContactShadowLength = 0.3f;
        [SettingsUIHidden]
        [SettingsUISection(MainSection, ShadowsGroup)]
        public float ContactShadowLength
        {
            get => m_ContactShadowLength;
            set => m_ContactShadowLength = float.IsNaN(value) || float.IsInfinity(value) ? 0.3f :
                Math.Max(0.01f, Math.Min(1f, (float)Math.Round(value, 2)));
        }

        [SettingsUIHidden]
        [SettingsUISection(MainSection, ShadowsGroup)]
        public int ContactShadowOpacityPercent { get; set; } = 100;

        [SettingsUIHidden]
        [SettingsUISection(MainSection, ShadowsGroup)]
        public int ContactShadowDistance { get; set; } = 300;

        [SettingsUIHidden]
        [SettingsUISection(MainSection, ShadowsGroup)]
        public int AmbientOcclusionStrengthPercent { get; set; } = 100;

        [SettingsUIHidden]
        [SettingsUISection(MainSection, ShadowsGroup)]
        public int AmbientOcclusionRadiusPercent { get; set; } = 100;

        [SettingsUIHidden]
        [SettingsUISection(MainSection, ShadowsGroup)]
        public int ShadowCascadeFocus { get; set; }

        private float m_ExposureCompensation;
        [SettingsUIHidden]
        [SettingsUISection(MainSection, SkyGroup)]
        public float ExposureCompensation
        {
            get => m_ExposureCompensation;
            set => m_ExposureCompensation = float.IsNaN(value) || float.IsInfinity(value) ? 0f :
                Math.Max(-3f, Math.Min(3f, (float)Math.Round(value, 2)));
        }

        [SettingsUIHidden]
        [SettingsUISection(MainSection, SkyGroup)]
        public int ExposureAdaptationPercent { get; set; } = 100;

        [SettingsUIHidden]
        [SettingsUISection(MainSection, SkyGroup)]
        public int FogDensityPercent { get; set; } = 100;

        internal void ResetEffectDefaults()
        {
            ContactShadowsEnabled = false;
            ContactShadowLength = 0.3f;
            ContactShadowOpacityPercent = 100;
            ContactShadowDistance = 300;
            AmbientOcclusionStrengthPercent = 100;
            AmbientOcclusionRadiusPercent = 100;
            ShadowCascadeFocus = 0;
            ExposureCompensation = 0f;
            ExposureAdaptationPercent = 100;
            FogDensityPercent = 100;
        }
    }
}
