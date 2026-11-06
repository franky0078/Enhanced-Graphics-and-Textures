using Game.Common;
using Game.Net;
using Game.Tools;
using Unity.Entities;

namespace Advanced_Graphics
{
    public partial class SidewalkRoadAtlasSystem
    {
        private readonly MaterialScanGate m_ScanGate = new MaterialScanGate();
        private EntityQuery m_NetChanges;
        private bool m_NetChanged = true;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_NetChanges = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Edge>() },
                Any = new[] { ComponentType.ReadOnly<Created>(), ComponentType.ReadOnly<Updated>(), ComponentType.ReadOnly<Deleted>() },
                None = new[] { ComponentType.ReadOnly<Temp>() }
            });
        }

        // Every frame (Updated/Deleted only live for one frame).
        protected void TrackNetChanges()
        {
            if (!m_NetChanged && !m_NetChanges.IsEmptyIgnoreFilter) m_NetChanged = true;
        }

        // At a poll tick: true when the expensive pass should run now. forced: the owner's own reasons.
        protected bool ShouldRunPass(bool forced)
        {
            bool run = m_ScanGate.ShouldScan(World, forced || m_NetChanged);
            if (run) m_NetChanged = false;
            return run;
        }

        protected void PassFinished(bool waiting) => m_ScanGate.Passed(World, waiting);

        protected void ResetScanGate()
        {
            m_ScanGate.Reset();
            m_NetChanged = true;
        }
    }
}
