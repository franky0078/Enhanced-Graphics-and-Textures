using Colossal.IO.AssetDatabase;
using System;
using System.Diagnostics;
using System.IO;
using Game.Modding;
using Game.Settings;
using Game.UI;

namespace Advanced_Graphics
{
    // Railway ballast tab.
    public sealed partial class Setting
    {
        [SettingsUIHidden]
        public bool ShowRailPanelButton { get; set; } = true;

        [SettingsUIHidden]
        [SettingsUISection(RailSection, RailGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ExportRailMaterials
        {
            set { if (value) Mod.RailInspector?.RequestExport(); }
        }

        [SettingsUIHidden]
        [SettingsUISection(RailSection, RailGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool OpenRailDiagnosticFolder
        {
            set
            {
                if (!value) return;
                try
                {
                    Directory.CreateDirectory(RailMaterialInspector.DiagnosticFolder);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = RailMaterialInspector.DiagnosticFolder,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex) { Mod.log.Error(ex, "Could not open rail diagnostic folder."); }
            }
        }

        [SettingsUISection(RailSection, RailGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        public bool RailGravelEnabled { get; set; }

        private int m_RailGravelDetail;
        [SettingsUISection(RailSection, RailGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 2, step = 1, scalarMultiplier = 1)]
        public int RailGravelDetail
        {
            get => m_RailGravelDetail;
            set => SwitchTextureMode(5, ref m_RailGravelDetail, value);
        }

        [SettingsUISection(RailSection, RailGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool OpenRailTextureFolder
        {
            set
            {
                if (!value) return;
                try
                {
                    Directory.CreateDirectory(RailGravelSystem.RailTextureFolder);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = RailGravelSystem.RailTextureFolder,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex) { Mod.log.Error(ex, "Could not open rail texture folder."); }
            }
        }

        [SettingsUISection(RailSection, RailGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ReloadRailTexture { set { if (value) Mod.RailGravel?.ReloadCustomTexture(); } }

        [SettingsUISection(RailSection, RailUvGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 50, max = MaxTextureUvSize, step = 25, scalarMultiplier = 1)]
        public int RailGravelUvSizePercent { get; set; }

        [SettingsUISection(RailSection, RailUvGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureDepthStrength, step = 25, scalarMultiplier = 1)]
        public int RailGravelDepthStrengthPercent { get; set; }

        [SettingsUISection(RailSection, RailUvGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int RailGravelUvOffsetX { get; set; }

        [SettingsUISection(RailSection, RailUvGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int RailGravelUvOffsetY { get; set; }

        [SettingsUISection(RailSection, RailUvGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 360, step = 1, scalarMultiplier = 1)]
        public int RailGravelUvRotation { get; set; }

        [SettingsUISection(RailSection, RailUvGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ResetRailGravel
        {
            set
            {
                if (!value) return;
                ResetRailDefaults();
                ApplyAndSave();
            }
        }

        private void ResetRailDefaults()
        {
            ClearTextureModeProfiles(5); UseRailNormalMap = false;
            RailFilterColor = 0xffffff; RailFilterOpacityPercent = 0;
            RailGravelEnabled = true;
            RailGravelDetail = 1;
            RailGravelUvSizePercent = 150;
            RailGravelDepthStrengthPercent = 200;
            RailGravelUvOffsetX = 0;
            RailGravelUvOffsetY = 10;
            RailGravelUvRotation = 53;
        }
    }
}
