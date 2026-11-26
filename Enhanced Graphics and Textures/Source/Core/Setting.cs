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
    [SettingsUITabOrder(MainSection, RoadSection, SidewalkSection, FootpathSection, GrassSection, AlleySidewalkSection, GravelSection, GravelSidewalkSection, RailSection)]
    [SettingsUIGroupOrder(LauncherGroup, TreeGroup, DetailGroup, CacheGroup, TerrainGroup, ShadowsGroup, SharpnessGroup, RoadWearGroup, TextureGeneralGroup, BloomGroup, SkyGroup, RoadGroup, RoadUvGroup, RoadCrackGroup, RoadPanelGroup, SidewalkGroup, SidewalkUvGroup, SidewalkCrackGroup, FootpathGroup, GrassGroup, AlleySidewalkGroup, AlleySidewalkUvGroup, AlleySidewalkCrackGroup, GravelGroup, ShoulderGroup, RailGroup, RailUvGroup, ResetGroup, AboutGroup)]
    [SettingsUIShowGroupName(LauncherGroup, TreeGroup, DetailGroup, CacheGroup, TerrainGroup, ShadowsGroup, SharpnessGroup, RoadWearGroup, TextureGeneralGroup, BloomGroup, SkyGroup, RoadGroup, RoadUvGroup, RoadCrackGroup, RoadPanelGroup, SidewalkGroup, SidewalkUvGroup, SidewalkCrackGroup, FootpathGroup, GrassGroup, AlleySidewalkGroup, AlleySidewalkUvGroup, AlleySidewalkCrackGroup, GravelGroup, ShoulderGroup, RailGroup, RailUvGroup, ResetGroup, AboutGroup)]
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
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
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
            Mod.Footpath?.ReloadCustomTexture();
            Mod.RoadGravel?.ReloadCustomTexture();
            Mod.GravelShoulderSurface?.ReloadCustomTexture();
            Mod.RailGravel?.ReloadCustomTexture();
            Mod.RailCrossingSurface?.ReloadCustomTexture();
            CurbTexture.Configure(CurbMode, TexturesEnabled ? this : null, true);
        }

        private float m_SurfaceTextureMipBias;
        [SettingsUISection(MainSection, TextureGeneralGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
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
        // 0 = keep the game's terrain value.
        public const int DefaultTerrainTessellation = 0;
        public const int DefaultTerrainPatchSize = 0;

        public enum RoadTextureSource { Generated, Custom }

        public Setting(IMod mod) : base(mod) { SetDefaults(); }

        // Module switches: off leaves the game's graphics settings or the game's textures completely untouched.
        [SettingsUISection(MainSection, LauncherGroup)]
        public bool GraphicsEnabled { get; set; } = true;

        [SettingsUISection(MainSection, LauncherGroup)]
        public bool TexturesEnabled { get; set; } = true;

        [SettingsUIHidden]
        public bool GraphicsDisabled => !GraphicsEnabled;

        [SettingsUIHidden]
        public bool TexturesDisabled => !TexturesEnabled;

        // 1: terrain 0 = game value (previously 5 / 64 were always applied); shadow resolution 0 = game value.
        [SettingsUIHidden]
        public int GraphicsSettingsVersion { get; set; }

        // A separate first group provides the native divider before graphics controls.
        [SettingsUISection(MainSection, LauncherGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        public bool ShowAppButton { get; set; } = true;

        [SettingsUISection(MainSection, LauncherGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        public bool ShowGraphicsAppButton { get; set; } = true;

        // Debug tools live in their own in-game window; this is the only switch in the options.
        private bool m_ShowDebugWindow;
        [SettingsUISection(MainSection, LauncherGroup)]
        [SettingsUIHideByCondition(typeof(Setting), nameof(HideDebugWindowOption))]
        public bool ShowDebugWindow
        {
            get => Mod.DebugMode && m_ShowDebugWindow;
            set
            {
                value = value && Mod.DebugMode;
                if (m_ShowDebugWindow == value) return;
                m_ShowDebugWindow = value;
                if (!value) ResetDebugModes();
            }
        }

        [SettingsUIHidden]
        public bool HideDebugWindowOption => !Mod.DebugMode;

        [SettingsUIHidden]
        public float DebugWindowX { get; set; } = -1f;
        [SettingsUIHidden]
        public float DebugWindowY { get; set; } = -1f;

        // Test modes never outlive the debug window or a restart.
        internal void ResetDebugModes()
        {
            DiagnoseRoadEUAtlas = false;
            JunctionSyncBlendMode = 0;
            JunctionRecomputeTangents = false;
            if (Mod.RailInspector?.IsProbeActive == true) Mod.RailInspector.RestoreNow();
        }

        // Legacy Lumina fix is no longer registered in the frontend.
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
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        [SettingsUISlider(min = 10, max = 500, step = 10, scalarMultiplier = 1)]
        public int TreeLodDistance { get; set; }

        [SettingsUISection(MainSection, DetailGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        [SettingsUISlider(min = 10, max = 500, step = 10, scalarMultiplier = 1)]
        public int GlobalDetailScalePercent { get; set; }

        // The game's own slider stops at 4096 MB. Values beyond it are experimental.
        [SettingsUISection(MainSection, CacheGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        [SettingsUISlider(min = 128, max = 16384, step = 128, scalarMultiplier = 1)]
        public int GeometryCacheSizeMB { get; set; }

        // The game's terrain tessellation slider stops at 5.
        [SettingsUISection(MainSection, TerrainGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        [SettingsUISlider(min = 0, max = 16, step = 1, scalarMultiplier = 1)]
        public int TerrainTessellationLevel { get; set; }

        [SettingsUISection(MainSection, TerrainGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        [SettingsUISlider(min = 0, max = 128, step = 1, scalarMultiplier = 1)]
        public int TerrainPatchSizeUnits { get; set; }

        // Higher shadow resolution and a new distance control are separate from
        [SettingsUISection(MainSection, ShadowsGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        [SettingsUISlider(min = 0, max = 8192, step = 512, scalarMultiplier = 1)]
        public int ShadowResolution { get; set; }

        // Old metre value, no longer used: the game's shadow range follows the camera.
        [SettingsUIHidden]
        public int ShadowDistanceMeters { get; set; }

        // Percent of the game's current shadow range (100 = game).
        [SettingsUISection(MainSection, ShadowsGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        [SettingsUISlider(min = 25, max = 200, step = 5, scalarMultiplier = 1)]
        public int ShadowDistancePercent { get; set; } = 100;

        // Both thresholds are not exposed by the game's regular graphics sliders.
        [SettingsUISection(MainSection, ShadowsGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        [SettingsUISlider(min = 0, max = 10, step = 1, scalarMultiplier = 1)]
        public int SmallShadowCasterThreshold { get; set; }

        // Actual HDRP camera strength. 0 restores the camera's original value.
        [SettingsUISection(MainSection, SharpnessGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        // Game.UI.Unit formats the actual HDRP value with two decimal places.
        [SettingsUISlider(min = 0, max = 2, step = 0.05f,
            scalarMultiplier = 1, unit = Unit.kFloatTwoFractions)]
        public float TaaSharpenStrength { get; set; }

        public enum TaaStabilityControl { GameDefault, ReducedFlicker, StrongStability }

        // Retain the old serialized enum so saved settings still deserialize.
        [SettingsUIHidden]
        public TaaStabilityControl TaaStability { get; set; }

        [SettingsUISection(MainSection, SharpnessGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        public bool TaaStabilityEnabled { get; set; }

        [SettingsUISection(MainSection, SharpnessGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        [SettingsUISlider(min = 0, max = 1, step = 0.01f,
            scalarMultiplier = 1, unit = Unit.kFloatTwoFractions)]
        public float TaaStabilityStrength { get; set; } = 0.5f;

        [SettingsUISection(MainSection, SharpnessGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        [SettingsUIButton]
        public bool ResetTaaStability { set { if (value) { TaaStabilityEnabled = false; TaaStabilityStrength = 0.5f; ApplyAndSave(); } } }

        public enum BloomControl { GameDefault, Enabled, Disabled }

        [SettingsUISection(MainSection, BloomGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        public BloomControl BloomMode { get; set; }

        private float m_BloomIntensity = 0.2f;
        [SettingsUISection(MainSection, BloomGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
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
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        [SettingsUISlider(min = 0, max = 5, step = 0.05f,
            scalarMultiplier = 1, unit = Unit.kFloatTwoFractions)]
        public float SunBloomStrength { get; set; } = 1f;

        [SettingsUISection(MainSection, SkyGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        [SettingsUISlider(min = -5, max = 5, step = 0.05f,
            scalarMultiplier = 1, unit = Unit.kFloatTwoFractions)]
        public float SkyExposure { get; set; }

        [SettingsUISection(MainSection, BloomGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        [SettingsUIButton]
        public bool ResetBloom
        {
            set { if (value) { ResetBloomDefaults(); ApplyAndSave(); } }
        }

        [SettingsUISection(MainSection, BloomGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        [SettingsUIButton]
        public bool ResetSunBloom
        {
            set { if (value) { SunBloomStrength = 1f; ApplyAndSave(); } }
        }

        [SettingsUISection(MainSection, SkyGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
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

        [SettingsUIHidden]
        [SettingsUISection(GrassSection, GrassGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ExportGrassMaterials
        {
            set { if (value && Mod.DebugBackendEnabled) Mod.RoadInspector?.RequestGrassSurvey(); }
        }

        [SettingsUIHidden]
        [SettingsUISection(GrassSection, GrassGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
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

        public const string RoadWearGroup = "Road wear";
        [SettingsUISection(MainSection, RoadWearGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        public bool WearEnabled { get; set; }
        public bool WearOverrideDisabled => GraphicsDisabled || !WearEnabled;
        [SettingsUISection(MainSection, RoadWearGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(WearOverrideDisabled))]
        public bool WearHidden { get; set; }
        [SettingsUIHidden] public bool WearColorSelected { get; set; }
        [SettingsUIHidden] public int WearColor { get; set; } = 0xffffff;
        [SettingsUISection(MainSection, RoadWearGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(WearOverrideDisabled))]
        [SettingsUISlider(min = 0, max = 300, step = 1, scalarMultiplier = 1)]
        public int WearOpacity { get; set; } = 100;
        [SettingsUISection(MainSection, RoadWearGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(WearOverrideDisabled))]
        [SettingsUISlider(min = -1, max = 100, step = 1, scalarMultiplier = 1)]
        public int WearGloss { get; set; } = -1;
        [SettingsUISection(MainSection, RoadWearGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(WearOverrideDisabled))]
        [SettingsUISlider(min = 0, max = 1000, step = 1, scalarMultiplier = 1)]
        public int WearDepth { get; set; } = 100;

        [SettingsUISection(MainSection, TreeGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        [SettingsUIButton]
        public bool ResetTree { set { if (value) { TreeLodDistance = 100; ApplyAndSave(); } } }

        [SettingsUISection(MainSection, DetailGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        [SettingsUIButton]
        public bool ResetDetail { set { if (value) { GlobalDetailScalePercent = 100; ApplyAndSave(); } } }

        [SettingsUISection(MainSection, CacheGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        [SettingsUIButton]
        public bool ResetCache { set { if (value) { GeometryCacheSizeMB = DefaultCacheMB; ApplyAndSave(); } } }

        [SettingsUISection(MainSection, TerrainGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        [SettingsUIButton]
        public bool ResetTessellation { set { if (value) { TerrainTessellationLevel = DefaultTerrainTessellation; ApplyAndSave(); } } }

        [SettingsUISection(MainSection, TerrainGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        [SettingsUIButton]
        public bool ResetPatchSize { set { if (value) { TerrainPatchSizeUnits = DefaultTerrainPatchSize; ApplyAndSave(); } } }

        [SettingsUISection(MainSection, ShadowsGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        [SettingsUIButton]
        public bool ResetShadowResolution { set { if (value) { ShadowResolution = 0; ApplyAndSave(); } } }

        [SettingsUISection(MainSection, ShadowsGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        [SettingsUIButton]
        public bool ResetShadowDistance { set { if (value) { ShadowDistancePercent = 100; ApplyAndSave(); } } }

        [SettingsUISection(MainSection, ShadowsGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        [SettingsUIButton]
        public bool ResetSmallShadows { set { if (value) { SmallShadowCasterThreshold = 0; ApplyAndSave(); } } }

        [SettingsUISection(MainSection, SharpnessGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(GraphicsDisabled))]
        [SettingsUIButton]
        public bool ResetTaaSharpen { set { if (value) { TaaSharpenStrength = 0; ApplyAndSave(); } } }

        [SettingsUISection(MainSection, ResetGroup)]
        [SettingsUIButton]
        public bool ResetAll { set { if (value) { SetDefaults(); ApplyAndSave(); } } }

        // Off: only errors are written to the log.
        [SettingsUISection(MainSection, AboutGroup)]
        public bool EnableLogOutput { get; set; }

        [SettingsUISection(MainSection, AboutGroup)]
        public string Version => typeof(Mod).Assembly.GetName().Version?.ToString(3) ?? "Unknown";

        public override void SetDefaults()
        {
            ResetEndcapValues();
            WearEnabled = WearHidden = false;
            WearColorSelected = false;
            WearColor = 0xffffff;
            WearOpacity = WearDepth = 100;
            WearGloss = -1;
            ResetRailCrossing();
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
            ShadowDistancePercent = 100;
            SmallShadowCasterThreshold = 0;
            TaaSharpenStrength = 0;
            TaaStability = TaaStabilityControl.GameDefault;
            TaaStabilityEnabled = false;
            TaaStabilityStrength = 0.5f;
            ResetBloomDefaults();
            SunBloomStrength = 1f;
            ResetSkyExposureDefaults();
            ResetEffectDefaults();
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
            ShowAppButton = true;
            ShowGraphicsAppButton = true;
            GraphicsEnabled = true;
            TexturesEnabled = true;
            LuminaNumberFix = false;
            ShowRoadPanelButton = true;
            ShowSidewalkPanelButton = true;
            ShowRailPanelButton = true;
            DiagnoseRoadEUAtlas = false;
            RoadTextureTiling = 1;
            ParkingTextureScaleFactor = 1f;
            ParkingDepthStrengthFactor = 1f;
            RoadUvOffsetX = RoadUvOffsetY = RoadUvRotation = 0;
            SidewalkJunctionOverlay = false;
            SidewalkJunctionParking = false;
            ResetCurb();
            SetSidewalkDefaults();
            SetGrassDefaults();
            SetFootpathDefaults();
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

        public override void Apply()
        {
            CaptureTextureModeProfiles();
            base.Apply();
            ApplyToSystem();
        }

        private bool m_ApplyPending, m_TextureApplyPending;
        private float m_ApplyAt;
        internal bool HasPendingApply => m_ApplyPending;

        internal void QueueGraphicsApply()
        {
            m_ApplyPending = true;
            m_ApplyAt = UnityEngine.Time.unscaledTime + 0.25f;
        }

        internal void ApplyToSystem()
        {
            CaptureTextureModeProfiles();
            m_TextureApplyPending = true;
            QueueGraphicsApply();
        }

        internal void ProcessPendingApply()
        {
            if (!m_ApplyPending || UnityEngine.Time.unscaledTime < m_ApplyAt) return;
            m_ApplyPending = false;
            bool textures = m_TextureApplyPending;
            m_TextureApplyPending = false;
            if (textures) ApplyToSystemNow();
            else ApplyGraphicsToSystem(true);
        }

        // Graphics are applied only when a graphics value (or the module switch) changed, never because of texture edits.
        private int m_GraphicsSignature;
        private static System.Reflection.PropertyInfo[] s_GraphicsProperties;
        private static readonly string[] s_GraphicsGroups = { TreeGroup, DetailGroup, CacheGroup, TerrainGroup, ShadowsGroup, SharpnessGroup, BloomGroup, SkyGroup };

        private int GraphicsSignature()
        {
            if (s_GraphicsProperties == null)
            {
                var list = new System.Collections.Generic.List<System.Reflection.PropertyInfo>();
                foreach (var property in typeof(Setting).GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
                {
                    if (!property.CanRead || property.GetIndexParameters().Length != 0) continue;
                    foreach (var data in property.GetCustomAttributesData())
                    {
                        if (data.AttributeType.Name != "SettingsUISectionAttribute" || data.ConstructorArguments.Count < 2) continue;
                        if (Array.IndexOf(s_GraphicsGroups, data.ConstructorArguments[data.ConstructorArguments.Count - 1].Value as string) >= 0 ||
                            Array.IndexOf(s_GraphicsGroups, data.ConstructorArguments[1].Value as string) >= 0) list.Add(property);
                        break;
                    }
                }
                s_GraphicsProperties = list.ToArray();
            }
            unchecked
            {
                int hash = GraphicsEnabled ? 17 : 23;
                foreach (var property in s_GraphicsProperties)
                {
                    object value;
                    try { value = property.GetValue(this); }
                    catch (Exception) { continue; }
                    hash = hash * 31 + (value?.GetHashCode() ?? 0);
                }
                return hash;
            }
        }

        internal void ApplyGraphicsToSystem(bool force = false)
        {
            int signature = GraphicsSignature();
            if (!force && signature == m_GraphicsSignature) return;
            m_GraphicsSignature = signature;
            if (GraphicsEnabled)
            {
                if (Mod.TreeLod != null) Mod.TreeLod.DistancePercent = TreeLodDistance;
                Mod.Graphics?.Apply(this);
            }
            else
            {
                if (Mod.TreeLod != null) Mod.TreeLod.DistancePercent = 100;
                Mod.Graphics?.Disable();
            }
        }

        internal void ApplyToSystemNow()
        {
            RoadTextureTiling = Math.Max(0.1f, Math.Min(8f, (float)Math.Round(RoadTextureTiling, 2)));
            CaptureTextureModeProfiles();
            ApplyGraphicsToSystem();
            bool textures = TexturesEnabled;
            CurbTexture.Configure(CurbMode, textures ? this : null);
            Mod.RoadGravel?.Configure((textures && RoadGravelEnabled) && (RoadGravelDetail != 0 || RoadGravelUvSizePercent != 100 ||
                RoadGravelDepthStrengthPercent != 0 || TextureColorFilter.IsActive(RoadGravelFilterColor, RoadGravelFilterOpacityPercent) || RoadGravelUvOffsetX != 0 || RoadGravelUvOffsetY != 0 || RoadGravelUvRotation != 0), RoadGravelDetail,
                RoadGravelDetail == 2 ? RoadTextureSource.Custom : RoadTextureSource.Generated,
                RoadGravelUvSizePercent, RoadGravelDepthStrengthPercent, 1f,
                RoadGravelUvOffsetX, RoadGravelUvOffsetY, RoadGravelUvRotation, 0, 100, 100, 100, 50, RoadGravelFilterColor, RoadGravelFilterOpacityPercent);
            Mod.GravelShoulderSurface?.Configure((textures && GravelShoulderEnabled) && GravelShoulderDetail != 0,
                GravelShoulderDetail, RoadTextureSource.Generated, 100, 100, 1f,
                0, 0, 0, 0, 100, 100, 100, 50);
            Mod.GravelShoulder?.Configure((textures && GravelShoulderEnabled), GravelShoulderUvSizePercent, GravelShoulderDepthStrengthPercent,
                GravelShoulderDetail, GravelShoulderUvOffsetX, GravelShoulderUvOffsetY, GravelShoulderUvRotation, GravelShoulderFilterColor, GravelShoulderFilterOpacityPercent);
            Mod.RoadAsphalt?.Configure((textures && RoadAsphaltEnabled), RoadAsphaltDetail, RoadAsphaltDetail == 2 ? RoadTextureSource.Custom : RoadTextureSource.Generated,
                RoadUvSizePercent, RoadDepthStrengthPercent, RoadTextureTiling,
                RoadUvOffsetX, RoadUvOffsetY, RoadUvRotation, RoadCrackCount, RoadCrackSizePercent,
                RoadCrackWidthPercent, RoadCrackOpacityPercent, RoadCrackRandomnessPercent, RoadFilterColor, RoadFilterOpacityPercent, UseCustomRoadCracks, RoadCrackDepthStrengthPercent, RoadCrackRotation, RoadCrackOffsetX, RoadCrackOffsetY);
            Mod.Sidewalk?.Configure((textures && SidewalkEnabled), SidewalkDetail, SidewalkDetail == 2 ? RoadTextureSource.Custom : RoadTextureSource.Generated,
                SidewalkUvSizePercent, SidewalkDepthStrengthPercent, SidewalkTextureTiling,
                SidewalkUvOffsetX, SidewalkUvOffsetY, SidewalkUvRotation, SidewalkCrackCount, SidewalkCrackSizePercent,
                SidewalkCrackWidthPercent, SidewalkCrackOpacityPercent, SidewalkCrackRandomnessPercent, SidewalkFilterColor, SidewalkFilterOpacityPercent,
                crackDepth: SidewalkCrackDepthStrengthPercent, crackRotation: SidewalkCrackRotation,
                crackOffsetX: SidewalkCrackOffsetX, crackOffsetY: SidewalkCrackOffsetY);
            Mod.AlleySidewalk?.Configure((textures && AlleySidewalkEnabled), AlleySidewalkDetail, AlleySidewalkDetail == 2 ? RoadTextureSource.Custom : RoadTextureSource.Generated,
                AlleySidewalkUvSizePercent, AlleySidewalkDepthStrengthPercent, AlleySidewalkTextureTiling,
                AlleySidewalkUvOffsetX, AlleySidewalkUvOffsetY, AlleySidewalkUvRotation, AlleySidewalkCrackCount, AlleySidewalkCrackSizePercent,
                AlleySidewalkCrackWidthPercent, AlleySidewalkCrackOpacityPercent, AlleySidewalkCrackRandomnessPercent, AlleySidewalkFilterColor, AlleySidewalkFilterOpacityPercent,
                crackDepth: AlleySidewalkCrackDepthStrengthPercent, crackRotation: AlleySidewalkCrackRotation,
                crackOffsetX: AlleySidewalkCrackOffsetX, crackOffsetY: AlleySidewalkCrackOffsetY);
            Mod.AlleySidewalkAtlas?.Configure((textures && AlleySidewalkEnabled) &&
                (AlleySidewalkDetail != 0 || AlleySidewalkUvSizePercent != 100 || AlleySidewalkDepthStrengthPercent != 0 ||
                 AlleySidewalkUvOffsetX != 0 || AlleySidewalkUvOffsetY != 0 || AlleySidewalkUvRotation != 0 || AlleySidewalkCrackCount != 0 || TextureColorFilter.IsActive(AlleySidewalkFilterColor, AlleySidewalkFilterOpacityPercent)), false);
            // Leave the original RoadEU atlas untouched at unchanged defaults.
            Mod.SidewalkAtlas?.Configure((textures && SidewalkEnabled) &&
                ((Mod.DebugBackendEnabled && DiagnoseRoadEUAtlas) || HasSidewalkEndcapChanges || SidewalkDetail != 0 || SidewalkUvSizePercent != 100 ||
                 SidewalkDepthStrengthPercent != 0 || SidewalkUvOffsetX != 0 ||
                 SidewalkUvOffsetY != 0 || SidewalkUvRotation != 0 || SidewalkCrackCount != 0 || TextureColorFilter.IsActive(SidewalkFilterColor, SidewalkFilterOpacityPercent)),
                Mod.DebugBackendEnabled && DiagnoseRoadEUAtlas);
            Mod.RailGravel?.Configure((textures && RailGravelEnabled), RailGravelDetail,
                RailGravelDetail == 2 ? RoadTextureSource.Custom : RoadTextureSource.Generated,
                RailGravelUvSizePercent, RailGravelDepthStrengthPercent, 1f,
                RailGravelUvOffsetX, RailGravelUvOffsetY, RailGravelUvRotation,
                0, 100, 100, 100, 50, RailFilterColor, RailFilterOpacityPercent);
            Mod.RoadAsphalt?.UpdateTextureSampling();
            Mod.Sidewalk?.UpdateTextureSampling();
            Mod.AlleySidewalk?.UpdateTextureSampling();
            ApplyGrassToSystem();
            ApplyFootpathToSystem();
            Mod.Footpath?.UpdateTextureSampling();
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
