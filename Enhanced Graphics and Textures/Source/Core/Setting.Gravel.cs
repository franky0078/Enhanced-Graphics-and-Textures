using Colossal.IO.AssetDatabase;
using System;
using System.Diagnostics;
using System.IO;
using Game.Modding;
using Game.Settings;
using Game.UI;

namespace Advanced_Graphics
{
    // Gravel road and gravel sidewalk tabs.
    public sealed partial class Setting
    {
        [SettingsUISection(GravelSection, GravelGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        public bool RoadGravelEnabled { get; set; }
        [SettingsUIHidden]
        public bool ShowGravelPanelButton { get; set; } = true;
        private int m_RoadGravelDetail;
        [SettingsUISection(GravelSection, GravelGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 2, step = 1, scalarMultiplier = 1)]
        public int RoadGravelDetail
        {
            get => m_RoadGravelDetail;
            set => SwitchTextureMode(3, ref m_RoadGravelDetail, value);
        }
        [SettingsUISection(GravelSection, GravelGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 50, max = MaxTextureUvSize, step = 25, scalarMultiplier = 1)]
        public int RoadGravelUvSizePercent { get; set; } = 100;
        [SettingsUISection(GravelSection, GravelGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureDepthStrength, step = 25, scalarMultiplier = 1)]
        public int RoadGravelDepthStrengthPercent { get; set; }
        [SettingsUISection(GravelSection, GravelGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int RoadGravelUvOffsetX { get; set; }
        [SettingsUISection(GravelSection, GravelGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int RoadGravelUvOffsetY { get; set; }
        [SettingsUISection(GravelSection, GravelGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 360, step = 1, scalarMultiplier = 1)]
        public int RoadGravelUvRotation { get; set; }
        [SettingsUISection(GravelSection, GravelGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool OpenGravelTextureFolder
        {
            set
            {
                if (!value) return;
                try
                {
                    Directory.CreateDirectory(TextureStorage.RoadGravel);
                    Process.Start(new ProcessStartInfo { FileName = TextureStorage.RoadGravel, UseShellExecute = true });
                }
                catch (Exception ex) { Mod.log.Error(ex, "Could not open gravel road texture folder."); }
            }
        }
        [SettingsUISection(GravelSection, GravelGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ReloadGravelTexture { set { if (value) Mod.RoadGravel?.ReloadCustomTexture(); } }
        [SettingsUISection(GravelSection, GravelGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ResetRoadGravel { set { if (value) { ResetRoadGravelDefaults(); ApplyAndSave(); } } }

        [SettingsUISection(GravelSidewalkSection, ShoulderGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        public bool GravelShoulderEnabled { get; set; }
        [SettingsUISection(GravelSidewalkSection, ShoulderGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 50, max = MaxTextureUvSize, step = 25, scalarMultiplier = 1)]
        public int GravelShoulderUvSizePercent { get; set; } = 100;
        [SettingsUISection(GravelSidewalkSection, ShoulderGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureDepthStrength, step = 25, scalarMultiplier = 1)]
        public int GravelShoulderDepthStrengthPercent { get; set; }
        private int m_GravelShoulderDetail;
        [SettingsUISection(GravelSidewalkSection, ShoulderGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 2, step = 1, scalarMultiplier = 1)]
        public int GravelShoulderDetail
        {
            get => m_GravelShoulderDetail;
            set => SwitchTextureMode(4, ref m_GravelShoulderDetail, value);
        }
        [SettingsUISection(GravelSidewalkSection, ShoulderGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int GravelShoulderUvOffsetX { get; set; }
        [SettingsUISection(GravelSidewalkSection, ShoulderGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int GravelShoulderUvOffsetY { get; set; }
        [SettingsUISection(GravelSidewalkSection, ShoulderGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 360, step = 1, scalarMultiplier = 1)]
        public int GravelShoulderUvRotation { get; set; }
        [SettingsUISection(GravelSidewalkSection, ShoulderGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ReloadGravelShoulderTexture { set { if (value) Mod.GravelShoulderSurface?.ReloadCustomTexture(); } }
        [SettingsUISection(GravelSidewalkSection, ShoulderGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool OpenGravelShoulderTextureFolder
        {
            set
            {
                if (!value) return;
                try
                {
                    Directory.CreateDirectory(TextureStorage.GravelShoulder);
                    Process.Start(new ProcessStartInfo { FileName = TextureStorage.GravelShoulder, UseShellExecute = true });
                }
                catch (Exception ex) { Mod.log.Error(ex, "Could not open gravel sidewalk texture folder."); }
            }
        }
        [SettingsUISection(GravelSidewalkSection, ShoulderGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ResetGravelShoulder
        {
            set
            {
                if (value)
                {
                    ClearTextureModeProfiles(4); UseGravelShoulderNormalMap = false; GravelShoulderFilterColor = 0xffffff; GravelShoulderFilterOpacityPercent = 0; GravelShoulderEnabled = false; GravelShoulderUvSizePercent = 100;
                    GravelShoulderDepthStrengthPercent = 0; GravelShoulderDetail = 0;
                    GravelShoulderUvOffsetX = GravelShoulderUvOffsetY = GravelShoulderUvRotation = 0; ApplyAndSave();
                }
            }
        }

        private void ResetRoadGravelDefaults()
        {
            ClearTextureModeProfiles(3); UseRoadGravelNormalMap = false;
            RoadGravelFilterColor = 0xffffff; RoadGravelFilterOpacityPercent = 0;
            RoadGravelEnabled = false;
            RoadGravelDetail = 0;
            RoadGravelUvSizePercent = 100;
            RoadGravelDepthStrengthPercent = 0;
            RoadGravelUvOffsetX = RoadGravelUvOffsetY = RoadGravelUvRotation = 0;
        }
    }
}
