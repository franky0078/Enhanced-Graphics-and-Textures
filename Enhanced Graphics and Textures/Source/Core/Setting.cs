using Colossal.IO.AssetDatabase;
using System;
using System.Diagnostics;
using System.IO;
using Game.Modding;
using Game.Settings;
using Game.UI;

namespace Advanced_Graphics
{
    [FileLocation(nameof(Advanced_Graphics))]
    [SettingsUITabOrder(MainSection, RoadSection, SidewalkSection, GrassSection, AlleySidewalkSection, GravelSection, GravelSidewalkSection, RailSection)]
    [SettingsUIGroupOrder(LauncherGroup, TreeGroup, DetailGroup, CacheGroup, TerrainGroup, ShadowsGroup, SharpnessGroup, TextureGeneralGroup, BloomGroup, SkyGroup, RoadGroup, RoadUvGroup, RoadCrackGroup, RoadPanelGroup, SidewalkGroup, SidewalkUvGroup, SidewalkCrackGroup, GrassGroup, AlleySidewalkGroup, AlleySidewalkUvGroup, AlleySidewalkCrackGroup, GravelGroup, ShoulderGroup, RailGroup, RailUvGroup, ResetGroup, AboutGroup)]
    [SettingsUIShowGroupName(LauncherGroup, TreeGroup, DetailGroup, CacheGroup, TerrainGroup, ShadowsGroup, SharpnessGroup, TextureGeneralGroup, BloomGroup, SkyGroup, RoadGroup, RoadUvGroup, RoadCrackGroup, RoadPanelGroup, SidewalkGroup, SidewalkUvGroup, SidewalkCrackGroup, GrassGroup, AlleySidewalkGroup, AlleySidewalkUvGroup, AlleySidewalkCrackGroup, GravelGroup, ShoulderGroup, RailGroup, RailUvGroup, ResetGroup, AboutGroup)]
    public sealed partial class Setting : ModSetting
    {
        public const int MaxTextureUvSize = 3000;
        public const int MaxTextureDepthStrength = 3000;
        public const int MaxTextureUvOffset = 300;
        public const int MaxRoadCrackCount = 1200;
        public const int MaxRoadCrackSize = 1800;
        public const int MaxRoadCrackWidth = 1200;
        public const int MaxRoadCrackOpacity = 600;

        [SettingsUISection(MainSection, TextureGeneralGroup)]
        [SettingsUIButton]
        public bool ReloadAllCustomTextures
        {
            set { if (value) ReloadAllCustomTexturesNow(); }
        }

        internal void ReloadAllCustomTexturesNow()
        {
            // ReloadCustomTexture also clears the optional normal and crack caches.
            Mod.RoadAsphalt?.ReloadCustomTexture();
            Mod.Sidewalk?.ReloadCustomTexture();
            Mod.AlleySidewalk?.ReloadCustomTexture();
            Mod.Grass?.ReloadCustomTexture();
            Mod.RoadGravel?.ReloadCustomTexture();
            Mod.GravelShoulderSurface?.ReloadCustomTexture();
            Mod.RailGravel?.ReloadCustomTexture();
        }

