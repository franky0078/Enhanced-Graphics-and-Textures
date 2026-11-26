using Colossal.IO.AssetDatabase;
using System;
using System.Diagnostics;
using System.IO;
using Game.Modding;
using Game.Settings;
using Game.UI;

namespace Advanced_Graphics
{
    // Alley / one-way road sidewalk tab.
    public sealed partial class Setting
    {
        [SettingsUIHidden]
        public int AlleySidewalkSettingsVersion { get; set; }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        public bool AlleySidewalkEnabled { get; set; }

        [SettingsUIHidden]
        public bool ShowAlleySidewalkPanelButton { get; set; } = true;

        private int m_AlleySidewalkDetail;
        [SettingsUISection(AlleySidewalkSection, AlleySidewalkGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 2, step = 1, scalarMultiplier = 1)]
        public int AlleySidewalkDetail
        {
            get => m_AlleySidewalkDetail;
            set => SwitchTextureMode(2, ref m_AlleySidewalkDetail, value);
        }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool OpenAlleySidewalkTextureFolder
        {
            set
            {
                if (!value) return;
                try
                {
                    Directory.CreateDirectory(AlleySidewalkSurfaceSystem.AlleyTextureFolder);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = AlleySidewalkSurfaceSystem.AlleyTextureFolder,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex) { Mod.log.Error(ex, "Could not open sidewalk texture folder."); }
            }
        }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ReloadAlleySidewalkTexture { set { if (value) Mod.AlleySidewalk?.ReloadCustomTexture(); } }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkUvGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 50, max = MaxTextureUvSize, step = 25, scalarMultiplier = 1)]
        public int AlleySidewalkUvSizePercent { get; set; }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkUvGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureDepthStrength, step = 25, scalarMultiplier = 1)]
        public int AlleySidewalkDepthStrengthPercent { get; set; }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkUvGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int AlleySidewalkUvOffsetX { get; set; }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkUvGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int AlleySidewalkUvOffsetY { get; set; }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkUvGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 360, step = 1, scalarMultiplier = 1)]
        public int AlleySidewalkUvRotation { get; set; }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 400, step = 10, scalarMultiplier = 1)]
        public int AlleySidewalkCrackCount { get; set; }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 25, max = 600, step = 25, scalarMultiplier = 1)]
        public int AlleySidewalkCrackSizePercent { get; set; }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 25, max = 400, step = 25, scalarMultiplier = 1)]
        public int AlleySidewalkCrackWidthPercent { get; set; }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureDepthStrength, step = 25, scalarMultiplier = 1)]
        public int AlleySidewalkCrackDepthStrengthPercent { get; set; } = 100;

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 360, step = 1, scalarMultiplier = 1)]
        public int AlleySidewalkCrackRotation { get; set; } = 0;

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 1, scalarMultiplier = 1)]
        public int AlleySidewalkCrackOffsetX { get; set; } = 0;

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 1, scalarMultiplier = 1)]
        public int AlleySidewalkCrackOffsetY { get; set; } = 0;

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 200, step = 10, scalarMultiplier = 1)]
        public int AlleySidewalkCrackOpacityPercent { get; set; }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 100, step = 10, scalarMultiplier = 1)]
        public int AlleySidewalkCrackRandomnessPercent { get; set; }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0.1f, max = 8, step = 0.05f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float AlleySidewalkTextureTiling { get; set; }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ResetAlleySidewalk
        {
            set
            {
                if (!value) return;
                SetAlleySidewalkDefaults();
                ApplyAndSave();
            }
        }

        private void SetAlleySidewalkDefaults()
        {
            ClearTextureModeProfiles(2); UseAlleySidewalkNormalMap = false;
            AlleySidewalkFilterColor = 0xffffff; AlleySidewalkFilterOpacityPercent = 0;
            ShowAlleySidewalkPanelButton = true;
            AlleySidewalkEnabled = false;
            AlleySidewalkDetail = 0;
            AlleySidewalkUvSizePercent = 100;
            AlleySidewalkDepthStrengthPercent = 0;
            AlleySidewalkUvOffsetX = AlleySidewalkUvOffsetY = AlleySidewalkUvRotation = 0;
            AlleySidewalkCrackDepthStrengthPercent = 100;
            AlleySidewalkCrackRotation = AlleySidewalkCrackOffsetX = AlleySidewalkCrackOffsetY = 0;
            AlleySidewalkCrackCount = 0;
            AlleySidewalkCrackSizePercent = AlleySidewalkCrackWidthPercent = AlleySidewalkCrackOpacityPercent = 100;
            AlleySidewalkCrackRandomnessPercent = 50;
            AlleySidewalkTextureTiling = 1;
        }

        internal void MigrateAlleySidewalkSettings()
        {
            if (AlleySidewalkSettingsVersion >= 2) return;
            if (AlleySidewalkSettingsVersion < 1)
            {
                AlleySidewalkEnabled = SidewalkEnabled;
                AlleySidewalkDetail = SidewalkDetail;
                AlleySidewalkDepthStrengthPercent = SidewalkDepthStrengthPercent;
                AlleySidewalkUvOffsetX = SidewalkUvOffsetX;
                AlleySidewalkUvOffsetY = SidewalkUvOffsetY;
                AlleySidewalkUvRotation = SidewalkUvRotation;
                AlleySidewalkCrackCount = SidewalkCrackCount;
                AlleySidewalkCrackSizePercent = SidewalkCrackSizePercent;
                AlleySidewalkCrackWidthPercent = SidewalkCrackWidthPercent;
                AlleySidewalkCrackOpacityPercent = SidewalkCrackOpacityPercent;
                AlleySidewalkCrackRandomnessPercent = SidewalkCrackRandomnessPercent;
                AlleySidewalkTextureTiling = SidewalkTextureTiling;
                AlleySidewalkUvSizePercent = 100;
            }
            // Repair hidden tabs from settings saved before the split.
            ShowAlleySidewalkPanelButton = true;
            AlleySidewalkSettingsVersion = 2;
        }
    }
}
