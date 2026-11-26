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

    // Straight sidewalk slabs at junctions: runtime copies of node meshes with remapped sidewalk UVs, swapped in only while rendering.
    public partial class SidewalkJunctionMeshSystem : GameSystemBase, IPreSerialize, IPreDeserialize
    {
        private const float LeftUv = 318f / 1024f;
        private const float RightUv = 759f / 1024f;
        private const float UvEpsilon = 0.0005f;
        // Parking atlas (2048 px reference): sidewalk strip.
        private const float ParkingLeftUv = 516f / 2048f;
        private const float ParkingRightUv = 942f / 2048f;
        private const float ParkingAsphaltRightUv = 482f / 2048f;
        private static float s_WalkLeft = LeftUv, s_WalkRight = RightUv;
        private static bool s_ParkingMode;
        private const int BuildsPerScan = 16;
        // Stop building after ScanBudgetMs per scan (one corner costs 1-8 ms); the rest follows 0.1 s later.
        private const double ScanBudgetMs = 6.0;
        private long m_ScanStart;
        private bool OverScanBudget(int builds) => builds > 0 &&
            (System.Diagnostics.Stopwatch.GetTimestamp() - m_ScanStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency > ScanBudgetMs;
        private const int MaximumCorners = 4096;
        private bool m_LimitLogged, m_MainPending;
        private readonly Dictionary<(Entity, bool), float2> m_BuildHistory = new Dictionary<(Entity, bool), float2>();

        // Only a real loop is blocked (back to an earlier state more than 3 times in 60 s); progress while loading always builds.
        private readonly Dictionary<(Entity, bool), float2> m_Oscillations = new Dictionary<(Entity, bool), float2>();

        private bool Flapping((Entity, bool) key) =>
            m_Oscillations.TryGetValue(key, out var history) && UnityEngine.Time.unscaledTime - history.x <= 60f && history.y > 3f;

        private bool Oscillating((Entity, bool) key, Runtime old, int signature)
        {
            if (old == null || !((old.Stale && signature == old.BuiltSignature) || (old.PreviousSignature != 0 && signature == old.PreviousSignature)))
                return Flapping(key);
            float now = UnityEngine.Time.unscaledTime;
            if (m_Oscillations.Count > 20000) m_Oscillations.Clear();
            if (!m_Oscillations.TryGetValue(key, out var history) || now - history.x > 60f) history = new float2(now, 0f);
            history.y += 1f;
            m_Oscillations[key] = history;
            return history.y > 3f;
        }

        // Rebuilds of an existing corner after its first rebuild in the minute: which part of the signature changed (log output option).
        private int m_RebuildLogged;
        private static readonly string[] s_PartNames = { "node geometry", "node composition", "mesh", "edge length", "borrowed frames / alley frames", "atlas pattern" };

        private void LogRebuild((Entity, bool) key, Runtime old, int[] parts, string name, Entity node, string detail = null)
        {
            if (old == null || m_RebuildLogged >= 40 || !m_BuildHistory.TryGetValue(key, out var history) ||
                UnityEngine.Time.unscaledTime - history.x > 60f || history.y < 1f) return;
            m_RebuildLogged++;
            var changed = new List<string>();
            if (old.Stale) changed.Add("swap stale (native node composition changed)");
            if (old.Parts != null)
                for (int i = 0; i < parts.Length && i < old.Parts.Length; i++)
                    if (parts[i] != old.Parts[i]) changed.Add(s_PartNames[i]);
            if (changed.Count == 0) changed.Add("settings");
            string place = EntityManager.HasComponent<Game.Net.Node>(node) ? EntityManager.GetComponentData<Game.Net.Node>(node).m_Position.xz.ToString() : "?";
            Mod.LogLater("Junction corner rebuilt again: " + name + " | node " + place + " | " + (key.Item2 ? "start" : "end") +
                " | build " + (history.y + 1f).ToString("0") + " this minute | changed: " + string.Join(", ", changed) +
                (detail != null ? " | before: " + (old.Detail ?? "?") + " | now: " + detail : ""));
        }

        // Parking corners and roads at parking junctions that keep the game's look, once per corner (log output option).
        private readonly HashSet<(Entity, bool)> m_SkipLogged = new HashSet<(Entity, bool)>();

        private void LogSkip(bool relevant, Entity road, bool start, string name, Entity node, string reason)
        {
            if (!relevant || m_SkipLogged.Count >= 60 || !m_SkipLogged.Add((road, start))) return;
            string place = EntityManager.HasComponent<Game.Net.Node>(node) ? EntityManager.GetComponentData<Game.Net.Node>(node).m_Position.xz.ToString() : "?";
            Mod.LogLater("Junction corner kept original: " + name + " | node " + place + " | " + (start ? "start" : "end") + " | " + reason);
        }

        private void RecordBuild((Entity, bool) key)
        {
            m_WindowBuilds++;
            float now = UnityEngine.Time.unscaledTime;
            if (m_BuildHistory.Count > 20000) m_BuildHistory.Clear();
            if (!m_BuildHistory.TryGetValue(key, out var history) || now - history.x > 60f) history = new float2(now, 0f);
            history.y += 1f;
            m_BuildHistory[key] = history;
        }

        private sealed class Runtime
        {
            public int Signature;
            public int[] Parts;
            public int BuiltSignature, PreviousSignature;
            public bool Stale;
            // Alley nodes (log output option): scan type and the corners their frames came from, for "rebuilt again" lines.
            public string Detail;
            public Entity Source;
            public Entity Composition;
            public bool Announced;
            public bool Parking;
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
        private static readonly HashSet<Entity> s_ParkingCornerMeshes = new HashSet<Entity>();
        internal static IEnumerable<Entity> ParkingCornerMeshes => s_ParkingCornerMeshes;
        internal static int ParkingCornerVersion { get; private set; }

        private static bool s_RecomputeTangents = false;
        private static float s_EndTilingU = 1f, s_EndTilingV = 1f;

        private readonly Dictionary<(Entity, bool), Runtime> m_Runtimes = new Dictionary<(Entity, bool), Runtime>();
        private readonly Dictionary<Entity, Composition> m_Attached = new Dictionary<Entity, Composition>();
        private readonly HashSet<Entity> m_Owned = new HashSet<Entity>();
        private readonly List<Retired> m_Retired = new List<Retired>();
        private PrefabSystem m_Prefabs;
        private EntityQuery m_Roads;
        private float m_NextScan;
        private bool m_ScanWanted = true;
        private static bool s_NormalOn, s_ParkingOn;
        private float m_FullScanAt, m_RetryAt, m_RetryDelay = 2f;
        private int m_LastConfig;
        private EntityQuery m_NetChanges;

        private void WantScan(float delay)
        {
            m_ScanWanted = true;
            m_NextScan = math.min(m_NextScan, UnityEngine.Time.unscaledTime + delay);
        }

        // Incremental scans touch only changed nodes; a full scan runs on load, on settings changes and every 5 minutes.
        private readonly HashSet<Entity> m_DirtyNodes = new HashSet<Entity>();
        private HashSet<Entity> m_ScanNodes;
        private bool m_FullWanted = true;
        private const float FullScanInterval = 300f;
        private const int IncrementalLimit = 2000;
        private int m_FullScans;

        private void MarkNode(Entity node, float delay)
        {
            if (node == Entity.Null) return;
            m_DirtyNodes.Add(node);
            WantScan(delay);
        }

        private void MarkRoadSide(Entity road, bool start, float delay)
        {
            if (!EntityManager.Exists(road) || !EntityManager.HasComponent<Edge>(road)) { m_FullWanted = true; WantScan(delay); return; }
            var edge = EntityManager.GetComponentData<Edge>(road);
            MarkNode(start ? edge.m_Start : edge.m_End, delay);
        }

        private void CollectNetChanges()
        {
            var changed = m_NetChanges.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var entity in changed)
                {
                    if (EntityManager.HasComponent<Edge>(entity))
                    {
                        var edge = EntityManager.GetComponentData<Edge>(entity);
                        if (edge.m_Start != Entity.Null) { m_DirtyNodes.Add(edge.m_Start); NodeChanged(edge.m_Start); }
                        if (edge.m_End != Entity.Null) { m_DirtyNodes.Add(edge.m_End); NodeChanged(edge.m_End); }
                    }
                    else { m_DirtyNodes.Add(entity); NodeChanged(entity); }
                }
            }
            finally { changed.Dispose(); }
        }

        // Roads at the given nodes (same filter as m_Roads).
        private Entity[] RoadsAt(HashSet<Entity> nodes)
        {
            var roads = new HashSet<Entity>();
            foreach (var node in nodes)
            {
                if (!EntityManager.Exists(node) || EntityManager.HasComponent<Deleted>(node) || !EntityManager.HasBuffer<ConnectedEdge>(node)) continue;
                var edges = EntityManager.GetBuffer<ConnectedEdge>(node, true);
                for (int i = 0; i < edges.Length; i++)
                {
                    var road = edges[i].m_Edge;
                    if (EntityManager.Exists(road) && !EntityManager.HasComponent<Deleted>(road) && !EntityManager.HasComponent<Temp>(road) &&
                        EntityManager.HasComponent<Edge>(road) && EntityManager.HasComponent<Composition>(road) && EntityManager.HasComponent<PrefabRef>(road))
                        roads.Add(road);
                }
            }
            var result = new Entity[roads.Count];
            roads.CopyTo(result);
            return result;
        }

        // Geometry not streamed yet: the node waits for its pieces (JunctionWaiting.cs); Entity.Null falls back to a full scan.
        private void ScheduleRetry(Entity node)
        {
            if (node != Entity.Null) { WaitForGeometry(node); return; }
            m_FullWanted = true;
            if (m_RetryAt > 0f) return;
            m_RetryAt = UnityEngine.Time.unscaledTime + m_RetryDelay;
            m_RetryDelay = math.min(m_RetryDelay * 2f, 60f);
        }

        private int ConfigSignature()
        {
            var setting = Mod.ActiveSetting;
            unchecked
            {
                int hash = PatternSignature();
                hash = hash * 31 + (Mod.DebugBackendEnabled ? setting?.JunctionSyncBlendMode ?? 0 : 0);
                hash = hash * 31 + (Mod.DebugBackendEnabled && (setting?.JunctionRecomputeTangents ?? false) ? 1 : 0);
                hash = hash * 31 + (s_NormalOn ? 1 : 0) + (s_ParkingOn ? 2 : 0);
                hash = hash * 31 + Quantize(setting?.RoadEndTilingU ?? 1f);
                return hash * 31 + Quantize(setting?.RoadEndTilingV ?? 1f);
            }
        }
        private bool m_Failed;
        private bool m_SyncOnRestore;
        private static int s_SyncBlendMode;
        private static bool s_SquareSync;
        private static bool s_BendSync;

        private const bool BuildRoundedCurbEnds = false;
        private const bool BuildStraightEnds = false;
        private static string s_CurbEndWhy = "", s_CurbEndNumbers = "";
        private string m_LastReport;

        private int m_Caps, m_CurbEnds, m_WrappedParts, m_SidewalkEnds, m_AlleyMeshes, m_CurbTriangles, m_CurbLogged, m_RampsReshaped, m_RampsFailed, m_MixedTriangles;
        private Dictionary<Entity, MeshVertex[]> m_Reshaped;
        private const bool VerboseJunctionLog = false;
        private readonly HashSet<string> m_DumpedRamps = new HashSet<string>();
        private Dictionary<Entity, int[]> m_ReshapeMap;
        private Dictionary<Entity, float[]> m_ReshapeU;
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
        private int m_ParkingCorners, m_ParkingLogged, m_ParkingNeighbourCorners, m_DensityStandIns, m_AsphaltTriangles;
        private List<ParkingStrip> m_ParkingStrips;
        private List<NetCompositionPiece> m_ExtraPieces;
        private Entity m_ParkingEdgeComposition;
        private int m_Corners, m_Built, m_Skipped, m_SplitPieces, m_WalkTriangles, m_NoFrame, m_Stale, m_NotLoaded, m_Clamped;
        private float m_ClampExcess;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_Roads = GetEntityQuery(ComponentType.ReadOnly<Edge>(), ComponentType.ReadOnly<Composition>(),
                ComponentType.ReadOnly<PrefabRef>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
            m_NetChanges = GetEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<Edge>() },
                    Any = new[] { ComponentType.ReadOnly<Updated>(), ComponentType.ReadOnly<Deleted>() },
                    None = new[] { ComponentType.ReadOnly<Temp>() }
                },
                new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<Game.Net.Node>() },
                    Any = new[] { ComponentType.ReadOnly<Updated>(), ComponentType.ReadOnly<Deleted>() },
                    None = new[] { ComponentType.ReadOnly<Temp>() }
                });
        }

        protected override void OnUpdate()
        {
            EndRendering();
            var setting = Mod.ActiveSetting;
            s_NormalOn = setting != null && setting.TexturesEnabled && setting.SidewalkJunctionOverlay;
            s_ParkingOn = setting != null && setting.TexturesEnabled && setting.SidewalkJunctionParking;
            if (setting == null || (!s_NormalOn && !s_ParkingOn))
            {
                if (m_Runtimes.Count > 0 || m_ParkingEdgeRuntimes.Count > 0 || m_ParkingNodeRuntimes.Count > 0) DropAll();
                m_Failed = false;
                m_LastReport = null;
                return;
            }
            if (m_Failed) return;
            try
            {
                ReleaseRetired(false);
                float now = UnityEngine.Time.unscaledTime;
                bool netChanged = !m_NetChanges.IsEmptyIgnoreFilter;
                if (netChanged) { CollectNetChanges(); m_ScanWanted = true; m_RetryDelay = 2f; }
                bool settling = LoadSettling(now, netChanged);
                int config = ConfigSignature();
                if (config != m_LastConfig) { m_LastConfig = config; m_FullWanted = true; m_ScanWanted = true; }
                if (m_RetryAt > 0f && now >= m_RetryAt)
                {
                    m_RetryAt = 0f;
                    m_ScanWanted = true;
                }
                CheckWaitingNodes(now);
                if (now >= m_FullScanAt) { m_FullWanted = true; m_ScanWanted = true; }
                if (!settling && setting.HasPendingApply == false && m_ScanWanted && now >= m_NextScan)
                {
                    m_ScanWanted = false;
                    m_NextScan = now + 0.25f;
                    bool full = m_FullWanted || m_DirtyNodes.Count > IncrementalLimit;
                    if (full)
                    {
                        if (now >= m_FullScanAt && m_FullScanAt > 0f) m_RebuildLogged = 0;
                        m_FullWanted = false;
                        m_FullScanAt = now + FullScanInterval;
                        m_DirtyNodes.Clear();
                        m_ScanNodes = null;
                        m_FullScans++;
                    }
                    else
                    {
                        m_ScanNodes = new HashSet<Entity>(m_DirtyNodes);
                        m_DirtyNodes.Clear();
                    }
                    if (full || m_ScanNodes.Count > 0)
                    {
                        long scanStart = System.Diagnostics.Stopwatch.GetTimestamp();
                        Scan();
                        ResolveWaiting(m_ScanNodes);
                        TrackTime(ref m_ScanTicks, ref m_ScanMaxTicks, scanStart);
                        m_Scans++;
                    }
                    m_ScanNodes = null;
                }
                long attachStart = System.Diagnostics.Stopwatch.GetTimestamp();
                Attach();
                TrackTime(ref m_AttachTicks, ref m_AttachMaxTicks, attachStart);
                m_Frames++;
                ReportTiming();
            }
            catch (Exception ex)
            {
                m_Failed = true;
                try { DropAll(); } catch (Exception inner) { Mod.ErrorLater(inner, "Junction sidewalk cleanup failed."); }
                Mod.ErrorLater(ex, "Junction sidewalk meshes stopped. Toggle the option off/on to retry.");
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
                    runtime.Signature = 0;
                    runtime.Stale = true;
                    if (!Flapping(pair.Key)) MarkRoadSide(edge, start, 0.25f);
                    m_Stale++;
                    continue;
                }
                if (!m_Attached.ContainsKey(edge)) m_Attached.Add(edge, current);
                if (start) current.m_StartNode = runtime.Composition;
                else current.m_EndNode = runtime.Composition;
                EntityManager.SetComponentData(edge, current);
                if (!runtime.Announced)
                {
                    EntityManager.AddComponent<BatchesUpdated>(edge);
                    runtime.Announced = true;
                    announced = true;
                    if (runtime.Parking) ParkingCornerVersion++;
                }
            }
            AttachParkingEdges(ref announced);
            if (announced && m_Attached.Count > 0)
            {
                World.GetOrCreateSystemManaged<Game.Rendering.RequiredBatchesSystem>().Update();
                EntityManager.CompleteAllTrackedJobs();
                m_SyncOnRestore = true;
            }
        }

        // Restores the native compositions.
        public void EndRendering()
        {
            if (m_Attached.Count == 0) return;
            long restoreStart = System.Diagnostics.Stopwatch.GetTimestamp();
            try { EndRenderingCore(); }
            finally { TrackTime(ref m_RestoreTicks, ref m_RestoreMaxTicks, restoreStart); }
        }

        // Timing per 10 s (log output option): scans, swaps per frame and rebuilds, to find what costs frame time.
        private long m_ScanTicks, m_ScanMaxTicks, m_AttachTicks, m_AttachMaxTicks, m_RestoreTicks, m_RestoreMaxTicks;
        // Restore split: waiting for the game's jobs that use Composition (sync point) vs. the mod's own write-back.
        private long m_RestoreWaitTicks, m_RestoreWaitMaxTicks, m_RestoreWorkMaxTicks;
        private int m_Scans, m_Frames, m_WindowBuilds;
        private float m_TimingSince = -1f;

        private static void TrackTime(ref long total, ref long max, long start)
        {
            long ticks = System.Diagnostics.Stopwatch.GetTimestamp() - start;
            total += ticks;
            if (ticks > max) max = ticks;
        }

        private void ReportTiming()
        {
            float now = UnityEngine.Time.unscaledTime;
            if (m_TimingSince < 0f) m_TimingSince = now;
            if (now - m_TimingSince < 10f) return;
            double ms = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            int frames = Math.Max(1, m_Frames), scans = Math.Max(1, m_Scans);
            Mod.LogLater("Junction sidewalk timing (10 s): " + m_Scans + " scans (" + m_FullScans + " full) avg " + (m_ScanTicks * ms / scans).ToString("0.0") +
                " ms max " + (m_ScanMaxTicks * ms).ToString("0.0") + " ms; swap per frame avg " + (m_AttachTicks * ms / frames).ToString("0.00") +
                " ms max " + (m_AttachMaxTicks * ms).ToString("0.0") + " ms; restore per frame avg " + (m_RestoreTicks * ms / frames).ToString("0.00") +
                " ms max " + (m_RestoreMaxTicks * ms).ToString("0.0") + " ms (job wait avg " + (m_RestoreWaitTicks * ms / frames).ToString("0.00") +
                " ms max " + (m_RestoreWaitMaxTicks * ms).ToString("0.0") + " ms, own work max " + (m_RestoreWorkMaxTicks * ms).ToString("0.0") + " ms); " +
                m_WindowBuilds + " rebuilds; " + m_Runtimes.Count + " corners, " +
                m_TiledParkingEdges.Count + " tiled parking edges, " + m_ParkingNodeRoads.Count + " parking node roads, " + m_Attached.Count + " swapped edges; " +
                m_Waiting.Count + " nodes waiting for geometry (" + m_WaitingChecks + " checks, " + m_WaitingReady + " ready, check max " +
                (m_WaitingCheckMaxTicks * ms).ToString("0.00") + " ms); invisible pieces left out in " + m_InvisibleLeftOut + " builds.");
            m_InvisibleLeftOut = 0;
            m_WaitingCheckMaxTicks = 0;
            LogMissingPieces(now);
            m_ScanTicks = m_ScanMaxTicks = m_AttachTicks = m_AttachMaxTicks = m_RestoreTicks = m_RestoreMaxTicks = 0;
            m_RestoreWaitTicks = m_RestoreWaitMaxTicks = m_RestoreWorkMaxTicks = 0;
            m_Scans = m_Frames = m_WindowBuilds = m_FullScans = 0;
            m_WaitingChecks = m_WaitingReady = 0;
            PruneCornerBuiltAt();
            m_TimingSince = now;
        }

        private void EndRenderingCore()
        {
            long waitStart = System.Diagnostics.Stopwatch.GetTimestamp();
            if (m_SyncOnRestore)
            {
                EntityManager.CompleteAllTrackedJobs();
                m_SyncOnRestore = false;
            }
            EntityManager.CompleteDependencyBeforeRW<Composition>();
            long workStart = System.Diagnostics.Stopwatch.GetTimestamp();
            long wait = workStart - waitStart;
            m_RestoreWaitTicks += wait;
            if (wait > m_RestoreWaitMaxTicks) m_RestoreWaitMaxTicks = wait;
            try { RestoreAttached(); }
            finally
            {
                long work = System.Diagnostics.Stopwatch.GetTimestamp() - workStart;
                if (work > m_RestoreWorkMaxTicks) m_RestoreWorkMaxTicks = work;
            }
        }

        private void RestoreAttached()
        {
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
                if (current.m_Edge != pair.Value.m_Edge && m_Owned.Contains(current.m_Edge))
                { current.m_Edge = pair.Value.m_Edge; changed = true; }
                if (changed) EntityManager.SetComponentData(edge, current);
            }
            m_Attached.Clear();
        }

        public void PreSerialize(Context context) => EndRendering();

        public void PreDeserialize(Context context)
        {
            m_Attached.Clear();
            m_BuildHistory.Clear();
            m_Oscillations.Clear();
            m_SkipLogged.Clear();
            m_LimitLogged = false;
            m_RebuildLogged = 0;
            m_Runtimes.Clear();
            m_Owned.Clear();
            m_Retired.Clear();
            m_SidewalkFreeMeshes.Clear();
            m_RoadSidewalkMeshes.Clear();
            s_RuntimeMeshes.Clear();
            s_CornerMeshes.Clear();
            s_ParkingCornerMeshes.Clear();
            ForgetParkingEdges();
            ParkingCornerVersion++;
            m_NextScan = 0f;
            m_ScanWanted = true;
            m_FullWanted = true;
            m_DirtyNodes.Clear();
            ClearWaiting();
            m_InvisiblePrefabs.Clear();
            m_FlipLogged.Clear();
            BeginLoadSettle();
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
            DropParkingEdges();
            ClearWaiting();
            m_NextScan = 0f;
            m_ScanWanted = true;
            m_FullWanted = true;
        }

        protected override void OnDestroy()
        {
            try { DropAll(); ReleaseRetired(true); } catch (Exception) { }
            base.OnDestroy();
        }

        private void Retire(Runtime runtime)
        {
            if (runtime.Composition != Entity.Null) m_Owned.Remove(runtime.Composition);
            if (runtime.Parking)
            {
                foreach (var entity in runtime.Created) s_ParkingCornerMeshes.Remove(entity);
                ParkingCornerVersion++;
            }
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
                    s_ParkingCornerMeshes.Remove(entity);
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
            m_ScanStart = System.Diagnostics.Stopwatch.GetTimestamp();
            m_ParkingCorners = m_ParkingNeighbourCorners = m_DensityStandIns = m_AsphaltTriangles = m_MiterParts = m_MiterLogged = m_BorrowedSlots = 0;
            m_ParkingSidewalkEnds.Clear();
            m_ParkingEndCorners.Clear();
            m_ParkingAlleyCorners.Clear();
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
            m_MainPending = false;
            m_PatternDeferred.Clear();
            Entity[] ordered;
            if (m_ScanNodes == null)
            {
                var roads = m_Roads.ToEntityArray(Allocator.Temp);
                ordered = roads.ToArray();
                roads.Dispose();
            }
            else ordered = RoadsAt(m_ScanNodes);
            {
                // Stable order: chunk order changes with every BatchesUpdated, and at the corner limit that rebuilt other corners each scan.
                Array.Sort(ordered, (x, y) => x.Index.CompareTo(y.Index));
                var laterRoads = new HashSet<Entity>();
                for (int pass = 0; pass < 3; pass++)
                foreach (var road in ordered)
                {
                    if ((m_ScanNodes == null ? active.Count : m_Runtimes.Count) >= MaximumCorners) break;
                    if (pass > 0 && !laterRoads.Contains(road)) continue;
                    if (IsAlley(road) || !EntityManager.HasComponent<Game.Net.Road>(road) || IsInvisibleEdge(road)) continue;
                    var prefabEntity = EntityManager.GetComponentData<PrefabRef>(road).m_Prefab;
                    if (!m_Prefabs.TryGetPrefab<PrefabBase>(prefabEntity, out var prefab) || prefab == null) continue;
                    var edge = EntityManager.GetComponentData<Edge>(road);
                    var composition = EntityManager.GetComponentData<Composition>(road);
                    bool parkingRoad = IsParkingPrefab(prefab) && !HasRoadSidewalk(road);
                    // Road Builder roads: only their parking corners (free layouts, other corners keep the game's look).
                    if (!parkingRoad && RoadBuilderSurfaceCompatibility.IsRoadBuilderRoad(prefab)) continue;
                    for (int side = 0; side < 2; side++)
                    {
                        bool start = side == 0;
                        var junction = start ? edge.m_Start : edge.m_End;
                        if (m_ScanNodes != null && !m_ScanNodes.Contains(junction)) continue;
                        bool cap = EntityManager.HasBuffer<ConnectedEdge>(junction) &&
                            EntityManager.GetBuffer<ConnectedEdge>(junction, true).Length == 1;
                        bool parking = false, parkingNeighbour = false;
                        int sideMask = 3;
                        if (!cap && parkingRoad && AtParkingJunction(road, prefabEntity, junction)) parking = true;
                        else if (!cap && !AtAlleyJunction(road, junction, out sideMask))
                        {
                            if (!parkingRoad) { if (!HasParkingNeighbour(road, junction)) continue; parkingNeighbour = true; }
                            else if (!AtParkingJunction(road, prefabEntity, junction)) continue;
                            else parking = true;
                        }
                        if ((parking || parkingNeighbour) ? !s_ParkingOn : !s_NormalOn) continue;
                        if (pass == 0 && (cap || parkingNeighbour)) laterRoads.Add(road);
                        if ((cap ? 2 : parkingNeighbour ? 1 : 0) != pass) continue;
                        if (!parking && !parkingNeighbour && EntityManager.GetBuffer<ConnectedEdge>(junction, true).Length == 2) sidewalkEnds.Add(junction);
                        if (parking && EntityManager.GetBuffer<ConnectedEdge>(junction, true).Length == 2 && AtAlleyJunction(junction))
                        {
                            sidewalkEnds.Add(junction);
                            m_ParkingSidewalkEnds.Add(junction);
                            m_ParkingEndCorners[junction] = (road, start);
                        }
                        if (Mod.DebugBackendEnabled && VerboseJunctionLog && !cap && !parking && !parkingNeighbour) InspectAlleyNode(start ? edge.m_Start : edge.m_End);
                        if (cap) m_Caps++; else if (parking) m_ParkingCorners++; else if (parkingNeighbour) m_ParkingNeighbourCorners++; else m_Corners++;
                        bool hasNodeGeometry = start ? EntityManager.HasComponent<StartNodeGeometry>(road)
                            : EntityManager.HasComponent<EndNodeGeometry>(road);
                        if (!hasNodeGeometry || !EntityManager.HasComponent<EdgeGeometry>(road)) { m_Skipped++; LogSkip(parking || parkingNeighbour, road, start, prefab.name, junction, "no node geometry"); continue; }
                        var geometry = start ? EntityManager.GetComponentData<StartNodeGeometry>(road).m_Geometry
                            : EntityManager.GetComponentData<EndNodeGeometry>(road).m_Geometry;
                        var edgeGeometry = EntityManager.GetComponentData<EdgeGeometry>(road);
                        var source = start ? composition.m_StartNode : composition.m_EndNode;
                        if (source == Entity.Null || m_Owned.Contains(source) ||
                            !EntityManager.HasComponent<NetCompositionMeshRef>(source) ||
                            !EntityManager.HasComponent<NetCompositionData>(source)) { m_Skipped++; LogSkip(parking || parkingNeighbour, road, start, prefab.name, junction, "no node composition"); continue; }
                        var meshRef = EntityManager.GetComponentData<NetCompositionMeshRef>(source);
                        if (Mod.DebugBackendEnabled && VerboseJunctionLog && meshRef.m_Mesh != Entity.Null && m_InspectedMeshes.Count <= 16 &&
                            EntityManager.HasBuffer<NetCompositionPiece>(meshRef.m_Mesh) && m_InspectedMeshes.Add(meshRef.m_Mesh))
                            InspectMesh(meshRef.m_Mesh, "main road node mesh (" + (start ? "start" : "end") + ")");
                        var compositionData = EntityManager.GetComponentData<NetCompositionData>(source);
                        if (meshRef.m_Mesh == Entity.Null || !EntityManager.HasComponent<NetCompositionMeshData>(meshRef.m_Mesh) ||
                            !EntityManager.HasBuffer<NetCompositionPiece>(meshRef.m_Mesh) ||
                            !ValidSync(compositionData.m_SyncVertexOffsetsLeft, geometry.m_SyncVertexTargetsLeft) ||
                            !ValidSync(compositionData.m_SyncVertexOffsetsRight, geometry.m_SyncVertexTargetsRight))
                        {
                            m_Skipped++;
                            LogSkip(parking || parkingNeighbour, road, start, prefab.name, junction, "node sync targets out of order (left " +
                                compositionData.m_SyncVertexOffsetsLeft + " -> " + geometry.m_SyncVertexTargetsLeft + ", right " +
                                compositionData.m_SyncVertexOffsetsRight + " -> " + geometry.m_SyncVertexTargetsRight + ")");
                            continue;
                        }

                        var key = (road, start);
                        active.Add(key);
                        if (parking && EntityManager.GetBuffer<ConnectedEdge>(junction, true).Length >= 3 && HasAlleyArm(junction))
                            m_ParkingAlleyCorners.Add((junction, key));
                        int borrowSignature = 0;
                        var borrow = parkingNeighbour ? BorrowParkingFrames(road, start, junction, out borrowSignature) : null;
                        if (borrow != null) borrowSignature = borrowSignature * 31 + PatternSignature();
                        if (!parking && !parkingNeighbour) mainCorners.Add((start ? edge.m_Start : edge.m_End, key));
                        int signature = geometry.GetHashCode() ^ source.GetHashCode() * 397 ^ meshRef.m_Mesh.GetHashCode() * 31 ^
                            (edgeGeometry.m_Start.m_Length + edgeGeometry.m_End.m_Length).GetHashCode() * 17 ^ (start ? 0x5bd1e995 : 0) ^
                            (s_SyncBlendMode + 1) * 0x01000193 ^ (cap ? 0x3c6ef372 ^ (s_EndTilingU * 97f + s_EndTilingV * 7919f).GetHashCode() : 0) ^ (s_RecomputeTangents ? 0x6a09e667 : 0) ^ (parking ? 0x1b873593 : 0) ^ (parkingNeighbour ? 0x2e1b2138 : 0) ^ borrowSignature * 0x3243f6a9 ^
                            (parking ? PatternSignature() * 0x2c1b3c6d : 0);
                        if (cap || parking || parkingNeighbour) sideMask = 3;
                        signature ^= sideMask * 0x4f1bbcdd;
                        if (signature == 0) signature = 1;
                        m_Runtimes.TryGetValue(key, out var old);
                        if (old != null && old.Signature == signature) continue;
                        var parts = new[] { geometry.GetHashCode(), source.GetHashCode(), meshRef.m_Mesh.GetHashCode(),
                            (edgeGeometry.m_Start.m_Length + edgeGeometry.m_End.m_Length).GetHashCode(), borrowSignature, parking ? PatternSignature() : 0 };
                        if ((parking || parkingNeighbour) && PatternPending(UnityEngine.Time.unscaledTime))
                        {
                            MarkNode(junction, 0.5f);
                            m_PatternDeferred.Add(junction);
                            continue;
                        }
                        if (builds >= BuildsPerScan || OverScanBudget(builds))
                        {
                            MarkNode(junction, 0.1f);
                            m_NextScan = UnityEngine.Time.unscaledTime + 0.1f;
                            m_MainPending = true;
                            continue;
                        }
                        if (Oscillating(key, old, signature)) continue;
                        LogRebuild(key, old, parts, prefab.name, junction);
                        builds++;
                        m_DumpNeighbour = parkingNeighbour;
                        s_SquareSync = parking;
                        s_BendSync = false;
                        m_DumpLabel = prefab.name + " | " + (cap ? "cap" : parking ? "parking corner" : parkingNeighbour ? "road at parking junction" : "corner") +
                            " | " + EntityManager.GetBuffer<ConnectedEdge>(junction, true).Length + " edges | node " +
                            (EntityManager.HasComponent<Game.Net.Node>(junction) ? EntityManager.GetComponentData<Game.Net.Node>(junction).m_Position.xz.ToString() : "?");
                        m_MiterJunction = (parking && UseMiter) || borrow != null ? junction : Entity.Null;
                        m_MiterRoad = road;
                        m_BorrowFrames = borrow;
                        SetSideFilter(road, start, junction, sideMask);
                        var runtime = SafeBuild(source, () => Build(source, meshRef, compositionData, geometry, edgeGeometry, start,
                            capInfo: cap ? MakeCapInfo(edgeGeometry, start) : null, parking: parking, edgeComposition: composition.m_Edge));
                        m_MiterJunction = Entity.Null;
                        m_BorrowFrames = null;
                        m_SideMask = 3;
                        m_DumpNeighbour = false;
                        s_SquareSync = false;
                        s_BendSync = false;
                        if (runtime == null)
                        {
                            builds--;
                            ScheduleRetry(junction);
                            continue;
                        }
                        runtime.Signature = signature;
                        runtime.Parts = parts;
                        runtime.BuiltSignature = signature;
                        runtime.PreviousSignature = old?.BuiltSignature ?? 0;
                        RecordBuild(key);
                        runtime.Mesh = runtime.Created.Count > 0 ? FindMesh(runtime) : Entity.Null;
                        if (old != null)
                        {
                            EndRendering();
                            Retire(old);
                            EntityManager.AddComponent<BatchesUpdated>(road);
                        }
                        m_Runtimes[key] = runtime;
                        CornerBuilt(junction);
                        if (runtime.Composition != Entity.Null) m_Built++;
                        else LogSkip(parking || parkingNeighbour, road, start, prefab.name, junction, "nothing remapped (no straight strip or sidewalk found at the seam)");
                    }
                }
            }
            if (active.Count >= MaximumCorners && !m_LimitLogged)
            {
                m_LimitLogged = true;
                Mod.LogLater("Junction sidewalk meshes: limit of " + MaximumCorners + " corners reached, further corners keep the game's look.");
            }

            ScanAlleyNodes(active, mainCorners, sidewalkEnds, ref builds);

            var remove = new List<(Entity, bool)>();
            foreach (var pair in m_Runtimes)
            {
                if (active.Contains(pair.Key)) continue;
                if (m_ScanNodes != null)
                {
                    var road = pair.Key.Item1;
                    bool gone = !EntityManager.Exists(road) || EntityManager.HasComponent<Deleted>(road) || !EntityManager.HasComponent<Edge>(road);
                    if (!gone)
                    {
                        var edge = EntityManager.GetComponentData<Edge>(road);
                        if (!m_ScanNodes.Contains(pair.Key.Item2 ? edge.m_Start : edge.m_End)) continue;
                    }
                }
                remove.Add(pair.Key);
            }
            if (remove.Count > 0) EndRendering();
            foreach (var key in remove)
            {
                Retire(m_Runtimes[key]);
                m_Runtimes.Remove(key);
                if (EntityManager.Exists(key.Item1) && !EntityManager.HasComponent<Deleted>(key.Item1))
                    EntityManager.AddComponent<BatchesUpdated>(key.Item1);
            }

            ScanParkingEdges(ordered);

            if (Mod.DebugBackendEnabled)
            {
                int live = 0, withBatches = 0, batchGroups = 0;
                foreach (var runtime in m_Runtimes.Values)
                {
                    if (runtime.Composition == Entity.Null) continue;
                    live++;
                    if (runtime.Mesh != Entity.Null && EntityManager.Exists(runtime.Mesh) && EntityManager.HasBuffer<BatchGroup>(runtime.Mesh))
                    {
                        int groups = EntityManager.GetBuffer<BatchGroup>(runtime.Mesh, true).Length;
                        batchGroups += groups;
                        if (groups > 0) withBatches++;
                    }
                }
                string report = "Junction sidewalk meshes: " + live + " corners active (" + m_Corners + " alley corners found, " + m_ParkingCorners + " parking road corners (" + m_AsphaltTriangles + " asphalt triangles remapped, " + m_MiterParts + " mitered), " + m_TiledEdgeCompositions + " tiled parking edge compositions (" + m_TiledParkingEdges.Count + " edges), " + m_TiledNodeCompositions + " tiled parking node compositions, " + m_ParkingNeighbourCorners + " road corners at parking junctions (" + m_BorrowedSlots + " with the parking grid), " + m_Caps + " road ends, " + m_SidewalkEnds + " sidewalk ends, " + m_AlleyMeshes + " alley node meshes remapped, " +
                    m_Built + " built now, " + m_Skipped + " skipped, " + m_SplitPieces + " sidewalk pieces split, " +
                    m_WalkTriangles + " sidewalk triangles remapped (" + m_CurbTriangles + " widened curb, " + m_RampsReshaped + " curb ramps shortened (" + m_CurbEnds + " curb ends), " + m_RampsFailed + " not, " + m_MixedTriangles + " ramp end faces), " + m_Clamped + " UVs outside the strip (max " + m_ClampExcess.ToString("0.#") + " px, " + m_WrappedParts + " wrapped parts), " + m_NoFrame + " without frame, " + m_Stale + " stale swaps, " +
                    m_NotLoaded + " waiting for geometry, sync blend " + s_SyncBlendMode + ", " + withBatches + " meshes with batches / " + batchGroups + " batch groups).";
                if (report != m_LastReport)
                {
                    m_LastReport = report;
                    Mod.LogLater(report);
                }
            }
            m_Stale = 0;
        }

        private void ScanAlleyNodes(HashSet<(Entity, bool)> active, List<(Entity, (Entity, bool))> mainCorners,
            HashSet<Entity> sidewalkEnds, ref int builds)
        {
            var frames = new Dictionary<Entity, List<SourceFrame>>();
            var signatures = new Dictionary<Entity, int>();
            var details = ModLog.Enabled ? new Dictionary<Entity, List<string>>() : null;
            foreach (var end in sidewalkEnds)
            {
                frames[end] = new List<SourceFrame>();
                signatures[end] = 0x2545f491;
                m_SidewalkEnds++;
                AddDetail(details, end, "sidewalk end");
            }
            foreach (var corner in mainCorners)
            {
                if (!m_Runtimes.TryGetValue(corner.Item2, out var main) || main.Composition == Entity.Null || main.Frames.Count == 0)
                {
                    AddDetail(details, corner.Item1, "main " + CornerName(corner.Item2) + " missing");
                    continue;
                }
                if (!frames.TryGetValue(corner.Item1, out var list)) { list = new List<SourceFrame>(); frames.Add(corner.Item1, list); signatures.Add(corner.Item1, 0); }
                list.AddRange(main.Frames);
                signatures[corner.Item1] = signatures[corner.Item1] * 31 + main.Signature;
                AddDetail(details, corner.Item1, "main " + CornerName(corner.Item2) + "=" + main.Signature + "/" + main.Frames.Count);
            }
            foreach (var pair in m_ParkingEndCorners)
            {
                if (!frames.TryGetValue(pair.Key, out var list) || !m_Runtimes.TryGetValue(pair.Value, out var main) ||
                    main.Composition == Entity.Null || main.Frames.Count == 0)
                {
                    AddDetail(details, pair.Key, "parking end " + CornerName(pair.Value) + " missing");
                    continue;
                }
                AddDetail(details, pair.Key, "parking end " + CornerName(pair.Value) + "=" + main.Signature + "/" + main.Frames.Count);
                foreach (var item in main.Frames)
                {
                    var road = item.Frame != null ? ToRoadFrame(item.Frame) : null;
                    if (road == null) continue;
                    list.Add(new SourceFrame { Frame = road, Centroid = item.Centroid, MaterialIndex = AnyMaterial, MaxU = RightUv });
                }
                signatures[pair.Key] = (signatures[pair.Key] * 31 + main.Signature) * 31 + PatternSignature();
            }
            // Alleys at a parking road junction (T or crossing): the parking corners' straight frames, converted to the RoadEU atlas.
            foreach (var pair in m_ParkingAlleyCorners)
            {
                if (!m_Runtimes.TryGetValue(pair.Item2, out var main) || main.Composition == Entity.Null || main.Frames.Count == 0)
                {
                    AddDetail(details, pair.Item1, "parking corner " + CornerName(pair.Item2) + " missing");
                    continue;
                }
                AddDetail(details, pair.Item1, "parking corner " + CornerName(pair.Item2) + "=" + main.Signature + "/" + main.Frames.Count);
                if (!frames.TryGetValue(pair.Item1, out var list)) { list = new List<SourceFrame>(); frames.Add(pair.Item1, list); signatures.Add(pair.Item1, 0); }
                foreach (var item in main.Frames)
                {
                    var road = item.Frame != null ? ToRoadFrame(item.Frame) : null;
                    if (road == null) continue;
                    list.Add(new SourceFrame { Frame = road, Centroid = item.Centroid, MaterialIndex = AnyMaterial, MaxU = RightUv });
                }
                signatures[pair.Item1] = (signatures[pair.Item1] * 31 + main.Signature) * 31 + PatternSignature();
            }
            foreach (var pair in frames)
            {
                var node = pair.Key;
                if (!EntityManager.HasBuffer<ConnectedEdge>(node)) continue;
                var connected = EntityManager.GetBuffer<ConnectedEdge>(node, true).ToNativeArray(Allocator.Temp).ToArray();
                foreach (var item in connected)
                {
                    var alley = item.m_Edge;
                    if (!IsAlley(alley) || !EntityManager.HasComponent<Edge>(alley) || IsInvisibleEdge(alley)) continue;
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
                        signatures[node] * 0x2f0b3 ^ (start ? 0x7a3c5 : 0x13579) ^
                        (s_SyncBlendMode + 1) * 0x9e3779b ^ (m_ParkingSidewalkEnds.Contains(node) ? 0x51ed27 : 0);
                    if (signature == 0) signature = 1;
                    m_Runtimes.TryGetValue(key, out var old);
                    if (old != null && old.Signature == signature) { if (old.Composition != Entity.Null) m_AlleyMeshes++; continue; }
                    // Main corners at this node built less than AlleySettleTime ago: wait (one rebuild instead of a chain).
                    float settle = AlleySettleWait(node);
                    if (settle > 0f) { MarkNode(node, settle + 0.05f); continue; }
                    if (m_PatternDeferred.Contains(node)) { MarkNode(node, 0.5f); continue; }
                    var parts = new[] { geometry.GetHashCode(), source.GetHashCode(), meshRef.m_Mesh.GetHashCode(), 0, signatures[node], 0 };
                    // Alley nodes follow the main corners' frames: wait until those are all built (no chain of rebuilds while loading).
                    if (builds >= BuildsPerScan || OverScanBudget(builds) || m_MainPending) { MarkNode(node, 0.1f); m_NextScan = UnityEngine.Time.unscaledTime + 0.1f; continue; }
                    if (Oscillating(key, old, signature)) continue;
                    string detail = details != null && details.TryGetValue(node, out var detailList)
                        ? (m_ScanNodes == null ? "full scan: " : "incremental scan: ") + string.Join(", ", detailList) : null;
                    LogRebuild(key, old, parts, "alley node", node, detail);
                    LogAlleyFlip(key, old, signature, node, detail);
                    builds++;
                    var edgeGeometry = EntityManager.GetComponentData<EdgeGeometry>(alley);
                    m_DumpAlleyEnd = m_ParkingSidewalkEnds.Contains(node);
                    s_UntaperWalk = false;
                    s_BendSync = m_DumpAlleyEnd;
                    m_DumpLabel = "alley at parking sidewalk end | node " +
                        (EntityManager.HasComponent<Game.Net.Node>(node) ? EntityManager.GetComponentData<Game.Net.Node>(node).m_Position.xz.ToString() : "?");
                    var runtime = SafeBuild(source, () => Build(source, meshRef, compositionData, geometry, edgeGeometry, start, pair.Value, sidewalkEnds.Contains(node)));
                    m_DumpAlleyEnd = false;
                    s_UntaperWalk = false;
                    s_BendSync = false;
                    if (runtime == null) { builds--; ScheduleRetry(node); continue; }
                    runtime.Signature = signature;
                    runtime.Parts = parts;
                    runtime.Detail = detail;
                    runtime.BuiltSignature = signature;
                    runtime.PreviousSignature = old?.BuiltSignature ?? 0;
                    RecordBuild(key);
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

        private static void AddDetail(Dictionary<Entity, List<string>> details, Entity node, string text)
        {
            if (details == null) return;
            if (!details.TryGetValue(node, out var list)) { list = new List<string>(); details.Add(node, list); }
            list.Add(text);
        }

        private static string CornerName((Entity, bool) key) => "road " + key.Item1.Index + (key.Item2 ? " start" : " end");

        private readonly HashSet<Entity> m_ParkingSidewalkEnds = new HashSet<Entity>();
        private readonly Dictionary<Entity, (Entity, bool)> m_ParkingEndCorners = new Dictionary<Entity, (Entity, bool)>();
        private readonly List<(Entity, (Entity, bool))> m_ParkingAlleyCorners = new List<(Entity, (Entity, bool))>();
    }
}
