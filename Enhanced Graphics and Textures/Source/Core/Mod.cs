using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.Rendering;
using Game.SceneFlow;
using Game.Serialization;
using Game.Simulation;

namespace Advanced_Graphics
{
    public sealed class Mod : IMod
    {
        public static readonly ILog log = LogManager.GetLogger("Enhanced Graphics and Textures").SetShowsErrorsInUI(false);
        // Developer builds may opt in here; a saved checkbox cannot enable diagnostics by itself.
        internal const bool DebugMode = false;
        internal static bool DebugBackendEnabled => DebugMode && ActiveSetting?.ShowDebugWindow == false;
        private static readonly System.Collections.Concurrent.ConcurrentQueue<string> s_DeferredLog =
            new System.Collections.Concurrent.ConcurrentQueue<string>();
        internal static void LogLater(string message)
        {
            if (!string.IsNullOrEmpty(message) && s_DeferredLog.Count < 256) s_DeferredLog.Enqueue(message);
        }
        internal static void FlushDeferredLog()
        {
            if (!s_DeferredLog.TryDequeue(out var message)) return;
            try { log.Info(message); } catch (System.Exception) { }
        }
        private Setting m_Setting;
        internal static TreeLodSystem TreeLod;
        internal static GraphicsOverrides Graphics;
        internal static Setting ActiveSetting;
        internal static RoadMaterialInspector RoadInspector;
        internal static RailMaterialInspector RailInspector;
        internal static RailGravelSystem RailGravel;
        internal static RoadGravelSystem RoadGravel;
        internal static GrassSurfaceSystem Grass;
        internal static FootpathSurfaceSystem Footpath;
        internal static GravelShoulderSystem GravelShoulder;
        internal static GravelShoulderSurfaceSystem GravelShoulderSurface;
        internal static RoadAsphaltSystem RoadAsphalt;
        internal static SidewalkSurfaceSystem Sidewalk;
        internal static SidewalkJunctionMeshSystem SidewalkJunctionMeshes;
        internal static SidewalkRoadAtlasSystem SidewalkAtlas;
        internal static AlleySidewalkSurfaceSystem AlleySidewalk;
        internal static AlleySidewalkAtlasSystem AlleySidewalkAtlas;
        internal static ParkingRoadAtlasSystem ParkingRoadAtlas;

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info("Enhanced Graphics and Textures loading");
            TextureStorage.EnsureDirectories();

            // BatchManagerSystem builds the active groups; BatchDataSystem later reads their LOD thresholds. 
            updateSystem.UpdateAfter<TreeLodSystem, BatchManagerSystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAfter<GraphicsCameraSystem, Game.Rendering.LightingSystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<GrassSurfaceSystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<FootpathSurfaceSystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<RoadMaterialInspector>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<RailMaterialInspector>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<RailGravelSystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<RoadGravelSystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<GravelShoulderSurfaceSystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAfter<GravelShoulderSystem, GravelShoulderSurfaceSystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<RoadAsphaltSystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<SidewalkSurfaceSystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateBefore<SidewalkJunctionMeshSystem, PreCullingSystem>(SystemUpdatePhase.PreCulling);
            updateSystem.UpdateBefore<SidewalkJunctionRestoreSystem, EndFrameBarrier>(SystemUpdatePhase.MainLoop);
            updateSystem.UpdateBefore<SidewalkJunctionRestoreSystem>(SystemUpdatePhase.PreTool);
            updateSystem.UpdateBefore<SidewalkJunctionRestoreSystem>(SystemUpdatePhase.ApplyTool);
            updateSystem.UpdateAfter<SidewalkJunctionRestoreSystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateBefore<SidewalkJunctionRestoreSystem, SimulationSystem>(SystemUpdatePhase.LateUpdate);
            updateSystem.UpdateAfter<SidewalkJunctionRestoreSystem>(SystemUpdatePhase.CompleteRendering);
            updateSystem.UpdateBefore<PreSerialize<SidewalkJunctionMeshSystem>, BeginPrefabSerializationSystem>(SystemUpdatePhase.Serialize);
            updateSystem.UpdateBefore<PreDeserialize<SidewalkJunctionMeshSystem>, ClearSystem>(SystemUpdatePhase.Deserialize);
            updateSystem.UpdateAfter<SidewalkRoadAtlasSystem, SidewalkSurfaceSystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<AlleySidewalkSurfaceSystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAfter<AlleySidewalkAtlasSystem, AlleySidewalkSurfaceSystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAfter<ParkingRoadAtlasSystem, SidewalkRoadAtlasSystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<RoadPanelUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<GraphicsPanelUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<DebugPanelUISystem>(SystemUpdatePhase.UIUpdate);
            TreeLod = updateSystem.World.GetOrCreateSystemManaged<TreeLodSystem>();
            RoadInspector = updateSystem.World.GetOrCreateSystemManaged<RoadMaterialInspector>();
            RailInspector = updateSystem.World.GetOrCreateSystemManaged<RailMaterialInspector>();
            RailGravel = updateSystem.World.GetOrCreateSystemManaged<RailGravelSystem>();
            RoadGravel = updateSystem.World.GetOrCreateSystemManaged<RoadGravelSystem>();
            Grass = updateSystem.World.GetOrCreateSystemManaged<GrassSurfaceSystem>();
            Footpath = updateSystem.World.GetOrCreateSystemManaged<FootpathSurfaceSystem>();
            GravelShoulder = updateSystem.World.GetOrCreateSystemManaged<GravelShoulderSystem>();
            GravelShoulderSurface = updateSystem.World.GetOrCreateSystemManaged<GravelShoulderSurfaceSystem>();
            RoadAsphalt = updateSystem.World.GetOrCreateSystemManaged<RoadAsphaltSystem>();
            Sidewalk = updateSystem.World.GetOrCreateSystemManaged<SidewalkSurfaceSystem>();
            SidewalkJunctionMeshes = updateSystem.World.GetOrCreateSystemManaged<SidewalkJunctionMeshSystem>();
            SidewalkAtlas = updateSystem.World.GetOrCreateSystemManaged<SidewalkRoadAtlasSystem>();
            AlleySidewalk = updateSystem.World.GetOrCreateSystemManaged<AlleySidewalkSurfaceSystem>();
            AlleySidewalkAtlas = updateSystem.World.GetOrCreateSystemManaged<AlleySidewalkAtlasSystem>();
            ParkingRoadAtlas = updateSystem.World.GetOrCreateSystemManaged<ParkingRoadAtlasSystem>();
            Graphics = new GraphicsOverrides();
            Graphics.Capture();

