using System;
using System.Collections.Generic;
using System.Linq;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;

namespace Advanced_Graphics
{
    public partial class SidewalkJunctionMeshSystem
    {
        private sealed class WaitingNode
        {
            public int Attempts;
            public float NextCheck;
        }

        private readonly Dictionary<Entity, WaitingNode> m_Waiting = new Dictionary<Entity, WaitingNode>();
        // Nodes that failed again during the current scan (all others at the scanned nodes are resolved).
        private readonly HashSet<Entity> m_RetriedNow = new HashSet<Entity>();
        private float m_NextWaitingCheck;
        private const float WaitingCheckInterval = 1f;
        private const float MaximumBackoff = 300f;
        private int m_WaitingChecks, m_WaitingReady;
        private long m_WaitingCheckMaxTicks;
        private float m_MissingLoggedAt = -1f;
        // Per check round: compositions share meshes, meshes share pieces; each is looked at once.
        private readonly Dictionary<Entity, bool> m_LoadedCompositions = new Dictionary<Entity, bool>();
        private readonly Dictionary<Entity, bool> m_LoadedPieces = new Dictionary<Entity, bool>();

        // A build at this node returned null (pieces not loaded).
        private void WaitForGeometry(Entity node)
        {
            float now = UnityEngine.Time.unscaledTime;
            m_RetriedNow.Add(node);
            if (!m_Waiting.TryGetValue(node, out var state))
            {
                state = new WaitingNode();
                m_Waiting.Add(node, state);
            }
            state.Attempts++;
            state.NextCheck = state.Attempts <= 1 ? now : now + math.min(2f * (1 << math.min(state.Attempts - 2, 8)), MaximumBackoff);
        }

        private void NodeChanged(Entity node)
        {
            if (m_Waiting.TryGetValue(node, out var state))
            {
                state.Attempts = 0;
                state.NextCheck = 0f;
            }
        }

        private void CheckWaitingNodes(float now)
        {
            if (m_Waiting.Count == 0 || now < m_NextWaitingCheck) return;
            m_NextWaitingCheck = now + WaitingCheckInterval;
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            m_LoadedCompositions.Clear();
            m_LoadedPieces.Clear();
            List<Entity> gone = null;
            foreach (var pair in m_Waiting)
            {
                var node = pair.Key;
                var state = pair.Value;
                if (!EntityManager.Exists(node) || EntityManager.HasComponent<Deleted>(node))
                {
                    if (gone == null) gone = new List<Entity>();
                    gone.Add(node);
                    continue;
                }
                if (now < state.NextCheck) continue;
                m_WaitingChecks++;
                if (!NodeGeometryLoaded(node)) continue;
                m_WaitingReady++;
                state.NextCheck = now + 5f;
                m_DirtyNodes.Add(node);
                m_ScanWanted = true;
            }
            if (gone != null) foreach (var node in gone) m_Waiting.Remove(node);
            long ticks = System.Diagnostics.Stopwatch.GetTimestamp() - start;
            if (ticks > m_WaitingCheckMaxTicks) m_WaitingCheckMaxTicks = ticks;
        }

        private void ResolveWaiting(HashSet<Entity> scanned)
        {
            if (m_Waiting.Count > 0)
            {
                List<Entity> done = null;
                foreach (var node in m_Waiting.Keys)
                {
                    if (m_RetriedNow.Contains(node) || (scanned != null && !scanned.Contains(node))) continue;
                    if (done == null) done = new List<Entity>();
                    done.Add(node);
                }
                if (done != null) foreach (var node in done) m_Waiting.Remove(node);
            }
            m_RetriedNow.Clear();
        }

        private bool NodeGeometryLoaded(Entity node)
        {
            if (!EntityManager.HasBuffer<ConnectedEdge>(node)) return true;
            var edges = EntityManager.GetBuffer<ConnectedEdge>(node, true);
            for (int i = 0; i < edges.Length; i++)
            {
                var road = edges[i].m_Edge;
                if (!EntityManager.Exists(road) || !EntityManager.HasComponent<Composition>(road)) continue;
                var composition = EntityManager.GetComponentData<Composition>(road);
                if (!CompositionLoaded(composition.m_Edge) || !CompositionLoaded(composition.m_StartNode) ||
                    !CompositionLoaded(composition.m_EndNode)) return false;
            }
            return true;
        }

