using System;
using System.Collections.Generic;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;

namespace Advanced_Graphics
{
    // After loading: wait until the game has rebuilt the network and the atlases have measured their patterns.
    public partial class SidewalkJunctionMeshSystem
    {
        private const float LoadQuietTime = 1f;
        private const float LoadSettleLimit = 10f;
        private const float PatternWaitLimit = 20f;

        private bool m_Settling = true;
        private float m_SettleStart = -1f, m_QuietSince = -1f;
        private float m_PatternWaitStart = -1f;
        private int m_LoadRoadPasses = -1, m_LoadParkingPasses = -1;
        private int m_SettleScansSkipped;
        private readonly HashSet<Entity> m_PatternDeferred = new HashSet<Entity>();

        private void BeginLoadSettle()
        {
            m_Settling = true;
            m_SettleStart = m_QuietSince = -1f;
            m_PatternWaitStart = -1f;
            m_LoadRoadPasses = SidewalkRoadAtlasSystem.RoadPatternPasses;
            m_LoadParkingPasses = SidewalkRoadAtlasSystem.ParkingPatternPasses;
        }

        // true while the network is still changing after a load (quiet for LoadQuietTime, at most LoadSettleLimit).
        private bool LoadSettling(float now, bool netChanged)
        {
            if (!m_Settling) return false;
            if (m_SettleStart < 0f) m_SettleStart = now;
            if (m_QuietSince < 0f || netChanged) m_QuietSince = now;
            if (now - m_QuietSince < LoadQuietTime && now - m_SettleStart < LoadSettleLimit)
            {
                m_SettleScansSkipped++;
                return true;
            }
            m_Settling = false;
            m_FullWanted = true;
            m_ScanWanted = true;
            m_DirtyNodes.Clear();
            m_PatternWaitStart = now;
            if (Mod.DebugBackendEnabled)
                Mod.LogLater("Junction sidewalk meshes: network settled " + (now - m_SettleStart).ToString("0.0") +
                    " s after loading (" + m_SettleScansSkipped + " frames waited).");
            m_SettleScansSkipped = 0;
            return false;
        }

        // Parking corners use the atlas pattern: build them once both atlases finished a pass after loading.
        private bool PatternPending(float now)
        {
            if (m_PatternWaitStart < 0f) return false;
            bool ready = SidewalkRoadAtlasSystem.RoadPatternPasses != m_LoadRoadPasses &&
                SidewalkRoadAtlasSystem.ParkingPatternPasses != m_LoadParkingPasses;
            if (ready || now - m_PatternWaitStart > PatternWaitLimit)
            {
                if (!ready && Mod.DebugBackendEnabled) Mod.LogLater("Junction sidewalk meshes: atlas pattern not reported after " + PatternWaitLimit + " s, parking corners are built now.");
                m_PatternWaitStart = -1f;
                return false;
            }
            return true;
        }

        private readonly Dictionary<Entity, bool> m_InvisiblePrefabs = new Dictionary<Entity, bool>();

        // Invisible roads and paths have no geometry: they never get an own corner.
        private bool IsInvisibleEdge(Entity edge)
        {
            if (!EntityManager.HasComponent<PrefabRef>(edge)) return false;
            var prefabEntity = EntityManager.GetComponentData<PrefabRef>(edge).m_Prefab;
            if (m_InvisiblePrefabs.TryGetValue(prefabEntity, out bool invisible)) return invisible;
            invisible = m_Prefabs.TryGetPrefab<PrefabBase>(prefabEntity, out var prefab) && prefab != null && prefab.name != null &&
                prefab.name.IndexOf("Invisible", StringComparison.OrdinalIgnoreCase) >= 0;
            m_InvisiblePrefabs[prefabEntity] = invisible;
            return invisible;
        }

        private readonly HashSet<(Entity, bool)> m_FlipLogged = new HashSet<(Entity, bool)>();

        // Debug mode: alley node back at its previous build (A -> B -> A), logs the arms once to find what differs between scans.
        private void LogAlleyFlip((Entity, bool) key, Runtime old, int signature, Entity node, string detail)
        {
            if (!Mod.DebugBackendEnabled || old == null || old.PreviousSignature == 0 || signature != old.PreviousSignature ||
                m_FlipLogged.Count >= 20 || !m_FlipLogged.Add(key) || !EntityManager.HasBuffer<ConnectedEdge>(node)) return;
            var arms = new List<string>();
            var edges = EntityManager.GetBuffer<ConnectedEdge>(node, true);
            for (int i = 0; i < edges.Length; i++)
            {
                var edge = edges[i].m_Edge;
                string name = "?";
                if (EntityManager.HasComponent<PrefabRef>(edge) &&
                    m_Prefabs.TryGetPrefab<PrefabBase>(EntityManager.GetComponentData<PrefabRef>(edge).m_Prefab, out var prefab) && prefab != null)
                    name = prefab.name;
                arms.Add(edge.Index + " " + name + (IsAlley(edge) ? " [alley]" : "") +
                    (EntityManager.HasComponent<Temp>(edge) ? " [temp]" : "") +
                    (EntityManager.HasComponent<Game.Common.Deleted>(edge) ? " [deleted]" : "") +
                    (EntityManager.HasComponent<Game.Net.Road>(edge) ? "" : " [no road]"));
            }
            string place = EntityManager.HasComponent<Game.Net.Node>(node) ? EntityManager.GetComponentData<Game.Net.Node>(node).m_Position.xz.ToString() : "?";
            Mod.LogLater("Junction alley node flips back to its previous build: node " + place + " | " + edges.Length + " arms: " +
                string.Join("; ", arms) + " | before: " + (old.Detail ?? "?") + " | now: " + (detail ?? "?"));
        }
    }
}
