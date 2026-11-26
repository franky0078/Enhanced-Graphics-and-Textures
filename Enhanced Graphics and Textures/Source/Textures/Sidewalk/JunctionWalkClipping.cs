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
    // Clipping and wrapping of sidewalk triangles at strip and cell borders.
    public partial class SidewalkJunctionMeshSystem
    {
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
    }
}
