using Game;
using Game.Simulation;

namespace Advanced_Graphics
{
    // Applies camera and lighting overrides after the game updates lighting.
    public partial class GraphicsCameraSystem : GameSystemBase
    {
        private int m_Frame;
        private bool m_Announced;
        private PlanetarySystem m_PlanetarySystem;
        private Game.Rendering.CameraUpdateSystem m_CameraUpdate;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_PlanetarySystem = World.GetOrCreateSystemManaged<PlanetarySystem>();
        }

        protected override void OnUpdate()
        {
            var settings = Mod.ActiveSetting;
            if (settings == null || settings.HasPendingApply || !settings.GraphicsEnabled) return;
            if (!m_Announced)
            {
                m_Announced = true;
                Mod.LogLater("Graphics camera system running (lighting, camera and effects).");
            }
            Mod.Graphics?.ApplyCamera(settings);
            if (m_CameraUpdate == null) m_CameraUpdate = World.GetExistingSystemManaged<Game.Rendering.CameraUpdateSystem>();
            var sun = m_PlanetarySystem.SunLight;
            Mod.Graphics?.ApplyLighting(Mod.ActiveSetting, sun.isValid ? sun.additionalData : null);
            Mod.Graphics?.ApplyEffects(Mod.ActiveSetting, sun.isValid ? sun.additionalData : null, m_CameraUpdate);
            if (++m_Frame % 60 == 0)
                Mod.Graphics?.SyncFromGame(Mod.ActiveSetting);
        }
    }
}




