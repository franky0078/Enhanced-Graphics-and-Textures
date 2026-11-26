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
    // Runtime copies of node compositions and their pieces.
    public partial class SidewalkJunctionMeshSystem
    {
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
    }
}
