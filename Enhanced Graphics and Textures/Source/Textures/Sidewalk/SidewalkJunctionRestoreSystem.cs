using Game;
using Unity.Entities;

namespace Advanced_Graphics
{
    public partial class SidewalkJunctionRestoreSystem : GameSystemBase
    {
        private SidewalkJunctionMeshSystem m_Meshes;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Meshes = World.GetOrCreateSystemManaged<SidewalkJunctionMeshSystem>();
        }

        protected override void OnUpdate() => m_Meshes.EndRendering();
    }
}
