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
        // Builds stop for this scan after ScanBudgetMs (the rest follows 0.1 s later): one corner costs 1-8 ms,
        // 16 in one frame made scans of up to 130 ms while loading or after editing.
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
        // Scans only on a reason (net change, pending builds, stale swap, settings), not every 2 s: a full scan costs 15-70 ms in big cities.
        private bool m_ScanWanted = true;
        // Two options: alleys / road ends, and parking roads (corners, roads at parking junctions, tiling).
        private static bool s_NormalOn, s_ParkingOn;
        private float m_FullScanAt, m_RetryAt, m_RetryDelay = 2f;
        private int m_LastConfig;
        private EntityQuery m_NetChanges;

        private void WantScan(float delay)
        {
            m_ScanWanted = true;
            m_NextScan = math.min(m_NextScan, UnityEngine.Time.unscaledTime + delay);
        }

        // Incremental scans: after the first full scan only the nodes touched by net changes, pending builds, stale swaps or
        // retries are scanned (with all roads at them). A full scan runs on load, settings changes and every 5 minutes as a safety net.
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

        // Geometry not streamed yet. A node waits for its pieces (JunctionWaiting.cs, cheap check, no timed rescans);
        // Entity.Null (road without Edge) falls back to a full scan with growing delay.
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

        private int m_Caps, m_CurbEnds, m_WrappedParts, m_SidewalkEnds, m_AlleyMeshes, m_CurbTriangles, m_CurbLogged, m_RampsReshaped, m_RampsFailed, m_RampLogged, m_MixedTriangles;
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
                if (!m_NetChanges.IsEmptyIgnoreFilter) { CollectNetChanges(); m_ScanWanted = true; m_RetryDelay = 2f; }
                int config = ConfigSignature();
                if (config != m_LastConfig) { m_LastConfig = config; m_FullWanted = true; m_ScanWanted = true; }
                if (m_RetryAt > 0f && now >= m_RetryAt)
                {
                    m_RetryAt = 0f;
                    m_ScanWanted = true;
                }
                CheckWaitingNodes(now);
                if (now >= m_FullScanAt) { m_FullWanted = true; m_ScanWanted = true; }
                if (setting.HasPendingApply == false && m_ScanWanted && now >= m_NextScan)
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
            // SetComponentData below would wait for these jobs anyway; doing it explicitly separates the wait from the own work.
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

        // Finds the corners to rebuild (alley junctions, parking corners, roads at parking junctions, road ends).
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
                    if (IsAlley(road)) continue;
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
                // Incremental scan: only corners at the scanned nodes (or of removed roads) can be dropped.
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

            // Status report (debug only): walks every corner's batches, skipped in normal play.
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
                        signatures[node] * 0x2f0b3 ^ (start ? 0x7a3c5 : 0x13579) ^
                        (s_SyncBlendMode + 1) * 0x9e3779b ^ (m_ParkingSidewalkEnds.Contains(node) ? 0x51ed27 : 0);
                    if (signature == 0) signature = 1;
                    m_Runtimes.TryGetValue(key, out var old);
                    if (old != null && old.Signature == signature) { if (old.Composition != Entity.Null) m_AlleyMeshes++; continue; }
                    // Main corners at this node built less than AlleySettleTime ago: wait (one rebuild instead of a chain).
                    float settle = AlleySettleWait(node);
                    if (settle > 0f) { MarkNode(node, settle + 0.05f); continue; }
                    var parts = new[] { geometry.GetHashCode(), source.GetHashCode(), meshRef.m_Mesh.GetHashCode(), 0, signatures[node], 0 };
                    // Alley nodes follow the main corners' frames: wait until those are all built (no chain of rebuilds while loading).
                    if (builds >= BuildsPerScan || OverScanBudget(builds) || m_MainPending) { MarkNode(node, 0.1f); m_NextScan = UnityEngine.Time.unscaledTime + 0.1f; continue; }
                    if (Oscillating(key, old, signature)) continue;
                    string detail = details != null && details.TryGetValue(node, out var detailList)
                        ? (m_ScanNodes == null ? "full scan: " : "incremental scan: ") + string.Join(", ", detailList) : null;
                    LogRebuild(key, old, parts, "alley node", node, detail);
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

        private readonly Dictionary<Entity, bool> m_SidewalkFreeMeshes = new Dictionary<Entity, bool>();
        private readonly Dictionary<Entity, bool> m_RoadSidewalkMeshes = new Dictionary<Entity, bool>();

        private bool HasRoadSidewalk(Entity edge)
        {
            if (!EntityManager.HasComponent<Composition>(edge)) return false;
            var composed = EntityManager.GetComponentData<Composition>(edge).m_Edge;
            if (composed == Entity.Null || !EntityManager.HasComponent<NetCompositionMeshRef>(composed)) return false;
            var mesh = EntityManager.GetComponentData<NetCompositionMeshRef>(composed).m_Mesh;
            if (mesh == Entity.Null || !EntityManager.HasBuffer<NetCompositionPiece>(mesh)) return false;
            if (m_RoadSidewalkMeshes.TryGetValue(mesh, out bool found)) return found;
            found = false;
            var pieces = EntityManager.GetBuffer<NetCompositionPiece>(mesh, true);
            for (int i = 0; i < pieces.Length && !found; i++)
            {
                if (!m_Prefabs.TryGetPrefab<PrefabBase>(pieces[i].m_Piece, out var prefab) || prefab == null || prefab.name == null) continue;
                string name = prefab.name;
                found = name.IndexOf("Sidewalk", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    name.IndexOf("Side Piece", StringComparison.OrdinalIgnoreCase) < 0 && !IsParkingAtlasPiece(name);
            }
            m_RoadSidewalkMeshes[mesh] = found;
            return found;
        }

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

        // Per road: at 2-3 edges the node rule above. At crossings with 4 or more edges (alley crossing a road) only a road
        // whose two neighbouring arms are both alleys; a corner between two roads with sidewalks keeps the game's look.
        private bool AtAlleyJunction(Entity road, Entity node, out int sides)
        {
            sides = 3;
            if (!EntityManager.HasBuffer<ConnectedEdge>(node)) return false;
            var edges = EntityManager.GetBuffer<ConnectedEdge>(node, true);
            if (edges.Length <= 3) return AtAlleyJunction(node);
            sides = 0;
            var arms = new List<(float, Entity)>(edges.Length);
            for (int i = 0; i < edges.Length; i++)
            {
                var other = edges[i].m_Edge;
                if (!EntityManager.HasComponent<Edge>(other)) return false;
                bool start = EntityManager.GetComponentData<Edge>(other).m_Start == node;
                if (!EdgeDirection(other, start, out var direction)) return false;
                arms.Add((math.atan2(direction.y, direction.x), other));
            }
            arms.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            int index = arms.FindIndex(a => a.Item2 == road);
            if (index < 0) return false;
            var previous = arms[(index + arms.Count - 1) % arms.Count].Item2;
            var next = arms[(index + 1) % arms.Count].Item2;
            if (previous == road || next == road || IsAlley(road)) return false;
            sides = (IsAlley(previous) ? 1 : 0) | (IsAlley(next) ? 2 : 0);
            return sides != 0;
        }

        // Crossing with 4+ arms where only one neighbouring arm of this road is an alley: only the sidewalk on that side is
        // straightened, the corner towards the other road keeps the game's look. Bit 1: clockwise neighbour, bit 2: counter-clockwise.
        private int m_SideMask = 3;
        private float2 m_SideNode, m_SideDirection;

        private void SetSideFilter(Entity road, bool start, Entity node, int mask)
        {
            m_SideMask = 3;
            if (mask == 3 || !EntityManager.HasComponent<Game.Net.Node>(node) || !EdgeDirection(road, start, out var direction)) return;
            m_SideMask = mask;
            m_SideNode = EntityManager.GetComponentData<Game.Net.Node>(node).m_Position.xz;
            m_SideDirection = direction;
        }

        private bool SideAllowed(float2 center)
        {
            if (m_SideMask == 3) return true;
            if (math.any(math.isnan(center))) return false;
            float side = Cross2(m_SideDirection, center - m_SideNode);
            return side > 0f ? (m_SideMask & 2) != 0 : (m_SideMask & 1) != 0;
        }

        private static bool IsParkingRoad(string name) => name != null &&
            (name.StartsWith("Alley", StringComparison.Ordinal) || name.StartsWith("Small Road", StringComparison.Ordinal) ||
             name.StartsWith("Medium Road", StringComparison.Ordinal)) &&
            name.IndexOf(" - ", StringComparison.Ordinal) >= 0 &&
            name.IndexOf("Parking", StringComparison.OrdinalIgnoreCase) >= 0;

        // Parking road by name, or a Road Builder road with parking lanes (same rule as the parking atlas).
        private readonly Dictionary<PrefabBase, bool> m_ParkingPrefabs = new Dictionary<PrefabBase, bool>();

        private bool IsParkingPrefab(PrefabBase prefab)
        {
            if (prefab == null) return false;
            if (m_ParkingPrefabs.TryGetValue(prefab, out bool parking)) return parking;
            parking = IsParkingRoad(prefab.name);
            if (!parking)
            {
                try { parking = RoadBuilderSurfaceCompatibility.IsRoadBuilderRoad(prefab) && RoadBuilderSurfaceCompatibility.HasParkingLanes(prefab); }
                catch (Exception) { parking = false; }
            }
            m_ParkingPrefabs[prefab] = parking;
            return parking;
        }

        // Junction of a parking road with other roads (or the transition into a road of another kind).
        private bool AtParkingJunction(Entity road, Entity prefabEntity, Entity node)
        {
            if (!EntityManager.HasBuffer<ConnectedEdge>(node)) return false;
            var edges = EntityManager.GetBuffer<ConnectedEdge>(node, true);
            if (edges.Length >= 3) return true;
            if (edges.Length != 2) return false;
            var other = edges[0].m_Edge == road ? edges[1].m_Edge : edges[0].m_Edge;
            if (other == road || !EntityManager.HasComponent<PrefabRef>(other)) return false;
            var otherPrefab = EntityManager.GetComponentData<PrefabRef>(other).m_Prefab;
            if (otherPrefab == prefabEntity) return false;
            if (!m_Prefabs.TryGetPrefab<PrefabBase>(otherPrefab, out var prefab) || prefab == null || !IsParkingPrefab(prefab)) return true;
            bool otherAlley = prefab.name.StartsWith("Alley", StringComparison.Ordinal);
            bool ownAlley = m_Prefabs.TryGetPrefab<PrefabBase>(prefabEntity, out var own) && own != null &&
                own.name.StartsWith("Alley", StringComparison.Ordinal);
            return otherAlley != ownAlley;
        }

        // A road with its own sidewalk meeting a parking road (2 edges of different kind or a junction).
        private bool HasParkingNeighbour(Entity road, Entity node)
        {
            if (!EntityManager.HasBuffer<ConnectedEdge>(node)) return false;
            var edges = EntityManager.GetBuffer<ConnectedEdge>(node, true);
            if (edges.Length < 2) return false;
            for (int i = 0; i < edges.Length; i++)
            {
                var other = edges[i].m_Edge;
                if (other == road || !EntityManager.HasComponent<PrefabRef>(other)) continue;
                if (m_Prefabs.TryGetPrefab<PrefabBase>(EntityManager.GetComponentData<PrefabRef>(other).m_Prefab, out var prefab) &&
                    prefab != null && IsParkingPrefab(prefab)) return true;
            }
            return false;
        }

        private static bool IsParkingAtlasPiece(string name) =>
            !string.IsNullOrEmpty(name) && name.EndsWith(" Mesh", StringComparison.Ordinal);

        private static void SetParkingMode(bool parking)
        {
            s_ParkingMode = parking;
            s_WalkLeft = parking ? ParkingLeftUv : LeftUv;
            s_WalkRight = parking ? ParkingRightUv : RightUv;
        }

        private int m_BuildErrors;
        private Runtime SafeBuild(Entity source, Func<Runtime> build)
        {
            try { return build(); }
            catch (Exception ex)
            {
                m_Reshaped = null; m_ReshapeMap = null; m_ReshapeU = null; m_ReshapeExtra = null; m_ReshapeDrop = null; m_ReshapeFill = null;
                SetParkingMode(false);
                if (m_BuildErrors++ < 5) Mod.ErrorLater(ex, "Junction sidewalk mesh skipped for one corner/road end.");
                return new Runtime { Source = source };
            }
        }

        private Runtime Build(Entity source, NetCompositionMeshRef meshRef, NetCompositionData compositionData,
            EdgeNodeGeometry geometry, EdgeGeometry edgeGeometry, bool start, List<SourceFrame> external = null, bool sidewalkEnd = false,
            CapInfo capInfo = null, bool parking = false, Entity edgeComposition = default)
        {
            SetParkingMode(parking);
            m_ParkingStrips = null;
            m_ParkingEdgeComposition = edgeComposition;
            try
            {
                var runtime = BuildCore(source, meshRef, compositionData, geometry, edgeGeometry, start, external, sidewalkEnd, capInfo);
                if (runtime != null) runtime.Parking = parking;
                return runtime;
            }
            finally
            {
                SetParkingMode(false);
                m_ParkingStrips = null;
                m_ExtraPieces = null;
                m_ParkingEdgeComposition = Entity.Null;
            }
        }

        private Runtime BuildCore(Entity source, NetCompositionMeshRef meshRef, NetCompositionData compositionData,
            EdgeNodeGeometry geometry, EdgeGeometry edgeGeometry, bool start, List<SourceFrame> external, bool sidewalkEnd,
            CapInfo capInfo)
        {
            var runtime = new Runtime { Source = source };
            var meshEntity = meshRef.m_Mesh;
            var meshData = EntityManager.GetComponentData<NetCompositionMeshData>(meshEntity);
            var pieces = WithoutGeometryFreePieces(EntityManager.GetBuffer<NetCompositionPiece>(meshEntity, true).ToNativeArray(Allocator.Temp).ToArray());
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

                if (s_ParkingMode)
                {
                    m_ParkingStrips = BuildParkingStrips(pieces, meshData, geometry, compositionData, edgeGeometry, start);
                    if (m_ParkingStrips.Count == 0)
                    {
                        m_NoFrame++;
                        ReleaseCreated(runtime);
                        return runtime;
                    }
                }

                m_ExtraPieces = new List<NetCompositionPiece>();
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
                    if (split[i] != null || borrowed.Count == 0 || s_ParkingMode) continue;
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
                    bool tileParking = s_ParkingMode && IsParkingAtlasPiece(
                        m_Prefabs.TryGetPrefab<PrefabBase>(pieces[i].m_Piece, out var piecePrefab) && piecePrefab != null ? piecePrefab.name : null);
                    if (split[i] != null)
                    {
                        if (tileParking)
                            foreach (var part in split[i])
                                if ((part.m_PieceFlags & NetPieceFlags.DisableTiling) == 0) TileNegativeV(part.m_Piece);
                        result.AddRange(split[i]);
                        continue;
                    }
                    var copy = pieces[i];
                    copy.m_Piece = CopyPiece(pieces[i].m_Piece);
                    runtime.Created.Add(copy.m_Piece);
                    if (tileParking && (copy.m_PieceFlags & NetPieceFlags.DisableTiling) == 0) TileNegativeV(copy.m_Piece);
                    result.Add(copy);
                }
                AddCurbEndPieces(pieces, result, runtime);
                result.AddRange(m_ExtraPieces);
                m_ExtraPieces = null;
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
                if (s_ParkingMode) s_ParkingCornerMeshes.Add(mesh);
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

        private static float NodeT(float3 position, NetCompositionPiece piece)
        {
            var local = position;
            int flags = (int)piece.m_SectionFlags;
            local.z *= (flags & 0x100) != 0 ? 1f : -1f;
            if ((flags & 0x4000) != 0) local.z = local.z * 2f + piece.m_Size.z * 0.5f;
            local.z += piece.m_Offset.z;
            return (local.z / math.max(0.0001f, piece.m_Size.z) + 0.5f) * 2f;
        }

        private sealed class ParkingStrip
        {
            public Frame Frame;
            public float2 A, B;
            public string Info;
        }

        private static bool IsEdgeParkingPiece(string name) => !string.IsNullOrEmpty(name) &&
            (IsParkingAtlasPiece(name) || (name.IndexOf("Parking", StringComparison.OrdinalIgnoreCase) >= 0 && !IsRoadAtlasPiece(name)));

        private static List<float2> SeamPoints(MeshVertex[] positions, MeshUV0[] uvs, MeshIndex[] indices, int begin, int end,
            NetCompositionPiece piece, NetCompositionMeshData data, EdgeNodeGeometry geometry, NetCompositionData compositionData)
        {
            var points = new List<float2>();
            var seen = new HashSet<int>();
            for (int i = begin; i + 2 < end; i += 3)
            {
                int a = indices[i].m_Index, b = indices[i + 1].m_Index, c = indices[i + 2].m_Index;
                if (a < 0 || b < 0 || c < 0 || a >= positions.Length || b >= positions.Length || c >= positions.Length) continue;
                if (!IsSidewalkTop(positions[a].m_Vertex, positions[b].m_Vertex, positions[c].m_Vertex,
                    uvs[a].m_Uv, uvs[b].m_Uv, uvs[c].m_Uv)) continue;
                foreach (int v in new[] { a, b, c })
                {
                    if (!seen.Add(v)) continue;
                    float t = NodeT(positions[v].m_Vertex, piece);
                    if (t >= -0.02f && t <= 0.08f)
                        points.Add(ProjectToWorld(positions[v].m_Vertex, piece, data, geometry, compositionData).xz);
                }
            }
            return points;
        }

        private static float SegmentDistance(float2 p, float2 a, float2 b)
        {
            var ab = b - a;
            float t = math.saturate(math.dot(p - a, ab) / math.max(1e-6f, math.lengthsq(ab)));
            return math.distance(p, a + ab * t);
        }

        private Frame PickParkingStrip(MeshVertex[] positions, MeshUV0[] uvs, MeshIndex[] indices, int begin, int end,
            NetCompositionPiece piece, NetCompositionMeshData data, EdgeNodeGeometry geometry, NetCompositionData compositionData)
        {
            if (m_ParkingStrips == null || m_ParkingStrips.Count == 0) return null;
            var points = SeamPoints(positions, uvs, indices, begin, end, piece, data, geometry, compositionData);
            Frame best = null;
            float bestDistance = 0.6f;
            foreach (var strip in m_ParkingStrips)
                foreach (var point in points)
                {
                    float distance = SegmentDistance(point, strip.A, strip.B);
                    if (distance < bestDistance) { bestDistance = distance; best = strip.Frame; }
                }
            return best;
        }

        // Straight frames of a parking road corner, one per parking sidewalk strip of the straight road.
        private List<ParkingStrip> BuildParkingStrips(NetCompositionPiece[] nodePieces, NetCompositionMeshData nodeData,
            EdgeNodeGeometry geometry, NetCompositionData compositionData, EdgeGeometry edgeGeometry, bool start)
        {
            var result = new List<ParkingStrip>();
            string log = Mod.DebugBackendEnabled && m_ParkingLogged < 6 ? "" : null;
            try
            {
                var anchors = new List<float2>();
                foreach (var piece in nodePieces)
                {
                    if ((piece.m_PieceFlags & NetPieceFlags.PreserveShape) != 0) continue;
                    if (!m_Prefabs.TryGetPrefab<PrefabBase>(piece.m_Piece, out var prefab) || prefab == null ||
                        !IsParkingAtlasPiece(prefab.name) || !PieceLoaded(piece.m_Piece)) continue;
                    var positions = EntityManager.GetBuffer<MeshVertex>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray();
                    var uvs = EntityManager.GetBuffer<MeshUV0>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray();
                    var indices = EntityManager.GetBuffer<MeshIndex>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray();
                    if (uvs.Length != positions.Length) continue;
                    anchors.AddRange(SeamPoints(positions, uvs, indices, 0, indices.Length, piece, nodeData, geometry, compositionData));
                }

                var composition = m_ParkingEdgeComposition;
                if (anchors.Count == 0 || composition == Entity.Null || !EntityManager.HasComponent<NetCompositionMeshRef>(composition)) return result;
                var mesh = EntityManager.GetComponentData<NetCompositionMeshRef>(composition).m_Mesh;
                if (mesh == Entity.Null || !EntityManager.HasComponent<NetCompositionMeshData>(mesh) ||
                    !EntityManager.HasBuffer<NetCompositionPiece>(mesh)) return result;
                float width = EntityManager.GetComponentData<NetCompositionMeshData>(mesh).m_Width;
                if (width < 0.5f) return result;
                var segment = start ? edgeGeometry.m_Start : edgeGeometry.m_End;
                var left = start ? segment.m_Left.a : segment.m_Left.d;
                var right = start ? segment.m_Right.a : segment.m_Right.d;
                var tangent = start
                    ? MathUtils.StartTangent(segment.m_Left) + MathUtils.StartTangent(segment.m_Right)
                    : MathUtils.EndTangent(segment.m_Left) + MathUtils.EndTangent(segment.m_Right);
                var along = math.normalizesafe(tangent.xz);
                if (math.lengthsq(along) < 0.5f || math.distance(left.xz, right.xz) < 0.5f) return result;

                var edgePieces = EntityManager.GetBuffer<NetCompositionPiece>(mesh, true).ToNativeArray(Allocator.Temp).ToArray();
                var sumV = new Dictionary<int, float>();
                var sumZ = new Dictionary<int, float>();
                var candidates = new List<(float lxMin, float lxMax, float u0, float lx0, float duDlx, int material, float pieceDensity, string name)>();
                foreach (var piece in edgePieces)
                {
                    if ((piece.m_PieceFlags & (NetPieceFlags.PreserveShape | NetPieceFlags.DisableTiling)) != 0) continue;
                    if (!PieceLoaded(piece.m_Piece)) continue;
                    string name = m_Prefabs.TryGetPrefab<PrefabBase>(piece.m_Piece, out var prefab) && prefab != null ? prefab.name : null;
                    if (log != null) log += (log.Length > 0 ? ", " : "") + name;
                    var positions = EntityManager.GetBuffer<MeshVertex>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray();
                    var uvs = EntityManager.GetBuffer<MeshUV0>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray();
                    var indices = EntityManager.GetBuffer<MeshIndex>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray();
                    var slots = EntityManager.GetBuffer<MeshMaterial>(piece.m_Piece, true).ToNativeArray(Allocator.Temp).ToArray();
                    if (uvs.Length != positions.Length) continue;
                    float signX = ((int)piece.m_SectionFlags & 1) != 0 ? 1f : -1f;
                    foreach (var slot in slots)
                    {
                        int begin = slot.m_StartIndex, end = slot.m_StartIndex + slot.m_IndexCount;
                        if (begin < 0 || end > indices.Length || begin > end) continue;
                        float slotV = 0f, slotZ = 0f;
                        for (int i = begin; i + 2 < end; i += 3)
                        {
                            int a = indices[i].m_Index, b = indices[i + 1].m_Index, c = indices[i + 2].m_Index;
                            if (a < 0 || b < 0 || c < 0 || a >= positions.Length || b >= positions.Length || c >= positions.Length) continue;
                            float va = uvs[a].m_Uv.y, vb = uvs[b].m_Uv.y, vc = uvs[c].m_Uv.y;
                            if (va < 0f && vb < 0f && vc < 0f) { va += 1f; vb += 1f; vc += 1f; }
                            if (va < 0f || vb < 0f || vc < 0f) continue;
                            float za = positions[a].m_Vertex.z, zb = positions[b].m_Vertex.z, zc = positions[c].m_Vertex.z;
                            slotV += math.abs(vb - va) + math.abs(vc - vb) + math.abs(va - vc);
                            slotZ += math.abs(zb - za) + math.abs(zc - zb) + math.abs(za - zc);
                        }
                        sumV[slot.m_MaterialIndex] = (sumV.TryGetValue(slot.m_MaterialIndex, out float v) ? v : 0f) + slotV;
                        sumZ[slot.m_MaterialIndex] = (sumZ.TryGetValue(slot.m_MaterialIndex, out float z) ? z : 0f) + slotZ;
                        if (!IsEdgeParkingPiece(name)) continue;

                        float lxMin = float.MaxValue, lxMax = float.MinValue, duDlx = float.NaN, uRef = 0f, lxRef = 0f;
                        for (int i = begin; i + 2 < end; i += 3)
                        {
                            int a = indices[i].m_Index, b = indices[i + 1].m_Index, c = indices[i + 2].m_Index;
                            if (a < 0 || b < 0 || c < 0 || a >= positions.Length || b >= positions.Length || c >= positions.Length) continue;
                            var pa = positions[a].m_Vertex; var pb = positions[b].m_Vertex; var pc = positions[c].m_Vertex;
                            if (!IsSidewalkTop(pa, pb, pc, uvs[a].m_Uv, uvs[b].m_Uv, uvs[c].m_Uv)) continue;
                            foreach (var p in new[] { pa, pb, pc })
                            {
                                float lx = signX * p.x + piece.m_Offset.x;
                                lxMin = math.min(lxMin, lx);
                                lxMax = math.max(lxMax, lx);
                            }
                            if (!float.IsNaN(duDlx)) continue;
                            var ab = pb - pa; var ac = pc - pa;
                            float determinant = ab.x * ac.z - ac.x * ab.z;
                            if (math.abs(determinant) < 0.00001f) continue;
                            float du = uvs[b].m_Uv.x - uvs[a].m_Uv.x, dv = uvs[c].m_Uv.x - uvs[a].m_Uv.x;
                            float uPerX = (du * ac.z - dv * ab.z) / determinant;
                            if (math.abs(uPerX) < 1e-5f) continue;
                            duDlx = uPerX * signX;
                            uRef = uvs[a].m_Uv.x;
                            lxRef = signX * pa.x + piece.m_Offset.x;
                        }
                        if (float.IsNaN(duDlx) || lxMax - lxMin < 0.2f) continue;
                        float lx0 = (lxMin + lxMax) * 0.5f;
                        candidates.Add((lxMin, lxMax, uRef + duDlx * (lx0 - lxRef), lx0, duDlx, slot.m_MaterialIndex,
                            TemplateVDensity(positions, uvs, indices, begin, end, false, false), name));
                    }
                }
                if (candidates.Count == 0) return result;

                float bestScore = float.MaxValue;
                bool flip = false;
                for (int f = 0; f < 2; f++)
                {
                    var a3 = f == 1 ? right : left; var b3 = f == 1 ? left : right;
                    float score = 0f;
                    foreach (var c in candidates)
                    {
                        var e0 = math.lerp(a3, b3, c.lxMin / width + 0.5f).xz;
                        var e1 = math.lerp(a3, b3, c.lxMax / width + 0.5f).xz;
                        float d = float.MaxValue;
                        foreach (var anchor in anchors) d = math.min(d, math.min(math.distance(anchor, e0), math.distance(anchor, e1)));
                        score += math.min(d, 5f);
                    }
                    if (score < bestScore - 0.01f) { bestScore = score; flip = f == 1; }
                }

                var from = flip ? right : left; var to = flip ? left : right;
                var span = (to - from).xz;
                float spanLength = math.length(span);
                var lateral = span / spanLength;
                float det = lateral.x * along.y - along.x * lateral.y;
                if (math.abs(det) < 0.2f) return result;
                float2 total = edgeGeometry.m_Start.m_Length + edgeGeometry.m_End.m_Length;
                var roadSpan = (right - left).xz;
                foreach (var c in candidates)
                {
                    float density = sumZ.TryGetValue(c.material, out float z) && z > 0.000001f && sumV[c.material] > 0.000001f
                        ? sumV[c.material] / z : c.pieceDensity;
                    if (float.IsNaN(density)) density = math.abs(c.duDlx) * 0.5f;
                    var origin = math.lerp(from, to, c.lx0 / width + 0.5f).xz;
                    float uPerMetre = c.duDlx * width / spanLength;
                    float v0 = 0f;
                    if (!start)
                    {
                        float lateralPosition = math.saturate(math.dot(origin - left.xz, roadSpan) / math.max(0.0001f, math.lengthsq(roadSpan)));
                        v0 = density * math.lerp(total.x, total.y, lateralPosition);
                        v0 -= math.floor(v0);
                    }
                    result.Add(new ParkingStrip
                    {
                        A = math.lerp(from, to, c.lxMin / width + 0.5f).xz,
                        B = math.lerp(from, to, c.lxMax / width + 0.5f).xz,
                        Info = c.name + " " + (c.lxMax - c.lxMin).ToString("0.0") + " m, V/m " + density.ToString("0.0000") +
                            " (piece " + c.pieceDensity.ToString("0.0000") + ")",
                        Frame = new Frame
                        {
                            Origin = origin,
                            Uv = new float2(c.u0, v0),
                            UvX = new float2(uPerMetre * along.y / det, density * -lateral.y / det),
                            UvZ = new float2(uPerMetre * -along.x / det, density * lateral.x / det)
                        }
                    });
                }
                return result;
            }
            finally
            {
                if (log != null)
                {
                    m_ParkingLogged++;
                    var text = new System.Text.StringBuilder("Parking road corner: ").Append(result.Count).Append(" straight strips");
                    foreach (var strip in result) text.Append("; ").Append(strip.Info);
                    text.Append("; straight road pieces [").Append(log).Append("].");
                    Mod.LogLater(text.ToString());
                }
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
            if (s_UntaperWalk) UntaperWalk(EntityManager.GetBuffer<MeshVertex>(source, true).ToNativeArray(Allocator.Temp).ToArray(), positions, uvs);
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

        private static bool s_UntaperWalk;

        private static void UntaperWalk(MeshVertex[] template, MeshVertex[] positions, MeshUV0[] uvs)
        {
            if (template.Length != uvs.Length || positions.Length != uvs.Length) return;
            var seam = new List<float2>();
            for (int i = 0; i < template.Length; i++)
            {
                var t = template[i].m_Vertex;
                float u = uvs[i].m_Uv.x;
                if (math.abs(t.z) > 0.01f || u < LeftUv - UvEpsilon || u > RightUv + UvEpsilon) continue;
                seam.Add(new float2(t.x, u));
            }
            if (seam.Count < 2) return;
            seam.Sort((a, b) => a.x.CompareTo(b.x));
            if (seam[seam.Count - 1].x - seam[0].x < 0.05f) return;
            for (int i = 0; i < uvs.Length; i++)
            {
                float u = uvs[i].m_Uv.x;
                if (u < LeftUv - UvEpsilon || u > RightUv + UvEpsilon || template[i].m_Vertex.z < -0.01f) continue;
                float x = positions[i].m_Vertex.x;
                float mapped;
                if (x <= seam[0].x) mapped = seam[0].y;
                else if (x >= seam[seam.Count - 1].x) mapped = seam[seam.Count - 1].y;
                else
                {
                    int k = 1;
                    while (k < seam.Count - 1 && seam[k].x < x) k++;
                    var a = seam[k - 1]; var b = seam[k];
                    mapped = b.x - a.x > 0.0001f ? math.lerp(a.y, b.y, (x - a.x) / (b.x - a.x)) : a.y;
                }
                uvs[i] = new MeshUV0 { m_Uv = new float2(mapped, uvs[i].m_Uv.y) };
            }
        }

        private void ReleaseCreated(Runtime runtime)
        {
            foreach (var entity in runtime.Created)
            {
                s_RuntimeMeshes.Remove(entity);
                s_CornerMeshes.Remove(entity);
                s_ParkingCornerMeshes.Remove(entity);
                if (EntityManager.Exists(entity)) EntityManager.DestroyEntity(entity);
            }
            runtime.Created.Clear();
            runtime.Composition = Entity.Null;
        }

        // Splits a piece into rest and sidewalk part; the sidewalk part gets UVs from a straight frame in world space.
        private bool TrySplit(NetCompositionPiece piece, NetCompositionMeshData data, EdgeNodeGeometry geometry,
            NetCompositionData compositionData, EdgeGeometry edgeGeometry, bool start, List<Entity> created,
            List<SourceFrame> external, List<SourceFrame> framesOut, bool nameFilter,
            out NetCompositionPiece rest, out NetCompositionPiece walk, out bool hasRest, CapInfo capInfo = null)
        {
            rest = walk = piece;
            hasRest = false;
            var source = piece.m_Piece;
            var skipFlags = s_ParkingMode ? NetPieceFlags.PreserveShape : NetPieceFlags.PreserveShape | NetPieceFlags.DisableTiling;
            if ((piece.m_PieceFlags & skipFlags) != 0 ||
                (piece.m_SectionFlags & NetSectionFlags.Median) != 0) return false;
            if (!m_Prefabs.TryGetPrefab<PrefabBase>(source, out var prefab) || prefab == null) return false;
            if (nameFilter && !(s_ParkingMode ? IsParkingAtlasPiece(prefab.name) : IsRoadAtlasPiece(prefab.name))) return false;
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
            bool keepDensity = false;
            var sunkPositions = new List<float3>();
            var sunkNormals = new List<float3>();
            var sunkTangents = new List<float4>();
            var sunkUvs = new List<float2>();
            var sunkIndices = new List<int>();
            var sunkSlots = new List<MeshMaterial>();
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
                bool slotBorrowed = false;
                if (s_ParkingMode)
                {
                    frame = PickParkingStrip(positions, uvs, indices, begin, end, piece, data, geometry, compositionData);
                }
                else if (external != null)
                {
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
                    if (frame != null && m_SideMask != 3 &&
                        !SideAllowed(SlotCenter(positions, uvs, indices, begin, end, piece, data, geometry, compositionData))) frame = null;
                    if (frame != null && m_BorrowFrames != null)
                    {
                        var center = SlotCenter(positions, uvs, indices, begin, end, piece, data, geometry, compositionData);
                        var borrowed = NearestBorrowed(m_BorrowFrames, center, out float distance);
                        LogBorrow(center, distance, borrowed != null);
                        if (borrowed != null) { frame = borrowed; slotBorrowed = true; m_BorrowedSlots++; }
                    }
                }
                var remap = new Dictionary<int, int>();
                var asphalt = new List<int>();
                var sunkRemap = new Dictionary<int, int>();
                int sunkIndexBegin = sunkIndices.Count, sunkVertexBegin = sunkPositions.Count;
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
                        else if (!s_ParkingMode && !reshapedPiece && IsStretchedCurbTop(pa, pb, pc, uvs[a].m_Uv, uvs[b].m_Uv, uvs[c].m_Uv, piece, data, geometry, compositionData))
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
                        if (keepDensity)
                        {
                            foreach (int v in new[] { a, b, c })
                            {
                                if (!sunkRemap.TryGetValue(v, out int mapped))
                                {
                                    mapped = sunkPositions.Count;
                                    sunkRemap.Add(v, mapped);
                                    sunkPositions.Add(positions[v].m_Vertex - new float3(0f, 0.5f, 0f));
                                    sunkNormals.Add(normals[v].m_Normal);
                                    sunkTangents.Add(tangents[v].m_Tangent);
                                    sunkUvs.Add(uvs[v].m_Uv);
                                }
                                sunkIndices.Add(mapped);
                            }
                            m_DensityStandIns++;
                        }
                        continue;
                    }
                    if (frame != null && valid && s_ParkingMode &&
                        IsParkingAsphaltTop(positions[a].m_Vertex, positions[b].m_Vertex, positions[c].m_Vertex, uvs[a].m_Uv, uvs[b].m_Uv, uvs[c].m_Uv))
                    {
                        asphalt.Add(a); asphalt.Add(b); asphalt.Add(c);
                        continue;
                    }
                    restIndices.Add(indices[i].m_Index);
                    restIndices.Add(indices[i + 1].m_Index);
                    restIndices.Add(indices[i + 2].m_Index);
                }
                restSlots.Add(new MeshMaterial(restBegin, restIndices.Count - restBegin, slot.m_StartVertex, slot.m_VertexCount, slot.m_MaterialIndex));
                if (sunkIndices.Count > sunkIndexBegin)
                    sunkSlots.Add(new MeshMaterial(sunkIndexBegin, sunkIndices.Count - sunkIndexBegin,
                        sunkVertexBegin, sunkPositions.Count - sunkVertexBegin, slot.m_MaterialIndex));
                if (walkIndices.Count > walkIndexBegin)
                {
                    List<bool> miterSide = null;
                    Frame miterFrame = null;
                    Func<float3, float> miterTest = null;
                    bool sideRoadMiter = !s_ParkingMode && slotBorrowed;
                    if ((s_ParkingMode || sideRoadMiter) && m_MiterJunction != Entity.Null && walkPositions.Count > walkVertexBegin)
                    {
                        float2 centroid = 0f;
                        for (int v = walkVertexBegin; v < walkPositions.Count; v++)
                            centroid += ProjectToWorld(walkPositions[v], piece, data, geometry, compositionData).xz;
                        centroid /= walkPositions.Count - walkVertexBegin;
                        if (TryMiter(frame, centroid, edgeGeometry, start, sideRoadMiter, out var miterPoint, out var miterDirection, out float miterSign, out miterFrame))
                        {
                            if (sideRoadMiter)
                            {
                                miterFrame = ReflectFrame(frame, miterPoint, miterDirection);
                                miterSign = -miterSign;
                            }
                            var pieceCopy = piece;
                            float2 testPoint = miterPoint, testDirection = miterDirection;
                            float testSign = miterSign;
                            miterTest = t =>
                            {
                                var w = ProjectToWorld(t, pieceCopy, data, geometry, compositionData).xz - testPoint;
                                return (testDirection.x * w.y - testDirection.y * w.x) * testSign;
                            };
                            miterSide = SplitWalkAtLine(walkIndexBegin, walkVertexBegin, walkPositions, walkNormals, walkTangents, walkUvs, walkIndices,
                                miterTest);
                            m_MiterParts++;
                            int turned = 0;
                            foreach (bool side in miterSide) if (side) turned++;
                            LogMiter("split: " + turned + " of " + miterSide.Count + " vertices turned", centroid, miterPoint, miterDirection);
                        }
                    }
                    for (int v = walkVertexBegin; v < walkPositions.Count; v++)
                    {
                        var world = ProjectToWorld(walkPositions[v], piece, data, geometry, compositionData);
                        var atlas = (miterSide != null && miterSide[v - walkVertexBegin] ? miterFrame : frame).Sample(world);
                        if (atlas.x < s_WalkLeft + 0.0005f || atlas.x > s_WalkRight - 0.0005f)
                        {
                            m_Clamped++;
                            m_ClampExcess = math.max(m_ClampExcess, math.max(s_WalkLeft - atlas.x, atlas.x - s_WalkRight) * 1024f);
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
                        var exactPiece = piece;
                        var exactFrame = frame;
                        var exactMiter = miterFrame;
                        var exactTest = miterTest;
                        s_ExactUv = cutPoint => (exactTest != null && exactMiter != null && exactTest(cutPoint) > 0f ? exactMiter : exactFrame)
                            .Sample(ProjectToWorld(cutPoint, exactPiece, data, geometry, compositionData));
                        try
                        {
                            WrapStrip(walkIndexBegin, walkVertexBegin, walkPositions, walkNormals, walkTangents, walkUvs, walkIndices);
                            WrapCapV(walkIndexBegin, walkVertexBegin, walkPositions, walkNormals, walkTangents, walkUvs, walkIndices);
                        }
                        finally { s_ExactUv = null; }
                    }
                    walkSlots.Add(new MeshMaterial(walkIndexBegin, walkIndices.Count - walkIndexBegin,
                        walkVertexBegin, walkPositions.Count - walkVertexBegin, slot.m_MaterialIndex));
                }
                if (asphalt.Count > 0)
                {
                    int asphaltIndexBegin = walkIndices.Count, asphaltVertexBegin = walkPositions.Count;
                    var asphaltRemap = new Dictionary<int, int>();
                    foreach (int v in asphalt)
                        walkIndices.Add(Remap(v, asphaltRemap, positions, normals, tangents, walkPositions, walkNormals, walkTangents, walkUvs));
                    for (int v = asphaltVertexBegin; v < walkPositions.Count; v++)
                    {
                        var world = ProjectToWorld(walkPositions[v], piece, data, geometry, compositionData);
                        walkUvs[v] = s_SyncBlendMode == 6 ? new float2(0.1f, 0.5f) : frame.Sample(world);
                    }
                    if (s_SyncBlendMode != 6)
                    {
                        float keepLeft = s_WalkLeft, keepRight = s_WalkRight;
                        var asphaltPiece = piece;
                        var asphaltFrame = frame;
                        s_ExactUv = cutPoint => asphaltFrame.Sample(ProjectToWorld(cutPoint, asphaltPiece, data, geometry, compositionData));
                        s_WalkLeft = 0f;
                        s_WalkRight = ParkingAsphaltRightUv;
                        try
                        {
                            WrapStrip(asphaltIndexBegin, asphaltVertexBegin, walkPositions, walkNormals, walkTangents, walkUvs, walkIndices);
                            WrapCapV(asphaltIndexBegin, asphaltVertexBegin, walkPositions, walkNormals, walkTangents, walkUvs, walkIndices);
                        }
                        finally { s_WalkLeft = keepLeft; s_WalkRight = keepRight; s_ExactUv = null; }
                    }
                    walkSlots.Add(new MeshMaterial(asphaltIndexBegin, walkIndices.Count - asphaltIndexBegin,
                        asphaltVertexBegin, walkPositions.Count - asphaltVertexBegin, slot.m_MaterialIndex));
                    moved += asphalt.Count / 3;
                    m_AsphaltTriangles += asphalt.Count / 3;
                }
            }
            if (moved == 0) return false;

            if (capInfo == null && s_SyncBlendMode == 6)
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

            if (sunkIndices.Count > 0 && m_ExtraPieces != null)
            {
                var sunkEntity = CreatePiece(prefabData, meshData, sunkPositions, sunkNormals, sunkTangents, sunkUvs, sunkIndices, sunkSlots);
                created.Add(sunkEntity);
                var sunk = piece;
                sunk.m_Piece = sunkEntity;
                m_ExtraPieces.Add(sunk);
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

        private const bool UseMiter = false;
        private Entity m_MiterJunction, m_MiterRoad;

        private List<SourceFrame> m_BorrowFrames;
        private int m_BorrowedSlots, m_BorrowLogged;
        private const float BorrowRadius = 10f;
        private const float StraightDot = 0.85f;

        private static Frame NearestBorrowed(List<SourceFrame> frames, float2 center, out float distance)
        {
            distance = float.MaxValue;
            Frame best = null;
            if (frames == null || math.any(math.isnan(center))) return null;
            foreach (var item in frames)
            {
                float d = math.distance(item.Centroid, center);
                if (d < distance) { distance = d; best = item.Frame; }
            }
            return distance <= BorrowRadius ? best : null;
        }

        private void LogBorrow(float2 center, float distance, bool used)
        {
            if (!Mod.DebugBackendEnabled || m_BorrowLogged >= 40) return;
            m_BorrowLogged++;
            Mod.LogLater("Parking side road: " + (used ? "borrowed" : "kept own") + " frame | " + m_DumpLabel +
                " | slot centre " + center + " | nearest parking corner " + (distance < float.MaxValue ? distance.ToString("0.0") + " m" : "none"));
        }

        private bool EdgeDirection(Entity edge, bool start, out float2 direction)
        {
            direction = default;
            if (!EntityManager.HasComponent<Game.Net.Curve>(edge)) return false;
            var bezier = EntityManager.GetComponentData<Game.Net.Curve>(edge).m_Bezier;
            float2 d = start ? (bezier.b - bezier.a).xz : (bezier.c - bezier.d).xz;
            if (math.lengthsq(d) < 1e-6f) d = start ? (bezier.d - bezier.a).xz : (bezier.a - bezier.d).xz;
            if (math.lengthsq(d) < 1e-6f) return false;
            direction = math.normalize(d);
            return true;
        }

        // Side road at a T junction with a parking road running through: borrows its straight frames (L joint).
        private List<SourceFrame> BorrowParkingFrames(Entity road, bool start, Entity junction, out int signature)
        {
            signature = 0;
            if (!EntityManager.HasBuffer<ConnectedEdge>(junction) || !EdgeDirection(road, start, out var own)) return null;
            var edges = EntityManager.GetBuffer<ConnectedEdge>(junction, true).ToNativeArray(Allocator.Temp).ToArray();
            var parking = new List<(Runtime, float2)>();
            foreach (var item in edges)
            {
                var other = item.m_Edge;
                if (other == road || !EntityManager.HasComponent<Edge>(other)) continue;
                bool otherStart = EntityManager.GetComponentData<Edge>(other).m_Start == junction;
                if (!EdgeDirection(other, otherStart, out var direction)) continue;
                if (math.dot(own, direction) < -StraightDot) return null;
                if (m_Runtimes.TryGetValue((other, otherStart), out var runtime) && runtime.Parking && runtime.Frames.Count > 0)
                    parking.Add((runtime, direction));
            }
            bool through = false;
            for (int i = 0; i < parking.Count && !through; i++)
                for (int j = i + 1; j < parking.Count; j++)
                    if (math.dot(parking[i].Item2, parking[j].Item2) < -StraightDot) { through = true; break; }
            if (!through) return null;
            var list = new List<SourceFrame>();
            foreach (var pair in parking)
            {
                foreach (var item in pair.Item1.Frames)
                {
                    var frame = item.Frame != null ? ToRoadFrame(item.Frame) : null;
                    if (frame == null) continue;
                    list.Add(new SourceFrame { Frame = frame, Centroid = item.Centroid, MaterialIndex = AnyMaterial, MaxU = RightUv });
                }
                signature = signature * 31 + pair.Item1.Signature;
            }
            return list.Count > 0 ? list : null;
        }
        private int m_MiterParts;

        private static float Cross2(float2 a, float2 b) => a.x * b.y - a.y * b.x;

        // Frame mirrored at a line through point (unit direction): equal UVs on the line itself.
        private static Frame ReflectFrame(Frame frame, float2 point, float2 direction)
        {
            float m00 = 2f * direction.x * direction.x - 1f, m01 = 2f * direction.x * direction.y, m11 = 2f * direction.y * direction.y - 1f;
            return new Frame
            {
                Origin = point,
                Uv = frame.Sample(new float3(point.x, 0f, point.y)),
                UvX = frame.UvX * m00 + frame.UvZ * m01,
                UvZ = frame.UvX * m01 + frame.UvZ * m11
            };
        }

        private static int Quantize(float value) => (int)math.round(value * 4096f);

        private static int PatternSignature()
        {
            unchecked
            {
                int hash = SidewalkRoadAtlasSystem.RoadPatternValid ? 0x45d9f3b : 0x119de1f3;
                hash = hash * 31 + Quantize(SidewalkRoadAtlasSystem.RoadPatternRepeatsV);
                hash = hash * 31 + Quantize(SidewalkRoadAtlasSystem.RoadPatternShiftU);
                hash = hash * 31 + Quantize(SidewalkRoadAtlasSystem.RoadPatternShiftV);
                hash = hash * 31 + Quantize(ParkingRoadAtlasSystem.SidewalkPatternRepeatsU);
                hash = hash * 31 + Quantize(ParkingRoadAtlasSystem.SidewalkRepeatShiftRight);
                hash = hash * 31 + Quantize(ParkingRoadAtlasSystem.SidewalkRepeatShiftLeft);
                return hash * 31 + Quantize(ParkingRoadAtlasSystem.SidewalkPatternRepeatsV);
            }
        }
        private int m_MiterLogged;

        private void LogMiter(string what, float2 centroid, float2 own, float2 neighbour)
        {
            if (!Mod.DebugBackendEnabled || m_MiterLogged >= 40) return;
            m_MiterLogged++;
            string node = EntityManager.HasComponent<Game.Net.Node>(m_MiterJunction)
                ? EntityManager.GetComponentData<Game.Net.Node>(m_MiterJunction).m_Position.xz.ToString() : "?";
            Mod.LogLater("Parking corner miter: " + what + " | node " + node + " | slot centre " + centroid + " | own edge end " + own + " | neighbour edge end " + neighbour);
        }

        // Miter line through the crossing of both outer sidewalk edges, bisecting the corner.
        private bool TryMiter(Frame frame, float2 centroid, EdgeGeometry edgeGeometry, bool start, bool neighbourParking,
            out float2 point, out float2 direction, out float sign, out Frame rotated)
        {
            point = direction = default;
            sign = 0f;
            rotated = null;
            if (frame == null || frame.Seam != null || !EntityManager.HasBuffer<ConnectedEdge>(m_MiterJunction)) { LogMiter("no plain frame", centroid, default, default); return false; }
            var segment = start ? edgeGeometry.m_Start : edgeGeometry.m_End;
            float2 left = (start ? segment.m_Left.a : segment.m_Left.d).xz, right = (start ? segment.m_Right.a : segment.m_Right.d).xz;
            bool ownLeft = math.distancesq(left, centroid) <= math.distancesq(right, centroid);
            var ownCurve = ownLeft ? segment.m_Left : segment.m_Right;
            float2 a1 = ownLeft ? left : right;
            float2 d1 = math.normalizesafe((start ? MathUtils.StartTangent(ownCurve) : MathUtils.EndTangent(ownCurve)).xz);
            if (math.lengthsq(d1) < 0.5f) return false;
            float2 n1 = new float2(-d1.y, d1.x);
            if (math.dot(n1, (left + right) * 0.5f - a1) < 0f) n1 = -n1;

            float2 c1 = (left + right) * 0.5f;
            float best = 50f * 50f;
            float2 a2 = default, d2 = default, c2 = default;
            bool found = false;
            var edges = EntityManager.GetBuffer<ConnectedEdge>(m_MiterJunction, true).ToNativeArray(Allocator.Temp).ToArray();
            foreach (var item in edges)
            {
                var other = item.m_Edge;
                if (other == m_MiterRoad || !EntityManager.HasComponent<EdgeGeometry>(other) || !EntityManager.HasComponent<Edge>(other) ||
                    !EntityManager.HasComponent<PrefabRef>(other) || IsAlley(other)) continue;
                bool otherParking = m_Prefabs.TryGetPrefab<PrefabBase>(EntityManager.GetComponentData<PrefabRef>(other).m_Prefab, out var prefab) &&
                    prefab != null && IsParkingPrefab(prefab) && !HasRoadSidewalk(other);
                if (otherParking != neighbourParking) continue;
                var geometry = EntityManager.GetComponentData<EdgeGeometry>(other);
                bool otherStart = EntityManager.GetComponentData<Edge>(other).m_Start == m_MiterJunction;
                var otherSegment = otherStart ? geometry.m_Start : geometry.m_End;
                float2 l2 = (otherStart ? otherSegment.m_Left.a : otherSegment.m_Left.d).xz;
                float2 r2 = (otherStart ? otherSegment.m_Right.a : otherSegment.m_Right.d).xz;
                for (int side = 0; side < 2; side++)
                {
                    var curve = side == 0 ? otherSegment.m_Left : otherSegment.m_Right;
                    float2 a = side == 0 ? l2 : r2;
                    float2 d = math.normalizesafe((otherStart ? MathUtils.StartTangent(curve) : MathUtils.EndTangent(curve)).xz);
                    if (math.lengthsq(d) < 0.5f || math.abs(Cross2(d1, d)) < 0.5f) continue;
                    if (math.dot(a - c1, a1 - c1) <= 0f) continue;
                    float distance = math.distancesq(a, a1);
                    if (distance >= best) continue;
                    best = distance;
                    a2 = a; d2 = d; c2 = (l2 + r2) * 0.5f;
                    found = true;
                }
            }
            if (!found) { LogMiter("no neighbour side", centroid, a1, default); return false; }
            point = a1 + d1 * (Cross2(a2 - a1, d2) / Cross2(d1, d2));
            LogMiter("corner " + point + " normals " + n1 + " / " + new float2(-d2.y, d2.x), centroid, a1, a2);
            if (math.distancesq(point, centroid) > 40f * 40f) { LogMiter("corner point too far", centroid, a1, a2); return false; }
            float2 n2 = new float2(-d2.y, d2.x);
            if (math.dot(n2, c2 - a2) < 0f) n2 = -n2;
            direction = math.normalizesafe(n1 + n2);
            if (math.lengthsq(direction) < 0.5f) { LogMiter("opposite edge normals", centroid, a1, a2); return false; }
            sign = math.sign(Cross2(direction, a2 + n2 - point));
            if (sign == 0f) { LogMiter("no side", centroid, a1, a2); return false; }
            float theta = math.atan2(Cross2(n1, n2), math.dot(n1, n2));
            float c = math.cos(theta), s = math.sin(theta);
            rotated = new Frame
            {
                Origin = point,
                Uv = frame.Sample(new float3(point.x, 0f, point.y)),
                UvX = frame.UvX * c - frame.UvZ * s,
                UvZ = frame.UvX * s + frame.UvZ * c
            };
            return true;
        }

        // Clips the walk triangles of one slot at a line; returns per vertex whether it lies on the positive side.
        private static List<bool> SplitWalkAtLine(int indexBegin, int vertexBegin, List<float3> positions, List<float3> normals,
            List<float4> tangents, List<float2> uvs, List<int> indices, Func<float3, float> signedDistance)
        {
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
            var sides = new List<bool>();
            foreach (var tri in triangles)
            {
                var distances = new float[3];
                for (int k = 0; k < 3; k++) distances[k] = signedDistance(tri[k].Position);
                for (int pass = 0; pass < 2; pass++)
                {
                    bool positive = pass == 1;
                    var polygon = new List<WalkVertex>(5);
                    for (int k = 0; k < 3; k++)
                    {
                        var a = tri[k]; var b = tri[(k + 1) % 3];
                        float da = positive ? distances[k] : -distances[k];
                        float db = positive ? distances[(k + 1) % 3] : -distances[(k + 1) % 3];
                        if (da >= 0f) polygon.Add(a);
                        if ((da >= 0f) != (db >= 0f))
                            polygon.Add(WalkVertex.Lerp(a, b, ExactCut(a.Position, b.Position, da / (da - db), signedDistance)));
                    }
                    if (polygon.Count < 3) continue;
                    int first = positions.Count;
                    foreach (var vertex in polygon)
                    {
                        positions.Add(vertex.Position);
                        normals.Add(vertex.Normal);
                        tangents.Add(vertex.Tangent);
                        uvs.Add(vertex.Uv);
                        sides.Add(positive);
                    }
                    for (int n = 1; n + 1 < polygon.Count; n++)
                    {
                        if (math.lengthsq(math.cross(polygon[n].Position - polygon[0].Position,
                            polygon[n + 1].Position - polygon[0].Position)) < 1e-12f) continue;
                        indices.Add(first); indices.Add(first + n); indices.Add(first + n + 1);
                    }
                }
            }
            return sides;
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

        // Keeps U inside the atlas strip: repeat (RoadEU), shifted repeat or mirror (parking).
        private void WrapStrip(int indexBegin, int vertexBegin, List<float3> positions, List<float3> normals,
            List<float4> tangents, List<float2> uvs, List<int> indices)
        {
            float left = s_WalkLeft, right = s_WalkRight;
            float width = right - left;
            bool mirror = s_ParkingMode;
            float shiftRight = ParkingRoadAtlasSystem.SidewalkRepeatShiftRight, shiftLeft = ParkingRoadAtlasSystem.SidewalkRepeatShiftLeft;
            bool shifted = s_ParkingMode && math.abs(left - ParkingLeftUv) < 1e-5f && math.abs(right - ParkingRightUv) < 1e-5f &&
                shiftRight > 0.001f && shiftLeft > 0.001f && shiftRight < width && shiftLeft < width;
            if (shifted) mirror = false;
            bool outside = false;
            for (int v = vertexBegin; v < uvs.Count; v++)
                if (uvs[v].x < left || uvs[v].x > right) { outside = true; break; }
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
                int k0 = shifted ? ShiftedCell(u0, left, right, shiftLeft, shiftRight) : (int)math.floor((u0 - left) / width);
                int k1 = shifted ? ShiftedCell(u1, left, right, shiftLeft, shiftRight) : (int)math.floor((u1 - left) / width);
                if (k1 - k0 > 8) k1 = k0 + 8;
                for (int k = k0; k <= k1; k++)
                {
                    float lo = left + k * width, hi = lo + width, move = -k * width;
                    if (shifted)
                    {
                        if (k == 0) { lo = left; hi = right; move = 0f; }
                        else if (k > 0) { lo = right + (k - 1) * shiftRight; hi = lo + shiftRight; move = -k * shiftRight; }
                        else { hi = left + (-k - 1) * -shiftLeft; lo = hi - shiftLeft; move = -k * shiftLeft; }
                    }
                    bool flip = mirror && (k & 1) != 0;
                    var polygon = new List<WalkVertex>(tri);
                    polygon = ClipU(polygon, lo, true);
                    polygon = ClipU(polygon, hi, false);
                    if (polygon.Count < 3) continue;
                    int first = positions.Count;
                    foreach (var vertex in polygon)
                    {
                        positions.Add(vertex.Position);
                        normals.Add(vertex.Normal);
                        tangents.Add(flip ? -vertex.Tangent : vertex.Tangent);
                        float local = vertex.Uv.x - lo;
                        float u = math.clamp(shifted ? vertex.Uv.x + move : left + (flip ? width - local : local), left + 0.0005f, right - 0.0005f);
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

        // Cell of the shifted repeat: 0 = inside the strip, k > 0 = k-th repeat beyond the right end
        private static int ShiftedCell(float u, float left, float right, float shiftLeft, float shiftRight)
        {
            if (u > right) return (int)math.ceil((u - right) / shiftRight);
            if (u < left) return -(int)math.ceil((left - u) / shiftLeft);
            return 0;
        }

        // Cuts at whole V and stores each part in [-1, 0) (the game shifts DisableTiling V by ceil(max V)).
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
                var fold = UFold(tri);
                for (int tile = firstTile; tile <= lastTile; tile++)
                {
                    var polygon = ClipV(new List<WalkVertex>(tri), tile, true, fold);
                    polygon = ClipV(polygon, tile + 1f, false, fold);
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

        private static List<WalkVertex> ClipV(List<WalkVertex> polygon, float edge, bool keepAbove, float2 fold)
        {
            var result = new List<WalkVertex>(polygon.Count + 2);
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count];
                float da = keepAbove ? a.Uv.y - edge : edge - a.Uv.y;
                float db = keepAbove ? b.Uv.y - edge : edge - b.Uv.y;
                if (da >= 0f) result.Add(a);
                if ((da >= 0f) != (db >= 0f)) result.Add(ExactSplit(a, b, da / (da - db), 1, edge, fold));
            }
            return result;
        }

        // Cut points at the projected position: the game bends node pieces per vertex, linear cuts would be off.
        private static Func<float3, float2> s_ExactUv;

        private static float ExactCut(float3 a, float3 b, float linear, Func<float3, float> value)
        {
            if (value == null) return linear;
            float va = value(a), vb = value(b);
            if (!math.isfinite(va) || !math.isfinite(vb) || (va >= 0f) == (vb >= 0f)) return linear;
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 24; i++)
            {
                float mid = (lo + hi) * 0.5f;
                float vm = value(math.lerp(a, b, mid));
                if (!math.isfinite(vm)) return linear;
                if ((vm >= 0f) == (va >= 0f)) lo = mid; else hi = mid;
            }
            return (lo + hi) * 0.5f;
        }

        private static WalkVertex ExactSplit(WalkVertex a, WalkVertex b, float linear, int axis, float edge, float2 fold)
        {
            var exact = s_ExactUv;
            if (exact == null) return WalkVertex.Lerp(a, b, linear);
            float t = ExactCut(a.Position, b.Position, linear, p => exact(p)[axis] - edge);
            var vertex = WalkVertex.Lerp(a, b, t);
            var uv = exact(vertex.Position);
            if (!math.all(math.isfinite(uv))) return WalkVertex.Lerp(a, b, linear);
            float u = fold.x * uv.x + fold.y;
            if (axis == 0) u = edge;
            else u = math.clamp(u, math.min(a.Uv.x, b.Uv.x) - 0.002f, math.max(a.Uv.x, b.Uv.x) + 0.002f);
            vertex.Uv = new float2(u, axis == 1 ? edge : uv.y);
            return vertex;
        }

        // How a triangle's displayed U relates to its real U after WrapStrip (repeat: +1, mirror: -1, plus an offset).
        private static float2 UFold(WalkVertex[] tri)
        {
            var exact = s_ExactUv;
            if (exact == null) return new float2(1f, 0f);
            var real = new float[3];
            for (int k = 0; k < 3; k++)
            {
                real[k] = exact(tri[k].Position).x;
                if (!math.isfinite(real[k])) return new float2(1f, 0f);
            }
            float alpha = 1f, spread = 0f;
            for (int i = 0; i < 3; i++)
                for (int j = i + 1; j < 3; j++)
                {
                    float d = real[i] - real[j];
                    if (math.abs(d) <= spread) continue;
                    spread = math.abs(d);
                    alpha = (tri[i].Uv.x - tri[j].Uv.x) / d < 0f ? -1f : 1f;
                }
            float beta = 0f;
            for (int k = 0; k < 3; k++) beta += tri[k].Uv.x - alpha * real[k];
            return new float2(alpha, beta / 3f);
        }

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
                    result.Add(ExactSplit(a, b, da / (da - db), 0, edge, new float2(1f, 0f)));
            }
            return result;
        }

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
            if (m_CurbLogged >= 0) return;
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
            foreach (var item in frames) if (item.MaterialIndex == materialIndex || item.MaterialIndex == AnyMaterial) return true;
            return false;
        }

        private const float CurbLeftUv = 273f / 1024f;
        private const float CurbStripWidth = 0.3f;

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

        private int m_WorldDumps, m_ParkingDumps;
        private bool m_DumpNeighbour, m_DumpAlleyEnd;
        private int m_AlleyEndDumps;
        private readonly HashSet<Entity> m_ParkingSidewalkEnds = new HashSet<Entity>();
        private readonly Dictionary<Entity, (Entity, bool)> m_ParkingEndCorners = new Dictionary<Entity, (Entity, bool)>();
        private readonly List<(Entity, (Entity, bool))> m_ParkingAlleyCorners = new List<(Entity, (Entity, bool))>();

        private bool HasAlleyArm(Entity node)
        {
            var edges = EntityManager.GetBuffer<ConnectedEdge>(node, true);
            for (int i = 0; i < edges.Length; i++) if (IsAlley(edges[i].m_Edge)) return true;
            return false;
        }
        private const int AnyMaterial = int.MinValue;
        private const float RoadUPerMetre = 64f / 1024f;
        private const float RoadVPerMetre = 0.0313f;

        // Parking frame converted to the RoadEU atlas with the same slab pattern.
        private static Frame ToRoadFrame(Frame parking)
        {
            float repeatsU = ParkingRoadAtlasSystem.SidewalkPatternRepeatsU, repeatsV = ParkingRoadAtlasSystem.SidewalkPatternRepeatsV;
            float roadRepeatsV = SidewalkRoadAtlasSystem.RoadPatternRepeatsV;
            if (SidewalkRoadAtlasSystem.RoadPatternValid && repeatsU > 0f && repeatsV > 0f && roadRepeatsV > 0f)
            {
                const float strip = RightUv - LeftUv;
                float ku = repeatsU * strip;
                float au = LeftUv + (-ParkingLeftUv * repeatsU - SidewalkRoadAtlasSystem.RoadPatternShiftU) * strip;
                float whole = math.floor(parking.Uv.y);
                float kv = repeatsV / roadRepeatsV;
                float av = (-repeatsV * whole - SidewalkRoadAtlasSystem.RoadPatternShiftV) / roadRepeatsV;
                return new Frame
                {
                    Origin = parking.Origin,
                    Uv = new float2(au + ku * parking.Uv.x, av + kv * parking.Uv.y),
                    UvX = new float2(ku * parking.UvX.x, kv * parking.UvX.y),
                    UvZ = new float2(ku * parking.UvZ.x, kv * parking.UvZ.y)
                };
            }
            var gradientU = new float2(parking.UvX.x, parking.UvZ.x);
            var gradientV = new float2(parking.UvX.y, parking.UvZ.y);
            float lengthU = math.length(gradientU), lengthV = math.length(gradientV);
            if (lengthU < 1e-6f || lengthV < 1e-6f) return null;
            float scaleU = RoadUPerMetre / lengthU, scaleV = RoadVPerMetre / lengthV;
            return new Frame
            {
                Origin = parking.Origin,
                Uv = new float2(LeftUv + (parking.Uv.x - ParkingLeftUv) * scaleU, parking.Uv.y * scaleV),
                UvX = new float2(parking.UvX.x * scaleU, parking.UvX.y * scaleV),
                UvZ = new float2(parking.UvZ.x * scaleU, parking.UvZ.y * scaleV)
            };
        }
        private string m_DumpLabel = "";
        private void DumpWorld(List<NetCompositionPiece> pieces, NetCompositionMeshData data, EdgeNodeGeometry geometry,
            NetCompositionData compositionData, List<SourceFrame> frames, bool side, bool start)
        {
            bool parkingDump = ((s_ParkingMode || m_DumpNeighbour) && m_ParkingDumps < 60) || (m_DumpAlleyEnd && m_AlleyEndDumps < 8);
            if (m_DumpAlleyEnd) m_AlleyEndDumps++;
            if (m_WorldDumps >= 40 && !parkingDump) return;
            try
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                var text = new System.Text.StringBuilder();
                text.AppendLine("MESH " + m_WorldDumps + " " + (side ? "side" : "main") + " " + (start ? "start" : "end") + " " + data.m_Flags.m_General +
                    (parkingDump ? " || " + m_DumpLabel : ""));
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
                            if (parkingDump) text.Append(string.Format(inv, " v {0:0.####}", v < uvs.Length ? uvs[v].m_Uv.y : float.NaN));
                        }
                        text.AppendLine();
                    }
                }
                string folder = System.IO.Path.Combine(Application.persistentDataPath, "ModsData", "Enhanced_Graphics_and_Textures", "Diagnostics");
                System.IO.Directory.CreateDirectory(folder);
                if (parkingDump)
                {
                    string parkingFile = System.IO.Path.Combine(folder, "JunctionParking.txt");
                    if (m_ParkingDumps == 0) System.IO.File.WriteAllText(parkingFile, text.ToString());
                    else System.IO.File.AppendAllText(parkingFile, text.ToString());
                    m_ParkingDumps++;
                    return;
                }
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
                                var line = new System.Text.StringBuilder(string.Format(inv, "  {0} | {1} | {2:0.##} | {3:0.##} | piece {4} | size.z {5:0.##}", name, piece.m_SectionFlags, piece.m_Offset.x, piece.m_Size.x, piece.m_PieceFlags, piece.m_Size.z));
                                foreach (var slot in EntityManager.GetBuffer<MeshMaterial>(piece.m_Piece, true).ToNativeArray(Allocator.Temp))
                                {
                                    int top = 0;
                                    float u0 = float.MaxValue, u1 = float.MinValue, x0 = float.MaxValue, x1 = float.MinValue, sumV = 0f, sumZ = 0f;
                                    float vMin = float.MaxValue, vMax = float.MinValue; int negativeTop = 0;
                                    for (int i = slot.m_StartIndex; i + 2 < slot.m_StartIndex + slot.m_IndexCount && i + 2 < indices.Length; i += 3)
                                    {
                                        int a = indices[i].m_Index, b = indices[i + 1].m_Index, c = indices[i + 2].m_Index;
                                        if (a < 0 || b < 0 || c < 0 || a >= positions.Length || b >= positions.Length || c >= positions.Length) continue;
                                        var n = math.cross(positions[b].m_Vertex - positions[a].m_Vertex, positions[c].m_Vertex - positions[a].m_Vertex);
                                        float l = math.length(n);
                                        if (l < 1e-6f || math.abs(n.y) / l < 0.65f) continue;
                                        top++;
                                        if (uvs[a].m_Uv.y < 0f || uvs[b].m_Uv.y < 0f || uvs[c].m_Uv.y < 0f) negativeTop++;
                                        foreach (int v in new[] { a, b, c })
                                        {
                                            u0 = math.min(u0, uvs[v].m_Uv.x); u1 = math.max(u1, uvs[v].m_Uv.x);
                                            vMin = math.min(vMin, uvs[v].m_Uv.y); vMax = math.max(vMax, uvs[v].m_Uv.y);
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
                                    if (top > 0)
                                        line.Append(string.Format(inv, ", V {0:0.####}..{1:0.####}, {2} top with V<0", vMin, vMax, negativeTop));
                                }
                                text.AppendLine(line.ToString());
                            }
                        }
                    }
                }
                finally { roads.Dispose(); }
                AppendParkingEdgeLengths(text, inv);
                string folder = System.IO.Path.Combine(Application.persistentDataPath, "ModsData", "Enhanced_Graphics_and_Textures", "Diagnostics");
                System.IO.Directory.CreateDirectory(folder);
                string file = System.IO.Path.Combine(folder, "SidewalkScale.txt");
                System.IO.File.WriteAllText(file, text.ToString());
                Mod.log.Info("Sidewalk scale report written to " + file);
            }
            catch (Exception ex) { Mod.log.Warn("Sidewalk scale report failed: " + ex.Message); }
        }

        private void AppendParkingEdgeLengths(System.Text.StringBuilder text, IFormatProvider inv)
        {
            text.AppendLine();
            text.AppendLine("== Parking road edges: entity | edge length L/R m | start node length L/R m, radius | end node length L/R m, radius | edge mesh | start mesh | end mesh | start/end connections");
            var roads = m_Roads.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var road in roads)
                {
                    var prefabEntity = EntityManager.GetComponentData<PrefabRef>(road).m_Prefab;
                    if (!m_Prefabs.TryGetPrefab<PrefabBase>(prefabEntity, out var prefab) || prefab == null || !IsParkingPrefab(prefab)) continue;
                    if (!EntityManager.HasComponent<EdgeGeometry>(road)) continue;
                    var edgeGeometry = EntityManager.GetComponentData<EdgeGeometry>(road);
                    float2 edgeLength = edgeGeometry.m_Start.m_Length + edgeGeometry.m_End.m_Length;
                    var composition = EntityManager.GetComponentData<Composition>(road);
                    var edge = EntityManager.GetComponentData<Edge>(road);
                    string NodePart(bool start)
                    {
                        bool has = start ? EntityManager.HasComponent<StartNodeGeometry>(road) : EntityManager.HasComponent<EndNodeGeometry>(road);
                        if (!has) return "none";
                        var g = start ? EntityManager.GetComponentData<StartNodeGeometry>(road).m_Geometry
                            : EntityManager.GetComponentData<EndNodeGeometry>(road).m_Geometry;
                        float2 l = g.m_Left.m_Length + g.m_Right.m_Length;
                        return string.Format(inv, "{0:0.0}/{1:0.0}, r {2:0.0}", l.x, l.y, g.m_MiddleRadius);
                    }
                    string MeshId(Entity c) => c != Entity.Null && EntityManager.HasComponent<NetCompositionMeshRef>(c)
                        ? EntityManager.GetComponentData<NetCompositionMeshRef>(c).m_Mesh.Index.ToString(inv) : "-";
                    int Connections(Entity node) => node != Entity.Null && EntityManager.HasBuffer<ConnectedEdge>(node)
                        ? EntityManager.GetBuffer<ConnectedEdge>(node, true).Length : 0;
                    text.AppendLine(string.Format(inv, "  {0} {1} | {2:0.0}/{3:0.0} | {4} | {5} | {6} | {7} | {8} | {9}/{10}",
                        prefab.name, road.Index, edgeLength.x, edgeLength.y, NodePart(true), NodePart(false),
                        MeshId(composition.m_Edge), MeshId(composition.m_StartNode), MeshId(composition.m_EndNode),
                        Connections(edge.m_Start), Connections(edge.m_End)));
                }
            }
            finally { roads.Dispose(); }
        }

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
            float density = (uWalkOuter - uWalkInner) / walkWidth;
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

            var xs = new List<float>();
            foreach (int i in cutRow)
            {
                var q = result[i].m_Vertex;
                if (q.y < -0.01f || q.x < lo || q.x > hi) continue;
                if (!xs.Exists(x => math.abs(x - q.x) < 0.002f)) xs.Add(q.x);
            }
            if (xs.Count < 2) return false;
            xs.Sort((a, b) => math.abs(a - xr).CompareTo(math.abs(b - xr)));

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

            float lo = math.min(xr, xo) - 0.002f, hi = math.max(xr, xo) + 0.002f;
            foreach (int i in endList)
            {
                var q = result[i].m_Vertex;
                if (uvs[i].m_Uv.x < CurbLeftUv - UvEpsilon && q.x > lo && q.x < hi && math.abs(q.x - xr) > 0.002f)
                    result[i] = new MeshVertex { m_Vertex = new float3(q.x, q.y, cut) };
            }
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

            float uWalk = LeftUv + 0.05f;
            for (int k = 1; k + 1 < tInner.Count; k++)
                add(fill, P3(tInner[0], 0f), new float2(uWalk, 0f), P3(tInner[k], 0f), new float2(uWalk, 0f),
                    P3(tInner[k + 1], 0f), new float2(uWalk, 0f), true);
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
            if (u0 < s_WalkLeft - UvEpsilon || u1 > s_WalkRight + UvEpsilon) return false;
            var n = math.cross(b - a, c - a);
            float length = math.length(n);
            return length > 0.000001f && math.abs(n.y) / length >= 0.65f;
        }

        private static bool IsParkingAsphaltTop(float3 a, float3 b, float3 c, float2 ua, float2 ub, float2 uc)
        {
            if (math.max(ua.x, math.max(ub.x, uc.x)) > ParkingAsphaltRightUv + UvEpsilon) return false;
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
            else vPerMetre = math.abs(uPerMetre) * 0.5f;
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

        // Approximates the game's node deformation: template position -> world position.
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

        // Blend of the node sync targets along the node: t² for parking corners, 1-(1-t)² at the alley bend, else t.
        private static float SyncWeight(float t)
        {
            int mode = s_SyncBlendMode != 0 ? s_SyncBlendMode : s_BendSync ? 3 : s_SquareSync ? 2 : 0;
            switch (mode)
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