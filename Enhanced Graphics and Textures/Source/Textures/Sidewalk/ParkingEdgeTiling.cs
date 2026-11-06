using System;
using System.Collections.Generic;
using Colossal.Mathematics;
using Game;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Advanced_Graphics
{
    // Parking roads: runtime copies of edge and node compositions with tiled V (short edges keep the texture scale).
    public partial class SidewalkJunctionMeshSystem
    {
        private readonly Dictionary<Entity, Runtime> m_ParkingNodeRuntimes = new Dictionary<Entity, Runtime>();
        private readonly List<Entity> m_ParkingNodeRoads = new List<Entity>();
        private readonly Dictionary<(Entity, bool), Entity> m_AnnouncedParkingNodes = new Dictionary<(Entity, bool), Entity>();
        private int m_TiledNodeCompositions;

        private readonly Dictionary<Entity, Runtime> m_ParkingEdgeRuntimes = new Dictionary<Entity, Runtime>();
        private readonly List<Entity> m_TiledParkingEdges = new List<Entity>();
        private readonly Dictionary<Entity, Entity> m_AnnouncedParkingEdges = new Dictionary<Entity, Entity>();
        private int m_TiledEdgeCompositions, m_TiledEdgePieces;

        private static int TiledSignature => s_SyncBlendMode == 6 ? 2 : 1;

        // Road types in the city that carry parking but are not handled as parking roads, once per type (log output option).
        private readonly HashSet<string> m_UnhandledLogged = new HashSet<string>();

        private void LogUnhandledRoad(PrefabBase prefab)
        {
            string name = prefab.name;
            if (string.IsNullOrEmpty(name) || m_UnhandledLogged.Count >= 400 || !m_UnhandledLogged.Add(name)) return;
            bool roadBuilder, parking;
            try
            {
                roadBuilder = RoadBuilderSurfaceCompatibility.IsRoadBuilderRoad(prefab);
                parking = name.IndexOf("Parking", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    name.IndexOf("No Parking", StringComparison.OrdinalIgnoreCase) < 0;
            }
            catch (Exception) { return; }
            if (!parking) return;
            Mod.LogLater("Parking road type not handled: " + name + (roadBuilder ? " (Road Builder road)" : " (name rule)"));
        }

        // roads: all roads (full scan) or the roads at the scanned nodes (incremental: unused compositions are released by the next full scan).
        private void ScanParkingEdges(Entity[] roads)
        {
            if (!s_ParkingOn)
            {
                if (m_ParkingEdgeRuntimes.Count > 0 || m_ParkingNodeRuntimes.Count > 0) DropParkingEdges();
                return;
            }
            bool full = m_ScanNodes == null;
            if (full)
            {
                m_TiledParkingEdges.Clear();
                m_ParkingNodeRoads.Clear();
            }
            else
            {
                var scanned = new HashSet<Entity>(roads);
                Predicate<Entity> drop = e => scanned.Contains(e) || !EntityManager.Exists(e) || EntityManager.HasComponent<Deleted>(e);
                m_TiledParkingEdges.RemoveAll(drop);
                m_ParkingNodeRoads.RemoveAll(drop);
            }
            var used = new HashSet<Entity>();
            var usedNodes = new HashSet<Entity>();
            {
                foreach (var road in roads)
                {
                    var prefabEntity = EntityManager.GetComponentData<PrefabRef>(road).m_Prefab;
                    if (!m_Prefabs.TryGetPrefab<PrefabBase>(prefabEntity, out var prefab) || prefab == null) continue;
                    if (!IsParkingPrefab(prefab))
                    {
                        LogUnhandledRoad(prefab);
                        continue;
                    }
                    var native = EntityManager.GetComponentData<Composition>(road);
                    bool nodes = false;
                    foreach (var node in new[] { native.m_StartNode, native.m_EndNode })
                    {
                        if (node == Entity.Null || m_Owned.Contains(node)) continue;
                        usedNodes.Add(node);
                        if (m_ParkingNodeRuntimes.TryGetValue(node, out var staleNode) && staleNode.Signature != TiledSignature)
                        {
                            EndRendering();
                            Retire(staleNode);
                            m_ParkingNodeRuntimes.Remove(node);
                        }
                        if (!m_ParkingNodeRuntimes.TryGetValue(node, out var nodeRuntime))
                        {
                            nodeRuntime = BuildTiledParkingComposition(node, false);
                            if (nodeRuntime == null) { ScheduleRetryRoad(road); continue; }
                            m_ParkingNodeRuntimes.Add(node, nodeRuntime);
                        }
                        if (nodeRuntime.Composition != Entity.Null) nodes = true;
                    }
                    if (nodes) m_ParkingNodeRoads.Add(road);
                    var source = native.m_Edge;
                    if (source == Entity.Null || m_Owned.Contains(source)) continue;
                    used.Add(source);
                    if (m_ParkingEdgeRuntimes.TryGetValue(source, out var stale) && stale.Signature != TiledSignature)
                    {
                        EndRendering();
                        Retire(stale);
                        m_ParkingEdgeRuntimes.Remove(source);
                    }
                    if (!m_ParkingEdgeRuntimes.TryGetValue(source, out var runtime))
                    {
                        runtime = BuildTiledParkingComposition(source, true);
                        if (runtime == null) { ScheduleRetryRoad(road); continue; }
                        m_ParkingEdgeRuntimes.Add(source, runtime);
                    }
                    if (runtime.Composition != Entity.Null) m_TiledParkingEdges.Add(road);
                }
            }
            if (!full) { CountTiledCompositions(); return; }

            var remove = new List<Entity>();
            foreach (var pair in m_ParkingEdgeRuntimes) if (!used.Contains(pair.Key)) remove.Add(pair.Key);
            if (remove.Count > 0) EndRendering();
            foreach (var key in remove)
            {
                Retire(m_ParkingEdgeRuntimes[key]);
                m_ParkingEdgeRuntimes.Remove(key);
            }
            var removeNodes = new List<Entity>();
            foreach (var pair in m_ParkingNodeRuntimes) if (!usedNodes.Contains(pair.Key)) removeNodes.Add(pair.Key);
            if (removeNodes.Count > 0) EndRendering();
            foreach (var key in removeNodes)
            {
                Retire(m_ParkingNodeRuntimes[key]);
                m_ParkingNodeRuntimes.Remove(key);
            }
            CountTiledCompositions();
        }

        private void ScheduleRetryRoad(Entity road)
        {
            if (!EntityManager.HasComponent<Edge>(road)) { ScheduleRetry(Entity.Null); return; }
            var edge = EntityManager.GetComponentData<Edge>(road);
            ScheduleRetry(edge.m_Start);
            ScheduleRetry(edge.m_End);
        }

        private void CountTiledCompositions()
        {
            m_TiledEdgeCompositions = 0;
            foreach (var runtime in m_ParkingEdgeRuntimes.Values) if (runtime.Composition != Entity.Null) m_TiledEdgeCompositions++;
            m_TiledNodeCompositions = 0;
            foreach (var runtime in m_ParkingNodeRuntimes.Values) if (runtime.Composition != Entity.Null) m_TiledNodeCompositions++;
        }

        // Edge or node composition: copy with every parking piece tiled (see CopyPieceTiled).
        private Runtime BuildTiledParkingComposition(Entity source, bool edge)
        {
            if (!EntityManager.HasComponent<NetCompositionMeshRef>(source) || !EntityManager.HasComponent<NetCompositionData>(source))
                return new Runtime { Source = source, Signature = TiledSignature };
            var meshRef = EntityManager.GetComponentData<NetCompositionMeshRef>(source);
            var meshEntity = meshRef.m_Mesh;
            if (meshEntity == Entity.Null || !EntityManager.HasComponent<NetCompositionMeshData>(meshEntity) ||
                !EntityManager.HasBuffer<NetCompositionPiece>(meshEntity)) return new Runtime { Source = source, Signature = TiledSignature };
            var meshData = EntityManager.GetComponentData<NetCompositionMeshData>(meshEntity);
            var compositionData = EntityManager.GetComponentData<NetCompositionData>(source);
            var pieces = EntityManager.GetBuffer<NetCompositionPiece>(meshEntity, true).ToNativeArray(Allocator.Temp).ToArray();
            var meshMaterials = EntityManager.HasBuffer<MeshMaterial>(meshEntity)
                ? EntityManager.GetBuffer<MeshMaterial>(meshEntity, true).ToNativeArray(Allocator.Temp).ToArray()
                : new MeshMaterial[0];
            var carriageways = EntityManager.HasBuffer<NetCompositionCarriageway>(source)
                ? EntityManager.GetBuffer<NetCompositionCarriageway>(source, true).ToNativeArray(Allocator.Temp).ToArray()
                : new NetCompositionCarriageway[0];
            foreach (var piece in pieces)
                if (!PieceLoaded(piece.m_Piece) && !IsInvisiblePiece(piece.m_Piece)) { m_NotLoaded++; return null; }

            var runtime = new Runtime { Source = source, Parking = true, Signature = TiledSignature };
            try
            {
                var result = new List<NetCompositionPiece>(pieces.Length);
                int tiled = 0;
                foreach (var piece in pieces)
                {
                    string name = m_Prefabs.TryGetPrefab<PrefabBase>(piece.m_Piece, out var prefab) && prefab != null ? prefab.name : null;
                    var copy = piece;
                    if (IsParkingAtlasPiece(name))
                    {
                        var tiledPiece = CopyPieceTiled(piece.m_Piece, edge);
                        if (tiledPiece != Entity.Null)
                        {
                            runtime.Created.Add(tiledPiece);
                            copy.m_Piece = tiledPiece;
                            tiled++;
                        }
                    }
                    result.Add(copy);
                }
                if (tiled == 0)
                {
                    ReleaseCreated(runtime);
                    return new Runtime { Source = source, Signature = TiledSignature };
                }

                var mesh = EntityManager.CreateEntity(ComponentType.ReadWrite<NetCompositionMeshData>(),
                    ComponentType.ReadWrite<NetCompositionPiece>(), ComponentType.ReadWrite<MeshMaterial>(),
                    ComponentType.ReadWrite<BatchGroup>());
                runtime.Created.Add(mesh);
                s_RuntimeMeshes.Add(mesh);
                s_ParkingCornerMeshes.Add(mesh);
                var data = meshData;
                data.m_AvailableLayers = 0;
                data.m_State &= ~MeshFlags.Default;
                EntityManager.SetComponentData(mesh, data);
                var pieceBuffer = EntityManager.GetBuffer<NetCompositionPiece>(mesh);
                foreach (var piece in result) pieceBuffer.Add(piece);
                var materialBuffer = EntityManager.GetBuffer<MeshMaterial>(mesh);
                foreach (var material in meshMaterials)
                    materialBuffer.Add(new MeshMaterial { m_MaterialIndex = material.m_MaterialIndex });

                var composition = EntityManager.CreateEntity(ComponentType.ReadWrite<NetCompositionData>(),
                    ComponentType.ReadWrite<NetCompositionMeshRef>(), ComponentType.ReadWrite<NetCompositionCarriageway>());
                runtime.Created.Add(composition);
                EntityManager.SetComponentData(composition, compositionData);
                EntityManager.SetComponentData(composition, new NetCompositionMeshRef { m_Mesh = mesh, m_Rotate = meshRef.m_Rotate });
                var carriagewayBuffer = EntityManager.GetBuffer<NetCompositionCarriageway>(composition);
                foreach (var carriageway in carriageways) carriagewayBuffer.Add(carriageway);
                runtime.Composition = composition;
                runtime.Mesh = mesh;
                m_Owned.Add(composition);
                m_TiledEdgePieces += tiled;
                ParkingCornerVersion++;
                return runtime;
            }
            catch
            {
                ReleaseCreated(runtime);
                throw;
            }
        }

        // Copy of a piece with negative V shifted up by whole repeats; the V density the game derives stays the same.
        private Entity CopyPieceTiled(Entity source, bool edge)
        {
            var positions = EntityManager.GetBuffer<MeshVertex>(source, true).ToNativeArray(Allocator.Temp).ToArray();
            var normals = EntityManager.GetBuffer<MeshNormal>(source, true).ToNativeArray(Allocator.Temp).ToArray();
            var tangents = EntityManager.GetBuffer<MeshTangent>(source, true).ToNativeArray(Allocator.Temp).ToArray();
            var uvs = EntityManager.GetBuffer<MeshUV0>(source, true).ToNativeArray(Allocator.Temp).ToArray();
            var indices = EntityManager.GetBuffer<MeshIndex>(source, true).ToNativeArray(Allocator.Temp).ToArray();
            var slots = EntityManager.GetBuffer<MeshMaterial>(source, true).ToNativeArray(Allocator.Temp).ToArray();
            float minV = float.MaxValue, maxV = float.MinValue;
            foreach (var uv in uvs) { minV = math.min(minV, uv.m_Uv.y); maxV = math.max(maxV, uv.m_Uv.y); }
            if (uvs.Length == 0 || minV >= 0f || maxV > 0.0001f) return Entity.Null;
            float shift = -math.floor(minV);
            var meshData = EntityManager.GetComponentData<MeshData>(source);
            var prefabData = EntityManager.GetComponentData<PrefabData>(source);
            var p = new List<float3>(positions.Length);
            var n = new List<float3>(positions.Length);
            var t = new List<float4>(positions.Length);
            var u = new List<float2>(positions.Length);
            for (int i = 0; i < positions.Length; i++)
            {
                p.Add(positions[i].m_Vertex);
                n.Add(i < normals.Length ? normals[i].m_Normal : new float3(0f, 1f, 0f));
                t.Add(i < tangents.Length ? tangents[i].m_Tangent : new float4(1f, 0f, 0f, 1f));
                var uv = i < uvs.Length ? uvs[i].m_Uv : default(float2);
                uv.y = math.max(uv.y + shift, 0.0001f);
                if (s_SyncBlendMode == 6 && edge) uv.x = 0.1f;
                u.Add(uv);
            }
            var index = new List<int>(indices.Length);
            foreach (var item in indices) index.Add(item.m_Index);
            return CreatePiece(prefabData, meshData, p, n, t, u, index, new List<MeshMaterial>(slots));
        }

        private void AttachParkingEdges(ref bool announced)
        {
            foreach (var edge in m_TiledParkingEdges)
            {
                if (!EntityManager.Exists(edge) || EntityManager.HasComponent<Deleted>(edge) ||
                    !EntityManager.HasComponent<Composition>(edge)) continue;
                var current = EntityManager.GetComponentData<Composition>(edge);
                var native = m_Attached.TryGetValue(edge, out var saved) ? saved : current;
                if (!m_ParkingEdgeRuntimes.TryGetValue(native.m_Edge, out var runtime) || runtime.Composition == Entity.Null) continue;
                if (!m_Attached.ContainsKey(edge)) m_Attached.Add(edge, current);
                current.m_Edge = runtime.Composition;
                EntityManager.SetComponentData(edge, current);
                if (!m_AnnouncedParkingEdges.TryGetValue(edge, out var last) || last != runtime.Composition)
                {
                    EntityManager.AddComponent<BatchesUpdated>(edge);
                    m_AnnouncedParkingEdges[edge] = runtime.Composition;
                    announced = true;
                }
            }

            foreach (var edge in m_ParkingNodeRoads)
            {
                if (!EntityManager.Exists(edge) || EntityManager.HasComponent<Deleted>(edge) ||
                    !EntityManager.HasComponent<Composition>(edge)) continue;
                var current = EntityManager.GetComponentData<Composition>(edge);
                var native = m_Attached.TryGetValue(edge, out var saved) ? saved : current;
                bool changed = false;
                for (int side = 0; side < 2; side++)
                {
                    bool start = side == 0;
                    var drawn = start ? current.m_StartNode : current.m_EndNode;
                    if (m_Owned.Contains(drawn)) continue;
                    var source = start ? native.m_StartNode : native.m_EndNode;
                    if (drawn != source || !m_ParkingNodeRuntimes.TryGetValue(source, out var runtime) ||
                        runtime.Composition == Entity.Null) continue;
                    if (start) current.m_StartNode = runtime.Composition;
                    else current.m_EndNode = runtime.Composition;
                    changed = true;
                    var key = (edge, start);
                    if (!m_AnnouncedParkingNodes.TryGetValue(key, out var last) || last != runtime.Composition)
                    {
                        m_AnnouncedParkingNodes[key] = runtime.Composition;
                        announced = true;
                        EntityManager.AddComponent<BatchesUpdated>(edge);
                    }
                }
                if (!changed) continue;
                if (!m_Attached.ContainsKey(edge)) m_Attached.Add(edge, native);
                EntityManager.SetComponentData(edge, current);
            }
        }

        private void DropParkingEdges()
        {
            foreach (var runtime in m_ParkingEdgeRuntimes.Values) Retire(runtime);
            m_ParkingEdgeRuntimes.Clear();
            m_TiledParkingEdges.Clear();
            foreach (var edge in m_AnnouncedParkingEdges.Keys)
                if (EntityManager.Exists(edge) && !EntityManager.HasComponent<Deleted>(edge))
                    EntityManager.AddComponent<BatchesUpdated>(edge);
            m_AnnouncedParkingEdges.Clear();
            foreach (var runtime in m_ParkingNodeRuntimes.Values) Retire(runtime);
            m_ParkingNodeRuntimes.Clear();
            m_ParkingNodeRoads.Clear();
            foreach (var key in m_AnnouncedParkingNodes.Keys)
                if (EntityManager.Exists(key.Item1) && !EntityManager.HasComponent<Deleted>(key.Item1))
                    EntityManager.AddComponent<BatchesUpdated>(key.Item1);
            m_AnnouncedParkingNodes.Clear();
            m_TiledEdgeCompositions = m_TiledNodeCompositions = 0;
        }

        private void ForgetParkingEdges()
        {
            m_ParkingEdgeRuntimes.Clear();
            m_TiledParkingEdges.Clear();
            m_AnnouncedParkingEdges.Clear();
            m_ParkingNodeRuntimes.Clear();
            m_ParkingNodeRoads.Clear();
            m_AnnouncedParkingNodes.Clear();
            m_TiledEdgeCompositions = m_TiledNodeCompositions = 0;
        }

        // Rest pieces of parking corners: tile negative V like the edges.
        private void TileNegativeV(Entity piece)
        {
            if (piece == Entity.Null || !EntityManager.HasBuffer<MeshUV0>(piece)) return;
            var uvs = EntityManager.GetBuffer<MeshUV0>(piece);
            if (uvs.Length == 0) return;
            float minV = float.MaxValue, maxV = float.MinValue;
            for (int i = 0; i < uvs.Length; i++) { minV = math.min(minV, uvs[i].m_Uv.y); maxV = math.max(maxV, uvs[i].m_Uv.y); }
            if (minV >= 0f || maxV > 0.0001f) return;
            float shift = -math.floor(minV);
            for (int i = 0; i < uvs.Length; i++)
            {
                var uv = uvs[i].m_Uv;
                uv.y = math.max(uv.y + shift, 0.0001f);
                uvs[i] = new MeshUV0 { m_Uv = uv };
            }
            m_TiledEdgePieces++;
        }
    }
}
