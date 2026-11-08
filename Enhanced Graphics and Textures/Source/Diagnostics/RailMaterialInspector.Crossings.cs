using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Rendering;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Advanced_Graphics
{
    public partial class RailMaterialInspector
    {
        private const int MaxCrossingMeshes = 48;
        private const int RoadConnection = 1;
        private const int RailConnection = 2;
        private const int MixedConnection = RoadConnection | RailConnection;

        private void ExportCrossings()
        {
            var prefabs = World.GetExistingSystemManaged<PrefabSystem>();
            var batchManager = World.GetExistingSystemManaged<BatchManagerSystem>();
            if (prefabs == null || batchManager == null) return;

            var query = GetEntityQuery(ComponentType.ReadOnly<Edge>(), ComponentType.ReadOnly<Composition>(),
                ComponentType.ReadOnly<PrefabRef>(), ComponentType.Exclude<Deleted>());
            using var edges = query.ToEntityArray(Allocator.Temp);
            var networks = new Dictionary<Entity, (string Name, bool IsRail)>();
            var connections = new Dictionary<Entity, int>();
            foreach (var entity in edges)
            {
                var prefabEntity = EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab;
                string name = GetPrefabName(prefabs, prefabEntity);
                bool road = EntityManager.HasComponent<Game.Net.Road>(entity);
                bool rail = !road && (Contains(name, "train") || Contains(name, "rail") || Contains(name, "track"));
                if (!road && !rail) continue;
                networks.Add(entity, (name, rail));
                var edge = EntityManager.GetComponentData<Edge>(entity);
                int kind = road ? RoadConnection : RailConnection;
                AddCrossingNode(connections, edge.m_Start, kind);
                AddCrossingNode(connections, edge.m_End, kind);
            }

            var report = new StringBuilder();
            report.AppendLine("EGT level crossing composition and batch survey");
            report.AppendLine("Only loaded render batches are reported. Temp denotes tool preview geometry.");
            report.AppendLine("EGT textures enabled: " + Mod.ActiveSetting?.TexturesEnabled);
            var meshes = new HashSet<Entity>();
            var observations = new HashSet<(Entity, string, string, bool)>();
            foreach (var entity in edges)
            {
                if (!networks.TryGetValue(entity, out var network)) continue;
                var edge = EntityManager.GetComponentData<Edge>(entity);
                var composition = EntityManager.GetComponentData<Composition>(entity);
                bool start = connections.TryGetValue(edge.m_Start, out int startKind) && startKind == MixedConnection;
                bool end = connections.TryGetValue(edge.m_End, out int endKind) && endKind == MixedConnection;
                bool temp = EntityManager.HasComponent<Temp>(entity);
                AddCrossingMesh(composition.m_Edge, "edge", start || end, network.IsRail, network.Name, temp, meshes, observations);
                AddCrossingMesh(composition.m_StartNode, "start", start, network.IsRail, network.Name, temp, meshes, observations);
                AddCrossingMesh(composition.m_EndNode, "end", end, network.IsRail, network.Name, temp, meshes, observations);
                if (meshes.Count >= MaxCrossingMeshes)
                {
                    report.AppendLine("Mesh limit reached.");
                    break;
                }
            }

            foreach (var (mesh, network, part, temp) in observations)
                report.AppendLine($"{mesh} | {network} | {part} | {(temp ? "temp" : "built")}");

            var groups = batchManager.GetNativeBatchGroups(readOnly: true, out var dependencies);
            dependencies.Complete();
            var batches = batchManager.GetManagedBatches();
            foreach (var mesh in meshes)
            {
                var data = EntityManager.GetComponentData<NetCompositionMeshData>(mesh);
                report.AppendLine();
                report.AppendLine($"{mesh} | flags: {data.m_Flags.m_General}");
                if (!EntityManager.HasBuffer<NetCompositionPiece>(mesh)) continue;
                var pieces = EntityManager.GetBuffer<NetCompositionPiece>(mesh, true);
                foreach (var piece in pieces)
                {
                    report.AppendLine($"  Piece: {GetPrefabName(prefabs, piece.m_Piece)} | {piece.m_SectionFlags} | {piece.m_PieceFlags}");
                    if (!EntityManager.HasBuffer<MeshMaterial>(piece.m_Piece)) continue;
                    foreach (var slot in EntityManager.GetBuffer<MeshMaterial>(piece.m_Piece, true))
                        AppendCrossingSlot(report, piece.m_Piece, slot);
                }
            }

            int count = 0;
            for (int groupIndex = 0; groupIndex < groups.GetGroupCount(); groupIndex++)
            {
                if (!groups.IsValidGroup(groupIndex)) continue;
                var group = groups.GetGroupData(groupIndex);
                if (group.m_MeshType != MeshType.Net || !meshes.Contains(group.m_Mesh)) continue;
                for (int batchIndex = 0; batchIndex < groups.GetBatchCount(groupIndex); batchIndex++)
                {
                    int index = groups.GetManagedBatchIndex(groupIndex, batchIndex);
                    if (index < 0 || index >= batches.BatchCount || !(batches.GetBatch(index) is CustomBatch batch))
                        continue;
                    count++;
                    report.AppendLine();
                    report.AppendLine($"{group.m_Mesh} | render batch {index}");
                    AppendCrossingMaterial(report, "Active", batch.material);
                    if (batch.loadedMaterial != batch.material)
                        AppendCrossingMaterial(report, "Loaded", batch.loadedMaterial);
                }
            }

            report.AppendLine();
            report.AppendLine($"Matched meshes: {meshes.Count} | loaded batches: {count}");
            Directory.CreateDirectory(DiagnosticFolder);
            File.WriteAllText(Path.Combine(DiagnosticFolder, "RailCrossings.txt"), report.ToString());
        }

        private static void AddCrossingNode(Dictionary<Entity, int> nodes, Entity node, int kind)
        {
            if (node == Entity.Null) return;
            nodes.TryGetValue(node, out int current);
            nodes[node] = current | kind;
        }

        private void AddCrossingMesh(Entity composition, string part, bool mixed, bool rail, string network, bool temp,
            HashSet<Entity> meshes, HashSet<(Entity, string, string, bool)> observations)
        {
            if (meshes.Count >= MaxCrossingMeshes || composition == Entity.Null ||
                !EntityManager.HasComponent<NetCompositionMeshRef>(composition)) return;
            var mesh = EntityManager.GetComponentData<NetCompositionMeshRef>(composition).m_Mesh;
            if (mesh == Entity.Null || !EntityManager.HasComponent<NetCompositionMeshData>(mesh)) return;
            string flags = EntityManager.GetComponentData<NetCompositionMeshData>(mesh).m_Flags.m_General.ToString();
            if (!mixed && !Contains(flags, "levelcross") && !(rail && Contains(flags, "crossing"))) return;
            meshes.Add(mesh);
            observations.Add((mesh, network, part, temp));
        }

        private static string GetPrefabName(PrefabSystem prefabs, Entity entity) =>
            prefabs.TryGetPrefab<PrefabBase>(entity, out var prefab) && prefab != null ? prefab.name : "<unknown>";

        private void AppendCrossingSlot(StringBuilder report, Entity piece, MeshMaterial slot)
        {
            report.Append($"    Material index: {slot.m_MaterialIndex} | indices: {slot.m_IndexCount}");
            if (EntityManager.HasBuffer<MeshIndex>(piece) && EntityManager.HasBuffer<MeshUV0>(piece))
            {
                var indices = EntityManager.GetBuffer<MeshIndex>(piece, true);
                var uvs = EntityManager.GetBuffer<MeshUV0>(piece, true);
                var min = new float2(float.PositiveInfinity);
                var max = new float2(float.NegativeInfinity);
                int end = math.min(indices.Length, slot.m_StartIndex + slot.m_IndexCount);
                for (int i = math.max(0, slot.m_StartIndex); i < end; i++)
                {
                    int vertex = indices[i].m_Index;
                    if (vertex < 0 || vertex >= uvs.Length) continue;
                    var uv = uvs[vertex].m_Uv;
                    min = math.min(min, uv);
                    max = math.max(max, uv);
                }
                if (min.x <= max.x && min.y <= max.y)
                    report.Append($" | UV0: {min} to {max}");
            }
            report.AppendLine();
        }

        private static void AppendCrossingMaterial(StringBuilder report, string label, Material material)
        {
            if (material == null || material.shader == null)
            {
                report.AppendLine($"  {label}: <missing material or shader>");
                return;
            }

            report.AppendLine($"  {label}: {material.name} | {material.shader.name} | keywords: {string.Join(", ", material.shaderKeywords)}");
            if (material.HasProperty("_BaseColor"))
                report.AppendLine($"    _BaseColor = {material.GetColor("_BaseColor")}");
            foreach (string slot in material.GetTexturePropertyNames())
            {
                var texture = material.GetTexture(slot);
                report.AppendLine(texture == null
                    ? $"    {slot} = <empty>"
                    : $"    {slot} = {texture.name} ({texture.width}x{texture.height}) | " +
                      $"scale={material.GetTextureScale(slot)} | offset={material.GetTextureOffset(slot)}");
            }
        }
    }
}
