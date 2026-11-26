using Colossal.IO.AssetDatabase;
using System;
using System.Diagnostics;
using System.IO;
using Game.Modding;
using Game.Settings;
using Game.UI;

namespace Advanced_Graphics
{
    // Sidewalk tab, junction options and curb.
    public sealed partial class Setting
    {
        [SettingsUISection(SidewalkSection, SidewalkGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        public bool SidewalkJunctionOverlay { get; set; } = false;

        [SettingsUISection(SidewalkSection, SidewalkGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        public bool SidewalkJunctionParking { get; set; } = false;

        [SettingsUISection(SidewalkSection, SidewalkGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        public bool SidewalkEnabled { get; set; }

        [SettingsUIHidden]
        public bool ShowSidewalkPanelButton { get; set; } = true;

        [SettingsUIHidden]
        [SettingsUISection(SidewalkSection, SidewalkGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        public bool DiagnoseRoadEUAtlas { get; set; }

        // Debug only: sync blend used for junction sidewalk UVs. 0 linear, 1 smoothstep, 2 t², 3 1-(1-t)², 4 none, 5 full, 6 mark remapped piece.
        [SettingsUIHidden]
        [SettingsUISection(SidewalkSection, SidewalkGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 6, step = 1, scalarMultiplier = 1)]
        public int JunctionSyncBlendMode { get; set; }

        // Debug only: keep the game's tangents on remapped sidewalk faces (comparison for joint artefacts).
        [SettingsUIHidden]
        public bool JunctionRecomputeTangents { get; set; }

        private int m_CurbMode;
        [SettingsUISection(SidewalkSection, SidewalkGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 2, step = 1, scalarMultiplier = 1)]
        public int CurbMode
        {
            get => m_CurbMode;
            set => SwitchTextureMode(CurbSurface, ref m_CurbMode, value);
        }
        [SettingsUIHidden] public int CurbSize { get; set; } = 100;
        [SettingsUIHidden] public int CurbJointOpacity { get; set; } = 100;
        [SettingsUIHidden] public int CurbJointSpacing { get; set; }
        [SettingsUIHidden] public int CurbDepth { get; set; } = 100;
        [SettingsUIHidden] public int CurbRotation { get; set; } = 0;
        [SettingsUIHidden] public int CurbOffsetX { get; set; } = 0;
        [SettingsUIHidden] public int CurbOffsetY { get; set; } = 0;
        [SettingsUIHidden] public int CurbColor { get; set; } = 0xffffff;
        [SettingsUIHidden] public int CurbOpacity { get; set; } = 100;

        internal void ResetCurb()
        {
            ClearTextureModeProfiles(CurbSurface);
            UseCurbNormalMap = false;
            m_CurbMode = CurbJointSpacing = CurbRotation = CurbOffsetX = CurbOffsetY = 0;
            CurbSize = CurbDepth = CurbOpacity = CurbJointOpacity = 100;
            CurbColor = 0xffffff;
            if (m_TextureModeProfilesReady) EnsureTextureModeProfiles(CurbSurface);
        }

        private int m_SidewalkDetail;
        [SettingsUISection(SidewalkSection, SidewalkGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 2, step = 1, scalarMultiplier = 1)]
        public int SidewalkDetail
        {
            get => m_SidewalkDetail;
            set => SwitchTextureMode(1, ref m_SidewalkDetail, value);
        }

        [SettingsUISection(SidewalkSection, SidewalkGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool OpenSidewalkTextureFolder
        {
            set
            {
                if (!value) return;
                try
                {
                    Directory.CreateDirectory(SidewalkSurfaceSystem.SidewalkTextureFolder);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = SidewalkSurfaceSystem.SidewalkTextureFolder,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex) { Mod.log.Error(ex, "Could not open sidewalk texture folder."); }
            }
        }

        [SettingsUISection(SidewalkSection, SidewalkGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ReloadSidewalkTexture { set { if (value) Mod.Sidewalk?.ReloadCustomTexture(); } }

        [SettingsUIHidden]
        [SettingsUISection(SidewalkSection, SidewalkGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ExportSidewalkAtlas
        {
            set { if (value) Mod.RoadInspector?.RequestSidewalkAtlasExport(); }
        }

        [SettingsUISection(SidewalkSection, SidewalkUvGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 10, max = MaxTextureUvSize, step = 5, scalarMultiplier = 1)]
        public int SidewalkUvSizePercent { get; set; }

        [SettingsUISection(SidewalkSection, SidewalkUvGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureDepthStrength, step = 25, scalarMultiplier = 1)]
        public int SidewalkDepthStrengthPercent { get; set; }

        [SettingsUISection(SidewalkSection, SidewalkUvGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int SidewalkUvOffsetX { get; set; }

        [SettingsUISection(SidewalkSection, SidewalkUvGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int SidewalkUvOffsetY { get; set; }

        [SettingsUISection(SidewalkSection, SidewalkUvGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 360, step = 1, scalarMultiplier = 1)]
        public int SidewalkUvRotation { get; set; }

        [SettingsUISection(SidewalkSection, SidewalkCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 400, step = 10, scalarMultiplier = 1)]
        public int SidewalkCrackCount { get; set; }

        [SettingsUISection(SidewalkSection, SidewalkCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 25, max = 600, step = 25, scalarMultiplier = 1)]
        public int SidewalkCrackSizePercent { get; set; }

        [SettingsUISection(SidewalkSection, SidewalkCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 25, max = 400, step = 25, scalarMultiplier = 1)]
        public int SidewalkCrackWidthPercent { get; set; }

        [SettingsUISection(SidewalkSection, SidewalkCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureDepthStrength, step = 25, scalarMultiplier = 1)]
        public int SidewalkCrackDepthStrengthPercent { get; set; } = 100;

        [SettingsUISection(SidewalkSection, SidewalkCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 360, step = 1, scalarMultiplier = 1)]
        public int SidewalkCrackRotation { get; set; } = 0;

        [SettingsUISection(SidewalkSection, SidewalkCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 1, scalarMultiplier = 1)]
        public int SidewalkCrackOffsetX { get; set; } = 0;

        [SettingsUISection(SidewalkSection, SidewalkCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 1, scalarMultiplier = 1)]
        public int SidewalkCrackOffsetY { get; set; } = 0;

        [SettingsUISection(SidewalkSection, SidewalkCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 200, step = 10, scalarMultiplier = 1)]
        public int SidewalkCrackOpacityPercent { get; set; }

        [SettingsUISection(SidewalkSection, SidewalkCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0, max = 100, step = 10, scalarMultiplier = 1)]
        public int SidewalkCrackRandomnessPercent { get; set; }

        [SettingsUISection(SidewalkSection, SidewalkCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0.1f, max = 8, step = 0.05f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float SidewalkTextureTiling { get; set; }

        [SettingsUISection(SidewalkSection, SidewalkCrackGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ResetSidewalk
        {
            set
            {
                if (!value) return;
                SidewalkJunctionOverlay = false;
                SidewalkJunctionParking = false;
            SetSidewalkDefaults();
                ApplyAndSave();
            }
        }

        private void SetSidewalkDefaults()
        {

            ClearTextureModeProfiles(1); UseSidewalkNormalMap = false;
            SidewalkFilterColor = 0xffffff; SidewalkFilterOpacityPercent = 0;
            SidewalkEnabled = false;
            DiagnoseRoadEUAtlas = false;
            SidewalkDetail = 0;
            SidewalkUvSizePercent = 100;
            SidewalkDepthStrengthPercent = 0;
            SidewalkUvOffsetX = SidewalkUvOffsetY = SidewalkUvRotation = 0;
            SidewalkCrackDepthStrengthPercent = 100;
            SidewalkCrackRotation = SidewalkCrackOffsetX = SidewalkCrackOffsetY = 0;
            SidewalkCrackCount = 0;
            SidewalkCrackSizePercent = SidewalkCrackWidthPercent = SidewalkCrackOpacityPercent = 100;
            SidewalkCrackRandomnessPercent = 50;
            SidewalkTextureTiling = 1;
            ParkingSidewalkScaleU = ParkingSidewalkScaleV = 1f;
            SidewalkShiftU = SidewalkShiftV = 0;
        }
    }
}
