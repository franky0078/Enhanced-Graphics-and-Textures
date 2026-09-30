using Game;

namespace Advanced_Graphics
{
    //Applies TAA sharpness when the city camera appears or changes.
    public partial class GraphicsCameraSystem : GameSystemBase
    {
        private int m_Frame;

        protected override void OnUpdate()
        {
            Mod.Graphics?.ApplyCamera(Mod.ActiveSetting);
            if (++m_Frame % 60 == 0)
                Mod.Graphics?.SyncFromGame(Mod.ActiveSetting);
        }
    }
}