        private float m_SurfaceTextureMipBias;
        [SettingsUISection(MainSection, TextureGeneralGroup)]
        [SettingsUISlider(min = -1f, max = 1f, step = 0.1f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float SurfaceTextureMipBias
        {
            get => m_SurfaceTextureMipBias;
            set => m_SurfaceTextureMipBias = float.IsNaN(value) || float.IsInfinity(value) ? 0f :
                Math.Max(-1f, Math.Min(1f, (float)Math.Round(value, 1)));
        }


        public const string AboutGroup = "About";
        public const string LauncherGroup = "Interface";
        public const string MainSection = "Main";
        public const string RoadSection = "Road";
        public const string SidewalkSection = "Sidewalk";
        public const string AlleySidewalkSection = "Alley sidewalk";
        public const string GravelSidewalkSection = "Gravel sidewalk";
        public const string AlleySidewalkGroup = "Alley sidewalk materials";
        public const string AlleySidewalkUvGroup = "Alley sidewalk UV and depth";
        public const string AlleySidewalkCrackGroup = "Alley sidewalk cracks";
        public const string RailSection = "Rail";
        public const string GravelSection = "Gravel roads";
        public const string GravelGroup = "Gravel road surface";
        public const string ShoulderGroup = "Gravel road edge";
        public const string TreeGroup = "Trees";
        public const string DetailGroup = "Details";
        public const string CacheGroup = "Cache";
        public const string TerrainGroup = "Terrain";
        public const string ShadowsGroup = "Shadows";
        public const string SharpnessGroup = "Sharpness";
        public const string TextureGeneralGroup = "General texture settings";
        public const string BloomGroup = "Bloom";
        public const string SkyGroup = "Sky";
        public const string RoadGroup = "Road materials";
        public const string RoadUvGroup = "Road UV and depth";
        public const string RoadCrackGroup = "Road cracks";
        public const string RoadPanelGroup = "Road panel";
        public const string SidewalkGroup = "Sidewalk materials";
        public const string SidewalkUvGroup = "Sidewalk UV and depth";
        public const string SidewalkCrackGroup = "Sidewalk cracks";
        public const string RailGroup = "Rail materials";
        public const string RailUvGroup = "Rail gravel UV and depth";
        public const string ResetGroup = "Reset";
        public const int DefaultCacheMB = 4096;
        public const int DefaultTerrainTessellation = 5;
        public const int DefaultTerrainPatchSize = 64;

        public enum RoadTextureSource { Generated, Custom }

        public Setting(IMod mod) : base(mod) { SetDefaults(); }

        // A separate first group provides the native divider before graphics controls.
        [SettingsUISection(MainSection, LauncherGroup)]
        public bool ShowAppButton { get; set; } = true;

        [SettingsUISection(MainSection, LauncherGroup)]
        public bool ShowGraphicsAppButton { get; set; } = true;

        // Legacy Lumina fix is no longer registered in the frontend.
        // Restore SettingsUISection(MainSection, LauncherGroup) when re-enabling it.
        [SettingsUIHidden]
        public bool LuminaNumberFix { get; set; } = false;

        [SettingsUIHidden]
        public float TextureGeneralWindowX { get; set; } = -1f;
        [SettingsUIHidden]
        public float TextureGeneralWindowY { get; set; } = -1f;

        [SettingsUIHidden]
        public float GraphicsWindowX { get; set; } = -1f;
        [SettingsUIHidden]
        public float GraphicsWindowY { get; set; } = -1f;

        [SettingsUISection(MainSection, TreeGroup)]
        [SettingsUISlider(min = 10, max = 500, step = 10, scalarMultiplier = 1)]
        public int TreeLodDistance { get; set; }

        [SettingsUISection(MainSection, DetailGroup)]
        [SettingsUISlider(min = 10, max = 500, step = 10, scalarMultiplier = 1)]
        public int GlobalDetailScalePercent { get; set; }

        // The game's own slider stops at 4096 MB. Values beyond it are experimental.
        [SettingsUISection(MainSection, CacheGroup)]
        [SettingsUISlider(min = 128, max = 16384, step = 128, scalarMultiplier = 1)]
        public int GeometryCacheSizeMB { get; set; }

        // The game's terrain tessellation slider stops at 5.
        [SettingsUISection(MainSection, TerrainGroup)]
        [SettingsUISlider(min = 1, max = 16, step = 1, scalarMultiplier = 1)]
        public int TerrainTessellationLevel { get; set; }

        [SettingsUISection(MainSection, TerrainGroup)]
        [SettingsUISlider(min = 4, max = 128, step = 1, scalarMultiplier = 1)]
        public int TerrainPatchSizeUnits { get; set; }

        // Higher shadow resolution and a new distance control are separate from
        [SettingsUISection(MainSection, ShadowsGroup)]
        [SettingsUISlider(min = 0, max = 8192, step = 512, scalarMultiplier = 1)]
        public int ShadowResolution { get; set; }

        [SettingsUISection(MainSection, ShadowsGroup)]
        [SettingsUISlider(min = 0, max = 2000, step = 50, scalarMultiplier = 1)]
        public int ShadowDistanceMeters { get; set; }

        // Both thresholds are not exposed by the game's regular graphics sliders.
        [SettingsUISection(MainSection, ShadowsGroup)]
        [SettingsUISlider(min = 0, max = 10, step = 1, scalarMultiplier = 1)]
        public int SmallShadowCasterThreshold { get; set; }

        // Actual HDRP camera strength. 0 restores the camera's original value.
        [SettingsUISection(MainSection, SharpnessGroup)]
        // Game.UI.Unit formats the actual HDRP value with two decimal places.
        [SettingsUISlider(min = 0, max = 2, step = 0.05f,
            scalarMultiplier = 1, unit = Unit.kFloatTwoFractions)]
        public float TaaSharpenStrength { get; set; }

        public enum TaaStabilityControl { GameDefault, ReducedFlicker, StrongStability }

        // Retain the old serialized enum so saved settings still deserialize.
        [SettingsUIHidden]
        public TaaStabilityControl TaaStability { get; set; }

        [SettingsUISection(MainSection, SharpnessGroup)]
        public bool TaaStabilityEnabled { get; set; }

        [SettingsUISection(MainSection, SharpnessGroup)]
        [SettingsUISlider(min = 0, max = 1, step = 0.01f,
            scalarMultiplier = 1, unit = Unit.kFloatTwoFractions)]
        public float TaaStabilityStrength { get; set; } = 0.5f;

        [SettingsUISection(MainSection, SharpnessGroup)]
        [SettingsUIButton]
        public bool ResetTaaStability { set { if (value) { TaaStabilityEnabled = false; TaaStabilityStrength = 0.5f; ApplyAndSave(); } } }

        public enum BloomControl { GameDefault, Enabled, Disabled }

        [SettingsUISection(MainSection, BloomGroup)]
        public BloomControl BloomMode { get; set; }

        private float m_BloomIntensity = 0.2f;
        [SettingsUISection(MainSection, BloomGroup)]
        [SettingsUISlider(min = 0, max = 1, step = 0.01f,
            scalarMultiplier = 1, unit = Unit.kFloatTwoFractions)]
        public float BloomIntensity
        {
            get => m_BloomIntensity;
            // Migrate invalid values saved by the earlier 0–5 slider to vanilla intensity.
            set => m_BloomIntensity = float.IsNaN(value) || float.IsInfinity(value) ||
                value < 0f || value > 1f ? 0.2f : value;
        }

        // A separate HDRP celestial halo, independent of the screen-wide Bloom.
        [SettingsUISection(MainSection, BloomGroup)]
        [SettingsUISlider(min = 0, max = 5, step = 0.05f,
            scalarMultiplier = 1, unit = Unit.kFloatTwoFractions)]
        public float SunBloomStrength { get; set; } = 1f;

        [SettingsUISection(MainSection, SkyGroup)]
        [SettingsUISlider(min = -5, max = 5, step = 0.05f,
            scalarMultiplier = 1, unit = Unit.kFloatTwoFractions)]
        public float SkyExposure { get; set; }

        [SettingsUISection(MainSection, BloomGroup)]
        [SettingsUIButton]
        public bool ResetBloom
        {
            set { if (value) { ResetBloomDefaults(); ApplyAndSave(); } }
        }

        [SettingsUISection(MainSection, BloomGroup)]
        [SettingsUIButton]
        public bool ResetSunBloom
        {
            set { if (value) { SunBloomStrength = 1f; ApplyAndSave(); } }
        }

        [SettingsUISection(MainSection, SkyGroup)]
        [SettingsUIButton]
        public bool ResetSkyExposure
        {
            set { if (value) { ResetSkyExposureDefaults(); ApplyAndSave(); } }
        }

        private void ResetBloomDefaults()
        {
            BloomMode = BloomControl.GameDefault;
            BloomIntensity = 0.2f;
        }

        private void ResetSkyExposureDefaults()
        {
            SkyExposure = 0f;
        }

        [SettingsUIHidden]
        public bool HideDebugControls => !Mod.DebugBackendEnabled;

        [SettingsUIHideByCondition(typeof(Setting), nameof(HideDebugControls))]
        [SettingsUISection(RoadSection, RoadGroup)]
        [SettingsUIButton]
        public bool InspectRoadMaterials
        {
            set { if (value) Mod.RoadInspector?.RequestSurvey(); }
        }

        [SettingsUIHideByCondition(typeof(Setting), nameof(HideDebugControls))]
        [SettingsUISection(RoadSection, RoadGroup)]
        [SettingsUIButton]
        public bool ExportGravelRoadMaterials
        {
            set { if (value) Mod.RoadInspector?.RequestGravelSurvey(); }
        }

        [SettingsUIHideByCondition(typeof(Setting), nameof(HideDebugControls))]
        [SettingsUISection(GrassSection, GrassGroup)]
        [SettingsUIButton]
        public bool ExportGrassMaterials
        {
            set { if (value && Mod.DebugBackendEnabled) Mod.RoadInspector?.RequestGrassSurvey(); }
        }

        [SettingsUIHideByCondition(typeof(Setting), nameof(HideDebugControls))]
        [SettingsUISection(GrassSection, GrassGroup)]
        [SettingsUIButton]
        public bool OpenGrassDiagnosticFolder
        {
            set
            {
                if (!value || !Mod.DebugBackendEnabled) return;
                try
                {
                    Directory.CreateDirectory(RoadMaterialInspector.GrassDiagnosticFolder);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = RoadMaterialInspector.GrassDiagnosticFolder,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex) { Mod.log.Error(ex, "Could not open grass diagnostic folder."); }
            }
        }

        [SettingsUISection(GravelSection, GravelGroup)]
        public bool RoadGravelEnabled { get; set; }
        [SettingsUIHidden]
        public bool ShowGravelPanelButton { get; set; } = true;
        private int m_RoadGravelDetail;
        [SettingsUISection(GravelSection, GravelGroup)]
        [SettingsUISlider(min = 0, max = 2, step = 1, scalarMultiplier = 1)]
        public int RoadGravelDetail
        {
            get => m_RoadGravelDetail;
            set => SwitchTextureMode(3, ref m_RoadGravelDetail, value);
        }
        [SettingsUISection(GravelSection, GravelGroup)]
        [SettingsUISlider(min = 50, max = MaxTextureUvSize, step = 25, scalarMultiplier = 1)]
        public int RoadGravelUvSizePercent { get; set; } = 100;
        [SettingsUISection(GravelSection, GravelGroup)]
        [SettingsUISlider(min = 0, max = MaxTextureDepthStrength, step = 25, scalarMultiplier = 1)]
        public int RoadGravelDepthStrengthPercent { get; set; }
        [SettingsUISection(GravelSection, GravelGroup)]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int RoadGravelUvOffsetX { get; set; }
        [SettingsUISection(GravelSection, GravelGroup)]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int RoadGravelUvOffsetY { get; set; }
        [SettingsUISection(GravelSection, GravelGroup)]
        [SettingsUISlider(min = 0, max = 360, step = 1, scalarMultiplier = 1)]
        public int RoadGravelUvRotation { get; set; }
        [SettingsUISection(GravelSection, GravelGroup)]
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
        [SettingsUIButton]
        public bool ReloadGravelTexture { set { if (value) Mod.RoadGravel?.ReloadCustomTexture(); } }
        [SettingsUISection(GravelSection, GravelGroup)]
        [SettingsUIButton]
        public bool ResetRoadGravel { set { if (value) { ResetRoadGravelDefaults(); ApplyAndSave(); } } }

        [SettingsUISection(GravelSidewalkSection, ShoulderGroup)]
        public bool GravelShoulderEnabled { get; set; }
        [SettingsUISection(GravelSidewalkSection, ShoulderGroup)]
        [SettingsUISlider(min = 50, max = MaxTextureUvSize, step = 25, scalarMultiplier = 1)]
        public int GravelShoulderUvSizePercent { get; set; } = 100;
        [SettingsUISection(GravelSidewalkSection, ShoulderGroup)]
        [SettingsUISlider(min = 0, max = MaxTextureDepthStrength, step = 25, scalarMultiplier = 1)]
        public int GravelShoulderDepthStrengthPercent { get; set; }
        private int m_GravelShoulderDetail;
        [SettingsUISection(GravelSidewalkSection, ShoulderGroup)]
        [SettingsUISlider(min = 0, max = 2, step = 1, scalarMultiplier = 1)]
        public int GravelShoulderDetail
        {
            get => m_GravelShoulderDetail;
            set => SwitchTextureMode(4, ref m_GravelShoulderDetail, value);
        }
        [SettingsUISection(GravelSidewalkSection, ShoulderGroup)]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int GravelShoulderUvOffsetX { get; set; }
        [SettingsUISection(GravelSidewalkSection, ShoulderGroup)]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int GravelShoulderUvOffsetY { get; set; }
        [SettingsUISection(GravelSidewalkSection, ShoulderGroup)]
        [SettingsUISlider(min = 0, max = 360, step = 1, scalarMultiplier = 1)]
        public int GravelShoulderUvRotation { get; set; }
        [SettingsUISection(GravelSidewalkSection, ShoulderGroup)]
        [SettingsUIButton]
        public bool ReloadGravelShoulderTexture { set { if (value) Mod.GravelShoulderSurface?.ReloadCustomTexture(); } }
        [SettingsUISection(GravelSidewalkSection, ShoulderGroup)]
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

        // Kept for migrating saves made with the previous source selector.
        [SettingsUIHidden]
        public RoadTextureSource AsphaltSource { get; set; }

        [SettingsUIHidden]
        public int RoadTextureLayoutVersion { get; set; }

        [SettingsUIHidden]
        public int AlleySidewalkSettingsVersion { get; set; }

        private int m_RoadAsphaltDetail;
        [SettingsUISection(RoadSection, RoadGroup)]
        [SettingsUISlider(min = 0, max = 2, step = 1, scalarMultiplier = 1)]
        public int RoadAsphaltDetail
        {
            get => m_RoadAsphaltDetail;
            set => SwitchTextureMode(0, ref m_RoadAsphaltDetail, value);
        }

        [SettingsUISection(RoadSection, RoadGroup)]
        public bool RoadAsphaltEnabled { get; set; }

        [SettingsUISection(RoadSection, RoadGroup)]
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
        [SettingsUIButton]
        public bool ReloadRoadTexture { set { if (value) Mod.RoadAsphalt?.ReloadCustomTexture(); } }

        [SettingsUISection(RoadSection, RoadUvGroup)]
        [SettingsUISlider(min = 50, max = MaxTextureUvSize, step = 25, scalarMultiplier = 1)]
        public int RoadUvSizePercent { get; set; }

        private float m_ParkingTextureScaleFactor = 1f;
        [SettingsUISection(RoadSection, RoadUvGroup)]
        [SettingsUISlider(min = 0.1f, max = 8f, step = 0.1f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float ParkingTextureScaleFactor
        {
            get => m_ParkingTextureScaleFactor;
            set => m_ParkingTextureScaleFactor = float.IsNaN(value) || float.IsInfinity(value) ? 1f :
                Math.Max(0.1f, Math.Min(8f, (float)Math.Round(value, 1)));
        }

        private float m_ParkingDepthStrengthFactor = 1f;
        [SettingsUISection(RoadSection, RoadUvGroup)]
        [SettingsUISlider(min = 0, max = 8f, step = 0.1f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float ParkingDepthStrengthFactor
        {
            get => m_ParkingDepthStrengthFactor;
            set => m_ParkingDepthStrengthFactor = float.IsNaN(value) || float.IsInfinity(value) ? 1f :
                Math.Max(0f, Math.Min(8f, (float)Math.Round(value, 1)));
        }

        [SettingsUISection(RoadSection, RoadUvGroup)]
        [SettingsUISlider(min = 0, max = MaxTextureDepthStrength, step = 25, scalarMultiplier = 1)]
        public int RoadDepthStrengthPercent { get; set; }

        [SettingsUISection(RoadSection, RoadUvGroup)]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int RoadUvOffsetX { get; set; }

        [SettingsUISection(RoadSection, RoadUvGroup)]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int RoadUvOffsetY { get; set; }

        [SettingsUISection(RoadSection, RoadUvGroup)]
        [SettingsUISlider(min = 0, max = 360, step = 1, scalarMultiplier = 1)]
        public int RoadUvRotation { get; set; }

        [SettingsUISection(RoadSection, RoadCrackGroup)]
        [SettingsUISlider(min = 0, max = MaxRoadCrackCount, step = 10, scalarMultiplier = 1)]
        public int RoadCrackCount { get; set; }

        [SettingsUISection(RoadSection, RoadCrackGroup)]
        public bool UseCustomRoadCracks
        {
            get => m_UseCustomRoadCracks;
            set => SwitchRoadCrackSource(value);
        }

        [SettingsUISection(RoadSection, RoadCrackGroup)]
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
        [SettingsUIButton]
        public bool ReloadRoadCracks { set { if (value) Mod.RoadAsphalt?.ReloadCustomTexture(); } }

        [SettingsUISection(RoadSection, RoadCrackGroup)]
        [SettingsUISlider(min = 25, max = MaxRoadCrackSize, step = 25, scalarMultiplier = 1)]
        public int RoadCrackSizePercent { get; set; }

        [SettingsUISection(RoadSection, RoadCrackGroup)]
        [SettingsUISlider(min = 25, max = MaxRoadCrackWidth, step = 25, scalarMultiplier = 1)]
        public int RoadCrackWidthPercent { get; set; }

        [SettingsUISection(RoadSection, RoadCrackGroup)]
        [SettingsUISlider(min = 0, max = MaxRoadCrackOpacity, step = 10, scalarMultiplier = 1)]
        public int RoadCrackOpacityPercent { get; set; }

        [SettingsUISection(RoadSection, RoadCrackGroup)]
        [SettingsUISlider(min = 0, max = 100, step = 10, scalarMultiplier = 1)]
        public int RoadCrackRandomnessPercent { get; set; }

        [SettingsUISection(RoadSection, RoadCrackGroup)]
        [SettingsUISlider(min = 0.1f, max = 8, step = 0.1f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float RoadTextureTiling { get; set; }

        [SettingsUISection(RoadSection, RoadCrackGroup)]
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

        [SettingsUISection(SidewalkSection, SidewalkGroup)]
        public bool SidewalkEnabled { get; set; }

        [SettingsUIHidden]
        public bool ShowSidewalkPanelButton { get; set; } = true;

        [SettingsUIHidden]
        public bool ShowRailPanelButton { get; set; } = true;

        [SettingsUIHideByCondition(typeof(Setting), nameof(HideDebugControls))]
        [SettingsUISection(SidewalkSection, SidewalkGroup)]
        public bool DiagnoseRoadEUAtlas { get; set; }

        private int m_SidewalkDetail;
        [SettingsUISection(SidewalkSection, SidewalkGroup)]
        [SettingsUISlider(min = 0, max = 2, step = 1, scalarMultiplier = 1)]
        public int SidewalkDetail
        {
            get => m_SidewalkDetail;
            set => SwitchTextureMode(1, ref m_SidewalkDetail, value);
        }

        [SettingsUISection(SidewalkSection, SidewalkGroup)]
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
        [SettingsUIButton]
        public bool ReloadSidewalkTexture { set { if (value) Mod.Sidewalk?.ReloadCustomTexture(); } }

        [SettingsUIHideByCondition(typeof(Setting), nameof(HideDebugControls))]
        [SettingsUISection(SidewalkSection, SidewalkGroup)]
        [SettingsUIButton]
        public bool ExportSidewalkAtlas
        {
            set { if (value) Mod.RoadInspector?.RequestSidewalkAtlasExport(); }
        }

        [SettingsUIHideByCondition(typeof(Setting), nameof(HideDebugControls))]
        [SettingsUISection(RailSection, RailGroup)]
        [SettingsUIButton]
        public bool ExportRailMaterials
        {
            set { if (value) Mod.RailInspector?.RequestExport(); }
        }

        [SettingsUIHideByCondition(typeof(Setting), nameof(HideDebugControls))]
        [SettingsUISection(RailSection, RailGroup)]
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
        public bool RailGravelEnabled { get; set; }

        private int m_RailGravelDetail;
        [SettingsUISection(RailSection, RailGroup)]
        [SettingsUISlider(min = 0, max = 2, step = 1, scalarMultiplier = 1)]
        public int RailGravelDetail
        {
            get => m_RailGravelDetail;
            set => SwitchTextureMode(5, ref m_RailGravelDetail, value);
        }

        [SettingsUISection(RailSection, RailGroup)]
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
        [SettingsUIButton]
        public bool ReloadRailTexture { set { if (value) Mod.RailGravel?.ReloadCustomTexture(); } }

        [SettingsUISection(RailSection, RailUvGroup)]
        [SettingsUISlider(min = 50, max = MaxTextureUvSize, step = 25, scalarMultiplier = 1)]
        public int RailGravelUvSizePercent { get; set; }

        [SettingsUISection(RailSection, RailUvGroup)]
        [SettingsUISlider(min = 0, max = MaxTextureDepthStrength, step = 25, scalarMultiplier = 1)]
        public int RailGravelDepthStrengthPercent { get; set; }

        [SettingsUISection(RailSection, RailUvGroup)]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int RailGravelUvOffsetX { get; set; }

        [SettingsUISection(RailSection, RailUvGroup)]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int RailGravelUvOffsetY { get; set; }

        [SettingsUISection(RailSection, RailUvGroup)]
        [SettingsUISlider(min = 0, max = 360, step = 1, scalarMultiplier = 1)]
        public int RailGravelUvRotation { get; set; }

        [SettingsUISection(RailSection, RailUvGroup)]
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

        [SettingsUISection(SidewalkSection, SidewalkUvGroup)]
        [SettingsUISlider(min = 50, max = MaxTextureUvSize, step = 25, scalarMultiplier = 1)]
        public int SidewalkUvSizePercent { get; set; }

        [SettingsUISection(SidewalkSection, SidewalkUvGroup)]
        [SettingsUISlider(min = 0, max = MaxTextureDepthStrength, step = 25, scalarMultiplier = 1)]
        public int SidewalkDepthStrengthPercent { get; set; }

        [SettingsUISection(SidewalkSection, SidewalkUvGroup)]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int SidewalkUvOffsetX { get; set; }

        [SettingsUISection(SidewalkSection, SidewalkUvGroup)]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int SidewalkUvOffsetY { get; set; }

        [SettingsUISection(SidewalkSection, SidewalkUvGroup)]
        [SettingsUISlider(min = 0, max = 360, step = 1, scalarMultiplier = 1)]
        public int SidewalkUvRotation { get; set; }

        [SettingsUISection(SidewalkSection, SidewalkCrackGroup)]
        [SettingsUISlider(min = 0, max = 400, step = 10, scalarMultiplier = 1)]
        public int SidewalkCrackCount { get; set; }

        [SettingsUISection(SidewalkSection, SidewalkCrackGroup)]
        [SettingsUISlider(min = 25, max = 600, step = 25, scalarMultiplier = 1)]
        public int SidewalkCrackSizePercent { get; set; }

        [SettingsUISection(SidewalkSection, SidewalkCrackGroup)]
        [SettingsUISlider(min = 25, max = 400, step = 25, scalarMultiplier = 1)]
        public int SidewalkCrackWidthPercent { get; set; }

        [SettingsUISection(SidewalkSection, SidewalkCrackGroup)]
        [SettingsUISlider(min = 0, max = 200, step = 10, scalarMultiplier = 1)]
        public int SidewalkCrackOpacityPercent { get; set; }

        [SettingsUISection(SidewalkSection, SidewalkCrackGroup)]
        [SettingsUISlider(min = 0, max = 100, step = 10, scalarMultiplier = 1)]
        public int SidewalkCrackRandomnessPercent { get; set; }

        [SettingsUISection(SidewalkSection, SidewalkCrackGroup)]
        [SettingsUISlider(min = 0.1f, max = 8, step = 0.1f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float SidewalkTextureTiling { get; set; }

        [SettingsUISection(SidewalkSection, SidewalkCrackGroup)]
        [SettingsUIButton]
        public bool ResetSidewalk
        {
            set
            {
                if (!value) return;
                SetSidewalkDefaults();
                ApplyAndSave();
            }
        }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkGroup)]
        public bool AlleySidewalkEnabled { get; set; }

        [SettingsUIHidden]
        public bool ShowAlleySidewalkPanelButton { get; set; } = true;

        private int m_AlleySidewalkDetail;
        [SettingsUISection(AlleySidewalkSection, AlleySidewalkGroup)]
        [SettingsUISlider(min = 0, max = 2, step = 1, scalarMultiplier = 1)]
        public int AlleySidewalkDetail
        {
            get => m_AlleySidewalkDetail;
            set => SwitchTextureMode(2, ref m_AlleySidewalkDetail, value);
        }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkGroup)]
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
        [SettingsUIButton]
        public bool ReloadAlleySidewalkTexture { set { if (value) Mod.AlleySidewalk?.ReloadCustomTexture(); } }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkUvGroup)]
        [SettingsUISlider(min = 50, max = MaxTextureUvSize, step = 25, scalarMultiplier = 1)]
        public int AlleySidewalkUvSizePercent { get; set; }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkUvGroup)]
        [SettingsUISlider(min = 0, max = MaxTextureDepthStrength, step = 25, scalarMultiplier = 1)]
        public int AlleySidewalkDepthStrengthPercent { get; set; }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkUvGroup)]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int AlleySidewalkUvOffsetX { get; set; }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkUvGroup)]
        [SettingsUISlider(min = 0, max = MaxTextureUvOffset, step = 5, scalarMultiplier = 1)]
        public int AlleySidewalkUvOffsetY { get; set; }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkUvGroup)]
        [SettingsUISlider(min = 0, max = 360, step = 1, scalarMultiplier = 1)]
        public int AlleySidewalkUvRotation { get; set; }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkCrackGroup)]
        [SettingsUISlider(min = 0, max = 400, step = 10, scalarMultiplier = 1)]
        public int AlleySidewalkCrackCount { get; set; }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkCrackGroup)]
        [SettingsUISlider(min = 25, max = 600, step = 25, scalarMultiplier = 1)]
        public int AlleySidewalkCrackSizePercent { get; set; }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkCrackGroup)]
        [SettingsUISlider(min = 25, max = 400, step = 25, scalarMultiplier = 1)]
        public int AlleySidewalkCrackWidthPercent { get; set; }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkCrackGroup)]
        [SettingsUISlider(min = 0, max = 200, step = 10, scalarMultiplier = 1)]
        public int AlleySidewalkCrackOpacityPercent { get; set; }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkCrackGroup)]
        [SettingsUISlider(min = 0, max = 100, step = 10, scalarMultiplier = 1)]
        public int AlleySidewalkCrackRandomnessPercent { get; set; }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkCrackGroup)]
        [SettingsUISlider(min = 0.1f, max = 8, step = 0.1f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float AlleySidewalkTextureTiling { get; set; }

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkCrackGroup)]
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

        [SettingsUISection(MainSection, TreeGroup)]
        [SettingsUIButton]
        public bool ResetTree { set { if (value) { TreeLodDistance = 100; ApplyAndSave(); } } }

        [SettingsUISection(MainSection, DetailGroup)]
        [SettingsUIButton]
        public bool ResetDetail { set { if (value) { GlobalDetailScalePercent = 100; ApplyAndSave(); } } }

        [SettingsUISection(MainSection, CacheGroup)]
        [SettingsUIButton]
        public bool ResetCache { set { if (value) { GeometryCacheSizeMB = DefaultCacheMB; ApplyAndSave(); } } }

        [SettingsUISection(MainSection, TerrainGroup)]
        [SettingsUIButton]
        public bool ResetTessellation { set { if (value) { TerrainTessellationLevel = DefaultTerrainTessellation; ApplyAndSave(); } } }

        [SettingsUISection(MainSection, TerrainGroup)]
        [SettingsUIButton]
        public bool ResetPatchSize { set { if (value) { TerrainPatchSizeUnits = DefaultTerrainPatchSize; ApplyAndSave(); } } }

        [SettingsUISection(MainSection, ShadowsGroup)]
        [SettingsUIButton]
        public bool ResetShadowResolution { set { if (value) { ShadowResolution = 0; ApplyAndSave(); } } }

        [SettingsUISection(MainSection, ShadowsGroup)]
        [SettingsUIButton]
        public bool ResetShadowDistance { set { if (value) { ShadowDistanceMeters = 0; ApplyAndSave(); } } }

        [SettingsUISection(MainSection, ShadowsGroup)]
        [SettingsUIButton]
        public bool ResetSmallShadows { set { if (value) { SmallShadowCasterThreshold = 0; ApplyAndSave(); } } }

        [SettingsUISection(MainSection, SharpnessGroup)]
        [SettingsUIButton]
        public bool ResetTaaSharpen { set { if (value) { TaaSharpenStrength = 0; ApplyAndSave(); } } }

        [SettingsUISection(MainSection, ResetGroup)]
        [SettingsUIButton]
        public bool ResetAll { set { if (value) { SetDefaults(); ApplyAndSave(); } } }

        [SettingsUISection(MainSection, AboutGroup)]
        public string Version => typeof(Mod).Assembly.GetName().Version?.ToString(3) ?? "Unknown";

        public override void SetDefaults()
        {
            ResetEndcapValues();
            UseRoadNormalMap = UseSidewalkNormalMap = UseAlleySidewalkNormalMap = UseRoadGravelNormalMap = UseGravelShoulderNormalMap = UseRailNormalMap = UseGrassNormalMap = false;
            SurfaceTextureMipBias = 0f;
            RoadFilterColor = SidewalkFilterColor = AlleySidewalkFilterColor = RoadGravelFilterColor = GravelShoulderFilterColor = RailFilterColor = 0xffffff;
            Mod.Graphics?.Capture();
            TreeLodDistance = 100;
            GlobalDetailScalePercent = 100;
            GeometryCacheSizeMB = DefaultCacheMB;
            TerrainTessellationLevel = DefaultTerrainTessellation;
            TerrainPatchSizeUnits = DefaultTerrainPatchSize;
            ShadowResolution = 0;
            ShadowDistanceMeters = 0;
            SmallShadowCasterThreshold = 0;
            TaaSharpenStrength = 0;
            TaaStability = TaaStabilityControl.GameDefault;
            TaaStabilityEnabled = false;
            TaaStabilityStrength = 0.5f;
            ResetBloomDefaults();
            SunBloomStrength = 1f;
            ResetSkyExposureDefaults();
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
            RoadCrackRandomnessPercent = 50;
            ShowAppButton = true;
            ShowGraphicsAppButton = true;
            LuminaNumberFix = false;
            ShowRoadPanelButton = true;
            ShowSidewalkPanelButton = true;
            ShowRailPanelButton = true;
            DiagnoseRoadEUAtlas = false;
            RoadTextureTiling = 1;
            ParkingTextureScaleFactor = 1f;
            ParkingDepthStrengthFactor = 1f;
            RoadUvOffsetX = RoadUvOffsetY = RoadUvRotation = 0;
            SetSidewalkDefaults();
            SetGrassDefaults();
            SetAlleySidewalkDefaults();
            ResetRailDefaults();
            ResetRoadGravelDefaults();
            ShowGravelPanelButton = true;
            GravelShoulderEnabled = false;
            GravelShoulderUvSizePercent = 100;
            GravelShoulderDepthStrengthPercent = 0;
            GravelShoulderDetail = 0;
            GravelShoulderUvOffsetX = GravelShoulderUvOffsetY = GravelShoulderUvRotation = 0;
            RoadFilterOpacityPercent = SidewalkFilterOpacityPercent = AlleySidewalkFilterOpacityPercent = RoadGravelFilterOpacityPercent = GravelShoulderFilterOpacityPercent = RailFilterOpacityPercent = 100;
            ClearAllTextureModeProfiles();
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
            SidewalkCrackCount = 0;
            SidewalkCrackSizePercent = SidewalkCrackWidthPercent = SidewalkCrackOpacityPercent = 100;
            SidewalkCrackRandomnessPercent = 50;
            SidewalkTextureTiling = 1;
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
            AlleySidewalkCrackCount = 0;
            AlleySidewalkCrackSizePercent = AlleySidewalkCrackWidthPercent = AlleySidewalkCrackOpacityPercent = 100;
            AlleySidewalkCrackRandomnessPercent = 50;
            AlleySidewalkTextureTiling = 1;
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


        public override void Apply()
        {
            CaptureTextureModeProfiles();
            base.Apply();
            ApplyToSystem();
        }

        internal void ApplyToSystem()
        {
            RoadTextureTiling = Math.Max(0.1f, Math.Min(8f, (float)Math.Round(RoadTextureTiling, 1)));
            CaptureTextureModeProfiles();
            if (Mod.TreeLod != null) Mod.TreeLod.DistancePercent = TreeLodDistance;
            Mod.Graphics?.Apply(this);
            Mod.RoadGravel?.Configure(RoadGravelEnabled && (RoadGravelDetail != 0 || RoadGravelUvSizePercent != 100 ||
                RoadGravelDepthStrengthPercent != 0 || TextureColorFilter.IsActive(RoadGravelFilterColor, RoadGravelFilterOpacityPercent) || RoadGravelUvOffsetX != 0 || RoadGravelUvOffsetY != 0 || RoadGravelUvRotation != 0), RoadGravelDetail,
                RoadGravelDetail == 2 ? RoadTextureSource.Custom : RoadTextureSource.Generated,
                RoadGravelUvSizePercent, RoadGravelDepthStrengthPercent, 1f,
                RoadGravelUvOffsetX, RoadGravelUvOffsetY, RoadGravelUvRotation, 0, 100, 100, 100, 50, RoadGravelFilterColor, RoadGravelFilterOpacityPercent);
            Mod.GravelShoulderSurface?.Configure(GravelShoulderEnabled && GravelShoulderDetail != 0,
                GravelShoulderDetail, RoadTextureSource.Generated, 100, 100, 1f,
                0, 0, 0, 0, 100, 100, 100, 50);
            Mod.GravelShoulder?.Configure(GravelShoulderEnabled, GravelShoulderUvSizePercent, GravelShoulderDepthStrengthPercent,
                GravelShoulderDetail, GravelShoulderUvOffsetX, GravelShoulderUvOffsetY, GravelShoulderUvRotation, GravelShoulderFilterColor, GravelShoulderFilterOpacityPercent);
            Mod.RoadAsphalt?.Configure(RoadAsphaltEnabled, RoadAsphaltDetail, RoadAsphaltDetail == 2 ? RoadTextureSource.Custom : RoadTextureSource.Generated,
                RoadUvSizePercent, RoadDepthStrengthPercent, RoadTextureTiling,
                RoadUvOffsetX, RoadUvOffsetY, RoadUvRotation, RoadCrackCount, RoadCrackSizePercent,
                RoadCrackWidthPercent, RoadCrackOpacityPercent, RoadCrackRandomnessPercent, RoadFilterColor, RoadFilterOpacityPercent, UseCustomRoadCracks);
            Mod.Sidewalk?.Configure(SidewalkEnabled, SidewalkDetail, SidewalkDetail == 2 ? RoadTextureSource.Custom : RoadTextureSource.Generated,
                SidewalkUvSizePercent, SidewalkDepthStrengthPercent, SidewalkTextureTiling,
                SidewalkUvOffsetX, SidewalkUvOffsetY, SidewalkUvRotation, SidewalkCrackCount, SidewalkCrackSizePercent,
                SidewalkCrackWidthPercent, SidewalkCrackOpacityPercent, SidewalkCrackRandomnessPercent, SidewalkFilterColor, SidewalkFilterOpacityPercent);
            Mod.AlleySidewalk?.Configure(AlleySidewalkEnabled, AlleySidewalkDetail, AlleySidewalkDetail == 2 ? RoadTextureSource.Custom : RoadTextureSource.Generated,
                AlleySidewalkUvSizePercent, AlleySidewalkDepthStrengthPercent, AlleySidewalkTextureTiling,
                AlleySidewalkUvOffsetX, AlleySidewalkUvOffsetY, AlleySidewalkUvRotation, AlleySidewalkCrackCount, AlleySidewalkCrackSizePercent,
                AlleySidewalkCrackWidthPercent, AlleySidewalkCrackOpacityPercent, AlleySidewalkCrackRandomnessPercent, AlleySidewalkFilterColor, AlleySidewalkFilterOpacityPercent);
            Mod.AlleySidewalkAtlas?.Configure(AlleySidewalkEnabled &&
                (AlleySidewalkDetail != 0 || AlleySidewalkUvSizePercent != 100 || AlleySidewalkDepthStrengthPercent != 0 ||
                 AlleySidewalkUvOffsetX != 0 || AlleySidewalkUvOffsetY != 0 || AlleySidewalkUvRotation != 0 || AlleySidewalkCrackCount != 0 || TextureColorFilter.IsActive(AlleySidewalkFilterColor, AlleySidewalkFilterOpacityPercent)), false);
            // Leave the original RoadEU atlas untouched at unchanged defaults.
            Mod.SidewalkAtlas?.Configure(SidewalkEnabled &&
                ((Mod.DebugBackendEnabled && DiagnoseRoadEUAtlas) || SidewalkDetail != 0 || SidewalkUvSizePercent != 100 ||
                 SidewalkDepthStrengthPercent != 0 || SidewalkUvOffsetX != 0 ||
                 SidewalkUvOffsetY != 0 || SidewalkUvRotation != 0 || SidewalkCrackCount != 0 || TextureColorFilter.IsActive(SidewalkFilterColor, SidewalkFilterOpacityPercent)),
                Mod.DebugBackendEnabled && DiagnoseRoadEUAtlas);
            Mod.RailGravel?.Configure(RailGravelEnabled, RailGravelDetail,
                RailGravelDetail == 2 ? RoadTextureSource.Custom : RoadTextureSource.Generated,
                RailGravelUvSizePercent, RailGravelDepthStrengthPercent, 1f,
                RailGravelUvOffsetX, RailGravelUvOffsetY, RailGravelUvRotation,
                0, 100, 100, 100, 50, RailFilterColor, RailFilterOpacityPercent);
            Mod.RoadAsphalt?.UpdateTextureSampling();
            Mod.Sidewalk?.UpdateTextureSampling();
            Mod.AlleySidewalk?.UpdateTextureSampling();
            ApplyGrassToSystem();
            Mod.Grass?.UpdateTextureSampling();
            Mod.RoadGravel?.UpdateTextureSampling();
            Mod.GravelShoulderSurface?.UpdateTextureSampling();
            Mod.RailGravel?.UpdateTextureSampling();
            Mod.GravelShoulder?.UpdateTextureSampling();
            Mod.SidewalkAtlas?.UpdateTextureSampling();
            Mod.AlleySidewalkAtlas?.UpdateTextureSampling();
            Mod.ParkingRoadAtlas?.UpdateTextureSampling();
        }
    }
}






