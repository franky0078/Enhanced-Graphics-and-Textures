using Game.Prefabs;

namespace Advanced_Graphics
{
    internal static class RoadBuilderSurfaceCompatibility
    {
        // Recognize the runtime contract without a compile-time Road Builder dependency.
        internal static bool IsRoadBuilderRoad(PrefabBase prefab)
        {
            if (!(prefab is RoadPrefab)) return false;
            foreach (var contract in prefab.GetType().GetInterfaces())
                if (contract.FullName == "RoadBuilder.Domain.Prefabs.INetworkBuilderPrefab") return true;
            return false;
        }

        internal static bool HasParkingLanes(PrefabBase prefab)
        {
            var road = prefab as RoadPrefab;
            if (road == null || road.m_Sections == null) return false;
            foreach (var sectionInfo in road.m_Sections)
            {
                var section = sectionInfo.m_Section;
                if (section == null || section.m_Pieces == null) continue;
                foreach (var pieceInfo in section.m_Pieces)
                {
                    // Like Road Builder's lane classification, inspect the default pieces.
                    if ((pieceInfo.m_RequireAll != null && pieceInfo.m_RequireAll.Length > 0) ||
                        (pieceInfo.m_RequireAny != null && pieceInfo.m_RequireAny.Length > 0) ||
                        pieceInfo.m_Piece == null || !pieceInfo.m_Piece.TryGet<NetPieceLanes>(out var lanes) ||
                        lanes == null || lanes.m_Lanes == null) continue;
                    foreach (var laneInfo in lanes.m_Lanes)
                        if (laneInfo.m_Lane != null && laneInfo.m_Lane.TryGet<Game.Prefabs.ParkingLane>(out var parking) &&
                            parking != null) return true;
                }
            }
            return false;
        }
    }
}
