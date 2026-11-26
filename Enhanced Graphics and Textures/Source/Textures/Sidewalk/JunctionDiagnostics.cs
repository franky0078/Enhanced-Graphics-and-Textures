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
    // Debug inspection, dumps and reports (log output option / debug mode only).
    public partial class SidewalkJunctionMeshSystem
    {
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

        private void LogBorrow(float2 center, float distance, bool used)
        {
            if (!Mod.DebugBackendEnabled || m_BorrowLogged >= 40) return;
            m_BorrowLogged++;
            Mod.LogLater("Parking side road: " + (used ? "borrowed" : "kept own") + " frame | " + m_DumpLabel +
                " | slot centre " + center + " | nearest parking corner " + (distance < float.MaxValue ? distance.ToString("0.0") + " m" : "none"));
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

        private static string Fmt(float3 v) => "(" + v.x.ToString("0.00") + ";" + v.y.ToString("0.00") + ";" + v.z.ToString("0.00") + ")";

        private static string RowCounts(MeshVertex[] positions)
        {
            var text = new System.Text.StringBuilder();
            foreach (var pair in RampRows(positions)) text.Append(" z").Append(pair.Key.ToString("0.#")).Append(':').Append(pair.Value.Count);
            return text.ToString();
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

        private int m_WorldDumps, m_ParkingDumps;
        private bool m_DumpNeighbour, m_DumpAlleyEnd;
        private int m_AlleyEndDumps;

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
    }
}