        private bool CompositionLoaded(Entity composition)
        {
            if (composition == Entity.Null || m_Owned.Contains(composition)) return true;
            if (m_LoadedCompositions.TryGetValue(composition, out bool loaded)) return loaded;
            loaded = true;
            if (EntityManager.HasComponent<NetCompositionMeshRef>(composition))
            {
                var mesh = EntityManager.GetComponentData<NetCompositionMeshRef>(composition).m_Mesh;
                if (mesh != Entity.Null && EntityManager.HasBuffer<NetCompositionPiece>(mesh))
                {
                    var pieces = EntityManager.GetBuffer<NetCompositionPiece>(mesh, true);
                    for (int i = 0; i < pieces.Length && loaded; i++)
                    {
                        var piece = pieces[i].m_Piece;
                        if (!m_LoadedPieces.TryGetValue(piece, out bool pieceLoaded))
                        {
                            pieceLoaded = PieceLoaded(piece) || IsInvisiblePiece(piece);
                            m_LoadedPieces.Add(piece, pieceLoaded);
                        }
                        loaded = pieceLoaded;
                    }
                }
            }
            m_LoadedCompositions.Add(composition, loaded);
            return loaded;
        }

        // Log output option, every 5 minutes while nodes wait: which pieces they wait for and why (do they ever arrive?).
        private void LogMissingPieces(float now)
        {
            if (m_Waiting.Count == 0 || !ModLog.Enabled || (m_MissingLoggedAt >= 0f && now - m_MissingLoggedAt < 300f)) return;
            m_MissingLoggedAt = now;
            var counts = new Dictionary<string, int>();
            var seenPieces = new HashSet<Entity>();
            int nodes = 0;
            foreach (var node in m_Waiting.Keys)
            {
                if (++nodes > 2000) break;
                if (!EntityManager.Exists(node) || !EntityManager.HasBuffer<ConnectedEdge>(node)) continue;
                var nodePieces = new HashSet<Entity>();
                var edges = EntityManager.GetBuffer<ConnectedEdge>(node, true).ToNativeArray(Unity.Collections.Allocator.Temp).ToArray();
                foreach (var item in edges)
                {
                    if (!EntityManager.Exists(item.m_Edge) || !EntityManager.HasComponent<Composition>(item.m_Edge)) continue;
                    var composition = EntityManager.GetComponentData<Composition>(item.m_Edge);
                    foreach (var part in new[] { composition.m_Edge, composition.m_StartNode, composition.m_EndNode })
                    {
                        if (part == Entity.Null || m_Owned.Contains(part) || !EntityManager.HasComponent<NetCompositionMeshRef>(part)) continue;
                        var mesh = EntityManager.GetComponentData<NetCompositionMeshRef>(part).m_Mesh;
                        if (mesh == Entity.Null || !EntityManager.HasBuffer<NetCompositionPiece>(mesh)) continue;
                        var pieces = EntityManager.GetBuffer<NetCompositionPiece>(mesh, true).ToNativeArray(Unity.Collections.Allocator.Temp).ToArray();
                        foreach (var piece in pieces)
                            if (!PieceLoaded(piece.m_Piece) && !IsInvisiblePiece(piece.m_Piece)) nodePieces.Add(piece.m_Piece);
                    }
                }
                if (nodePieces.Count == 0) { Count(counts, "(nothing missing now)"); continue; }
                foreach (var piece in nodePieces)
                {
                    string name = m_Prefabs.TryGetPrefab<PrefabBase>(piece, out var prefab) && prefab != null ? prefab.name : "piece " + piece.Index;
                    Count(counts, name + " [" + PieceMissingReason(piece) + "]");
                    seenPieces.Add(piece);
                }
            }
            var top = counts.OrderByDescending(pair => pair.Value).Take(12).Select(pair => pair.Key + " at " + pair.Value + " nodes");
            Mod.LogLater("Junction nodes waiting for geometry: " + m_Waiting.Count + " nodes, " + seenPieces.Count + " different pieces missing: " +
                string.Join("; ", top) + ".");
        }

