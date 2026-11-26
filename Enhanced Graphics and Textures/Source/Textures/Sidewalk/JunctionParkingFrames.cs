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
    // Parking strips, borrowed and mitered frames, atlas pattern conversion.
    public partial class SidewalkJunctionMeshSystem
    {
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

        private const int AnyMaterial = int.MinValue;
        private const float RoadUPerMetre = 64f / 1024f;
        internal const float RoadVPerMetre = 0.0313f;

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
    }
}
