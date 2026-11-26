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
    // Splits pieces into rest and sidewalk part with UVs from a straight frame.
    public partial class SidewalkJunctionMeshSystem
    {
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
    }
}
