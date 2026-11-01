using System;
using System.Collections.Generic;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Serialization;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Advanced_Graphics
{

    public partial class SidewalkJunctionMeshSystem : GameSystemBase, IPreSerialize, IPreDeserialize
    {
        private const float LeftUv = 318f / 1024f;
        private const float RightUv = 759f / 1024f;
        private const float UvEpsilon = 0.0005f;
        private const int BuildsPerScan = 16;
        private const int MaximumCorners = 4096;

        private sealed class Runtime
        {
            public int Signature;
            public Entity Source;
            public Entity Composition;
            public bool Announced;
            public Entity Mesh;
            public readonly List<Entity> Created = new List<Entity>();
            public readonly List<SourceFrame> Frames = new List<SourceFrame>();
        }

        private sealed class SourceFrame
        {
            public Frame Frame;
            public float2 Centroid;
            public int MaterialIndex;
            public float MaxU;
        }

        private sealed class Frame
        {
            public float2 Origin, Uv, UvX, UvZ;
            public Frame Seam;
            public float2 Center, Axis, Lateral;
            public float Side, RefRadius, VPerMetre;
            public float2 Sample(float3 world)
            {
                if (Seam == null)
                {
                    var d = world.xz - Origin;
                    return Uv + UvX * d.x + UvZ * d.y;
                }
                var p = world.xz - Center;
                float along = math.dot(p, Axis);
                if (along <= 0f) return Seam.Sample(world);
                float across = math.dot(p, Lateral) * Side;
                float theta = math.atan2(along, across);
                var seamPoint = Center + Lateral * (Side * math.length(p));
                var uv = Seam.Sample(new float3(seamPoint.x, 0f, seamPoint.y));
                uv.y += VPerMetre * RefRadius * theta;
                return uv;
            }
        }

        private sealed class CapInfo
        {
            public float2 Center, Axis;
        }

        private sealed class Retired
        {
            public float Time;
            public List<Entity> Entities;
        }

        private static readonly HashSet<Entity> s_RuntimeMeshes = new HashSet<Entity>();
        internal static bool IsRuntimeMesh(Entity mesh) => s_RuntimeMeshes.Contains(mesh);
        private static readonly HashSet<Entity> s_CornerMeshes = new HashSet<Entity>();
        internal static bool IsCornerMesh(Entity mesh) => s_CornerMeshes.Contains(mesh);

        private static bool s_RecomputeTangents = false;
        private static float s_EndTilingU = 1f, s_EndTilingV = 1f;

        private readonly Dictionary<(Entity, bool), Runtime> m_Runtimes = new Dictionary<(Entity, bool), Runtime>();
        private readonly Dictionary<Entity, Composition> m_Attached = new Dictionary<Entity, Composition>();
        private readonly HashSet<Entity> m_Owned = new HashSet<Entity>();
        private readonly List<Retired> m_Retired = new List<Retired>();
        private PrefabSystem m_Prefabs;
        private EntityQuery m_Roads;
        private float m_NextScan;
        private bool m_Failed;
        private bool m_SyncOnRestore;
        // Debug: blend of the node sync targets used for UV placement (see SyncWeight).
        private static int s_SyncBlendMode;

        private const bool BuildRoundedCurbEnds = false;
        private const bool BuildStraightEnds = false; // off: the shortened game ramp is the accepted corner look
        private static string s_CurbEndWhy = "", s_CurbEndNumbers = "";
        private string m_LastReport;

        // Diagnostics of the last scan.
        private int m_Caps, m_CurbEnds, m_WrappedParts, m_SidewalkEnds, m_AlleyMeshes, m_CurbTriangles, m_CurbLogged, m_RampsReshaped, m_RampsFailed, m_RampLogged, m_MixedTriangles;
        private Dictionary<Entity, MeshVertex[]> m_Reshaped;
        // The game's logger failed on bursts of debug lines; detailed data goes to a file instead.
        private const bool VerboseJunctionLog = false;
        private readonly HashSet<string> m_DumpedRamps = new HashSet<string>();
        // Per reshaped piece: vertex -> vertex of row 0 whose profile it took (-1 = unchanged).
        private Dictionary<Entity, int[]> m_ReshapeMap;
        // Per reshaped piece: new atlas U per vertex (NaN = keep).
        private Dictionary<Entity, float[]> m_ReshapeU;
        // Per reshaped piece: added curb faces at the end of the ramp (curb around the corner).
        private Dictionary<Entity, RampExtra> m_ReshapeExtra;
        private Dictionary<Entity, HashSet<int>> m_ReshapeDrop;
        private Dictionary<Entity, RampExtra> m_ReshapeFill;

        private sealed class RampExtra
        {
            public readonly List<float3> Positions = new List<float3>();
            public readonly List<float3> Normals = new List<float3>();
            public readonly List<float4> Tangents = new List<float4>();
            public readonly List<float2> Uvs = new List<float2>();
            public readonly List<int> Indices = new List<int>();
        }
        private int m_Corners, m_Built, m_Skipped, m_SplitPieces, m_WalkTriangles, m_NoFrame, m_Stale, m_NotLoaded, m_Clamped;
        private float m_ClampExcess;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_Roads = GetEntityQuery(ComponentType.ReadOnly<Edge>(), ComponentType.ReadOnly<Composition>(),
                ComponentType.ReadOnly<PrefabRef>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
        }

        protected override void OnUpdate()
        {
            // Called in PreCulling, before PreCullingSystem. Restore first in case a phase was skipped.
            EndRendering();
            var setting = Mod.ActiveSetting;
            if (setting == null || !setting.SidewalkJunctionOverlay)
            {
                if (m_Runtimes.Count > 0) DropAll();
                m_Failed = false;
                m_LastReport = null;
                return;
            }
            if (m_Failed) return;
            try
            {
                ReleaseRetired(false);
                if (setting.HasPendingApply == false && UnityEngine.Time.unscaledTime >= m_NextScan)
                {
                    m_NextScan = UnityEngine.Time.unscaledTime + 2f;
                    Scan();
                }
                Attach();
            }
            catch (Exception ex)
            {
                m_Failed = true;
                try { DropAll(); } catch (Exception inner) { Mod.log.Error(inner, "Junction sidewalk cleanup failed."); }
                Mod.log.Error(ex, "Junction sidewalk meshes stopped. Toggle the option off/on to retry.");
            }
        }

        private void Attach()
        {
            bool announced = false;
            foreach (var pair in m_Runtimes)
            {
                var runtime = pair.Value;
                if (runtime.Composition == Entity.Null) continue;
                var edge = pair.Key.Item1;
                bool start = pair.Key.Item2;
                if (!EntityManager.Exists(edge) || EntityManager.HasComponent<Deleted>(edge) ||
                    !EntityManager.HasComponent<Composition>(edge)) continue;
                var current = EntityManager.GetComponentData<Composition>(edge);
                var native = m_Attached.TryGetValue(edge, out var saved) ? saved : current;
                if ((start ? native.m_StartNode : native.m_EndNode) != runtime.Source)
                {
                    // The game rebuilt this edge. Render natively until the next scan rebuilds.
                    runtime.Signature = 0;
                    m_NextScan = math.min(m_NextScan, UnityEngine.Time.unscaledTime + 0.25f);
                    m_Stale++;
                    continue;
                }
                if (!m_Attached.ContainsKey(edge)) m_Attached.Add(edge, current);
                if (start) current.m_StartNode = runtime.Composition;
                else current.m_EndNode = runtime.Composition;
                EntityManager.SetComponentData(edge, current);
                if (!runtime.Announced)
                {
                    // BatchInstanceSystem must pick the runtime mesh for this edge.
                    EntityManager.AddComponent<BatchesUpdated>(edge);
                    runtime.Announced = true;
                    announced = true;
                }
            }
            if (announced && m_Attached.Count > 0)
            {
                World.GetOrCreateSystemManaged<Game.Rendering.RequiredBatchesSystem>().Update();
                EntityManager.CompleteAllTrackedJobs();
                m_SyncOnRestore = true;
            }
        }

        // Restores the native compositions. Runs at every phase boundary where other systems read them.
        public void EndRendering()
        {
            if (m_Attached.Count == 0) return;
            if (m_SyncOnRestore)
            {
                // Jobs scheduled by the forced RequiredBatchesSystem update still read Composition.
                EntityManager.CompleteAllTrackedJobs();
                m_SyncOnRestore = false;
            }
            foreach (var pair in m_Attached)
            {
                var edge = pair.Key;
                if (!EntityManager.Exists(edge) || !EntityManager.HasComponent<Composition>(edge)) continue;
                var current = EntityManager.GetComponentData<Composition>(edge);
                bool changed = false;
                if (current.m_StartNode != pair.Value.m_StartNode && m_Owned.Contains(current.m_StartNode))
                { current.m_StartNode = pair.Value.m_StartNode; changed = true; }
                if (current.m_EndNode != pair.Value.m_EndNode && m_Owned.Contains(current.m_EndNode))
                { current.m_EndNode = pair.Value.m_EndNode; changed = true; }
                if (changed) EntityManager.SetComponentData(edge, current);
            }
            m_Attached.Clear();
        }

        public void PreSerialize(Context context) => EndRendering();

        public void PreDeserialize(Context context)
        {
            // The world is cleared next; forget runtime entities without touching them.
            m_Attached.Clear();
            m_Runtimes.Clear();
            m_Owned.Clear();
            m_Retired.Clear();
            m_SidewalkFreeMeshes.Clear();
            m_NextScan = 0f;
        }

        internal void DropAll()
        {
            EndRendering();
            foreach (var pair in m_Runtimes)
            {
                Retire(pair.Value);
                var edge = pair.Key.Item1;
                if (EntityManager.Exists(edge) && !EntityManager.HasComponent<Deleted>(edge))
                    EntityManager.AddComponent<BatchesUpdated>(edge);
            }
            m_Runtimes.Clear();
            m_NextScan = 0f;
        }

        protected override void OnDestroy()
        {
            try { DropAll(); ReleaseRetired(true); } catch (Exception) { }
            base.OnDestroy();
        }

        private void Retire(Runtime runtime)
        {
            if (runtime.Composition != Entity.Null) m_Owned.Remove(runtime.Composition);
            if (runtime.Created.Count > 0)
                m_Retired.Add(new Retired { Time = UnityEngine.Time.unscaledTime, Entities = new List<Entity>(runtime.Created) });
            runtime.Created.Clear();
            runtime.Composition = Entity.Null;
        }

        private void ReleaseRetired(bool all)
        {
            for (int i = m_Retired.Count - 1; i >= 0; i--)
            {
                var item = m_Retired[i];
                if (!all && UnityEngine.Time.unscaledTime - item.Time < 5f) continue;
                foreach (var entity in item.Entities)
                {
                    s_RuntimeMeshes.Remove(entity);
                    s_CornerMeshes.Remove(entity);
                    if (!EntityManager.Exists(entity)) continue;
                    if (EntityManager.HasBuffer<MeshVertex>(entity)) { var b = EntityManager.GetBuffer<MeshVertex>(entity); b.Clear(); b.TrimExcess(); }
                    if (EntityManager.HasBuffer<MeshNormal>(entity)) { var b = EntityManager.GetBuffer<MeshNormal>(entity); b.Clear(); b.TrimExcess(); }
                    if (EntityManager.HasBuffer<MeshTangent>(entity)) { var b = EntityManager.GetBuffer<MeshTangent>(entity); b.Clear(); b.TrimExcess(); }
                    if (EntityManager.HasBuffer<MeshUV0>(entity)) { var b = EntityManager.GetBuffer<MeshUV0>(entity); b.Clear(); b.TrimExcess(); }
                    if (EntityManager.HasBuffer<MeshIndex>(entity)) { var b = EntityManager.GetBuffer<MeshIndex>(entity); b.Clear(); b.TrimExcess(); }
                    if (EntityManager.HasBuffer<MeshMaterial>(entity) && EntityManager.HasComponent<MeshData>(entity))
                    {
                        var materials = EntityManager.GetBuffer<MeshMaterial>(entity);
                        for (int m = 0; m < materials.Length; m++)
                            materials[m] = new MeshMaterial(0, 0, 0, 0, materials[m].m_MaterialIndex);
                        var data = EntityManager.GetComponentData<MeshData>(entity);
                        data.m_IndexCount = 0;
                        EntityManager.SetComponentData(entity, data);
                    }
                }
                m_Retired.RemoveAt(i);
            }
        }

        private void Scan()
        {
            m_Corners = m_Built = m_Skipped = m_SplitPieces = m_WalkTriangles = m_NoFrame = m_NotLoaded = m_Clamped = 0;
            m_ClampExcess = 0f;
            s_SyncBlendMode = Mod.DebugBackendEnabled ? math.clamp(Mod.ActiveSetting?.JunctionSyncBlendMode ?? 0, 0, 6) : 0;
            s_EndTilingU = Mod.ActiveSetting?.RoadEndTilingU ?? 1f;
            s_EndTilingV = Mod.ActiveSetting?.RoadEndTilingV ?? 1f;
            s_RecomputeTangents = Mod.DebugBackendEnabled && (Mod.ActiveSetting?.JunctionRecomputeTangents ?? false);
            var active = new HashSet<(Entity, bool)>();
            var mainCorners = new List<(Entity, (Entity, bool))>();
            var sidewalkEnds = new HashSet<Entity>();
            m_AlleyMeshes = 0;
            m_CurbTriangles = 0;
            m_CurbLogged = 0;
            m_RampsReshaped = m_RampsFailed = m_MixedTriangles = m_SidewalkEnds = m_WrappedParts = m_CurbEnds = m_Caps = 0;
            int builds = 0;
            var roads = m_Roads.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var road in roads)
                {
                    if (active.Count >= MaximumCorners) break;
                    if (IsAlley(road)) continue;
                    var prefabEntity = EntityManager.GetComponentData<PrefabRef>(road).m_Prefab;
                    if (!m_Prefabs.TryGetPrefab<PrefabBase>(prefabEntity, out var prefab) || prefab == null ||
                        RoadBuilderSurfaceCompatibility.IsRoadBuilderRoad(prefab)) continue;
                    var edge = EntityManager.GetComponentData<Edge>(road);
                    var composition = EntityManager.GetComponentData<Composition>(road);
                    for (int side = 0; side < 2; side++)
                    {
                        bool start = side == 0;
                        var junction = start ? edge.m_Start : edge.m_End;
                        // Round road end (dead end): slabs follow the arc instead of fanning unevenly.
                        bool cap = EntityManager.HasBuffer<ConnectedEdge>(junction) &&
                            EntityManager.GetBuffer<ConnectedEdge>(junction, true).Length == 1;
                        if (!cap && !AtAlleyJunction(junction)) continue;
                        if (EntityManager.GetBuffer<ConnectedEdge>(junction, true).Length == 2) sidewalkEnds.Add(junction);
                        if (Mod.DebugBackendEnabled && VerboseJunctionLog && !cap) InspectAlleyNode(start ? edge.m_Start : edge.m_End);
                        if (cap) m_Caps++; else m_Corners++;
                        bool hasNodeGeometry = start ? EntityManager.HasComponent<StartNodeGeometry>(road)
                            : EntityManager.HasComponent<EndNodeGeometry>(road);
                        if (!hasNodeGeometry || !EntityManager.HasComponent<EdgeGeometry>(road)) { m_Skipped++; continue; }
                        var geometry = start ? EntityManager.GetComponentData<StartNodeGeometry>(road).m_Geometry
                            : EntityManager.GetComponentData<EndNodeGeometry>(road).m_Geometry;
                        var edgeGeometry = EntityManager.GetComponentData<EdgeGeometry>(road);
                        var source = start ? composition.m_StartNode : composition.m_EndNode;
                        if (source == Entity.Null || m_Owned.Contains(source) ||
                            !EntityManager.HasComponent<NetCompositionMeshRef>(source) ||
                            !EntityManager.HasComponent<NetCompositionData>(source)) { m_Skipped++; continue; }
                        var meshRef = EntityManager.GetComponentData<NetCompositionMeshRef>(source);
                        if (Mod.DebugBackendEnabled && VerboseJunctionLog && meshRef.m_Mesh != Entity.Null && m_InspectedMeshes.Count <= 16 &&
                            EntityManager.HasBuffer<NetCompositionPiece>(meshRef.m_Mesh) && m_InspectedMeshes.Add(meshRef.m_Mesh))
                            InspectMesh(meshRef.m_Mesh, "main road node mesh (" + (start ? "start" : "end") + ")");
                        var compositionData = EntityManager.GetComponentData<NetCompositionData>(source);
                        if (meshRef.m_Mesh == Entity.Null || !EntityManager.HasComponent<NetCompositionMeshData>(meshRef.m_Mesh) ||
                            !EntityManager.HasBuffer<NetCompositionPiece>(meshRef.m_Mesh) ||
                            !ValidSync(compositionData.m_SyncVertexOffsetsLeft, geometry.m_SyncVertexTargetsLeft) ||
                            !ValidSync(compositionData.m_SyncVertexOffsetsRight, geometry.m_SyncVertexTargetsRight)) { m_Skipped++; continue; }

                        var key = (road, start);
                        active.Add(key);
                        mainCorners.Add((start ? edge.m_Start : edge.m_End, key));
                        int signature = geometry.GetHashCode() ^ source.GetHashCode() * 397 ^ meshRef.m_Mesh.GetHashCode() * 31 ^
                            (edgeGeometry.m_Start.m_Length + edgeGeometry.m_End.m_Length).GetHashCode() * 17 ^ (start ? 0x5bd1e995 : 0) ^
                            (s_SyncBlendMode + 1) * 0x01000193 ^ (cap ? 0x3c6ef372 ^ (s_EndTilingU * 97f + s_EndTilingV * 7919f).GetHashCode() : 0) ^ (s_RecomputeTangents ? 0x6a09e667 : 0);
                        if (signature == 0) signature = 1;
                        m_Runtimes.TryGetValue(key, out var old);
                        if (old != null && old.Signature == signature) continue;
                        if (builds >= BuildsPerScan)
                        {
                            m_NextScan = UnityEngine.Time.unscaledTime + 0.1f;
                            continue;
                        }
                        builds++;
                        var runtime = SafeBuild(source, () => Build(source, meshRef, compositionData, geometry, edgeGeometry, start,
                            capInfo: cap ? MakeCapInfo(edgeGeometry, start) : null));
                        if (runtime == null)
                        {
                            builds--;
                            m_NextScan = UnityEngine.Time.unscaledTime + 0.5f;
                            continue;
                        }
                        runtime.Signature = signature;
                        runtime.Mesh = runtime.Created.Count > 0 ? FindMesh(runtime) : Entity.Null;
                        if (old != null)
                        {
                            EndRendering();
                            Retire(old);
                            EntityManager.AddComponent<BatchesUpdated>(road);
                        }
                        m_Runtimes[key] = runtime;
                        if (runtime.Composition != Entity.Null) m_Built++;
                    }
                }
            }
            finally { roads.Dispose(); }

            ScanAlleyNodes(active, mainCorners, sidewalkEnds, ref builds);

            var remove = new List<(Entity, bool)>();
            foreach (var pair in m_Runtimes) if (!active.Contains(pair.Key)) remove.Add(pair.Key);
            if (remove.Count > 0) EndRendering();
            foreach (var key in remove)
            {
                Retire(m_Runtimes[key]);
                m_Runtimes.Remove(key);
                if (EntityManager.Exists(key.Item1) && !EntityManager.HasComponent<Deleted>(key.Item1))
                    EntityManager.AddComponent<BatchesUpdated>(key.Item1);
            }

            int live = 0, withBatches = 0, batchGroups = 0;
            foreach (var runtime in m_Runtimes.Values)
            {
                if (runtime.Composition == Entity.Null) continue;
                live++;
                // BatchManagerSystem fills this buffer once it created batches for the runtime mesh.
                if (runtime.Mesh != Entity.Null && EntityManager.Exists(runtime.Mesh) && EntityManager.HasBuffer<BatchGroup>(runtime.Mesh))
                {
                    int groups = EntityManager.GetBuffer<BatchGroup>(runtime.Mesh, true).Length;
                    batchGroups += groups;
                    if (groups > 0) withBatches++;
                }
            }
            string report = "Junction sidewalk meshes: " + live + " corners active (" + m_Corners + " alley corners found, " + m_Caps + " road ends, " + m_SidewalkEnds + " sidewalk ends, " + m_AlleyMeshes + " alley node meshes remapped, " +
                m_Built + " built now, " + m_Skipped + " skipped, " + m_SplitPieces + " sidewalk pieces split, " +
                m_WalkTriangles + " sidewalk triangles remapped (" + m_CurbTriangles + " widened curb, " + m_RampsReshaped + " curb ramps shortened (" + m_CurbEnds + " curb ends), " + m_RampsFailed + " not, " + m_MixedTriangles + " ramp end faces), " + m_Clamped + " UVs outside the strip (max " + m_ClampExcess.ToString("0.#") + " px, " + m_WrappedParts + " wrapped parts), " + m_NoFrame + " without frame, " + m_Stale + " stale swaps, " +
                m_NotLoaded + " waiting for geometry, sync blend " + s_SyncBlendMode + ", " + withBatches + " meshes with batches / " + batchGroups + " batch groups).";
            if (report != m_LastReport)
            {
                m_LastReport = report;
                Mod.LogLater(report);
            }
            m_Stale = 0;
        }

        private void ScanAlleyNodes(HashSet<(Entity, bool)> active, List<(Entity, (Entity, bool))> mainCorners,
            HashSet<Entity> sidewalkEnds, ref int builds)
        {
            var frames = new Dictionary<Entity, List<SourceFrame>>();
            var signatures = new Dictionary<Entity, int>();
            foreach (var end in sidewalkEnds)
            {
                // No frames: the ramp is shortened, the tiles keep the game's mapping along the curve.
                frames[end] = new List<SourceFrame>();
                signatures[end] = 0x2545f491;
                m_SidewalkEnds++;
            }
            foreach (var corner in mainCorners)
            {
                if (!m_Runtimes.TryGetValue(corner.Item2, out var main) || main.Composition == Entity.Null || main.Frames.Count == 0) continue;
                if (!frames.TryGetValue(corner.Item1, out var list)) { list = new List<SourceFrame>(); frames.Add(corner.Item1, list); signatures.Add(corner.Item1, 0); }
                list.AddRange(main.Frames);
                signatures[corner.Item1] = signatures[corner.Item1] * 31 + main.Signature;
            }
            foreach (var pair in frames)
            {
                var node = pair.Key;
                if (!EntityManager.HasBuffer<ConnectedEdge>(node)) continue;
                var connected = EntityManager.GetBuffer<ConnectedEdge>(node, true).ToNativeArray(Allocator.Temp).ToArray();
                foreach (var item in connected)
                {
                    var alley = item.m_Edge;
                    if (!IsAlley(alley) || !EntityManager.HasComponent<Edge>(alley)) continue;
                    bool start = EntityManager.GetComponentData<Edge>(alley).m_Start == node;
                    bool hasNodeGeometry = start ? EntityManager.HasComponent<StartNodeGeometry>(alley)
                        : EntityManager.HasComponent<EndNodeGeometry>(alley);
                    if (!hasNodeGeometry || !EntityManager.HasComponent<EdgeGeometry>(alley)) continue;
                    var composition = EntityManager.GetComponentData<Composition>(alley);
                    var source = start ? composition.m_StartNode : composition.m_EndNode;
                    if (source == Entity.Null || m_Owned.Contains(source) ||
                        !EntityManager.HasComponent<NetCompositionMeshRef>(source) ||
                        !EntityManager.HasComponent<NetCompositionData>(source)) continue;
                    var meshRef = EntityManager.GetComponentData<NetCompositionMeshRef>(source);
                    var compositionData = EntityManager.GetComponentData<NetCompositionData>(source);
                    var geometry = start ? EntityManager.GetComponentData<StartNodeGeometry>(alley).m_Geometry
                        : EntityManager.GetComponentData<EndNodeGeometry>(alley).m_Geometry;
                    if (meshRef.m_Mesh == Entity.Null || !EntityManager.HasComponent<NetCompositionMeshData>(meshRef.m_Mesh) ||
                        !EntityManager.HasBuffer<NetCompositionPiece>(meshRef.m_Mesh) ||
                        !ValidSync(compositionData.m_SyncVertexOffsetsLeft, geometry.m_SyncVertexTargetsLeft) ||
                        !ValidSync(compositionData.m_SyncVertexOffsetsRight, geometry.m_SyncVertexTargetsRight)) continue;
                    if (Mod.DebugBackendEnabled && VerboseJunctionLog && m_InspectedMeshes.Count <= 16 && m_InspectedMeshes.Add(meshRef.m_Mesh))
                        InspectMesh(meshRef.m_Mesh, "alley node mesh (" + (start ? "start" : "end") + ")");

                    var key = (alley, start);
                    active.Add(key);
                    int signature = geometry.GetHashCode() ^ source.GetHashCode() * 397 ^ meshRef.m_Mesh.GetHashCode() * 31 ^
                        signatures[node] * 0x2f0b3 ^ (start ? 0x7a3c5 : 0x13579);
                    if (signature == 0) signature = 1;
                    m_Runtimes.TryGetValue(key, out var old);
                    if (old != null && old.Signature == signature) { if (old.Composition != Entity.Null) m_AlleyMeshes++; continue; }
                    if (builds >= BuildsPerScan) { m_NextScan = UnityEngine.Time.unscaledTime + 0.1f; continue; }
                    builds++;
                    var edgeGeometry = EntityManager.GetComponentData<EdgeGeometry>(alley);
                    // Sidewalk ends (road continues without sidewalk) keep the game's long taper.
                    var runtime = SafeBuild(source, () => Build(source, meshRef, compositionData, geometry, edgeGeometry, start, pair.Value, sidewalkEnds.Contains(node)));
                    if (runtime == null) { builds--; m_NextScan = UnityEngine.Time.unscaledTime + 0.5f; continue; }
                    runtime.Signature = signature;
                    runtime.Mesh = runtime.Created.Count > 0 ? FindMesh(runtime) : Entity.Null;
                    if (old != null)
                    {
                        EndRendering();
                        Retire(old);
                        EntityManager.AddComponent<BatchesUpdated>(alley);
                    }
                    m_Runtimes[key] = runtime;
                    if (runtime.Composition != Entity.Null) { m_Built++; m_AlleyMeshes++; }
                }
            }
        }

        private readonly Dictionary<Entity, bool> m_SidewalkFreeMeshes = new Dictionary<Entity, bool>();

        private bool IsAlley(Entity edge)
        {
            if (!EntityManager.HasComponent<Game.Net.Road>(edge) || !EntityManager.HasComponent<Composition>(edge)) return false;
            var composed = EntityManager.GetComponentData<Composition>(edge).m_Edge;
            if (composed == Entity.Null || !EntityManager.HasComponent<NetCompositionMeshRef>(composed)) return false;
            var mesh = EntityManager.GetComponentData<NetCompositionMeshRef>(composed).m_Mesh;
            if (mesh == Entity.Null || !EntityManager.HasBuffer<NetCompositionPiece>(mesh)) return false;
            if (m_SidewalkFreeMeshes.TryGetValue(mesh, out bool free)) return free;
            free = true;
            var pieces = EntityManager.GetBuffer<NetCompositionPiece>(mesh, true);
            for (int i = 0; i < pieces.Length && free; i++)
            {
                if (m_Prefabs.TryGetPrefab<PrefabBase>(pieces[i].m_Piece, out var prefab) && prefab != null && prefab.name != null &&
                    prefab.name.IndexOf("Sidewalk", StringComparison.OrdinalIgnoreCase) >= 0)
                    free = false;
            }
            m_SidewalkFreeMeshes[mesh] = free;
            return free;
        }

        private readonly HashSet<Entity> m_InspectedMeshes = new HashSet<Entity>();

        private void InspectAlleyNode(Entity node)
        {
            if (m_InspectedMeshes.Count > 16 || !EntityManager.HasBuffer<ConnectedEdge>(node)) return;
            var edges = EntityManager.GetBuffer<ConnectedEdge>(node, true).ToNativeArray(Allocator.Temp).ToArray();
            foreach (var connected in edges)
            {
                var alley = connected.m_Edge;
                if (!IsAlley(alley) || !EntityManager.HasComponent<Edge>(alley)) continue;
                var alleyEdge = EntityManager.GetComponentData<Edge>(alley);
                var composition = EntityManager.GetComponentData<Composition>(alley);
                bool atStart = alleyEdge.m_Start == node;
                var source = atStart ? composition.m_StartNode : composition.m_EndNode;
                if (source == Entity.Null || !EntityManager.HasComponent<NetCompositionMeshRef>(source)) continue;
                var mesh = EntityManager.GetComponentData<NetCompositionMeshRef>(source).m_Mesh;
                if (mesh == Entity.Null || !m_InspectedMeshes.Add(mesh) || !EntityManager.HasBuffer<NetCompositionPiece>(mesh)) continue;
                string prefabName = "?";
                if (EntityManager.HasComponent<PrefabRef>(alley) &&
                    m_Prefabs.TryGetPrefab<PrefabBase>(EntityManager.GetComponentData<PrefabRef>(alley).m_Prefab, out var alleyPrefab) && alleyPrefab != null)
                    prefabName = alleyPrefab.name;
                InspectMesh(mesh, "alley node mesh of " + prefabName + " (" + (atStart ? "start" : "end") + ")");
            }
        }

        private void InspectMesh(Entity mesh, string label)
        {
            foreach (var check in EntityManager.GetBuffer<NetCompositionPiece>(mesh, true).ToNativeArray(Allocator.Temp))
            {
                // Streamed geometry: retry on a later scan instead of logging empty pieces.
                if (!PieceLoaded(check.m_Piece)) { m_InspectedMeshes.Remove(mesh); return; }
            }
            {
                var data = EntityManager.GetComponentData<NetCompositionMeshData>(mesh);
                Mod.log.Info("Junction debug: " + label + ", width " + data.m_Width + ", flags " + data.m_Flags.m_General +
                    " / L " + data.m_Flags.m_Left + " / R " + data.m_Flags.m_Right);
                var pieces = EntityManager.GetBuffer<NetCompositionPiece>(mesh, true).ToNativeArray(Allocator.Temp).ToArray();
                foreach (var piece in pieces)
                {
                    string name = m_Prefabs.TryGetPrefab<PrefabBase>(piece.m_Piece, out var piecePrefab) && piecePrefab != null ? piecePrefab.name : "<no prefab>";
                    string line = "Junction debug:   piece " + name + "; flags " + piece.m_PieceFlags + "; section " + piece.m_SectionFlags +
                        "; offset " + piece.m_Offset + "; size " + piece.m_Size;
                    if (PieceLoaded(piece.m_Piece))
                    {
                        var positions = EntityManager.GetBuffer<MeshVertex>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray();
                        var uvs = EntityManager.GetBuffer<MeshUV0>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray();
                        var indices = EntityManager.GetBuffer<MeshIndex>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray();
                        var slots = EntityManager.GetBuffer<MeshMaterial>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray();
                        foreach (var slot in slots)
                        {
                            int top = 0;
                            float u0 = float.MaxValue, u1 = float.MinValue;
                            for (int i = slot.m_StartIndex; i + 2 < slot.m_StartIndex + slot.m_IndexCount && i + 2 < indices.Length; i += 3)
                            {
                                int a = indices[i].m_Index, b = indices[i + 1].m_Index, c = indices[i + 2].m_Index;
                                if (a < 0 || b < 0 || c < 0 || a >= positions.Length || b >= positions.Length || c >= positions.Length) continue;
                                var n = math.cross(positions[b].m_Vertex - positions[a].m_Vertex, positions[c].m_Vertex - positions[a].m_Vertex);
                                float length = math.length(n);
                                if (length < 0.000001f || math.abs(n.y) / length < 0.65f) continue;
                                top++;
                                u0 = math.min(u0, math.min(uvs[a].m_Uv.x, math.min(uvs[b].m_Uv.x, uvs[c].m_Uv.x)));
                                u1 = math.max(u1, math.max(uvs[a].m_Uv.x, math.max(uvs[b].m_Uv.x, uvs[c].m_Uv.x)));
                            }
                            line += "; slot material " + slot.m_MaterialIndex + ": " + top + " top faces" +
                                (top > 0 ? " u px " + Mathf.RoundToInt(u0 * 1024f) + ".." + Mathf.RoundToInt(u1 * 1024f) : "") +
                                " [" + RegionHistogram(positions, uvs, indices, slot) + "]";
                        }
                    }
                    else line += "; geometry not loaded";
                    Mod.log.Info(line);
                }
            }
        }

        private static string RegionHistogram(MeshVertex[] positions, MeshUV0[] uvs, MeshIndex[] indices, MeshMaterial slot)
        {
            var counts = new int[4, 3];
            float3 curbMin = float.MaxValue, curbMax = float.MinValue;
            for (int i = slot.m_StartIndex; i + 2 < slot.m_StartIndex + slot.m_IndexCount && i + 2 < indices.Length; i += 3)
            {
                int a = indices[i].m_Index, b = indices[i + 1].m_Index, c = indices[i + 2].m_Index;
                if (a < 0 || b < 0 || c < 0 || a >= positions.Length || b >= positions.Length || c >= positions.Length) continue;
                var pa = positions[a].m_Vertex; var pb = positions[b].m_Vertex; var pc = positions[c].m_Vertex;
                var n = math.cross(pb - pa, pc - pa);
                float length = math.length(n);
                float up = length > 0.000001f ? math.abs(n.y) / length : 0f;
                int orientation = up >= 0.65f ? 0 : up >= 0.3f ? 1 : 2;
                float u = (uvs[a].m_Uv.x + uvs[b].m_Uv.x + uvs[c].m_Uv.x) / 3f * 1024f;
                int region = u < 273f ? 0 : u < 318f ? 1 : u < 759f ? 2 : 3;
                counts[region, orientation]++;
                if (region == 1)
                {
                    curbMin = math.min(curbMin, math.min(pa, math.min(pb, pc)));
                    curbMax = math.max(curbMax, math.max(pa, math.max(pb, pc)));
                }
            }
            string[] names = { "red", "curb", "walk", "other" };
            var text = new System.Text.StringBuilder();
            for (int r = 0; r < 4; r++)
            {
                if (counts[r, 0] + counts[r, 1] + counts[r, 2] == 0) continue;
                if (text.Length > 0) text.Append(", ");
                text.Append(names[r]).Append(" top/slope/wall ").Append(counts[r, 0]).Append('/').Append(counts[r, 1]).Append('/').Append(counts[r, 2]);
            }
            if (curbMax.x >= curbMin.x)
                text.Append(", curb template x ").Append(curbMin.x.ToString("0.00")).Append("..").Append(curbMax.x.ToString("0.00"))
                    .Append(" y ").Append(curbMin.y.ToString("0.00")).Append("..").Append(curbMax.y.ToString("0.00"))
                    .Append(" z ").Append(curbMin.z.ToString("0.00")).Append("..").Append(curbMax.z.ToString("0.00"));
            return text.ToString();
        }

        private bool AtAlleyJunction(Entity node)
        {
            if (!EntityManager.HasBuffer<ConnectedEdge>(node)) return false;
            var edges = EntityManager.GetBuffer<ConnectedEdge>(node, true);
            if (edges.Length != 3 && edges.Length != 2) return false;
            int alleys = 0;
            for (int i = 0; i < edges.Length; i++) if (IsAlley(edges[i].m_Edge)) alleys++;
            return alleys >= 1 && alleys < edges.Length;
        }

        private int m_BuildErrors;
        private Runtime SafeBuild(Entity source, Func<Runtime> build)
        {
            try { return build(); }
            catch (Exception ex)
            {
                m_Reshaped = null; m_ReshapeMap = null; m_ReshapeU = null; m_ReshapeExtra = null; m_ReshapeDrop = null; m_ReshapeFill = null;
                if (m_BuildErrors++ < 5) Mod.log.Error(ex, "Junction sidewalk mesh skipped for one corner/road end.");
                return new Runtime { Source = source };
            }
        }

        private Runtime Build(Entity source, NetCompositionMeshRef meshRef, NetCompositionData compositionData,
            EdgeNodeGeometry geometry, EdgeGeometry edgeGeometry, bool start, List<SourceFrame> external = null, bool sidewalkEnd = false,
            CapInfo capInfo = null)
        {
            var runtime = new Runtime { Source = source };
            var meshEntity = meshRef.m_Mesh;
            var meshData = EntityManager.GetComponentData<NetCompositionMeshData>(meshEntity);
            // Copy everything before creating entities: structural changes invalidate buffers.
            var pieces = EntityManager.GetBuffer<NetCompositionPiece>(meshEntity, true).ToNativeArray(Allocator.Temp).ToArray();
            var meshMaterials = EntityManager.HasBuffer<MeshMaterial>(meshEntity)
                ? EntityManager.GetBuffer<MeshMaterial>(meshEntity, true).ToNativeArray(Allocator.Temp).ToArray()
                : new MeshMaterial[0];
            var carriageways = EntityManager.HasBuffer<NetCompositionCarriageway>(source)
                ? EntityManager.GetBuffer<NetCompositionCarriageway>(source, true).ToNativeArray(Allocator.Temp).ToArray()
                : new NetCompositionCarriageway[0];

            var result = new List<NetCompositionPiece>(pieces.Length + 4);
            bool any = false;
            try
            {
                foreach (var piece in pieces)
                {
                    if (!PieceLoaded(piece.m_Piece))
                    {
                        // Geometry is streamed; try again on a later scan.
                        ReleaseCreated(runtime);
                        m_NotLoaded++;
                        return null;
                    }
                }
                m_Reshaped = null;
                m_ReshapeMap = null;
                m_ReshapeU = null;
                m_ReshapeExtra = null;
                m_ReshapeDrop = null;
                m_ReshapeFill = null;
                if (external != null)
                {
                    m_Reshaped = new Dictionary<Entity, MeshVertex[]>();
                    m_ReshapeMap = new Dictionary<Entity, int[]>();
                    m_ReshapeU = new Dictionary<Entity, float[]>();
                    m_ReshapeExtra = new Dictionary<Entity, RampExtra>();
                    m_ReshapeDrop = new Dictionary<Entity, HashSet<int>>();
                    m_ReshapeFill = new Dictionary<Entity, RampExtra>();
                    var rampFlags = NetPieceFlags.AsymmetricMeshX | NetPieceFlags.AsymmetricMeshZ;
                    float cut = float.MaxValue;
                    var endClamp = new Dictionary<bool, float>();
                    foreach (var piece in pieces)
                        if ((piece.m_PieceFlags & rampFlags) == rampFlags)
                            cut = math.min(cut, RampCut(EntityManager.GetBuffer<MeshVertex>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray()));
                    foreach (var piece in pieces)
                    {
                        if ((piece.m_PieceFlags & rampFlags) != rampFlags || m_Reshaped.ContainsKey(piece.m_Piece)) continue;
                        var shaped = ShortenCurbRamp(EntityManager.GetBuffer<MeshVertex>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray(),
                            EntityManager.GetBuffer<MeshUV0>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray(),
                            EntityManager.GetBuffer<MeshIndex>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray(),
                            cut, out var shapeMap, out var shapeU, out var shapeExtra,
                            p => ProjectToWorld(p, piece, meshData, geometry, compositionData),
                            PieceFrameU(external, piece, meshData, geometry, compositionData), BuildStraightEnds && !sidewalkEnd);
                        if (shaped != null)
                        {
                            m_Reshaped[piece.m_Piece] = shaped; m_ReshapeMap[piece.m_Piece] = shapeMap; m_ReshapeU[piece.m_Piece] = shapeU;
                            if (shapeExtra != null)
                            {
                                m_ReshapeExtra[piece.m_Piece] = shapeExtra.Curb;
                                m_ReshapeFill[piece.m_Piece] = shapeExtra.Fill;
                                endClamp[piece.m_Offset.x < 0f] = shapeExtra.ClampZ;
                                m_CurbEnds++;
                            }
                            m_RampsReshaped++;
                        }
                        else m_RampsFailed++;
                        if (Mod.DebugBackendEnabled) DumpRamp(piece, meshData, geometry, compositionData, shaped, shapeMap, cut, start);
                    }
                    // The outer side wall ends with the sidewalk (same side of the side road).
                    foreach (var piece in pieces)
                    {
                        if (!m_Reshaped.TryGetValue(piece.m_Piece, out var shapedWall) || m_ReshapeExtra.ContainsKey(piece.m_Piece) ||
                            !endClamp.TryGetValue(piece.m_Offset.x < 0f, out float clampZ)) continue;
                        for (int i = 0; i < shapedWall.Length; i++)
                        {
                            var q = shapedWall[i].m_Vertex;
                            if (q.z > clampZ && q.z <= cut + 0.001f) shapedWall[i] = new MeshVertex { m_Vertex = new float3(q.x, q.y, clampZ) };
                        }
                    }
                    // The ramp behind the cut row goes in every ramp piece.
                    if (m_ReshapeExtra.Count > 0)
                        foreach (var piece in pieces)
                        {
                            if (!m_Reshaped.ContainsKey(piece.m_Piece) || m_ReshapeDrop.ContainsKey(piece.m_Piece)) continue;
                            m_ReshapeDrop[piece.m_Piece] = RampDrop(
                                EntityManager.GetBuffer<MeshVertex>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray(),
                                EntityManager.GetBuffer<MeshUV0>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray(),
                                EntityManager.GetBuffer<MeshIndex>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray(), cut);
                        }
                }

                var split = new NetCompositionPiece[pieces.Length][];
                for (int i = 0; i < pieces.Length; i++)
                {
                    if (TrySplit(pieces[i], meshData, geometry, compositionData, edgeGeometry, start, runtime.Created,
                        external, external == null ? runtime.Frames : null, external == null,
                        out var rest, out var walk, out bool hasRest, capInfo))
                    {
                        split[i] = hasRest ? new[] { rest, walk } : new[] { walk };
                        any = true;
                        m_SplitPieces++;
                    }
                }
                var borrowed = external ?? runtime.Frames;
                for (int i = 0; i < pieces.Length; i++)
                {
                    if (split[i] != null || borrowed.Count == 0) continue;
                    if (TrySplit(pieces[i], meshData, geometry, compositionData, edgeGeometry, start, runtime.Created,
                        borrowed, null, external == null, out var rest, out var walk, out bool hasRest, capInfo))
                    {
                        split[i] = hasRest ? new[] { rest, walk } : new[] { walk };
                        any = true;
                        m_SplitPieces++;
                    }
                }
                for (int i = 0; i < pieces.Length; i++)
                {
                    if (split[i] != null)
                    {
                        result.AddRange(split[i]);
                        continue;
                    }
                    var copy = pieces[i];
                    copy.m_Piece = CopyPiece(pieces[i].m_Piece);
                    runtime.Created.Add(copy.m_Piece);
                    result.Add(copy);
                }
                AddCurbEndPieces(pieces, result, runtime);
                // Sidewalk ends: no frames, but a shortened curb ramp is reason enough for a mesh.
                if (m_Reshaped != null && m_Reshaped.Count > 0) any = true;
                m_Reshaped = null;
                m_ReshapeMap = null;
                m_ReshapeU = null;
                m_ReshapeExtra = null;
                m_ReshapeDrop = null;
                m_ReshapeFill = null;
                if (!any)
                {
                    ReleaseCreated(runtime);
                    return runtime;
                }

                if (Mod.DebugBackendEnabled) DumpWorld(result, meshData, geometry, compositionData, external ?? runtime.Frames, external != null, start);

                var mesh = EntityManager.CreateEntity(ComponentType.ReadWrite<NetCompositionMeshData>(),
                    ComponentType.ReadWrite<NetCompositionPiece>(), ComponentType.ReadWrite<MeshMaterial>(),
                    ComponentType.ReadWrite<BatchGroup>());
                runtime.Created.Add(mesh);
                s_RuntimeMeshes.Add(mesh);
                if (capInfo == null) s_CornerMeshes.Add(mesh);
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
                m_Owned.Add(composition);
                return runtime;
            }
            catch
            {
                m_Reshaped = null;
                m_ReshapeMap = null;
                m_ReshapeU = null;
                m_ReshapeExtra = null;
                m_ReshapeDrop = null;
                m_ReshapeFill = null;
                ReleaseCreated(runtime);
                throw;
            }
        }

        private Entity FindMesh(Runtime runtime)
        {
            foreach (var entity in runtime.Created)
                if (EntityManager.Exists(entity) && EntityManager.HasComponent<NetCompositionMeshData>(entity)) return entity;
            return Entity.Null;
        }

        private bool PieceLoaded(Entity piece)
        {
            return EntityManager.HasBuffer<MeshVertex>(piece) && EntityManager.HasBuffer<MeshNormal>(piece) &&
                EntityManager.HasBuffer<MeshTangent>(piece) && EntityManager.HasBuffer<MeshUV0>(piece) &&
                EntityManager.HasBuffer<MeshIndex>(piece) && EntityManager.HasBuffer<MeshMaterial>(piece) &&
                EntityManager.HasComponent<MeshData>(piece) && EntityManager.HasComponent<PrefabData>(piece) &&
                EntityManager.GetBuffer<MeshVertex>(piece, true).Length > 0 &&
                EntityManager.GetBuffer<MeshIndex>(piece, true).Length > 0;
        }

        private Entity CopyPiece(Entity source)
        {
            var positions = m_Reshaped != null && m_Reshaped.TryGetValue(source, out var reshaped)
                ? (MeshVertex[])reshaped.Clone()
                : EntityManager.GetBuffer<MeshVertex>(source, true).ToNativeArray(Allocator.Temp).ToArray();
            var normals = EntityManager.GetBuffer<MeshNormal>(source, true).ToNativeArray(Allocator.Temp).ToArray();
            var tangents = EntityManager.GetBuffer<MeshTangent>(source, true).ToNativeArray(Allocator.Temp).ToArray();
            var uvs = EntityManager.GetBuffer<MeshUV0>(source, true).ToNativeArray(Allocator.Temp).ToArray();
            ApplyReshapeAttributes(source, normals, tangents, uvs);
            var indices = EntityManager.GetBuffer<MeshIndex>(source, true).ToNativeArray(Allocator.Temp).ToArray();
            var slots = EntityManager.GetBuffer<MeshMaterial>(source, true).ToNativeArray(Allocator.Temp).ToArray();
            ApplyRampDrop(source, ref indices, ref slots);
            AppendRampFill(source, ref positions, ref normals, ref tangents, ref uvs, ref indices, ref slots);
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
                u.Add(i < uvs.Length ? uvs[i].m_Uv : default(float2));
            }
            var index = new List<int>(indices.Length);
            foreach (var item in indices) index.Add(item.m_Index);
            return CreatePiece(prefabData, meshData, p, n, t, u, index, new List<MeshMaterial>(slots));
        }

        private void ReleaseCreated(Runtime runtime)
        {
            foreach (var entity in runtime.Created)
            {
                s_RuntimeMeshes.Remove(entity);
                s_CornerMeshes.Remove(entity);
                if (EntityManager.Exists(entity)) EntityManager.DestroyEntity(entity);
            }
            runtime.Created.Clear();
            runtime.Composition = Entity.Null;
        }

        private bool TrySplit(NetCompositionPiece piece, NetCompositionMeshData data, EdgeNodeGeometry geometry,
            NetCompositionData compositionData, EdgeGeometry edgeGeometry, bool start, List<Entity> created,
            List<SourceFrame> external, List<SourceFrame> framesOut, bool nameFilter,
            out NetCompositionPiece rest, out NetCompositionPiece walk, out bool hasRest, CapInfo capInfo = null)
        {
            rest = walk = piece;
            hasRest = false;
            var source = piece.m_Piece;
            if ((piece.m_PieceFlags & (NetPieceFlags.PreserveShape | NetPieceFlags.DisableTiling)) != 0 ||
                (piece.m_SectionFlags & NetSectionFlags.Median) != 0) return false;
            if (!m_Prefabs.TryGetPrefab<PrefabBase>(source, out var prefab) || prefab == null) return false;
            // Side roads: piece names vary (Alley/Gravel ... To Sidewalk); the material check below decides.
            if (nameFilter && !IsRoadAtlasPiece(prefab.name)) return false;
            if (!EntityManager.HasBuffer<MeshVertex>(source) || !EntityManager.HasBuffer<MeshNormal>(source) ||
                !EntityManager.HasBuffer<MeshTangent>(source) || !EntityManager.HasBuffer<MeshUV0>(source) ||
                !EntityManager.HasBuffer<MeshIndex>(source) || !EntityManager.HasBuffer<MeshMaterial>(source) ||
                !EntityManager.HasComponent<MeshData>(source) || !EntityManager.HasComponent<PrefabData>(source)) return false;

            var positions = m_Reshaped != null && m_Reshaped.TryGetValue(source, out var reshaped)
                ? (MeshVertex[])reshaped.Clone()
                : EntityManager.GetBuffer<MeshVertex>(source, true).ToNativeArray(Allocator.Temp).ToArray();
            var normals = EntityManager.GetBuffer<MeshNormal>(source, true).ToNativeArray(Allocator.Temp).ToArray();
            var tangents = EntityManager.GetBuffer<MeshTangent>(source, true).ToNativeArray(Allocator.Temp).ToArray();
            var uvs = EntityManager.GetBuffer<MeshUV0>(source, true).ToNativeArray(Allocator.Temp).ToArray();
            ApplyReshapeAttributes(source, normals, tangents, uvs);
            var indices = EntityManager.GetBuffer<MeshIndex>(source, true).ToNativeArray(Allocator.Temp).ToArray();
            var slots = EntityManager.GetBuffer<MeshMaterial>(source, true).ToNativeArray(Allocator.Temp).ToArray();
            ApplyRampDrop(source, ref indices, ref slots);
            AppendRampFill(source, ref positions, ref normals, ref tangents, ref uvs, ref indices, ref slots);
            var meshData = EntityManager.GetComponentData<MeshData>(source);
            var prefabData = EntityManager.GetComponentData<PrefabData>(source);
            int count = positions.Length;
            bool reshapedPiece = m_Reshaped != null && m_Reshaped.ContainsKey(source);
            if (normals.Length != count || tangents.Length != count || uvs.Length != count) return false;

            bool keepPositive = ((data.m_Flags.m_General & CompositionFlags.General.Node) != 0 &&
                (data.m_Flags.m_General & CompositionFlags.General.Roundabout) == 0) ||
                (piece.m_SectionFlags & NetSectionFlags.HalfLength) != 0;
            bool keepNegative = (piece.m_PieceFlags & NetPieceFlags.SkipBottomHalf) != 0;

            var restIndices = new List<int>(indices.Length);
            var restSlots = new List<MeshMaterial>(slots.Length);
            var walkPositions = new List<float3>();
            var walkNormals = new List<float3>();
            var walkTangents = new List<float4>();
            var walkUvs = new List<float2>();
            var walkIndices = new List<int>();
            var walkSlots = new List<MeshMaterial>();
            var walkDirections = new Dictionary<int, float3>();
            int moved = 0;

            foreach (var slot in slots)
            {
                int begin = slot.m_StartIndex, end = slot.m_StartIndex + slot.m_IndexCount;
                if (begin < 0 || end > indices.Length || begin > end) return false;
                int restBegin = restIndices.Count;
                int walkIndexBegin = walkIndices.Count, walkVertexBegin = walkPositions.Count;
                float density = TemplateVDensity(positions, uvs, indices, begin, end, keepPositive, keepNegative);
                Frame frame = null;
                // Side roads: only the slot drawn with the main road's sidewalk material.
                if (external != null)
                {
                    // Borrowed frame: nearest main-road corner, only for the main road's sidewalk material.
                    if (MaterialAllowed(external, slot.m_MaterialIndex))
                    {
                        frame = NearestFrame(external, SlotCenter(positions, uvs, indices, begin, end, piece, data, geometry, compositionData));
                        if (frame == null) m_NoFrame++;
                    }
                }
                else
                {
                    for (int i = begin; i + 2 < end; i += 3)
                    {
                        int a = indices[i].m_Index, b = indices[i + 1].m_Index, c = indices[i + 2].m_Index;
                        if (a < 0 || b < 0 || c < 0 || a >= count || b >= count || c >= count) continue;
                        if (!IsSidewalkTop(positions[a].m_Vertex, positions[b].m_Vertex, positions[c].m_Vertex,
                            uvs[a].m_Uv, uvs[b].m_Uv, uvs[c].m_Uv)) continue;
                        frame = MakeFrame(positions[a].m_Vertex, positions[b].m_Vertex, positions[c].m_Vertex,
                            uvs[a].m_Uv, uvs[b].m_Uv, uvs[c].m_Uv, piece, data, geometry, compositionData,
                            density, start, edgeGeometry);
                        if (frame != null && capInfo != null)
                            frame = MakeCapFrame(frame, capInfo, SlotRadius(positions, uvs, indices, begin, end, piece, data, geometry, compositionData, capInfo.Center));
                        if (frame == null) m_NoFrame++;
                        break;
                    }
                }
                var remap = new Dictionary<int, int>();
                for (int i = begin; i + 2 < end; i += 3)
                {
                    int a = indices[i].m_Index, b = indices[i + 1].m_Index, c = indices[i + 2].m_Index;
                    bool valid = a >= 0 && b >= 0 && c >= 0 && a < count && b < count && c < count;
                    bool move = false;
                    if (valid && Mod.DebugBackendEnabled) LogCurb((external != null ? "side " : "main ") + prefab.name, slot.m_MaterialIndex, frame != null,
                        positions[a].m_Vertex, positions[b].m_Vertex, positions[c].m_Vertex,
                        uvs[a].m_Uv, uvs[b].m_Uv, uvs[c].m_Uv, piece, data, geometry, compositionData);
                    if (frame != null && valid)
                    {
                        var pa = positions[a].m_Vertex; var pb = positions[b].m_Vertex; var pc = positions[c].m_Vertex;
                        if (IsSidewalkTop(pa, pb, pc, uvs[a].m_Uv, uvs[b].m_Uv, uvs[c].m_Uv)) move = true;
                        else if (!reshapedPiece && IsStretchedCurbTop(pa, pb, pc, uvs[a].m_Uv, uvs[b].m_Uv, uvs[c].m_Uv, piece, data, geometry, compositionData))
                        {
                            move = true;
                            m_CurbTriangles++;
                        }
                        else if (external != null && IsMixedWalkTop(pa, pb, pc, uvs[a].m_Uv, uvs[b].m_Uv, uvs[c].m_Uv, piece, data, geometry, compositionData))
                        {
                            move = true;
                            m_MixedTriangles++;
                        }
                    }
                    if (move)
                    {
                        walkIndices.Add(Remap(a, remap, positions, normals, tangents, walkPositions, walkNormals, walkTangents, walkUvs));
                        walkIndices.Add(Remap(b, remap, positions, normals, tangents, walkPositions, walkNormals, walkTangents, walkUvs));
                        walkIndices.Add(Remap(c, remap, positions, normals, tangents, walkPositions, walkNormals, walkTangents, walkUvs));
                        moved++;
                        continue;
                    }
                    restIndices.Add(indices[i].m_Index);
                    restIndices.Add(indices[i + 1].m_Index);
                    restIndices.Add(indices[i + 2].m_Index);
                }
                restSlots.Add(new MeshMaterial(restBegin, restIndices.Count - restBegin, slot.m_StartVertex, slot.m_VertexCount, slot.m_MaterialIndex));
                if (walkIndices.Count > walkIndexBegin)
                {
                    // Absolute UVs for this slot's moved vertices.
                    for (int v = walkVertexBegin; v < walkPositions.Count; v++)
                    {
                        var world = ProjectToWorld(walkPositions[v], piece, data, geometry, compositionData);
                        var atlas = frame.Sample(world);
                        if (atlas.x < LeftUv + 0.0005f || atlas.x > RightUv - 0.0005f)
                        {
                            m_Clamped++;
                            m_ClampExcess = math.max(m_ClampExcess, math.max(LeftUv - atlas.x, atlas.x - RightUv) * 1024f);
                        }
                        if (s_SyncBlendMode == 6) atlas = new float2(0.95f, 0.5f);
                        walkUvs[v] = atlas;
                        if (s_RecomputeTangents) walkDirections[v] = TemplateTangent(frame, world, walkPositions[v], piece, data, geometry, compositionData);
                    }
                    if (framesOut != null)
                    {
                        float2 sum = 0f;
                        for (int v = walkVertexBegin; v < walkPositions.Count; v++)
                            sum += ProjectToWorld(walkPositions[v], piece, data, geometry, compositionData).xz;
                        float maxU = 0f;
                        for (int i = begin; i + 2 < end; i += 3)
                        {
                            int a = indices[i].m_Index, b = indices[i + 1].m_Index, c = indices[i + 2].m_Index;
                            if (a < 0 || b < 0 || c < 0 || a >= count || b >= count || c >= count) continue;
                            if (!IsSidewalkTop(positions[a].m_Vertex, positions[b].m_Vertex, positions[c].m_Vertex,
                                uvs[a].m_Uv, uvs[b].m_Uv, uvs[c].m_Uv)) continue;
                            maxU = math.max(maxU, math.max(uvs[a].m_Uv.x, math.max(uvs[b].m_Uv.x, uvs[c].m_Uv.x)));
                        }
                        framesOut.Add(new SourceFrame
                        {
                            Frame = frame,
                            Centroid = sum / (walkPositions.Count - walkVertexBegin),
                            MaterialIndex = slot.m_MaterialIndex,
                            MaxU = maxU
                        });
                    }
                    if (s_SyncBlendMode != 6)
                    {
                        if (s_RecomputeTangents) RecomputeTangents(walkIndexBegin, walkVertexBegin, walkPositions, walkNormals, walkTangents, walkUvs, walkIndices, walkDirections);
                        WrapStrip(walkIndexBegin, walkVertexBegin, walkPositions, walkNormals, walkTangents, walkUvs, walkIndices);
                        if (capInfo != null)
                            WrapCapV(walkIndexBegin, walkVertexBegin, walkPositions, walkNormals, walkTangents, walkUvs, walkIndices);
                    }
                    walkSlots.Add(new MeshMaterial(walkIndexBegin, walkIndices.Count - walkIndexBegin,
                        walkVertexBegin, walkPositions.Count - walkVertexBegin, slot.m_MaterialIndex));
                }
            }
            if (moved == 0) return false;

            if (capInfo == null)
            {
                foreach (var slot in walkSlots)
                {
                    int endVertex = slot.m_StartVertex + slot.m_VertexCount;
                    float minV = float.MaxValue, maxV = float.MinValue;
                    for (int v = slot.m_StartVertex; v < endVertex; v++)
                    {
                        minV = math.min(minV, walkUvs[v].y);
                        maxV = math.max(maxV, walkUvs[v].y);
                    }
                    float shift = math.floor(minV);
                    float localMax = maxV - shift;
                    float margin = math.max(0.002f, math.max(1f, localMax) / 512f);
                    float fraction = localMax - math.floor(localMax);
                    if (fraction < margin) shift += fraction + margin;
                    else if (1f - fraction < margin) shift += margin;
                    for (int v = slot.m_StartVertex; v < endVertex; v++)
                        walkUvs[v] = new float2(walkUvs[v].x, walkUvs[v].y - shift);
                }

            }

            var walkEntity = CreatePiece(prefabData, meshData, walkPositions, walkNormals, walkTangents, walkUvs, walkIndices, walkSlots);
            created.Add(walkEntity);
            walk.m_Piece = walkEntity;
            walk.m_PieceFlags |= NetPieceFlags.DisableTiling;
            m_WalkTriangles += moved;

            if (restIndices.Count > 0)
            {
                var restPositions = new List<float3>(count);
                var restNormals = new List<float3>(count);
                var restTangents = new List<float4>(count);
                var restUvs = new List<float2>(count);
                for (int i = 0; i < count; i++)
                {
                    restPositions.Add(positions[i].m_Vertex);
                    restNormals.Add(normals[i].m_Normal);
                    restTangents.Add(tangents[i].m_Tangent);
                    restUvs.Add(uvs[i].m_Uv);
                }
                var restEntity = CreatePiece(prefabData, meshData, restPositions, restNormals, restTangents, restUvs, restIndices, restSlots);
                created.Add(restEntity);
                rest.m_Piece = restEntity;
                hasRest = true;
            }
            return true;
        }

        private static void RecomputeTangents(int indexBegin, int vertexBegin, List<float3> positions, List<float3> normals,
            List<float4> tangents, List<float2> uvs, List<int> indices, Dictionary<int, float3> directions)
        {
            int count = positions.Count - vertexBegin;
            if (count <= 0) return;
            var accumulatedT = new float3[count];
            var accumulatedB = new float3[count];
            for (int i = indexBegin; i + 2 < indices.Count; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                if (a < vertexBegin || b < vertexBegin || c < vertexBegin) continue;
                var e1 = positions[b] - positions[a];
                var e2 = positions[c] - positions[a];
                var d1 = uvs[b] - uvs[a];
                var d2 = uvs[c] - uvs[a];
                float r = d1.x * d2.y - d2.x * d1.y;
                if (math.abs(r) < 1e-12f) continue;
                float f = 1f / r;
                var t = (e1 * d2.y - e2 * d1.y) * f;
                var bt = (e2 * d1.x - e1 * d2.x) * f;
                foreach (int v in new[] { a, b, c })
                {
                    accumulatedT[v - vertexBegin] += t;
                    accumulatedB[v - vertexBegin] += bt;
                }
            }
            for (int k = 0; k < count; k++)
            {
                var n = math.normalizesafe(normals[vertexBegin + k], new float3(0f, 1f, 0f));
                // Direction: the world tangent expressed in the curve frame the shader rotates with.
                // Handedness (w): from the triangles (unchanged by the deformation).
                var direction = directions.TryGetValue(vertexBegin + k, out var d) && math.lengthsq(d) > 0.5f ? d : accumulatedT[k];
                var t = direction - n * math.dot(n, direction);
                if (math.lengthsq(t) < 1e-12f) continue;
                t = math.normalize(t);
                float w = math.dot(math.cross(n, t), accumulatedB[k]) < 0f ? -1f : 1f;
                tangents[vertexBegin + k] = new float4(t, w);
            }
        }

        private static float3 TemplateTangent(Frame frame, float3 world, float3 template, NetCompositionPiece piece,
            NetCompositionMeshData data, EdgeNodeGeometry geometry, NetCompositionData compositionData)
        {
            const float h = 0.05f;
            var s0 = frame.Sample(world);
            var sx = frame.Sample(world + new float3(h, 0f, 0f));
            var sz = frame.Sample(world + new float3(0f, 0f, h));
            float dUdx = (sx.x - s0.x) / h, dUdz = (sz.x - s0.x) / h;
            float dVdx = (sx.y - s0.y) / h, dVdz = (sz.y - s0.y) / h;
            float det = dUdx * dVdz - dUdz * dVdx;
            if (math.abs(det) < 1e-9f) return float3.zero;
            var dPdu = new float2(dVdz, -dVdx) / det;
            const float k = 0.02f;
            var p0 = ProjectToWorld(template, piece, data, geometry, compositionData).xz;
            var lateral = math.normalizesafe(ProjectToWorld(template + new float3(k, 0f, 0f), piece, data, geometry, compositionData).xz - p0);
            var forward = math.normalizesafe(ProjectToWorld(template + new float3(0f, 0f, k), piece, data, geometry, compositionData).xz - p0);
            float frameDet = lateral.x * forward.y - forward.x * lateral.y;
            if (math.abs(frameDet) < 1e-6f) return float3.zero;
            float tx = (dPdu.x * forward.y - forward.x * dPdu.y) / frameDet;
            float tz = (lateral.x * dPdu.y - dPdu.x * lateral.y) / frameDet;
            return math.normalizesafe(new float3(tx, 0f, tz));
        }

        private struct WalkVertex
        {
            public float3 Position, Normal;
            public float4 Tangent;
            public float2 Uv;
            public static WalkVertex Lerp(WalkVertex a, WalkVertex b, float t) => new WalkVertex
            {
                Position = math.lerp(a.Position, b.Position, t),
                Normal = math.normalizesafe(math.lerp(a.Normal, b.Normal, t), a.Normal),
                Tangent = math.lerp(a.Tangent, b.Tangent, t),
                Uv = math.lerp(a.Uv, b.Uv, t)
            };
        }

        private void WrapStrip(int indexBegin, int vertexBegin, List<float3> positions, List<float3> normals,
            List<float4> tangents, List<float2> uvs, List<int> indices)
        {
            float width = RightUv - LeftUv;
            bool outside = false;
            for (int v = vertexBegin; v < uvs.Count; v++)
                if (uvs[v].x < LeftUv || uvs[v].x > RightUv) { outside = true; break; }
            if (!outside) return;

            var triangles = new List<WalkVertex[]>();
            for (int i = indexBegin; i + 2 < indices.Count; i += 3)
            {
                var tri = new WalkVertex[3];
                for (int k = 0; k < 3; k++)
                {
                    int v = indices[i + k];
                    tri[k] = new WalkVertex { Position = positions[v], Normal = normals[v], Tangent = tangents[v], Uv = uvs[v] };
                }
                triangles.Add(tri);
            }
            positions.RemoveRange(vertexBegin, positions.Count - vertexBegin);
            normals.RemoveRange(vertexBegin, normals.Count - vertexBegin);
            tangents.RemoveRange(vertexBegin, tangents.Count - vertexBegin);
            uvs.RemoveRange(vertexBegin, uvs.Count - vertexBegin);
            indices.RemoveRange(indexBegin, indices.Count - indexBegin);

            foreach (var tri in triangles)
            {
                float u0 = math.min(tri[0].Uv.x, math.min(tri[1].Uv.x, tri[2].Uv.x));
                float u1 = math.max(tri[0].Uv.x, math.max(tri[1].Uv.x, tri[2].Uv.x));
                int k0 = (int)math.floor((u0 - LeftUv) / width);
                int k1 = (int)math.floor((u1 - LeftUv) / width);
                if (k1 - k0 > 8) k1 = k0 + 8; // degenerate input; never seen
                for (int k = k0; k <= k1; k++)
                {
                    float lo = LeftUv + k * width, hi = lo + width;
                    var polygon = new List<WalkVertex>(tri);
                    polygon = ClipU(polygon, lo, true);
                    polygon = ClipU(polygon, hi, false);
                    if (polygon.Count < 3) continue;
                    int first = positions.Count;
                    foreach (var vertex in polygon)
                    {
                        positions.Add(vertex.Position);
                        normals.Add(vertex.Normal);
                        tangents.Add(vertex.Tangent);
                        float u = math.clamp(vertex.Uv.x - k * width, LeftUv + 0.0005f, RightUv - 0.0005f);
                        uvs.Add(new float2(u, vertex.Uv.y));
                    }
                    for (int n = 1; n + 1 < polygon.Count; n++)
                    {
                        indices.Add(first);
                        indices.Add(first + n);
                        indices.Add(first + n + 1);
                    }
                    m_WrappedParts++;
                }
            }
        }

        private void WrapCapV(int indexBegin, int vertexBegin, List<float3> positions, List<float3> normals,
            List<float4> tangents, List<float2> uvs, List<int> indices)
        {
            var triangles = new List<WalkVertex[]>();
            for (int i = indexBegin; i + 2 < indices.Count; i += 3)
            {
                var tri = new WalkVertex[3];
                for (int n = 0; n < 3; n++)
                {
                    int v = indices[i + n];
                    tri[n] = new WalkVertex { Position = positions[v], Normal = normals[v], Tangent = tangents[v], Uv = uvs[v] };
                }
                triangles.Add(tri);
            }
            positions.RemoveRange(vertexBegin, positions.Count - vertexBegin);
            normals.RemoveRange(vertexBegin, normals.Count - vertexBegin);
            tangents.RemoveRange(vertexBegin, tangents.Count - vertexBegin);
            uvs.RemoveRange(vertexBegin, uvs.Count - vertexBegin);
            indices.RemoveRange(indexBegin, indices.Count - indexBegin);
            foreach (var tri in triangles)
            {
                float min = math.min(tri[0].Uv.y, math.min(tri[1].Uv.y, tri[2].Uv.y));
                float max = math.max(tri[0].Uv.y, math.max(tri[1].Uv.y, tri[2].Uv.y));
                if (!math.isfinite(min) || !math.isfinite(max) || max - min > 64f)
                    throw new InvalidOperationException("Invalid sidewalk cap V range.");
                int firstTile = (int)math.floor(min), lastTile = (int)math.floor(max);
                for (int tile = firstTile; tile <= lastTile; tile++)
                {
                    var polygon = ClipV(new List<WalkVertex>(tri), tile, true);
                    polygon = ClipV(polygon, tile + 1f, false);
                    if (polygon.Count < 3) continue;
                    int first = positions.Count;
                    foreach (var vertex in polygon)
                    {
                        positions.Add(vertex.Position);
                        normals.Add(vertex.Normal);
                        tangents.Add(vertex.Tangent);
                        uvs.Add(new float2(vertex.Uv.x, math.clamp(vertex.Uv.y - tile - 1f, -1f, -0.0005f)));
                    }
                    for (int n = 1; n + 1 < polygon.Count; n++)
                    {
                        if (math.lengthsq(math.cross(polygon[n].Position - polygon[0].Position,
                            polygon[n + 1].Position - polygon[0].Position)) < 1e-12f) continue;
                        indices.Add(first); indices.Add(first + n); indices.Add(first + n + 1);
                    }
                    m_WrappedParts++;
                }
            }
        }

        private static List<WalkVertex> ClipV(List<WalkVertex> polygon, float edge, bool keepAbove)
        {
            var result = new List<WalkVertex>(polygon.Count + 2);
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count];
                float da = keepAbove ? a.Uv.y - edge : edge - a.Uv.y;
                float db = keepAbove ? b.Uv.y - edge : edge - b.Uv.y;
                if (da >= 0f) result.Add(a);
                if ((da >= 0f) != (db >= 0f)) result.Add(WalkVertex.Lerp(a, b, da / (da - db)));
            }
            return result;
        }

        // Sutherland-Hodgman against u >= edge (keepAbove) or u <= edge.
        private static List<WalkVertex> ClipU(List<WalkVertex> polygon, float edge, bool keepAbove)
        {
            var result = new List<WalkVertex>(polygon.Count + 2);
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                float da = keepAbove ? a.Uv.x - edge : edge - a.Uv.x;
                float db = keepAbove ? b.Uv.x - edge : edge - b.Uv.x;
                if (da >= 0f) result.Add(a);
                if ((da >= 0f) != (db >= 0f))
                {
                    float t = da / (da - db);
                    result.Add(WalkVertex.Lerp(a, b, t));
                }
            }
            return result;
        }

        // World XZ centre of the sidewalk-top triangles of one slot.
        private static float2 SlotCenter(MeshVertex[] positions, MeshUV0[] uvs, MeshIndex[] indices, int begin, int end,
            NetCompositionPiece piece, NetCompositionMeshData data, EdgeNodeGeometry geometry, NetCompositionData compositionData)
        {
            float2 sum = 0f;
            int n = 0;
            for (int i = begin; i + 2 < end; i += 3)
            {
                int a = indices[i].m_Index, b = indices[i + 1].m_Index, c = indices[i + 2].m_Index;
                if (a < 0 || b < 0 || c < 0 || a >= positions.Length || b >= positions.Length || c >= positions.Length) continue;
                if (!IsSidewalkTop(positions[a].m_Vertex, positions[b].m_Vertex, positions[c].m_Vertex,
                    uvs[a].m_Uv, uvs[b].m_Uv, uvs[c].m_Uv)) continue;
                var center = (positions[a].m_Vertex + positions[b].m_Vertex + positions[c].m_Vertex) / 3f;
                sum += ProjectToWorld(center, piece, data, geometry, compositionData).xz;
                n++;
            }
            if (n > 0) return sum / n;
            // No sidewalk top in this slot (curb piece): centre of all its triangles.
            for (int i = begin; i + 2 < end; i += 3)
            {
                int a = indices[i].m_Index, b = indices[i + 1].m_Index, c = indices[i + 2].m_Index;
                if (a < 0 || b < 0 || c < 0 || a >= positions.Length || b >= positions.Length || c >= positions.Length) continue;
                var center = (positions[a].m_Vertex + positions[b].m_Vertex + positions[c].m_Vertex) / 3f;
                sum += ProjectToWorld(center, piece, data, geometry, compositionData).xz;
                n++;
            }
            return n > 0 ? sum / n : new float2(float.NaN, float.NaN);
        }

        private void LogCurb(string name, int material, bool hasFrame, float3 a, float3 b, float3 c, float2 ua, float2 ub, float2 uc,
            NetCompositionPiece piece, NetCompositionMeshData data, EdgeNodeGeometry geometry, NetCompositionData compositionData)
        {
            if (m_CurbLogged >= 0) return; // enough data collected; the game's logger choked on 400 lines per scan
            float u0 = math.min(ua.x, math.min(ub.x, uc.x));
            float u1 = math.max(ua.x, math.max(ub.x, uc.x));
            if (u1 < CurbLeftUv - UvEpsilon || u0 > LeftUv - UvEpsilon) return;
            var wa = ProjectToWorld(a, piece, data, geometry, compositionData);
            var wb = ProjectToWorld(b, piece, data, geometry, compositionData);
            var wc = ProjectToWorld(c, piece, data, geometry, compositionData);
            var wn = math.cross(wb - wa, wc - wa);
            var tn = math.cross(b - a, c - a);
            float wl = math.length(wn), tl = math.length(tn);
            float wy = wl > 0.000001f ? math.abs(wn.y) / wl : 0f;
            float ty = tl > 0.000001f ? math.abs(tn.y) / tl : 0f;
            bool side = name.StartsWith("side ", StringComparison.Ordinal);
            if (wy < 0.3f && !side) return;
            float templateWidth = Thickness(a.xz, b.xz, c.xz), worldWidth = Thickness(wa.xz, wb.xz, wc.xz);
            // Regular 0.2 m curb strip, unchanged: not interesting.
            if (!side && ty >= 0.65f && u1 <= LeftUv + UvEpsilon && worldWidth <= 0.22f && worldWidth <= templateWidth * 1.1f) return;
            m_CurbLogged++;
            Mod.log.Info("Junction curb: " + name + " mat " + material + (hasFrame ? " frame" : " NO frame") +
                ", u px " + (u0 * 1024f).ToString("0") + ".." + (u1 * 1024f).ToString("0") +
                ", up template " + ty.ToString("0.00") + " world " + wy.ToString("0.00") +
                ", width template " + templateWidth.ToString("0.000") + " world " + worldWidth.ToString("0.000") +
                ", area world " + (wl * 0.5f).ToString("0.000") +
                ", template " + Fmt(a) + " " + Fmt(b) + " " + Fmt(c) + ", world y " + wa.y.ToString("0.00") + "/" + wb.y.ToString("0.00") + "/" + wc.y.ToString("0.00") +
                ", moved " + (hasFrame && IsStretchedCurbTop(a, b, c, ua, ub, uc, piece, data, geometry, compositionData)));
        }

        // The alley's sidewalk transition belongs to the main road corner right next to it.
        private static Frame NearestFrame(List<SourceFrame> frames, float2 center)
        {
            if (frames == null || math.any(math.isnan(center))) return null;
            Frame best = null;
            float bestDistance = 15f * 15f;
            foreach (var item in frames)
            {
                float distance = math.distancesq(item.Centroid, center);
                if (distance < bestDistance) { bestDistance = distance; best = item.Frame; }
            }
            return best;
        }

        private static int Remap(int index, Dictionary<int, int> remap, MeshVertex[] positions, MeshNormal[] normals, MeshTangent[] tangents,
            List<float3> walkPositions, List<float3> walkNormals, List<float4> walkTangents, List<float2> walkUvs)
        {
            if (remap.TryGetValue(index, out int mapped)) return mapped;
            mapped = walkPositions.Count;
            remap.Add(index, mapped);
            walkPositions.Add(positions[index].m_Vertex);
            walkNormals.Add(normals[index].m_Normal);
            walkTangents.Add(tangents[index].m_Tangent);
            walkUvs.Add(default);
            return mapped;
        }

        private Entity CreatePiece(PrefabData prefabData, MeshData meshData, List<float3> positions, List<float3> normals,
            List<float4> tangents, List<float2> uvs, List<int> indices, List<MeshMaterial> slots)
        {
            var entity = EntityManager.CreateEntity(ComponentType.ReadWrite<MeshData>(), ComponentType.ReadWrite<MeshVertex>(),
                ComponentType.ReadWrite<MeshNormal>(), ComponentType.ReadWrite<MeshTangent>(), ComponentType.ReadWrite<MeshUV0>(),
                ComponentType.ReadWrite<MeshIndex>(), ComponentType.ReadWrite<MeshMaterial>());
            // Same as DrivewayEntries: a disabled PrefabData copy, no batches of its own.
            EntityManager.AddComponentData(entity, prefabData);
            EntityManager.SetComponentEnabled<PrefabData>(entity, false);
            var data = meshData;
            data.m_IndexCount = indices.Count;
            data.m_AvailableLayers = 0;
            data.m_AvailableTypes = 0;
            EntityManager.SetComponentData(entity, data);
            var vertexBuffer = EntityManager.GetBuffer<MeshVertex>(entity);
            vertexBuffer.ResizeUninitialized(positions.Count);
            for (int i = 0; i < positions.Count; i++) vertexBuffer[i] = new MeshVertex { m_Vertex = positions[i] };
            var normalBuffer = EntityManager.GetBuffer<MeshNormal>(entity);
            normalBuffer.ResizeUninitialized(normals.Count);
            for (int i = 0; i < normals.Count; i++) normalBuffer[i] = new MeshNormal { m_Normal = normals[i] };
            var tangentBuffer = EntityManager.GetBuffer<MeshTangent>(entity);
            tangentBuffer.ResizeUninitialized(tangents.Count);
            for (int i = 0; i < tangents.Count; i++) tangentBuffer[i] = new MeshTangent { m_Tangent = tangents[i] };
            var uvBuffer = EntityManager.GetBuffer<MeshUV0>(entity);
            uvBuffer.ResizeUninitialized(uvs.Count);
            for (int i = 0; i < uvs.Count; i++) uvBuffer[i] = new MeshUV0 { m_Uv = uvs[i] };
            var indexBuffer = EntityManager.GetBuffer<MeshIndex>(entity);
            indexBuffer.ResizeUninitialized(indices.Count);
            for (int i = 0; i < indices.Count; i++) indexBuffer[i] = new MeshIndex { m_Index = indices[i] };
            var slotBuffer = EntityManager.GetBuffer<MeshMaterial>(entity);
            foreach (var slot in slots) slotBuffer.Add(slot);
            return entity;
        }

        private static bool MaterialAllowed(List<SourceFrame> frames, int materialIndex)
        {
            foreach (var item in frames) if (item.MaterialIndex == materialIndex) return true;
            return false;
        }

        private const float CurbLeftUv = 273f / 1024f;
        private const float CurbStripWidth = 0.3f;

        // A face that touches the sidewalk strip (some vertex inside 318..759 px) but also reaches
        // outside it, and lies flat in the world.
        private static bool IsMixedWalkTop(float3 a, float3 b, float3 c, float2 ua, float2 ub, float2 uc,
            NetCompositionPiece piece, NetCompositionMeshData data, EdgeNodeGeometry geometry, NetCompositionData compositionData)
        {
            bool inA = ua.x >= LeftUv - UvEpsilon && ua.x <= RightUv + UvEpsilon;
            bool inB = ub.x >= LeftUv - UvEpsilon && ub.x <= RightUv + UvEpsilon;
            bool inC = uc.x >= LeftUv - UvEpsilon && uc.x <= RightUv + UvEpsilon;
            if (!(inA || inB || inC) || (inA && inB && inC)) return false;
            var wa = ProjectToWorld(a, piece, data, geometry, compositionData);
            var wb = ProjectToWorld(b, piece, data, geometry, compositionData);
            var wc = ProjectToWorld(c, piece, data, geometry, compositionData);
            var n = math.cross(wb - wa, wc - wa);
            float length = math.length(n);
            return length > 0.000001f && math.abs(n.y) / length >= 0.65f;
        }
        private static bool IsStretchedCurbTop(float3 a, float3 b, float3 c, float2 ua, float2 ub, float2 uc,
            NetCompositionPiece piece, NetCompositionMeshData data, EdgeNodeGeometry geometry, NetCompositionData compositionData)
        {
            float u0 = math.min(ua.x, math.min(ub.x, uc.x));
            float u1 = math.max(ua.x, math.max(ub.x, uc.x));
            if (u0 < CurbLeftUv - UvEpsilon || u0 > LeftUv - UvEpsilon || u1 > RightUv + UvEpsilon) return false;
            var wa = ProjectToWorld(a, piece, data, geometry, compositionData);
            var wb = ProjectToWorld(b, piece, data, geometry, compositionData);
            var wc = ProjectToWorld(c, piece, data, geometry, compositionData);
            var worldNormal = math.cross(wb - wa, wc - wa);
            float worldLength = math.length(worldNormal);
            if (worldLength < 0.000001f || math.abs(worldNormal.y) / worldLength < 0.65f) return false;
            float worldWidth = Thickness(wa.xz, wb.xz, wc.xz);
            if (worldWidth > CurbStripWidth) return true;
            if (u1 > LeftUv + UvEpsilon) return false;
            var templateNormal = math.cross(b - a, c - a);
            float templateLength = math.length(templateNormal);
            bool templateTop = templateLength > 0.000001f && math.abs(templateNormal.y) / templateLength >= 0.65f;
            if (!templateTop) return true;
            // Regular curb strips stay at or below their template width (main road corners measured
            // 0.11..0.20 m for a 0.20 m template); the side road transition widens them to 0.26..0.33 m.
            return worldWidth > Thickness(a.xz, b.xz, c.xz) * 1.15f + 0.01f;
        }

        private static string Fmt(float3 v) => "(" + v.x.ToString("0.00") + ";" + v.y.ToString("0.00") + ";" + v.z.ToString("0.00") + ")";

        private static string RowCounts(MeshVertex[] positions)
        {
            var text = new System.Text.StringBuilder();
            foreach (var pair in RampRows(positions)) text.Append(" z").Append(pair.Key.ToString("0.#")).Append(':').Append(pair.Value.Count);
            return text.ToString();
        }

        private void ApplyReshapeAttributes(Entity source, MeshNormal[] normals, MeshTangent[] tangents, MeshUV0[] uvs)
        {
            if (m_ReshapeMap == null || !m_ReshapeMap.TryGetValue(source, out var map)) return;
            var originalUvs = (MeshUV0[])uvs.Clone();
            var originalNormals = (MeshNormal[])normals.Clone();
            var originalTangents = (MeshTangent[])tangents.Clone();
            for (int i = 0; i < map.Length && i < uvs.Length; i++)
            {
                int from = map[i];
                if (from < 0 || from >= uvs.Length) continue;
                uvs[i] = new MeshUV0 { m_Uv = new float2(originalUvs[from].m_Uv.x, originalUvs[i].m_Uv.y) };
                if (from < normals.Length && i < normals.Length) normals[i] = originalNormals[from];
                if (from < tangents.Length && i < tangents.Length) tangents[i] = originalTangents[from];
            }
            if (m_ReshapeU != null && m_ReshapeU.TryGetValue(source, out var newU))
                for (int i = 0; i < newU.Length && i < uvs.Length; i++)
                    if (!float.IsNaN(newU[i])) uvs[i] = new MeshUV0 { m_Uv = new float2(newU[i], uvs[i].m_Uv.y) };
        }


        private void DumpRamp(NetCompositionPiece piece, NetCompositionMeshData data, EdgeNodeGeometry geometry,
            NetCompositionData compositionData, MeshVertex[] shaped, int[] map, float cut, bool start)
        {
            try
            {
                string name = m_Prefabs.TryGetPrefab<PrefabBase>(piece.m_Piece, out var prefab) && prefab != null ? prefab.name : "?";
                string key = name + "|" + piece.m_SectionFlags + "|" + data.m_Flags.m_General;
                if (m_DumpedRamps.Count >= 8 || !m_DumpedRamps.Add(key)) return;
                var positions = EntityManager.GetBuffer<MeshVertex>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray();
                var uvs = EntityManager.GetBuffer<MeshUV0>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray();
                var indices = EntityManager.GetBuffer<MeshIndex>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray();
                var slots = EntityManager.GetBuffer<MeshMaterial>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray();
                var text = new System.Text.StringBuilder();
                text.AppendLine("=== " + name + " | section " + piece.m_SectionFlags + " | flags " + piece.m_PieceFlags +
                    " | offset " + piece.m_Offset + " size " + piece.m_Size + " | mesh " + data.m_Flags.m_General +
                    " | " + (start ? "start" : "end") + " | cut z" + cut.ToString("0.##") + " | " + (shaped != null ? "shortened" : "kept") +
                    " | curb end: " + s_CurbEndWhy + " " + s_CurbEndNumbers);
                text.AppendLine("i; template x;y;z; u px; v; -> shaped x;y; from; world x;y;z (shaped)");
                for (int i = 0; i < positions.Length; i++)
                {
                    var t = positions[i].m_Vertex;
                    var sh = shaped != null ? shaped[i].m_Vertex : t;
                    var w = ProjectToWorld(sh, piece, data, geometry, compositionData);
                    text.AppendLine(i + "; " + t.x.ToString("0.###") + ";" + t.y.ToString("0.###") + ";" + t.z.ToString("0.##") + "; " +
                        (uvs.Length > i ? (uvs[i].m_Uv.x * 1024f).ToString("0.#") + "; " + uvs[i].m_Uv.y.ToString("0.###") : "-; -") + "; -> " +
                        sh.x.ToString("0.###") + ";" + sh.y.ToString("0.###") + "; " + (map != null ? map[i].ToString() : "-") + "; " +
                        w.x.ToString("0.###") + ";" + w.y.ToString("0.###") + ";" + w.z.ToString("0.###"));
                }
                foreach (var slot in slots)
                {
                    text.AppendLine("slot material " + slot.m_MaterialIndex + " indices " + slot.m_StartIndex + "+" + slot.m_IndexCount);
                    var line = new System.Text.StringBuilder();
                    for (int i = slot.m_StartIndex; i + 2 < slot.m_StartIndex + slot.m_IndexCount && i + 2 < indices.Length; i += 3)
                        line.Append(indices[i].m_Index).Append(',').Append(indices[i + 1].m_Index).Append(',').Append(indices[i + 2].m_Index).Append(' ');
                    text.AppendLine(line.ToString());
                }
                string folder = System.IO.Path.Combine(Application.persistentDataPath, "ModsData", "Enhanced_Graphics_and_Textures", "Diagnostics");
                System.IO.Directory.CreateDirectory(folder);
                string file = System.IO.Path.Combine(folder, "JunctionRamps.txt");
                if (m_DumpedRamps.Count == 1) System.IO.File.WriteAllText(file, text.ToString());
                else System.IO.File.AppendAllText(file, text.ToString());
                if (m_DumpedRamps.Count == 1) Mod.log.Info("Junction ramp geometry written to " + file);
            }
            catch (Exception ex)
            {
                Mod.log.Warn("Junction ramp dump failed: " + ex.Message);
            }
        }

        // Atlas V per metre of the piece (game: sum |dV| / sum |dz| over its triangles), node half only.
        private static float RampVDensity(MeshVertex[] positions, MeshUV0[] uvs, MeshIndex[] indices)
        {
            float sumV = 0f, sumZ = 0f;
            for (int t = 0; t + 2 < indices.Length; t += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    int a = indices[t + k].m_Index, b = indices[t + (k + 1) % 3].m_Index;
                    if (a < 0 || b < 0 || a >= positions.Length || b >= positions.Length || a >= uvs.Length || b >= uvs.Length) continue;
                    if (positions[a].m_Vertex.z < -0.001f || positions[b].m_Vertex.z < -0.001f) continue;
                    sumV += math.abs(uvs[a].m_Uv.y - uvs[b].m_Uv.y);
                    sumZ += math.abs(positions[a].m_Vertex.z - positions[b].m_Vertex.z);
                }
            }
            return sumZ > 0.0001f ? sumV / sumZ : 0.03125f;
        }

        private int m_WorldDumps;
        private void DumpWorld(List<NetCompositionPiece> pieces, NetCompositionMeshData data, EdgeNodeGeometry geometry,
            NetCompositionData compositionData, List<SourceFrame> frames, bool side, bool start)
        {
            if (m_WorldDumps >= 40) return;
            try
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                var text = new System.Text.StringBuilder();
                text.AppendLine("MESH " + m_WorldDumps + " " + (side ? "side" : "main") + " " + (start ? "start" : "end") + " " + data.m_Flags.m_General);
                if (frames != null)
                    foreach (var f in frames)
                        if (f.Frame != null)
                            text.AppendLine(string.Format(inv, "F {0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10}",
                                f.Frame.Origin.x, f.Frame.Origin.y, f.Frame.Uv.x, f.Frame.Uv.y, f.Frame.UvX.x, f.Frame.UvX.y,
                                f.Frame.UvZ.x, f.Frame.UvZ.y, f.MaxU, f.Centroid.x, f.Centroid.y));
                for (int k = 0; k < pieces.Count; k++)
                {
                    var piece = pieces[k];
                    if (!EntityManager.HasBuffer<MeshVertex>(piece.m_Piece)) continue;
                    var positions = EntityManager.GetBuffer<MeshVertex>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray();
                    var uvs = EntityManager.GetBuffer<MeshUV0>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray();
                    var indices = EntityManager.GetBuffer<MeshIndex>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray();
                    text.AppendLine("P " + k + " " + piece.m_PieceFlags.ToString().Replace(" ", "") + " " + piece.m_SectionFlags.ToString().Replace(" ", "") +
                        string.Format(inv, " {0} {1}", piece.m_Offset.x, piece.m_Size.x));
                    var world = new float3[positions.Length];
                    for (int i = 0; i < positions.Length; i++) world[i] = ProjectToWorld(positions[i].m_Vertex, piece, data, geometry, compositionData);
                    for (int t = 0; t + 2 < indices.Length; t += 3)
                    {
                        text.Append('T');
                        for (int n = 0; n < 3; n++)
                        {
                            int v = indices[t + n].m_Index;
                            if (v < 0 || v >= positions.Length) continue;
                            var tp = positions[v].m_Vertex;
                            text.Append(string.Format(inv, " {0:0.###} {1:0.###} {2:0.###} {3:0.#} {4:0.###} {5:0.##}",
                                world[v].x, world[v].y, world[v].z, v < uvs.Length ? uvs[v].m_Uv.x * 1024f : -1f, tp.x, tp.z));
                        }
                        text.AppendLine();
                    }
                }
                string folder = System.IO.Path.Combine(Application.persistentDataPath, "ModsData", "Enhanced_Graphics_and_Textures", "Diagnostics");
                System.IO.Directory.CreateDirectory(folder);
                string file = System.IO.Path.Combine(folder, "JunctionWorld.txt");
                if (m_WorldDumps == 0) System.IO.File.WriteAllText(file, text.ToString());
                else System.IO.File.AppendAllText(file, text.ToString());
                m_WorldDumps++;
            }
            catch (Exception ex)
            {
                Mod.log.Warn("Junction world dump failed: " + ex.Message);
            }
        }

        internal void WriteSidewalkScaleReport()
        {
            try
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                var text = new System.Text.StringBuilder();
                text.AppendLine("road | part | piece | section | offset.x | size.x | slot material: top faces, u px min..max, metres per 100 px, V per metre");
                var seen = new HashSet<Entity>();
                var roads = m_Roads.ToEntityArray(Allocator.Temp);
                try
                {
                    foreach (var road in roads)
                    {
                        string roadName = m_Prefabs.TryGetPrefab<PrefabBase>(EntityManager.GetComponentData<PrefabRef>(road).m_Prefab, out var roadPrefab) && roadPrefab != null
                            ? roadPrefab.name : "?";
                        var composition = EntityManager.GetComponentData<Composition>(road);
                        foreach (var (part, entity) in new[] { ("edge", composition.m_Edge), ("start", composition.m_StartNode), ("end", composition.m_EndNode) })
                        {
                            if (entity == Entity.Null || !EntityManager.HasComponent<NetCompositionMeshRef>(entity)) continue;
                            var mesh = EntityManager.GetComponentData<NetCompositionMeshRef>(entity).m_Mesh;
                            if (mesh == Entity.Null || !seen.Add(mesh) || !EntityManager.HasBuffer<NetCompositionPiece>(mesh)) continue;
                            var meshData = EntityManager.GetComponentData<NetCompositionMeshData>(mesh);
                            text.AppendLine("== " + roadName + " | " + part + " | width " + meshData.m_Width.ToString("0.##", inv) + " | " + meshData.m_Flags.m_General);
                            foreach (var piece in EntityManager.GetBuffer<NetCompositionPiece>(mesh, true).ToNativeArray(Allocator.Temp))
                            {
                                if (!PieceLoaded(piece.m_Piece)) continue;
                                string name = m_Prefabs.TryGetPrefab<PrefabBase>(piece.m_Piece, out var pp) && pp != null ? pp.name : "?";
                                var positions = EntityManager.GetBuffer<MeshVertex>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray();
                                var uvs = EntityManager.GetBuffer<MeshUV0>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray();
                                var indices = EntityManager.GetBuffer<MeshIndex>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray();
                                var line = new System.Text.StringBuilder(string.Format(inv, "  {0} | {1} | {2:0.##} | {3:0.##}", name, piece.m_SectionFlags, piece.m_Offset.x, piece.m_Size.x));
                                foreach (var slot in EntityManager.GetBuffer<MeshMaterial>(piece.m_Piece, true).ToNativeArray(Allocator.Temp))
                                {
                                    int top = 0;
                                    float u0 = float.MaxValue, u1 = float.MinValue, x0 = float.MaxValue, x1 = float.MinValue, sumV = 0f, sumZ = 0f;
                                    for (int i = slot.m_StartIndex; i + 2 < slot.m_StartIndex + slot.m_IndexCount && i + 2 < indices.Length; i += 3)
                                    {
                                        int a = indices[i].m_Index, b = indices[i + 1].m_Index, c = indices[i + 2].m_Index;
                                        if (a < 0 || b < 0 || c < 0 || a >= positions.Length || b >= positions.Length || c >= positions.Length) continue;
                                        var n = math.cross(positions[b].m_Vertex - positions[a].m_Vertex, positions[c].m_Vertex - positions[a].m_Vertex);
                                        float l = math.length(n);
                                        if (l < 1e-6f || math.abs(n.y) / l < 0.65f) continue;
                                        top++;
                                        foreach (int v in new[] { a, b, c })
                                        {
                                            u0 = math.min(u0, uvs[v].m_Uv.x); u1 = math.max(u1, uvs[v].m_Uv.x);
                                            x0 = math.min(x0, positions[v].m_Vertex.x); x1 = math.max(x1, positions[v].m_Vertex.x);
                                        }
                                        foreach (var (p, q) in new[] { (a, b), (b, c), (c, a) })
                                        {
                                            sumV += math.abs(uvs[p].m_Uv.y - uvs[q].m_Uv.y);
                                            sumZ += math.abs(positions[p].m_Vertex.z - positions[q].m_Vertex.z);
                                        }
                                    }
                                    line.Append(string.Format(inv, " | mat {0}: {1} top", slot.m_MaterialIndex, top));
                                    if (top > 0)
                                        line.Append(string.Format(inv, ", u {0:0}..{1:0} px, x {2:0.##}..{3:0.##} m, {4:0.###} m/100px, V/m {5:0.####}",
                                            u0 * 1024f, u1 * 1024f, x0, x1, u1 > u0 ? (x1 - x0) / ((u1 - u0) * 1024f) * 100f : 0f, sumZ > 0f ? sumV / sumZ : 0f));
                                }
                                text.AppendLine(line.ToString());
                            }
                        }
                    }
                }
                finally { roads.Dispose(); }
                string folder = System.IO.Path.Combine(Application.persistentDataPath, "ModsData", "Enhanced_Graphics_and_Textures", "Diagnostics");
                System.IO.Directory.CreateDirectory(folder);
                string file = System.IO.Path.Combine(folder, "SidewalkScale.txt");
                System.IO.File.WriteAllText(file, text.ToString());
                Mod.log.Info("Sidewalk scale report written to " + file);
            }
            catch (Exception ex) { Mod.log.Warn("Sidewalk scale report failed: " + ex.Message); }
        }

        // Rows of a ramp piece along template z (rounded to 1 cm).
        private static SortedDictionary<float, List<int>> RampRows(MeshVertex[] positions)
        {
            var rows = new SortedDictionary<float, List<int>>();
            for (int i = 0; i < positions.Length; i++)
            {
                float row = math.round(positions[i].m_Vertex.z * 100f) / 100f;
                if (!rows.TryGetValue(row, out var list)) rows[row] = list = new List<int>();
                list.Add(i);
            }
            return rows;
        }


        private static float RampCut(MeshVertex[] positions)
        {
            var rows = RampRows(positions);
            int count = -1;
            float cut = 0f;
            foreach (var pair in rows)
            {
                if (pair.Key <= 0.001f) continue;
                if (count < 0) count = pair.Value.Count;
                if (pair.Value.Count != count) break;
                cut = pair.Key;
            }
            return cut;
        }


        private static MeshVertex[] ShortenCurbRamp(MeshVertex[] positions, MeshUV0[] uvs, MeshIndex[] indices, float cut,
            out int[] map, out float[] newU, out CurbEndResult extra, Func<float3, float3> toWorld, Func<float3, float> frameU,
            bool straightEnd)
        {
            map = null;
            newU = null;
            extra = null;
            if (positions.Length == 0 || uvs.Length != positions.Length || cut <= 0.001f) return null;
            var rows = RampRows(positions);
            if (!rows.TryGetValue(0f, out var zero)) return null;
            Comparison<int> order = (x, y) =>
            {
                int c = uvs[x].m_Uv.x.CompareTo(uvs[y].m_Uv.x);
                if (c == 0) c = positions[x].m_Vertex.y.CompareTo(positions[y].m_Vertex.y);
                if (c == 0) c = positions[x].m_Vertex.x.CompareTo(positions[y].m_Vertex.x);
                return c;
            };
            // Row 0 holds the seam of both template halves twice; keep one vertex per position/column.
            var reference = new List<int>();
            foreach (int i in zero)
            {
                bool duplicate = false;
                foreach (int j in reference)
                    if (math.distancesq(positions[i].m_Vertex, positions[j].m_Vertex) < 0.000001f &&
                        math.abs(uvs[i].m_Uv.x - uvs[j].m_Uv.x) < 0.0001f) { duplicate = true; break; }
                if (!duplicate) reference.Add(i);
            }
            reference.Sort(order);
            var result = (MeshVertex[])positions.Clone();
            var sourceOf = new int[positions.Length];
            for (int i = 0; i < sourceOf.Length; i++) sourceOf[i] = -1;
            bool changed = false;

            float endRow = float.NaN;
            foreach (var key in rows.Keys) if (key > cut + 0.001f) { endRow = key; break; }
            float bound = cut;
            foreach (var pair in rows)
            {
                if (pair.Key <= 0.001f || pair.Key > bound + 0.001f) continue;
                var list = pair.Value;
                if (list.Count != reference.Count) return null;
                list.Sort(order);
                for (int k = 0; k < list.Count; k++)
                {
                    var source = positions[reference[k]].m_Vertex;
                    var current = positions[list[k]].m_Vertex;
                    if (math.distancesq(source.xy, current.xy) > 0.000001f) changed = true;
                    result[list[k]] = new MeshVertex { m_Vertex = new float3(source.x, source.y, current.z) };
                    sourceOf[list[k]] = reference[k];
                }
            }
            var u = new float[positions.Length];
            for (int i = 0; i < u.Length; i++) u[i] = float.NaN;
            if (FixRampWidths(positions, uvs, rows, reference, cut, result, sourceOf, u, toWorld, out var lateral)) changed = true;
            s_CurbEndWhy = !lateral.Valid ? "lateral columns not found" : float.IsNaN(endRow) ? "no end row" : "not tried";
            s_CurbEndNumbers = "";
            if (BuildRoundedCurbEnds && lateral.Valid && !float.IsNaN(endRow) && bound < endRow &&
                BuildCurbEnd(positions, uvs, indices, rows, sourceOf, cut, endRow, lateral, result, RampVDensity(positions, uvs, indices),
                    toWorld, frameU, out extra)) changed = true;
            if (straightEnd && !BuildRoundedCurbEnds && lateral.Valid && !float.IsNaN(endRow) &&
                (BuildRoundedEnd(positions, uvs, rows, sourceOf, cut, endRow, lateral, result, RampVDensity(positions, uvs, indices), toWorld, out extra) ||
                 BuildStraightEnd(positions, uvs, rows, sourceOf, cut, endRow, lateral, result, RampVDensity(positions, uvs, indices), toWorld, out extra)))
                changed = true;
            if (!changed) return null;
            map = sourceOf;
            newU = u;
            return result;
        }

        private static bool FixRampWidths(MeshVertex[] positions, MeshUV0[] uvs, SortedDictionary<float, List<int>> rows,
            List<int> reference, float cut, MeshVertex[] result, int[] sourceOf, float[] newU, Func<float3, float3> toWorld,
            out RampLateral lateral)
        {
            lateral = default;
            if (toWorld == null) return false;
            Func<float, bool> isCurb = x => x >= CurbLeftUv - UvEpsilon && x < LeftUv - UvEpsilon;
            Func<float, bool> isWalk = x => x >= LeftUv - UvEpsilon && x <= RightUv + UvEpsilon;
            var curbX = new List<float>();
            var walkX = new List<float>();
            float uWalkInner = float.NaN, uWalkOuter = float.NaN;
            foreach (int i in reference)
            {
                var p = positions[i].m_Vertex;
                if (p.y < -0.01f) continue;
                float uu = uvs[i].m_Uv.x;
                if (isCurb(uu) && !curbX.Exists(x => math.abs(x - p.x) < 0.002f)) curbX.Add(p.x);
                if (isWalk(uu) && !walkX.Exists(x => math.abs(x - p.x) < 0.002f)) walkX.Add(p.x);
            }
            if (curbX.Count != 2 || walkX.Count != 2) return false;
            float xi = float.NaN, xr = float.NaN, xo = float.NaN;
            for (int a = 0; a < 2; a++)
                for (int b = 0; b < 2; b++)
                    if (math.abs(curbX[a] - walkX[b]) < 0.002f) { xi = curbX[a]; xr = curbX[1 - a]; xo = walkX[1 - b]; }
            if (float.IsNaN(xi)) return false;
            foreach (int i in reference)
            {
                var p = positions[i].m_Vertex;
                if (p.y < -0.01f || !isWalk(uvs[i].m_Uv.x)) continue;
                if (math.abs(p.x - xi) < 0.002f) uWalkInner = uvs[i].m_Uv.x;
                if (math.abs(p.x - xo) < 0.002f) uWalkOuter = uvs[i].m_Uv.x;
            }
            float curbWidth = math.abs(xr - xi), walkWidth = math.abs(xo - xi);
            if (float.IsNaN(uWalkInner) || float.IsNaN(uWalkOuter) || curbWidth < 0.02f || walkWidth < 0.05f) return false;
            float density = (uWalkOuter - uWalkInner) / walkWidth; // atlas U per template metre, signed
            bool changed = false;
            lateral = new RampLateral { Valid = true, Xi = xi, Xr = xr, Xo = xo };
            foreach (int i in reference)
            {
                var p = positions[i].m_Vertex;
                if (p.y < -0.01f || !isCurb(uvs[i].m_Uv.x)) continue;
                if (math.abs(p.x - xi) < 0.002f) lateral.UCurbInner = uvs[i].m_Uv.x;
            }

            float bestDistance = -1f;
            foreach (int i in reference)
            {
                var p = positions[i].m_Vertex;
                if (p.y < -0.01f || !isCurb(uvs[i].m_Uv.x) || math.abs(p.x - xr) > 0.002f) continue;
                float d = math.abs(uvs[i].m_Uv.x - lateral.UCurbInner);
                if (d > bestDistance) { bestDistance = d; lateral.UCurbOuter = uvs[i].m_Uv.x; }
            }
            if (lateral.UCurbInner == 0f || lateral.UCurbOuter == 0f) lateral.Valid = false;

            float lo = math.min(xo, xr) + 0.002f, hi = math.max(xo, xr) - 0.002f;
            foreach (var pair in rows)
            {
                if (pair.Key < -0.001f) continue;
                float z = pair.Key;
                float scale = math.distance(toWorld(new float3(xr, 0f, z)), toWorld(new float3(xi, 0f, z))) / curbWidth;
                if (scale < 1.05f) continue;
                foreach (int i in pair.Value)
                {
                    var p = result[i].m_Vertex;
                    if (p.x <= lo || p.x >= hi) continue;
                    result[i] = new MeshVertex { m_Vertex = new float3(xr + (p.x - xr) / scale, p.y, p.z) };
                    changed = true;
                }
            }

            foreach (var pair in rows)
            {
                if (pair.Key < -0.001f) continue;
                var walk = new List<int>();
                foreach (int i in pair.Value)
                {
                    int from = sourceOf[i] >= 0 ? sourceOf[i] : i;
                    if (isWalk(uvs[from].m_Uv.x)) walk.Add(i);
                }
                foreach (int o in walk)
                {
                    int fromO = sourceOf[o] >= 0 ? sourceOf[o] : o;
                    if (math.abs(uvs[fromO].m_Uv.x - uWalkInner) < 0.0005f) continue;
                    // Inner partner: same height, inner sidewalk column.
                    int partner = -1;
                    float best = float.MaxValue;
                    foreach (int i in walk)
                    {
                        int fromI = sourceOf[i] >= 0 ? sourceOf[i] : i;
                        if (math.abs(uvs[fromI].m_Uv.x - uWalkInner) >= 0.0005f) continue;
                        float dy = math.abs(result[i].m_Vertex.y - result[o].m_Vertex.y);
                        if (dy < best) { best = dy; partner = i; }
                    }
                    if (partner < 0 || best > 0.01f) continue;
                    float world = math.distance(toWorld(result[o].m_Vertex), toWorld(result[partner].m_Vertex));
                    float template = math.abs(result[o].m_Vertex.x - result[partner].m_Vertex.x);
                    if (template < 0.05f || world < template * 1.05f) continue;
                    float value = uWalkInner + math.sign(density) * math.abs(density) * world;
                    newU[o] = math.clamp(value, LeftUv + 0.001f, RightUv - 0.001f);
                    changed = true;
                }
            }
            return changed;
        }

        private struct RampLateral
        {
            public bool Valid;
            public float Xi, Xr, Xo, UCurbInner, UCurbOuter;
        }

        private sealed class CurbEndResult
        {
            public RampExtra Curb, Fill;
            public float ClampZ;
        }

        // U of the nearest main road sidewalk frame at a world point (its straight continuation).
        private static Func<float3, float> PieceFrameU(List<SourceFrame> frames, NetCompositionPiece piece,
            NetCompositionMeshData data, EdgeNodeGeometry geometry, NetCompositionData compositionData)
        {
            if (frames == null || frames.Count == 0) return null;
            var frame = NearestFrame(frames, ProjectToWorld(new float3(0f, 0f, 0f), piece, data, geometry, compositionData).xz);
            if (frame == null) return null;
            return w => frame.Sample(w).x;
        }

        // Template (x, z) of a world point (Newton on the node deformation).
        private static float2 InverseTemplate(Func<float3, float3> toWorld, float2 target, float2 guess)
        {
            var t = guess;
            for (int it = 0; it < 25; it++)
            {
                var w = toWorld(new float3(t.x, 0f, t.y)).xz;
                var f = w - target;
                if (math.lengthsq(f) < 1e-10f) break;
                const float h = 0.01f;
                var jx = (toWorld(new float3(t.x + h, 0f, t.y)).xz - w) / h;
                var jz = (toWorld(new float3(t.x, 0f, t.y + h)).xz - w) / h;
                float det = jx.x * jz.y - jz.x * jx.y;
                if (math.abs(det) < 1e-9f) break;
                t += new float2((-f.x * jz.y + jz.x * f.y) / det, (-jx.x * f.y + f.x * jx.y) / det);
            }
            return t;
        }

        // z on template column x where frame U reaches target (bisection in [z0, z1]); NaN if no crossing.
        private static float CrossZ(Func<float3, float3> toWorld, Func<float3, float> frameU, float x, float z0, float z1, float target)
        {
            float g0 = frameU(toWorld(new float3(x, 0f, z0))) - target;
            float g1 = frameU(toWorld(new float3(x, 0f, z1))) - target;
            if (g0 * g1 > 0f) return float.NaN;
            for (int i = 0; i < 40; i++)
            {
                float zm = (z0 + z1) * 0.5f;
                float gm = frameU(toWorld(new float3(x, 0f, zm))) - target;
                if (g0 * gm <= 0f) { z1 = zm; } else { z0 = zm; g0 = gm; }
            }
            return (z0 + z1) * 0.5f;
        }

        private static bool BuildCurbEnd(MeshVertex[] positions, MeshUV0[] uvs, MeshIndex[] indices,
            SortedDictionary<float, List<int>> rows, int[] sourceOf, float cut, float endRow,
            RampLateral lateral, MeshVertex[] result, float density,
            Func<float3, float3> toWorld, Func<float3, float> frameU, out CurbEndResult end)
        {
            end = null;
            s_CurbEndWhy = "ok";
            s_CurbEndNumbers = "";
            if (frameU == null) { s_CurbEndWhy = "no frame"; return false; }
            Func<float, bool> isCurb = x => x >= CurbLeftUv - UvEpsilon && x < LeftUv - UvEpsilon;
            Func<float, bool> isWalk = x => x >= LeftUv - UvEpsilon && x <= RightUv + UvEpsilon;
            if (!rows.TryGetValue(cut, out var cutRow) || !rows.TryGetValue(endRow, out var endList)) { s_CurbEndWhy = "rows missing"; return false; }
            float xr = lateral.Xr, xo = lateral.Xo, xi = lateral.Xi;
            const float curb = 0.2f;
            float yRoad = 0f;
            foreach (var v in positions) yRoad = math.min(yRoad, v.m_Vertex.y);
            yRoad = math.max(yRoad, -0.2f);
            if (yRoad > -0.05f) { s_CurbEndWhy = "no road level"; return false; }
            Func<float, float> innerX = z =>
            {
                float scale = math.distance(toWorld(new float3(xr, 0f, z)), toWorld(new float3(xi, 0f, z))) / math.abs(xi - xr);
                return scale > 0.0001f ? xr + (xi - xr) / scale : xi;
            };
            Func<float, float, float2> W = (x, z) => toWorld(new float3(x, 0f, z)).xz;
            Func<float2, float> U = w => frameU(new float3(w.x, 0f, w.y));

            // Main road outer sidewalk edge: U = edge, the edge value beyond which U leaves the sidewalk.
            float u0 = U(W(xr, 0f)), uc = U(W(xr, cut));
            float uEdge = uc > u0 ? RightUv : LeftUv;
            float zc = CrossZ(toWorld, frameU, xr, 0f, cut, uEdge);
            s_CurbEndNumbers = "U(z0)=" + (u0 * 1024f).ToString("0.#") + " U(cut)=" + (uc * 1024f).ToString("0.#") +
                " U(end)=" + (U(W(xr, endRow)) * 1024f).ToString("0.#") + " U(outer z0)=" + (U(W(xo, 0f)) * 1024f).ToString("0.#") +
                " U(outer end)=" + (U(W(xo, endRow)) * 1024f).ToString("0.#") + " edge=" + (uEdge * 1024f).ToString("0.#") + " zc=" + zc.ToString("0.##");
            if (float.IsNaN(zc) || zc < 0.05f) { s_CurbEndWhy = "edge not crossed on curb line"; return false; }
            float2 C = W(xr, zc);
            float2 grad = new float2(U(C + new float2(0.05f, 0f)) - U(C), U(C + new float2(0f, 0.05f)) - U(C)) / 0.05f;
            if (math.lengthsq(grad) < 1e-10f) { s_CurbEndWhy = "no gradient"; return false; }
            float2 r1 = math.normalizesafe(W(xr, math.max(0f, zc - 1f)) - C);
            float2 r2 = math.normalizesafe(new float2(-grad.y, grad.x));
            if (math.dot(r2, W(xo, zc) - C) < 0f) r2 = -r2;
            float zEo = CrossZ(toWorld, frameU, xo, 0f, endRow, uEdge);
            if (float.IsNaN(zEo)) { s_CurbEndWhy = "edge not crossed on outer column"; return false; }
            float2 Eo = W(xo, zEo);
            float theta = math.acos(math.clamp(math.dot(r1, r2), -1f, 1f));
            if (theta < math.radians(25f) || theta > math.radians(165f)) { s_CurbEndWhy = "bad angle"; return false; }
            float tanHalf = math.tan(theta * 0.5f);
            float lenLine = math.distance(C, W(xr, 0f));
            float lenEdge = math.distance(C, Eo);
            float d = math.min(1.0f / tanHalf, 0.8f * math.min(lenLine, lenEdge - 0.25f));
            if (d < 0.1f) { s_CurbEndWhy = "no room"; return false; }
            float radius = d * tanHalf;
            if (radius < curb + 0.05f) { s_CurbEndWhy = "radius too small"; return false; }
            float2 T1 = C + r1 * d, T2 = C + r2 * d;
            float2 O = C + math.normalizesafe(r1 + r2) * (radius / math.sin(theta * 0.5f));

            // Road edge path (arc + edge to the outer side) and inner edge (0.2 m inside).
            const int segments = 6;
            var road = new List<float2>();
            var inner = new List<float2>();
            float a1 = math.atan2(T1.y - O.y, T1.x - O.x), a2 = math.atan2(T2.y - O.y, T2.x - O.x);
            float delta = a2 - a1;
            while (delta > math.PI) delta -= 2f * math.PI;
            while (delta < -math.PI) delta += 2f * math.PI;
            for (int k = 0; k <= segments; k++)
            {
                float a = a1 + delta * k / segments;
                var dir = new float2(math.cos(a), math.sin(a));
                road.Add(O + dir * radius);
                inner.Add(O + dir * (radius - curb));
            }
            road.Add(Eo);
            float2 inward = math.normalizesafe(O - T2);
            float uInnerEdge = uEdge - math.sign(uc - u0) * curb * math.length(grad);
            float zEoIn = CrossZ(toWorld, frameU, xo, 0f, endRow, uInnerEdge);
            if (float.IsNaN(zEoIn)) { s_CurbEndWhy = "inner edge not crossed"; return false; }
            inner.Add(W(xo, zEoIn));

            // Back to the template. Path starts exactly on the game's curb columns.
            float zT1 = InverseTemplate(toWorld, T1, new float2(xr, zc - d)).y;
            var tRoad = new List<float2>();
            var tInner = new List<float2>();
            for (int k = 0; k < road.Count; k++)
            {
                var guess = new float2(math.lerp(xr, xo, (float)k / (road.Count - 1)), math.lerp(zT1, zEo, (float)k / (road.Count - 1)));
                tRoad.Add(InverseTemplate(toWorld, road[k], guess));
                tInner.Add(InverseTemplate(toWorld, inner[k], guess));
            }
            tRoad[0] = new float2(xr, zT1);
            tInner[0] = new float2(innerX(zT1), zT1);
            tRoad[tRoad.Count - 1] = new float2(xo, zEo);
            tInner[tInner.Count - 1] = new float2(xo, zEoIn);
            foreach (var t in tRoad) if (!math.all(math.isfinite(t))) { s_CurbEndWhy = "inverse failed"; return false; }
            foreach (var t in tInner) if (!math.all(math.isfinite(t))) { s_CurbEndWhy = "inverse failed"; return false; }
            var tCenter = InverseTemplate(toWorld, O, new float2((xr + xo) * 0.5f, zT1));

            // Clamp the game's sidewalk/curb rows to the new end.
            float xb = innerX(zT1);
            for (int i = 0; i < result.Length; i++)
            {
                var q = result[i].m_Vertex;
                if (q.z <= 0.001f || q.z > cut + 0.001f) continue;
                float uu = uvs[sourceOf[i] >= 0 ? sourceOf[i] : i].m_Uv.x;
                bool outerWalk = isWalk(uu) && math.abs(q.x - xo) < 0.002f;
                if (outerWalk)
                {
                    if (q.z > zEoIn) result[i] = new MeshVertex { m_Vertex = new float3(xo, q.y, zEoIn) };
                }
                else if ((isCurb(uu) || isWalk(uu)) && q.z > zT1)
                {
                    float x = math.abs(q.x - xr) < 0.002f ? xr : xb;
                    result[i] = new MeshVertex { m_Vertex = new float3(x, q.y, zT1) };
                }
            }

            float uIn = lateral.UCurbInner, uOut = lateral.UCurbOuter;
            Func<float2, float, float3> P3 = (t, y) => new float3(t.x, y, t.y);
            var curbPiece = new RampExtra();
            var fill = new RampExtra();

            Action<RampExtra, float3, float2, float3, float2, float3, float2, bool> add = (target, pa, ua, pb, ub, pc, uc2, top) =>
            {
                var wa = toWorld(pa); var wb = toWorld(pb); var wc = toWorld(pc);
                var wn = math.cross(wb - wa, wc - wa);
                bool flip = top ? wn.y < 0f : math.dot(wn.xz, ((wa + wb + wc) / 3f).xz - O) < 0f;
                if (flip) { var tp = pb; pb = pc; pc = tp; var tu = ub; ub = uc2; uc2 = tu; }
                var n = math.cross(pb - pa, pc - pa);
                var centre = (pa + pb + pc) / 3f;
                n = top ? new float3(0f, 1f, 0f)
                    : math.normalizesafe(new float3(centre.x - tCenter.x, 0f, centre.z - tCenter.y), new float3(1f, 0f, 0f));
                var tangent = top ? new float4(0f, 0f, 1f, 1f) : new float4(0f, 1f, 0f, 1f);
                foreach (var (pos, uv) in new[] { (pa, ua), (pb, ub), (pc, uc2) })
                {
                    target.Positions.Add(pos);
                    target.Normals.Add(n);
                    target.Tangents.Add(tangent);
                    target.Uvs.Add(uv);
                    target.Indices.Add(target.Positions.Count - 1);
                }
            };

            // Curb top and face along the path; V follows the path length in the world.
            float s = 0f;
            for (int k = 0; k + 1 < tRoad.Count; k++)
            {
                float s1 = s + math.distance(road[k], road[k + 1]);
                float v0 = -density * s, v1 = -density * s1;
                var ri = P3(tRoad[k], 0f); var rj = P3(tRoad[k + 1], 0f);
                var ii = P3(tInner[k], 0f); var ij = P3(tInner[k + 1], 0f);
                add(curbPiece, ii, new float2(uIn, v0), ri, new float2(uOut, v0), rj, new float2(uOut, v1), true);
                add(curbPiece, ii, new float2(uIn, v0), rj, new float2(uOut, v1), ij, new float2(uIn, v1), true);
                var bi = P3(tRoad[k], yRoad); var bj = P3(tRoad[k + 1], yRoad);
                add(curbPiece, ri, new float2(uIn, v0), rj, new float2(uIn, v1), bj, new float2(uOut, v1), false);
                add(curbPiece, ri, new float2(uIn, v0), bj, new float2(uOut, v1), bi, new float2(uOut, v0), false);
                s = s1;
            }

            float uWalk = LeftUv + 0.05f;
            for (int k = 1; k + 1 < tInner.Count; k++)
                add(fill, P3(tInner[0], 0f), new float2(uWalk, 0f), P3(tInner[k], 0f), new float2(uWalk, 0f),
                    P3(tInner[k + 1], 0f), new float2(uWalk, 0f), true);

            // Asphalt from the curb's road edge to the side road's own surface.
            float uRoad = float.NaN;
            var closing = new List<float2>();
            var endRoad = new List<int>();
            foreach (int i in endList)
            {
                var q = result[i].m_Vertex;
                if (uvs[i].m_Uv.x < CurbLeftUv - UvEpsilon && q.x > xo - 0.002f && q.x < xr - 0.002f && q.y < -0.05f) endRoad.Add(i);
            }
            endRoad.Sort((a, b) => result[a].m_Vertex.x.CompareTo(result[b].m_Vertex.x));
            if (endRoad.Count == 0 || math.abs(result[endRoad[0]].m_Vertex.x - xo) > 0.002f) closing.Add(new float2(xo, endRow));
            foreach (int i in endRoad)
            {
                closing.Add(new float2(result[i].m_Vertex.x, endRow));
                if (float.IsNaN(uRoad)) uRoad = uvs[i].m_Uv.x;
            }
            closing.Add(new float2(xr, cut));
            if (float.IsNaN(uRoad)) uRoad = 0.05f;
            var polygon = new List<float2>(tRoad);
            polygon.AddRange(closing);
            foreach (var tri in EarClip(polygon))
                add(fill, P3(tri.Item1, yRoad), new float2(uRoad, 0f), P3(tri.Item2, yRoad), new float2(uRoad, 0f),
                    P3(tri.Item3, yRoad), new float2(uRoad, 0f), true);

            end = new CurbEndResult { Curb = curbPiece, Fill = fill, ClampZ = zEo };
            return true;
        }


        private static bool BuildStraightEnd(MeshVertex[] positions, MeshUV0[] uvs, SortedDictionary<float, List<int>> rows,
            int[] sourceOf, float cut, float endRow, RampLateral lateral, MeshVertex[] result, float density,
            Func<float3, float3> toWorld, out CurbEndResult end)
        {
            end = null;
            if (!rows.TryGetValue(cut, out var cutRow) || !rows.TryGetValue(endRow, out var endList)) return false;
            float xr = lateral.Xr, xo = lateral.Xo;
            float lo = math.min(xr, xo) - 0.002f, hi = math.max(xr, xo) + 0.002f;
            float yRoad = 0f;
            foreach (var v in positions) yRoad = math.min(yRoad, v.m_Vertex.y);
            yRoad = math.max(yRoad, -0.2f);
            if (yRoad > -0.05f) return false;

            // Top edge of the end: distinct x of the full-height vertices of the cut row.
            var xs = new List<float>();
            foreach (int i in cutRow)
            {
                var q = result[i].m_Vertex;
                if (q.y < -0.01f || q.x < lo || q.x > hi) continue;
                if (!xs.Exists(x => math.abs(x - q.x) < 0.002f)) xs.Add(q.x);
            }
            if (xs.Count < 2) return false;
            xs.Sort((a, b) => math.abs(a - xr).CompareTo(math.abs(b - xr)));

            // Road surface of the end row moves onto the cut row (fills the old ramp area).
            foreach (int i in endList)
            {
                var q = result[i].m_Vertex;
                if (uvs[i].m_Uv.x < CurbLeftUv - UvEpsilon && q.x > lo && q.x < math.max(xr, xo) && q.x > math.min(xr, xo) - 0.002f &&
                    math.abs(q.x - xr) > 0.002f)
                    result[i] = new MeshVertex { m_Vertex = new float3(q.x, q.y, cut) };
            }

            float uIn = lateral.UCurbInner, uOut = lateral.UCurbOuter;
            var inside = toWorld(new float3((xr + xo) * 0.5f, 0f, math.max(0f, cut - 4f)));
            var piece = new RampExtra();
            float s = 0f;
            for (int k = 0; k + 1 < xs.Count; k++)
            {
                var a = new float3(xs[k], 0f, cut);
                var b = new float3(xs[k + 1], 0f, cut);
                var al = new float3(xs[k], yRoad, cut);
                var bl = new float3(xs[k + 1], yRoad, cut);
                float s1 = s + math.distance(toWorld(a).xz, toWorld(b).xz);
                var quad = new[] { (a, new float2(uIn, -density * s)), (b, new float2(uIn, -density * s1)),
                    (bl, new float2(uOut, -density * s1)), (al, new float2(uOut, -density * s)) };
                foreach (var tri in new[] { new[] { 0, 1, 2 }, new[] { 0, 2, 3 } })
                {
                    var p0 = quad[tri[0]]; var p1 = quad[tri[1]]; var p2 = quad[tri[2]];
                    var w0 = toWorld(p0.Item1); var w1 = toWorld(p1.Item1); var w2 = toWorld(p2.Item1);
                    var wn = math.cross(w1 - w0, w2 - w0);
                    if (math.dot(wn.xz, ((w0 + w1 + w2) / 3f - inside).xz) < 0f) { var t = p1; p1 = p2; p2 = t; }
                    foreach (var (pos, uv) in new[] { p0, p1, p2 })
                    {
                        piece.Positions.Add(pos);
                        piece.Normals.Add(new float3(0f, 0f, 1f));
                        piece.Tangents.Add(new float4(1f, 0f, 0f, 1f));
                        piece.Uvs.Add(uv);
                        piece.Indices.Add(piece.Positions.Count - 1);
                    }
                }
                s = s1;
            }
            end = new CurbEndResult { Curb = piece, Fill = new RampExtra(), ClampZ = cut };
            return true;
        }

        private static bool BuildRoundedEnd(MeshVertex[] positions, MeshUV0[] uvs, SortedDictionary<float, List<int>> rows,
            int[] sourceOf, float cut, float endRow, RampLateral lateral, MeshVertex[] result, float density,
            Func<float3, float3> toWorld, out CurbEndResult end)
        {
            end = null;
            Func<float, bool> isCurb = x => x >= CurbLeftUv - UvEpsilon && x < LeftUv - UvEpsilon;
            Func<float, bool> isWalk = x => x >= LeftUv - UvEpsilon && x <= RightUv + UvEpsilon;
            if (!rows.TryGetValue(cut, out var cutRow) || !rows.TryGetValue(endRow, out var endList) || cut < 1f) return false;
            float xr = lateral.Xr, xo = lateral.Xo, xi = lateral.Xi;
            const float curb = 0.2f;
            float yRoad = 0f;
            foreach (var v in positions) yRoad = math.min(yRoad, v.m_Vertex.y);
            yRoad = math.max(yRoad, -0.2f);
            if (yRoad > -0.05f) return false;
            Func<float, float> innerX = z =>
            {
                float scale = math.distance(toWorld(new float3(xr, 0f, z)), toWorld(new float3(xi, 0f, z))) / math.abs(xi - xr);
                return scale > 0.0001f ? xr + (xi - xr) / scale : xi;
            };
            Func<float, float, float2> W = (x, z) => toWorld(new float3(x, 0f, z)).xz;

            float2 C = W(xr, cut);
            float2 r1 = math.normalizesafe(W(xr, cut - 1f) - C);
            float2 Eo = W(xo, cut);
            float2 r2 = math.normalizesafe(Eo - C);
            float theta = math.acos(math.clamp(math.dot(r1, r2), -1f, 1f));
            if (theta < math.radians(30f) || theta > math.radians(160f)) return false;
            float tanHalf = math.tan(theta * 0.5f);
            float lenLine = math.distance(C, W(xr, 0f));
            float lenEdge = math.distance(C, Eo);
            float d = math.min(0.6f / tanHalf, math.min(0.6f * lenLine, 0.7f * (lenEdge - curb)));
            float radius = d * tanHalf;
            if (radius < curb + 0.05f) return false;
            float2 T1 = C + r1 * d, T2 = C + r2 * d;
            float2 O = C + math.normalizesafe(r1 + r2) * (radius / math.sin(theta * 0.5f));
            float2 inward = math.normalizesafe(O - T2);

            const int segments = 6;
            var road = new List<float2>();
            var inner = new List<float2>();
            float a1 = math.atan2(T1.y - O.y, T1.x - O.x), a2 = math.atan2(T2.y - O.y, T2.x - O.x);
            float delta = a2 - a1;
            while (delta > math.PI) delta -= 2f * math.PI;
            while (delta < -math.PI) delta += 2f * math.PI;
            for (int k = 0; k <= segments; k++)
            {
                float a = a1 + delta * k / segments;
                var dir = new float2(math.cos(a), math.sin(a));
                road.Add(O + dir * radius);
                inner.Add(O + dir * (radius - curb));
            }
            road.Add(Eo);
            inner.Add(Eo + inward * curb);

            float zT1 = InverseTemplate(toWorld, T1, new float2(xr, cut - 1f)).y;
            if (!math.isfinite(zT1) || zT1 <= 0.01f || zT1 >= cut) return false;
            var tRoad = new List<float2>();
            var tInner = new List<float2>();
            for (int k = 0; k < road.Count; k++)
            {
                float f = (float)k / (road.Count - 1);
                var guess = new float2(math.lerp(xr, xo, f), math.lerp(zT1, cut, f));
                tRoad.Add(InverseTemplate(toWorld, road[k], guess));
                tInner.Add(InverseTemplate(toWorld, inner[k], guess - new float2(0f, 0.5f)));
            }
            tRoad[0] = new float2(xr, zT1);
            tInner[0] = new float2(innerX(zT1), zT1);
            tRoad[tRoad.Count - 1] = new float2(xo, cut);
            float zEoIn = tInner[tInner.Count - 1].y;
            tInner[tInner.Count - 1] = new float2(xo, zEoIn);
            foreach (var t in tRoad) if (!math.all(math.isfinite(t))) return false;
            foreach (var t in tInner) if (!math.all(math.isfinite(t))) return false;
            if (zEoIn <= 0.01f || zEoIn >= cut) return false;
            var tCenter = InverseTemplate(toWorld, O, new float2((xr + xo) * 0.5f, zT1));

            // Road surface of the end row onto the cut row (fills the old ramp area).
            float lo = math.min(xr, xo) - 0.002f, hi = math.max(xr, xo) + 0.002f;
            foreach (int i in endList)
            {
                var q = result[i].m_Vertex;
                if (uvs[i].m_Uv.x < CurbLeftUv - UvEpsilon && q.x > lo && q.x < hi && math.abs(q.x - xr) > 0.002f)
                    result[i] = new MeshVertex { m_Vertex = new float3(q.x, q.y, cut) };
            }
            // The game's sidewalk/curb rows end at the new curb.
            float xb = innerX(zT1);
            for (int i = 0; i < result.Length; i++)
            {
                var q = result[i].m_Vertex;
                if (q.z <= 0.001f || q.z > cut + 0.001f) continue;
                float uu = uvs[sourceOf[i] >= 0 ? sourceOf[i] : i].m_Uv.x;
                if (isWalk(uu) && math.abs(q.x - xo) < 0.002f)
                {
                    if (q.z > zEoIn) result[i] = new MeshVertex { m_Vertex = new float3(xo, q.y, zEoIn) };
                }
                else if ((isCurb(uu) || isWalk(uu)) && q.z > zT1)
                {
                    float x = math.abs(q.x - xr) < 0.002f ? xr : xb;
                    result[i] = new MeshVertex { m_Vertex = new float3(x, q.y, zT1) };
                }
            }

            float uIn = lateral.UCurbInner, uOut = lateral.UCurbOuter;
            Func<float2, float, float3> P3 = (t, y) => new float3(t.x, y, t.y);
            var curbPiece = new RampExtra();
            var fill = new RampExtra();
            Action<RampExtra, float3, float2, float3, float2, float3, float2, bool> add = (target, pa, ua, pb, ub, pc, uc, top) =>
            {
                var wa = toWorld(pa); var wb = toWorld(pb); var wc = toWorld(pc);
                var wn = math.cross(wb - wa, wc - wa);
                bool flip = top ? wn.y < 0f : math.dot(wn.xz, ((wa + wb + wc) / 3f).xz - O) < 0f;
                if (flip) { var tp = pb; pb = pc; pc = tp; var tu = ub; ub = uc; uc = tu; }
                var centre = (pa + pb + pc) / 3f;
                var n = top ? new float3(0f, 1f, 0f)
                    : math.normalizesafe(new float3(centre.x - tCenter.x, 0f, centre.z - tCenter.y), new float3(1f, 0f, 0f));
                var along = math.normalizesafe(new float3(n.z, 0f, -n.x), new float3(0f, 0f, 1f));
                var tangent = top ? new float4(0f, 0f, 1f, 1f) : new float4(along, 1f);
                foreach (var (pos, uv) in new[] { (pa, ua), (pb, ub), (pc, uc) })
                {
                    target.Positions.Add(pos);
                    target.Normals.Add(n);
                    target.Tangents.Add(tangent);
                    target.Uvs.Add(uv);
                    target.Indices.Add(target.Positions.Count - 1);
                }
            };

            float s = 0f;
            for (int k = 0; k + 1 < tRoad.Count; k++)
            {
                float s1 = s + math.distance(road[k], road[k + 1]);
                float v0 = -density * s, v1 = -density * s1;
                var ri = P3(tRoad[k], 0f); var rj = P3(tRoad[k + 1], 0f);
                var ii = P3(tInner[k], 0f); var ij = P3(tInner[k + 1], 0f);
                add(curbPiece, ii, new float2(uIn, v0), ri, new float2(uOut, v0), rj, new float2(uOut, v1), true);
                add(curbPiece, ii, new float2(uIn, v0), rj, new float2(uOut, v1), ij, new float2(uIn, v1), true);
                var bi = P3(tRoad[k], yRoad); var bj = P3(tRoad[k + 1], yRoad);
                add(curbPiece, ri, new float2(uIn, v0), rj, new float2(uIn, v1), bj, new float2(uOut, v1), false);
                add(curbPiece, ri, new float2(uIn, v0), bj, new float2(uOut, v1), bi, new float2(uOut, v0), false);
                s = s1;
            }

            // Sidewalk between the clamped rows and the curb's inner edge (planar UVs later).
            float uWalk = LeftUv + 0.05f;
            for (int k = 1; k + 1 < tInner.Count; k++)
                add(fill, P3(tInner[0], 0f), new float2(uWalk, 0f), P3(tInner[k], 0f), new float2(uWalk, 0f),
                    P3(tInner[k + 1], 0f), new float2(uWalk, 0f), true);
            // Asphalt in the rounded-off corner (between the arc and the old corner).
            float uRoad = float.NaN;
            foreach (int i in endList) if (uvs[i].m_Uv.x < CurbLeftUv - UvEpsilon) { uRoad = uvs[i].m_Uv.x; break; }
            if (float.IsNaN(uRoad)) uRoad = 0.05f;
            var polygon = new List<float2>();
            for (int k = 0; k <= segments; k++) polygon.Add(tRoad[k]);
            polygon.Add(new float2(xr, cut));
            foreach (var tri in EarClip(polygon))
                add(fill, P3(tri.Item1, yRoad), new float2(uRoad, 0f), P3(tri.Item2, yRoad), new float2(uRoad, 0f),
                    P3(tri.Item3, yRoad), new float2(uRoad, 0f), true);

            end = new CurbEndResult { Curb = curbPiece, Fill = fill, ClampZ = cut };
            return true;
        }

        // Simple ear clipping of a simple polygon (template x/z).
        private static List<(float2, float2, float2)> EarClip(List<float2> polygon)
        {
            var result = new List<(float2, float2, float2)>();
            var points = new List<float2>();
            foreach (var p in polygon)
                if (points.Count == 0 || math.distancesq(points[points.Count - 1], p) > 1e-8f) points.Add(p);
            if (points.Count > 1 && math.distancesq(points[0], points[points.Count - 1]) < 1e-8f) points.RemoveAt(points.Count - 1);
            float area = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                var a = points[i]; var b = points[(i + 1) % points.Count];
                area += a.x * b.y - b.x * a.y;
            }
            float sign = math.sign(area);
            if (sign == 0f) return result;
            Func<float2, float2, float2, float> cross = (a, b, c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            int guard = 0;
            while (points.Count > 3 && guard++ < 500)
            {
                bool clipped = false;
                for (int i = 0; i < points.Count; i++)
                {
                    var a = points[(i + points.Count - 1) % points.Count];
                    var b = points[i];
                    var c = points[(i + 1) % points.Count];
                    if (cross(a, b, c) * sign <= 1e-9f) continue;
                    bool inside = false;
                    for (int j = 0; j < points.Count && !inside; j++)
                    {
                        var p = points[j];
                        if (math.all(p == a) || math.all(p == b) || math.all(p == c)) continue;
                        inside = cross(a, b, p) * sign > 0f && cross(b, c, p) * sign > 0f && cross(c, a, p) * sign > 0f;
                    }
                    if (inside) continue;
                    result.Add((a, b, c));
                    points.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (points.Count == 3) result.Add((points[0], points[1], points[2]));
            return result;
        }

        private void AppendRampFill(Entity source, ref MeshVertex[] positions, ref MeshNormal[] normals, ref MeshTangent[] tangents,
            ref MeshUV0[] uvs, ref MeshIndex[] indices, ref MeshMaterial[] slots)
        {
            if (m_ReshapeFill == null || !m_ReshapeFill.TryGetValue(source, out var extra) || slots.Length == 0 || extra.Positions.Count == 0) return;
            int count = positions.Length;
            if (normals.Length != count || tangents.Length != count || uvs.Length != count) return;
            int added = extra.Positions.Count;
            var p = new MeshVertex[count + added];
            var n = new MeshNormal[count + added];
            var t = new MeshTangent[count + added];
            var u = new MeshUV0[count + added];
            Array.Copy(positions, p, count);
            Array.Copy(normals, n, count);
            Array.Copy(tangents, t, count);
            Array.Copy(uvs, u, count);
            for (int i = 0; i < added; i++)
            {
                p[count + i] = new MeshVertex { m_Vertex = extra.Positions[i] };
                n[count + i] = new MeshNormal { m_Normal = extra.Normals[i] };
                t[count + i] = new MeshTangent { m_Tangent = extra.Tangents[i] };
                u[count + i] = new MeshUV0 { m_Uv = extra.Uvs[i] };
            }
            // Appended to the last slot (one material per ramp piece).
            var list = new List<MeshIndex>(indices);
            var last = slots[slots.Length - 1];
            int insertAt = math.min(last.m_StartIndex + last.m_IndexCount, list.Count);
            var insert = new List<MeshIndex>(extra.Indices.Count);
            foreach (int index in extra.Indices) insert.Add(new MeshIndex { m_Index = count + index });
            list.InsertRange(insertAt, insert);
            positions = p; normals = n; tangents = t; uvs = u;
            indices = list.ToArray();
            var newSlots = (MeshMaterial[])slots.Clone();
            newSlots[slots.Length - 1] = new MeshMaterial(last.m_StartIndex, last.m_IndexCount + insert.Count, last.m_StartVertex,
                math.max(last.m_VertexCount, count + added - last.m_StartVertex), last.m_MaterialIndex);
            slots = newSlots;
        }

        private static HashSet<int> RampDrop(MeshVertex[] positions, MeshUV0[] uvs, MeshIndex[] indices, float cut)
        {
            var rows = RampRows(positions);
            float endRow = float.NaN;
            foreach (var key in rows.Keys) if (key > cut + 0.001f) { endRow = key; break; }
            var drop = new HashSet<int>();
            if (float.IsNaN(endRow)) return drop;
            for (int t = 0; t + 2 < indices.Length; t += 3)
            {
                bool inside = true, beyond = false, sidewalk = false;
                for (int k = 0; k < 3; k++)
                {
                    int v = indices[t + k].m_Index;
                    if (v < 0 || v >= positions.Length) { inside = false; break; }
                    float z = positions[v].m_Vertex.z;
                    if (z < cut - 0.001f || z > endRow + 0.001f) inside = false;
                    if (z > cut + 0.001f) beyond = true;
                    if (v < uvs.Length && uvs[v].m_Uv.x >= CurbLeftUv - UvEpsilon) sidewalk = true;
                }
                if (inside && beyond && sidewalk) drop.Add(t);
            }
            return drop;
        }

        private void ApplyRampDrop(Entity source, ref MeshIndex[] indices, ref MeshMaterial[] slots)
        {
            if (m_ReshapeDrop == null || !m_ReshapeDrop.TryGetValue(source, out var drop) || drop.Count == 0) return;
            var list = new List<MeshIndex>(indices.Length);
            var newSlots = new MeshMaterial[slots.Length];
            for (int s = 0; s < slots.Length; s++)
            {
                var slot = slots[s];
                int begin = list.Count;
                for (int t = slot.m_StartIndex; t + 2 < slot.m_StartIndex + slot.m_IndexCount && t + 2 < indices.Length; t += 3)
                {
                    if (drop.Contains(t)) continue;
                    list.Add(indices[t]); list.Add(indices[t + 1]); list.Add(indices[t + 2]);
                }
                newSlots[s] = new MeshMaterial(begin, list.Count - begin, slot.m_StartVertex, slot.m_VertexCount, slot.m_MaterialIndex);
            }
            indices = list.ToArray();
            slots = newSlots;
        }

        // Curb faces added at the end of the ramp, as their own DisableTiling piece.
        private void AddCurbEndPieces(NetCompositionPiece[] pieces, List<NetCompositionPiece> result, Runtime runtime)
        {
            if (m_ReshapeExtra == null) return;
            foreach (var pair in m_ReshapeExtra)
            {
                NetCompositionPiece owner = default;
                bool found = false;
                foreach (var piece in pieces) if (piece.m_Piece == pair.Key) { owner = piece; found = true; break; }
                if (!found || !EntityManager.HasBuffer<MeshMaterial>(pair.Key)) continue;
                var sourceSlots = EntityManager.GetBuffer<MeshMaterial>(pair.Key, true);
                if (sourceSlots.Length == 0) continue;
                var extra = pair.Value;
                var uvs = new List<float2>(extra.Uvs);
                float minV = float.MaxValue, maxV = float.MinValue;
                foreach (var uv in uvs) minV = math.min(minV, uv.y);
                float shift = math.floor(minV);
                foreach (var uv in uvs) maxV = math.max(maxV, uv.y - shift);
                if (maxV - math.floor(maxV) < 0.0005f) shift += 0.0005f;
                for (int i = 0; i < uvs.Count; i++) uvs[i] = new float2(uvs[i].x, uvs[i].y - shift);
                var slots = new List<MeshMaterial> { new MeshMaterial(0, extra.Indices.Count, 0, extra.Positions.Count, sourceSlots[0].m_MaterialIndex) };
                var entity = CreatePiece(EntityManager.GetComponentData<PrefabData>(pair.Key), EntityManager.GetComponentData<MeshData>(pair.Key),
                    new List<float3>(extra.Positions), new List<float3>(extra.Normals), new List<float4>(extra.Tangents), uvs,
                    new List<int>(extra.Indices), slots);
                runtime.Created.Add(entity);
                var copy = owner;
                copy.m_Piece = entity;
                copy.m_PieceFlags |= NetPieceFlags.DisableTiling;
                result.Add(copy);
            }
        }

        // Height of a triangle over its longest side (its "width" for a long, thin strip).
        private static float Thickness(float2 a, float2 b, float2 c)
        {
            float2 ab = b - a, ac = c - a, bc = c - b;
            float area2 = math.abs(ab.x * ac.y - ab.y * ac.x);
            float longest = math.max(math.length(ab), math.max(math.length(ac), math.length(bc)));
            return longest > 0.0001f ? area2 / longest : 0f;
        }

        private static bool IsSidewalkTop(float3 a, float3 b, float3 c, float2 ua, float2 ub, float2 uc)
        {
            float u0 = math.min(ua.x, math.min(ub.x, uc.x));
            float u1 = math.max(ua.x, math.max(ub.x, uc.x));
            if (u0 < LeftUv - UvEpsilon || u1 > RightUv + UvEpsilon) return false;
            var n = math.cross(b - a, c - a);
            float length = math.length(n);
            return length > 0.000001f && math.abs(n.y) / length >= 0.65f;
        }

        private static bool IsRoadAtlasPiece(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.EndsWith(" Mesh", StringComparison.Ordinal)) return false;
            if (name.IndexOf("RoadParts", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (name.IndexOf("Parking", StringComparison.OrdinalIgnoreCase) >= 0 &&
                 name.IndexOf("Sidewalk", StringComparison.OrdinalIgnoreCase) < 0) ||
                name.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Gravel", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return name == "Road" || name.StartsWith("Road_", StringComparison.Ordinal) ||
                name.StartsWith("Road ", StringComparison.Ordinal) || name.StartsWith("RoadEU", StringComparison.Ordinal) ||
                name.IndexOf("Sidewalk", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static float TemplateVDensity(MeshVertex[] positions, MeshUV0[] uv, MeshIndex[] indices,
            int begin, int end, bool keepPositive, bool keepNegative)
        {
            float sumV = 0f, sumZ = 0f;
            for (int i = begin; i + 2 < end; i += 3)
            {
                int a = indices[i].m_Index, b = indices[i + 1].m_Index, c = indices[i + 2].m_Index;
                if (a < 0 || b < 0 || c < 0 || a >= positions.Length || b >= positions.Length || c >= positions.Length) continue;
                float za = positions[a].m_Vertex.z, zb = positions[b].m_Vertex.z, zc = positions[c].m_Vertex.z;
                if ((keepPositive && (za < -0.01f || zb < -0.01f || zc < -0.01f)) ||
                    (keepNegative && (za > 0.01f || zb > 0.01f || zc > 0.01f))) continue;
                float va = uv[a].m_Uv.y, vb = uv[b].m_Uv.y, vc = uv[c].m_Uv.y;
                if (va < 0f || vb < 0f || vc < 0f) continue;
                sumV += math.abs(vb - va) + math.abs(vc - vb) + math.abs(va - vc);
                sumZ += math.abs(zb - za) + math.abs(zc - zb) + math.abs(za - zc);
            }
            return sumZ > 0.000001f && sumV > 0.000001f ? sumV / sumZ : float.NaN;
        }

        private static Frame MakeFrame(float3 pa, float3 pb, float3 pc, float2 ua, float2 ub, float2 uc,
            NetCompositionPiece piece, NetCompositionMeshData data, EdgeNodeGeometry geometry,
            NetCompositionData compositionData, float density, bool startNode, EdgeGeometry edgeGeometry)
        {
            var ab = pb - pa;
            var ac = pc - pa;
            float determinant = ab.x * ac.z - ac.x * ab.z;
            if (math.abs(determinant) < 0.00001f) return null;
            var du = ub - ua;
            var dv = uc - ua;
            var uvDx = (du * ac.z - dv * ab.z) / determinant;
            var uvDz = (dv * ab.x - du * ac.x) / determinant;
            int flags = (int)piece.m_SectionFlags;
            float direction = (flags & 0x100) != 0 ? 1f : -1f;
            bool half = (flags & 0x4000) != 0;
            var origin = (pa + pb + pc) / 3f;
            // Template z of the straight seam (node t = 0).
            origin.z = (-piece.m_Size.z * 0.5f - piece.m_Offset.z - (half ? piece.m_Size.z * 0.5f : 0f)) /
                (direction * (half ? 2f : 1f));
            var world = ProjectToWorld(origin, piece, data, geometry, compositionData);
            bool right = (piece.m_SectionFlags & NetSectionFlags.Right) != 0;
            float signX = (flags & 1) != 0 ? 1f : -1f;
            var seamSpan = right
                ? geometry.m_Right.m_Right.a.xz - geometry.m_Right.m_Left.a.xz
                : geometry.m_Left.m_Right.a.xz - geometry.m_Left.m_Left.a.xz;
            float seamWidth = data.m_Width * 0.5f + (right ? -data.m_MiddleOffset : data.m_MiddleOffset);
            var wx = seamSpan * (signX / math.max(0.01f, seamWidth));

            var side = right ? geometry.m_Right : geometry.m_Left;
            var into = MathUtils.StartTangent(side.m_Left).xz + MathUtils.StartTangent(side.m_Right).xz;
            float intoLength = math.length(into);
            if (intoLength < 0.0001f) return null;
            into /= intoLength;
            var along = startNode ? -into : into;
            var lateral = new float2(-along.y, along.x);
            float wxLateral = math.dot(wx, lateral);
            if (math.abs(wxLateral) < 0.0001f) return null;

            var atlasOrigin = ua + uvDx * (origin.x - pa.x) + uvDz * (origin.z - pa.z);
            float uPerMetre = uvDx.x / wxLateral;
            float vPerMetre, v0 = 0f;
            if (!float.IsNaN(density))
            {
                vPerMetre = density;
                if (!startNode)
                {
                    float2 total = edgeGeometry.m_Start.m_Length + edgeGeometry.m_End.m_Length;
                    float lateralPosition = math.saturate(piece.m_Offset.x / math.max(0.01f, data.m_Width) + 0.5f);
                    v0 = density * math.lerp(total.x, total.y, lateralPosition);
                    v0 -= math.floor(v0);
                }
            }
            else vPerMetre = math.abs(uPerMetre) * 0.5f; // square texels in the 1:2 atlas
            var gradientU = lateral * uPerMetre;
            var gradientV = (along - lateral * (math.dot(wx, along) / wxLateral)) * vPerMetre;
            return new Frame
            {
                Origin = world.xz,
                Uv = new float2(atlasOrigin.x, v0),
                UvX = new float2(gradientU.x, gradientV.x),
                UvZ = new float2(gradientU.y, gradientV.y)
            };
        }

        // Centre and outward direction of a round road end: the middle of the edge's end cross-section.
        private static CapInfo MakeCapInfo(EdgeGeometry edgeGeometry, bool start)
        {
            var segment = start ? edgeGeometry.m_Start : edgeGeometry.m_End;
            var left = start ? segment.m_Left.a : segment.m_Left.d;
            var right = start ? segment.m_Right.a : segment.m_Right.d;
            var tangent = start
                ? -(MathUtils.StartTangent(segment.m_Left) + MathUtils.StartTangent(segment.m_Right))
                : MathUtils.EndTangent(segment.m_Left) + MathUtils.EndTangent(segment.m_Right);
            var axis = math.normalizesafe(tangent.xz, new float2(0f, 1f));
            return new CapInfo { Center = (left.xz + right.xz) * 0.5f, Axis = axis };
        }

        private static Frame MakeCapFrame(Frame seam, CapInfo cap, float radius)
        {
            if (float.IsNaN(radius) || radius < 0.5f) return seam;
            var lateral = new float2(-cap.Axis.y, cap.Axis.x);
            float side = math.dot(seam.Origin - cap.Center, lateral) >= 0f ? 1f : -1f;
            // V per metre forwards (into the road end) of the straight road.
            float vPerMetre = math.dot(new float2(seam.UvX.y, seam.UvZ.y), cap.Axis);
            return new Frame
            {
                Seam = seam,
                Center = cap.Center,
                Axis = cap.Axis,
                Lateral = lateral,
                Side = side,
                RefRadius = radius,
                VPerMetre = vPerMetre,
                Origin = seam.Origin
            };
        }

        // Mean distance of the slot's sidewalk-top vertices from the road end centre (its centre line).
        private static float SlotRadius(MeshVertex[] positions, MeshUV0[] uvs, MeshIndex[] indices, int begin, int end,
            NetCompositionPiece piece, NetCompositionMeshData data, EdgeNodeGeometry geometry, NetCompositionData compositionData, float2 center)
        {
            float min = float.MaxValue, max = float.MinValue;
            for (int i = begin; i + 2 < end; i += 3)
            {
                int a = indices[i].m_Index, b = indices[i + 1].m_Index, c = indices[i + 2].m_Index;
                if (a < 0 || b < 0 || c < 0 || a >= positions.Length || b >= positions.Length || c >= positions.Length) continue;
                if (!IsSidewalkTop(positions[a].m_Vertex, positions[b].m_Vertex, positions[c].m_Vertex,
                    uvs[a].m_Uv, uvs[b].m_Uv, uvs[c].m_Uv)) continue;
                foreach (int v in new[] { a, b, c })
                {
                    float r = math.distance(ProjectToWorld(positions[v].m_Vertex, piece, data, geometry, compositionData).xz, center);
                    min = math.min(min, r);
                    max = math.max(max, r);
                }
            }
            return max >= min ? (min + max) * 0.5f : float.NaN;
        }

        private static float3 ProjectToWorld(float3 position, NetCompositionPiece piece, NetCompositionMeshData data,
            EdgeNodeGeometry geometry, NetCompositionData compositionData)
        {
            var local = position;
            int flags = (int)piece.m_SectionFlags;
            local.x *= (flags & 1) != 0 ? 1f : -1f;
            local.z *= (flags & 0x100) != 0 ? 1f : -1f;
            if ((flags & 0x4000) != 0) local.z = local.z * 2f + piece.m_Size.z * 0.5f;
            local += piece.m_Offset;
            float t = math.saturate((local.z / piece.m_Size.z + 0.5f) * 2f);
            float half = data.m_Width * 0.5f, middle = data.m_MiddleOffset;
            bool right = (piece.m_SectionFlags & NetSectionFlags.Right) != 0;
            float3 world;
            if (!right)
            {
                float across = math.saturate((local.x - middle) / math.max(0.01f, half + middle) + 1f);
                across = SynchronizeAcross(across, t, compositionData.m_SyncVertexOffsetsLeft, geometry.m_SyncVertexTargetsLeft);
                world = math.lerp(MathUtils.Position(geometry.m_Left.m_Left, t), MathUtils.Position(geometry.m_Left.m_Right, t), across);
            }
            else
            {
                float across = math.saturate((local.x - middle) / math.max(0.01f, half - middle));
                across = SynchronizeAcross(across, t, compositionData.m_SyncVertexOffsetsRight, geometry.m_SyncVertexTargetsRight);
                world = math.lerp(MathUtils.Position(geometry.m_Right.m_Left, t), MathUtils.Position(geometry.m_Right.m_Right, t), across);
            }
            world.y += local.y;
            return world;
        }

        private static bool ValidSync(float4 source, float4 target)
        {
            return math.all(math.isfinite(source)) && math.all(math.isfinite(target)) &&
                source.x >= 0f && source.w <= 1f && source.x <= source.y && source.y <= source.z && source.z <= source.w &&
                target.x >= 0f && target.w <= 1f && target.x <= target.y && target.y <= target.z && target.z <= target.w;
        }

        private static float SynchronizeAcross(float across, float t, float4 source, float4 target)
        {
            float previousSource = 0f, previousTarget = 0f;
            float mapped = across;
            bool found = false;
            for (int i = 0; i < 4; i++)
            {
                float nextSource = source[i], nextTarget = target[i];
                float interval = nextSource - previousSource;
                if (interval > 0.000001f && across <= nextSource)
                {
                    mapped = math.lerp(previousTarget, nextTarget, math.saturate((across - previousSource) / interval));
                    found = true;
                    break;
                }
                previousSource = nextSource;
                previousTarget = nextTarget;
            }
            if (!found)
            {
                float remaining = 1f - previousSource;
                mapped = remaining > 0.000001f
                    ? math.lerp(previousTarget, 1f, math.saturate((across - previousSource) / remaining))
                    : previousTarget;
            }
            return math.saturate(math.lerp(across, mapped, SyncWeight(math.saturate(t))));
        }

        private static float SyncWeight(float t)
        {
            switch (s_SyncBlendMode)
            {
                case 1: return t * t * (3f - 2f * t);
                case 2: return t * t;
                case 3: return 1f - (1f - t) * (1f - t);
                case 4: return 0f;
                case 5: return t > 0f ? 1f : 0f;
                default: return t;
            }
        }
    }

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

