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
    // Frames, triangle tests and the game's node deformation.
    public partial class SidewalkJunctionMeshSystem
    {
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
}