        private static void Count(Dictionary<string, int> counts, string key)
        {
            counts.TryGetValue(key, out int value);
            counts[key] = value + 1;
        }

        // Which part of PieceLoaded fails.
        private string PieceMissingReason(Entity piece)
        {
            if (!EntityManager.Exists(piece)) return "entity gone";
            if (!EntityManager.HasComponent<PrefabData>(piece)) return "no PrefabData";
            if (!EntityManager.HasComponent<MeshData>(piece)) return "no MeshData";
            if (!EntityManager.HasBuffer<MeshVertex>(piece)) return "no vertex buffer";
            if (EntityManager.GetBuffer<MeshVertex>(piece, true).Length == 0) return "0 vertices";
            if (!EntityManager.HasBuffer<MeshIndex>(piece)) return "no index buffer";
            if (EntityManager.GetBuffer<MeshIndex>(piece, true).Length == 0) return "0 indices";
            if (!EntityManager.HasBuffer<MeshNormal>(piece)) return "no normals";
            if (!EntityManager.HasBuffer<MeshTangent>(piece)) return "no tangents";
            if (!EntityManager.HasBuffer<MeshUV0>(piece)) return "no UV0";
            if (!EntityManager.HasBuffer<MeshMaterial>(piece)) return "no materials";
            return "?";
        }

        private readonly Dictionary<Entity, float> m_CornerBuiltAt = new Dictionary<Entity, float>();
        private const float AlleySettleTime = 1f;

        private void CornerBuilt(Entity node) => m_CornerBuiltAt[node] = UnityEngine.Time.unscaledTime;

        // Seconds the alley node at this node still has to wait (0 = build now).
        private float AlleySettleWait(Entity node)
        {
            if (!m_CornerBuiltAt.TryGetValue(node, out float builtAt)) return 0f;
            return math.max(0f, AlleySettleTime - (UnityEngine.Time.unscaledTime - builtAt));
        }

        private void PruneCornerBuiltAt()
        {
            if (m_CornerBuiltAt.Count < 256) return;
            float now = UnityEngine.Time.unscaledTime;
            var old = new List<Entity>();
            foreach (var pair in m_CornerBuiltAt) if (now - pair.Value > AlleySettleTime * 4f) old.Add(pair.Key);
            foreach (var node in old) m_CornerBuiltAt.Remove(node);
        }

        private readonly Dictionary<Entity, bool> m_InvisiblePieces = new Dictionary<Entity, bool>();
        private int m_InvisibleLeftOut;

        private bool IsInvisiblePiece(Entity piece)
        {
            if (!m_InvisiblePieces.TryGetValue(piece, out bool invisible))
            {
                invisible = m_Prefabs.TryGetPrefab<PrefabBase>(piece, out var prefab) && prefab != null && prefab.name != null &&
                    prefab.name.IndexOf("Invisible", StringComparison.OrdinalIgnoreCase) >= 0;
                m_InvisiblePieces[piece] = invisible;
            }
            return invisible && !PieceLoaded(piece);
        }

        private NetCompositionPiece[] WithoutGeometryFreePieces(NetCompositionPiece[] pieces)
        {
            int keep = 0;
            foreach (var piece in pieces) if (!IsInvisiblePiece(piece.m_Piece)) keep++;
            if (keep == pieces.Length) return pieces;
            var result = new NetCompositionPiece[keep];
            int i = 0;
            foreach (var piece in pieces) if (!IsInvisiblePiece(piece.m_Piece)) result[i++] = piece;
            m_InvisibleLeftOut++;
            return result;
        }

        private void ClearWaiting()
        {
            m_InvisiblePieces.Clear();
            m_Waiting.Clear();
            m_RetriedNow.Clear();
            m_CornerBuiltAt.Clear();
        }
    }
}
