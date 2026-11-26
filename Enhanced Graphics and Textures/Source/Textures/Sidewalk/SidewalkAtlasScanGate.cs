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

        protected bool ShouldRunPass(bool forced)
        {
            bool run = m_ScanGate.ShouldScan(World, forced || m_NetChanged);
            if (run) m_NetChanged = false;
            return run;
        }

        // Finished atlas passes with a measured pattern: junction corners wait for them after loading.
        internal static int RoadPatternPasses, ParkingPatternPasses;
        protected virtual bool HasPatternMaterials => m_Materials.Count > 0;

        protected void PassFinished(bool waiting)
        {
            m_ScanGate.Passed(World, waiting);
            if (waiting && !HasPatternMaterials) return;
            if (this is ParkingRoadAtlasSystem) ParkingPatternPasses++;
            else if (!IsAlleyAtlas) RoadPatternPasses++;
        }

        protected void ResetScanGate()
        {
            m_ScanGate.Reset();
            m_NetChanged = true;
        }
    }
}