            m_Setting = new Setting(this);
            ActiveSetting = m_Setting;
            m_Setting.RegisterInOptionsUI();
            var localization = GameManager.instance.localizationManager;
            localization.AddSource("en-US", new GraphicsLocale(m_Setting, "en"));
            localization.AddSource("de-DE", new GraphicsLocale(m_Setting, "de"));
            localization.AddSource("es-ES", new GraphicsLocale(m_Setting, "es"));
            localization.AddSource("fr-FR", new GraphicsLocale(m_Setting, "fr"));
            localization.AddSource("it-IT", new GraphicsLocale(m_Setting, "it"));

            AssetDatabase.global.LoadSettings(nameof(Advanced_Graphics), m_Setting, new Setting(this));
            if (m_Setting.RoadTextureLayoutVersion < 2)
            {
                int previous = m_Setting.RoadAsphaltDetail;
                m_Setting.RoadAsphaltDetail = previous == 4 ||
                    (previous > 0 && m_Setting.AsphaltSource == Setting.RoadTextureSource.Custom)
                    ? 2 : previous > 0 ? 1 : 0;
                m_Setting.RoadTextureLayoutVersion = 2;
                _ = AssetDatabase.global.SaveSettings();
            }
            if (m_Setting.RoadTextureLayoutVersion < 3)
            {
                m_Setting.RoadAsphaltEnabled = m_Setting.RoadAsphaltDetail != 0 || m_Setting.RoadCrackCount != 0 ||
                    m_Setting.RoadUvSizePercent != 100 || m_Setting.RoadUvOffsetX != 0 ||
                    m_Setting.RoadUvOffsetY != 0 || m_Setting.RoadUvRotation != 0 ||
                    (m_Setting.RoadDepthStrengthPercent != 0 && m_Setting.RoadDepthStrengthPercent != 100);
                m_Setting.RoadTextureLayoutVersion = 3;
                _ = AssetDatabase.global.SaveSettings();
            }
            if (m_Setting.AlleySidewalkSettingsVersion < 2)
            {
                m_Setting.MigrateAlleySidewalkSettings();
                _ = AssetDatabase.global.SaveSettings();
            }
            m_Setting.AsphaltSource = Setting.RoadTextureSource.Generated;
            m_Setting.ResetDebugModes();
            m_Setting.InitializeTextureModeProfiles();
            m_Setting.ApplyToSystemNow();
        }

        public void OnDispose()
        {
            Graphics?.Restore();
            Graphics = null;
            // The game can re-create rendering batches as it unloads; do not retain an override.
            if (TreeLod != null)
            {
                TreeLod.DistancePercent = 100;
                TreeLod.RestoreNow();
                TreeLod = null;
            }
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
            ActiveSetting = null;
            ParkingRoadAtlas?.RestoreParkingNow();
            ParkingRoadAtlas = null;
            RoadAsphalt?.Configure(false, 0, Setting.RoadTextureSource.Generated, 100, 0, 1, 0, 0, 0, 0, 100, 100, 100, 50);
            RoadAsphalt?.RestoreNow();
            RoadAsphalt = null;
            AlleySidewalkAtlas?.RestoreNow();
            AlleySidewalkAtlas = null;
            AlleySidewalk?.RestoreNow();
            AlleySidewalk = null;
            SidewalkAtlas?.RestoreNow();
            SidewalkAtlas = null;
            SidewalkJunctionMeshes?.DropAll();
            SidewalkJunctionMeshes = null;
            Sidewalk?.RestoreNow();
            Sidewalk = null;
            RoadInspector = null;
            RailInspector?.RestoreNow();
            RailInspector = null;
            RailGravel?.RestoreNow();
            RailGravel = null;
            Grass?.RestoreNow();
            Grass = null;
            Footpath?.RestoreNow();
            Footpath = null;
            RoadGravel?.RestoreNow();
            RoadGravel = null;
            GravelShoulder?.RestoreNow();
            GravelShoulder = null;
            GravelShoulderSurface?.RestoreNow();
            GravelShoulderSurface = null;
            log.Info("Enhanced Graphics and Textures disposed");
        }
    }
}




