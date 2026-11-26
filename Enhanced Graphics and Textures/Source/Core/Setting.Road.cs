using Colossal.IO.AssetDatabase;
using System;
using System.Diagnostics;
using System.IO;
using Game.Modding;
using Game.Settings;
using Game.UI;

namespace Advanced_Graphics
{
    // Road asphalt tab.
    public sealed partial class Setting
    {
        [SettingsUIHidden]
        [SettingsUISection(RoadSection, RoadGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool InspectRoadMaterials
        {
            set { if (value) Mod.RoadInspector?.RequestSurvey(); }
        }

        [SettingsUIHidden]
        [SettingsUISection(RoadSection, RoadGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ExportGravelRoadMaterials
        {
            set { if (value) Mod.RoadInspector?.RequestGravelSurvey(); }
        }

        // Kept for migrating saves made with the previous source selector.
        [SettingsUIHidden]
        public RoadTextureSource AsphaltSource { get; set; }

        [SettingsUIHidden]
        public int RoadTextureLayoutVersion { get; set; }

        private int m_RoadAsphaltDetail;
        [SettingsUISection(RoadSection, RoadGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 2, step = 1, scalarMultiplier = 1)]
        public int RoadAsphaltDetail
        {
            get => m_RoadAsphaltDetail;
            set => SwitchTextureMode(0, ref m_RoadAsphaltDetail, value);
        }

        [SettingsUISection(RoadSection, RoadGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        public bool RoadAsphaltEnabled { get; set; }

        [SettingsUISection(RoadSection, RoadGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool OpenRoadTextureFolder
        {
            set
            {
                if (!value) return;
                try
                {
                    Directory.CreateDirectory(RoadAsphaltSystem.CustomTextureFolder);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = RoadAsphaltSystem.CustomTextureFolder,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex) { Mod.log.Error(ex, "Could not open road texture folder."); }
            }
        }

        [SettingsUISection(RoadSection, RoadGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ReloadRoadTexture { set { if (value) Mod.RoadAsphalt?.ReloadCustomTexture(); } }

        [SettingsUISection(RoadSection, RoadUvGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 50, max = MaxTextureUvSize, step = 25, scalarMultiplier = 1)]
        public int RoadUvSizePercent { get; set; }

        private float m_ParkingTextureScaleFactor = 1f;
        [SettingsUISection(RoadSection, RoadUvGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0.1f, max = 8f, step = 0.1f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float ParkingTextureScaleFactor
        {
            get => m_ParkingTextureScaleFactor;
            set => m_ParkingTextureScaleFactor = float.IsNaN(value) || float.IsInfinity(value) ? 1f :
                Math.Max(0.1f, Math.Min(8f, (float)Math.Round(value, 1)));
        }

        private float m_ParkingDepthStrengthFactor = 1f;
        [SettingsUISection(RoadSection, RoadUvGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 8f, step = 0.1f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float ParkingDepthStrengthFactor
        {
            get => m_ParkingDepthStrengthFactor;
            set => m_ParkingDepthStrengthFactor = float.IsNaN(value) || float.IsInfinity(value) ? 1f :
                Math.Max(0f, Math.Min(8f, (float)Math.Round(value, 1)));
        }

        [SettingsUISection(RoadSection, RoadUvGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureDepthStrength, step = 25, scalarMultiplier = 1)]
        public int RoadDepthStrengthPercent { get; set; }

        [SettingsUISection(RoadSection, RoadUvGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int RoadUvOffsetX { get; set; }

        [SettingsUISection(RoadSection, RoadUvGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int RoadUvOffsetY { get; set; }

        [SettingsUISection(RoadSection, RoadUvGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 360, step = 1, scalarMultiplier = 1)]
        public int RoadUvRotation { get; set; }

        [SettingsUISection(RoadSection, RoadCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxRoadCrackCount, step = 10, scalarMultiplier = 1)]
        public int RoadCrackCount { get; set; }

        [SettingsUISection(RoadSection, RoadCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        public bool UseCustomRoadCracks
        {
            get => m_UseCustomRoadCracks;
            set => SwitchRoadCrackSource(value);
        }

        [SettingsUISection(RoadSection, RoadCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool OpenRoadCrackFolder
        {
            set
            {
                if (!value) return;
                try
                {
                    Directory.CreateDirectory(TextureStorage.RoadCracks);
                    Process.Start(new ProcessStartInfo { FileName = TextureStorage.RoadCracks, UseShellExecute = true });
                }
                catch (Exception ex) { Mod.log.Error(ex, "Could not open road crack folder."); }
            }
        }

        [SettingsUISection(RoadSection, RoadCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ReloadRoadCracks { set { if (value) Mod.RoadAsphalt?.ReloadCustomTexture(); } }

        [SettingsUISection(RoadSection, RoadCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 25, max = MaxRoadCrackSize, step = 25, scalarMultiplier = 1)]
        public int RoadCrackSizePercent { get; set; }

        [SettingsUISection(RoadSection, RoadCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 25, max = MaxRoadCrackWidth, step = 25, scalarMultiplier = 1)]
        public int RoadCrackWidthPercent { get; set; }

        [SettingsUISection(RoadSection, RoadCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureDepthStrength, step = 25, scalarMultiplier = 1)]
        public int RoadCrackDepthStrengthPercent { get; set; } = 100;

        [SettingsUISection(RoadSection, RoadCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 360, step = 1, scalarMultiplier = 1)]
        public int RoadCrackRotation { get; set; }

        [SettingsUISection(RoadSection, RoadCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 1, scalarMultiplier = 1)]
        public int RoadCrackOffsetX { get; set; }

        [SettingsUISection(RoadSection, RoadCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 1, scalarMultiplier = 1)]
        public int RoadCrackOffsetY { get; set; }

        [SettingsUISection(RoadSection, RoadCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxRoadCrackOpacity, step = 10, scalarMultiplier = 1)]
        public int RoadCrackOpacityPercent { get; set; }

        [SettingsUISection(RoadSection, RoadCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 100, step = 10, scalarMultiplier = 1)]
        public int RoadCrackRandomnessPercent { get; set; }

        [SettingsUISection(RoadSection, RoadCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0.1f, max = 8, step = 0.05f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float RoadTextureTiling { get; set; }

        [SettingsUISection(RoadSection, RoadCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ResetRoadAsphalt
        {
            set
            {
                if (!value) return;
                RoadFilterColor = 0xffffff; RoadFilterOpacityPercent = 0;
                RoadAsphaltDetail = 0;
                RoadAsphaltEnabled = false;
                AsphaltSource = RoadTextureSource.Generated;
                RoadUvSizePercent = 100;
                RoadDepthStrengthPercent = 0;
                UseCustomRoadCracks = false;
                RoadCrackCount = 0;
                RoadCrackSizePercent = 100;
                RoadCrackWidthPercent = 100;
                RoadCrackOpacityPercent = 100;
                RoadCrackDepthStrengthPercent = 100;
                RoadCrackRotation = RoadCrackOffsetX = RoadCrackOffsetY = 0;
                RoadCrackRandomnessPercent = 50;
                RoadTextureTiling = 1;
                ParkingTextureScaleFactor = 1f;
                ParkingDepthStrengthFactor = 1f;
                RoadUvOffsetX = RoadUvOffsetY = RoadUvRotation = 0;
                ClearTextureModeProfiles(0); UseRoadNormalMap = false;
                ApplyAndSave();
            }
        }

        [SettingsUIHidden]
        public bool ShowRoadPanelButton { get; set; }
    }
}
