using System;
using System.Diagnostics;
using System.IO;
using Game.Settings;

namespace Advanced_Graphics
{
    public sealed partial class Setting
    {
        public const string GrassSection = "Grass";
        public const string GrassGroup = "Roadside grass materials";

        [SettingsUISection(GrassSection, GrassGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        public bool GrassEnabled { get; set; }

        private int m_GrassDetail;
        [SettingsUISection(GrassSection, GrassGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 2, step = 1, scalarMultiplier = 1)]
        public int GrassDetail
        {
            get => m_GrassDetail;
            set => SwitchTextureMode(6, ref m_GrassDetail, value);
        }

        [SettingsUISection(GrassSection, GrassGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 50, max = MaxTextureUvSize, step = 25, scalarMultiplier = 1)]
        public int GrassUvSizePercent { get; set; } = 100;

        [SettingsUISection(GrassSection, GrassGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureDepthStrength, step = 25, scalarMultiplier = 1)]
        public int GrassDepthStrengthPercent { get; set; }

        [SettingsUISection(GrassSection, GrassGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int GrassUvOffsetX { get; set; }

        [SettingsUISection(GrassSection, GrassGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int GrassUvOffsetY { get; set; }

        [SettingsUISection(GrassSection, GrassGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 360, step = 1, scalarMultiplier = 1)]
        public int GrassUvRotation { get; set; }

        [SettingsUISection(GrassSection, GrassGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool OpenGrassTextureFolder
        {
            set
            {
                if (!value) return;
                try
                {
                    Directory.CreateDirectory(TextureStorage.Grass);
                    Process.Start(new ProcessStartInfo { FileName = TextureStorage.Grass, UseShellExecute = true });
                }
                catch (Exception ex) { Mod.log.Error(ex, "Could not open grass texture folder."); }
            }
        }

        [SettingsUISection(GrassSection, GrassGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ReloadGrassTexture { set { if (value) Mod.Grass?.ReloadCustomTexture(); } }

        [SettingsUISection(GrassSection, GrassGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ResetGrass { set { if (value) { SetGrassDefaults(); ApplyAndSave(); } } }

        private void SetGrassDefaults()
        {
            ClearTextureModeProfiles(6); UseGrassNormalMap = false;
            GrassEnabled = false;
            GrassDetail = 0;
            GrassUvSizePercent = 100;
            GrassDepthStrengthPercent = 0;
            GrassUvOffsetX = GrassUvOffsetY = GrassUvRotation = 0;
            GrassFilterColor = 0xffffff;
            GrassFilterOpacityPercent = 0;
        }

        private void ApplyGrassToSystem()
        {
            Mod.Grass?.Configure(TexturesEnabled && GrassEnabled && (GrassDetail != 0 || GrassUvSizePercent != 100 ||
                GrassDepthStrengthPercent != 0 || GrassUvOffsetX != 0 || GrassUvOffsetY != 0 ||
                GrassUvRotation != 0 || TextureColorFilter.IsActive(GrassFilterColor, GrassFilterOpacityPercent)),
                GrassDetail, GrassDetail == 2 ? RoadTextureSource.Custom : RoadTextureSource.Generated,
                GrassUvSizePercent, GrassDepthStrengthPercent, 1f,
                GrassUvOffsetX, GrassUvOffsetY, GrassUvRotation, 0, 100, 100, 100, 50,
                GrassFilterColor, GrassFilterOpacityPercent);
        }
    }
}

