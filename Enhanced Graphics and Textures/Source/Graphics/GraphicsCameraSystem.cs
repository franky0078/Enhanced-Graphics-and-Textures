using Game;
using Game.Simulation;

namespace Advanced_Graphics
{
    // Applies camera and lighting overrides after the game updates lighting.
    public partial class GraphicsCameraSystem : GameSystemBase
    {
        private int m_Frame;
        private PlanetarySystem m_PlanetarySystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_PlanetarySystem = World.GetOrCreateSystemManaged<PlanetarySystem>();
        }

        protected override void OnUpdate()
        {
            Mod.Graphics?.ApplyCamera(Mod.ActiveSetting);
            var sun = m_PlanetarySystem.SunLight;
            Mod.Graphics?.ApplyLighting(Mod.ActiveSetting, sun.isValid ? sun.additionalData : null);
            if (++m_Frame % 60 == 0)
                Mod.Graphics?.SyncFromGame(Mod.ActiveSetting);
        }
    }
}


