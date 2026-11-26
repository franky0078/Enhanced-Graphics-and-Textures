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
    // Curb ramps and curb ends of side roads without sidewalk.
    public partial class SidewalkJunctionMeshSystem
    {
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
    }
}
