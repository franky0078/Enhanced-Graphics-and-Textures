using System;
using System.Diagnostics;
using System.IO;
using Game.Settings;

namespace Advanced_Graphics
{
    public sealed partial class Setting
    {
        public const string FootpathSection = "Footpath";
        public const string FootpathGroup = "Footpath materials";

        [SettingsUISection(FootpathSection, FootpathGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        public bool FootpathEnabled { get; set; }

        private int m_FootpathDetail;
        [SettingsUISection(FootpathSection, FootpathGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 2, step = 1, scalarMultiplier = 1)]
        public int FootpathDetail
        {
            get => m_FootpathDetail;
            set => SwitchTextureMode(7, ref m_FootpathDetail, value);
        }

        [SettingsUISection(FootpathSection, FootpathGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 10, max = MaxTextureUvSize, step = 5, scalarMultiplier = 1)]
        public int FootpathUvSizePercent { get; set; } = 100;

        [SettingsUISection(FootpathSection, FootpathGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureDepthStrength, step = 25, scalarMultiplier = 1)]
        public int FootpathDepthStrengthPercent { get; set; }

        [SettingsUISection(FootpathSection, FootpathGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int FootpathUvOffsetX { get; set; }

        [SettingsUISection(FootpathSection, FootpathGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int FootpathUvOffsetY { get; set; }

        [SettingsUISection(FootpathSection, FootpathGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 360, step = 1, scalarMultiplier = 1)]
        public int FootpathUvRotation { get; set; }

        [SettingsUISection(FootpathSection, FootpathGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool OpenFootpathTextureFolder
        {
            set
            {
                if (!value) return;
                try
                {
                    Directory.CreateDirectory(TextureStorage.Footpath);
                    Process.Start(new ProcessStartInfo { FileName = TextureStorage.Footpath, UseShellExecute = true });
                }
                catch (Exception ex) { Mod.log.Error(ex, "Could not open footpath texture folder."); }
            }
        }

        [SettingsUISection(FootpathSection, FootpathGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ReloadFootpathTexture { set { if (value) Mod.Footpath?.ReloadCustomTexture(); } }

        [SettingsUISection(FootpathSection, FootpathGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ResetFootpath { set { if (value) { SetFootpathDefaults(); ApplyAndSave(); } } }

        private void SetFootpathDefaults()
        {
            ClearTextureModeProfiles(7); UseFootpathNormalMap = false;
            FootpathEnabled = false;
            FootpathDetail = 0;
            FootpathUvSizePercent = 100;
            FootpathDepthStrengthPercent = 0;
            FootpathUvOffsetX = FootpathUvOffsetY = FootpathUvRotation = 0;
            FootpathFilterColor = 0xffffff;
            FootpathFilterOpacityPercent = 0;
        }

        private void ApplyFootpathToSystem()
        {
            Mod.Footpath?.Configure(TexturesEnabled && FootpathEnabled && (FootpathDetail != 0 || FootpathUvSizePercent != 100 ||
                FootpathDepthStrengthPercent != 0 || FootpathUvOffsetX != 0 || FootpathUvOffsetY != 0 ||
                FootpathUvRotation != 0 || TextureColorFilter.IsActive(FootpathFilterColor, FootpathFilterOpacityPercent)),
                FootpathDetail, FootpathDetail == 2 ? RoadTextureSource.Custom : RoadTextureSource.Generated,
                FootpathUvSizePercent, FootpathDepthStrengthPercent, 1f,
                FootpathUvOffsetX, FootpathUvOffsetY, FootpathUvRotation, 0, 100, 100, 100, 50,
                FootpathFilterColor, FootpathFilterOpacityPercent);
        }
    }
}

