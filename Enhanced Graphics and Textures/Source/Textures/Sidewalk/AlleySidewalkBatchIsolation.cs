using System;
using System.Collections.Generic;
using Game.Net;
using Game.Prefabs;
using Game.Rendering;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Advanced_Graphics
{
    public partial class SidewalkRoadAtlasSystem
    {
        private sealed class AlleyBatchBinding
        {
            public int Index;
            public Material Original;
            public Material Copy;
        }

        private readonly Dictionary<Material, Material> m_AlleyMaterialCopies = new Dictionary<Material, Material>();
        private readonly HashSet<Material> m_AlleyCurrentMaterialSources = new HashSet<Material>();
        private readonly Dictionary<CustomBatch, AlleyBatchBinding> m_AlleyBatchBindings = new Dictionary<CustomBatch, AlleyBatchBinding>();
        private int m_LastIsolatedBatches = -1;
        private int m_LastSharedAlleyMeshes = -1;

        protected virtual string IsolatedSurfaceLabel => "Alley";

        protected virtual bool IsTargetRoad(string name) => name != null && (IsAlleyAtlas
            ? name.StartsWith("Alley", StringComparison.Ordinal) && name.IndexOf("Parking", StringComparison.OrdinalIgnoreCase) < 0
            : !name.StartsWith("Alley", StringComparison.Ordinal) && (name.IndexOf("Parking", StringComparison.OrdinalIgnoreCase) < 0 ||
                name.StartsWith("Large Road", StringComparison.Ordinal)));

        protected virtual bool IsTargetRoad(PrefabBase prefab) => prefab != null &&
            (RoadBuilderSurfaceCompatibility.IsRoadBuilderRoad(prefab)
                ? IsAlleyAtlas && !RoadBuilderSurfaceCompatibility.HasParkingLanes(prefab)
                : IsTargetRoad(prefab.name));

        internal void RefreshWorldspaceBindings()
        {
            foreach (var pair in m_AlleyMaterialCopies)
                CopyWorldspaceBindings(pair.Key, pair.Value);
            // Endcaps inherit from the isolated copies, so update them second.
            foreach (var pair in m_EndcapMaterials)
                CopyWorldspaceBindings(pair.Key.Item1, pair.Value);
        }

        private static void CopyWorldspaceBindings(Material source, Material target)
        {
            if (source == null || target == null) return;
            CopyWorldspaceSlot(source, target, "_WorldspaceAlbedo");
            CopyWorldspaceSlot(source, target, "_WorldspaceNormalMap");
        }

        private static void CopyWorldspaceSlot(Material source, Material target, string slot)
        {
            if (!source.HasProperty(slot) || !target.HasProperty(slot)) return;
            target.SetTexture(slot, source.GetTexture(slot));
            target.SetTextureScale(slot, source.GetTextureScale(slot));
            target.SetTextureOffset(slot, source.GetTextureOffset(slot));
        }

        protected void BeginIsolatedMaterials() => m_AlleyCurrentMaterialSources.Clear();

        protected void PrepareIsolatedAlleyMaterial(Material original, Texture2D color, Texture2D normal, bool virtualTexturing)
        {
            if (!m_AlleyMaterialCopies.TryGetValue(original, out var copy))
            {
                copy = new Material(original)
                {
                    name = "Enhanced Graphics and Textures " + IsolatedSurfaceLabel + " isolated " + original.name,
                    hideFlags = HideFlags.HideAndDontSave
                };
                m_AlleyMaterialCopies.Add(original, copy);
            }
            copy.CopyPropertiesFromMaterial(original);
            copy.SetTexture(ColorSlot, color);
            if (normal != null) copy.SetTexture(NormalSlot, normal);
            if (virtualTexturing) copy.DisableKeyword("ENABLE_VT");
            m_AlleyCurrentMaterialSources.Add(original);
        }

        protected int ApplyIsolatedAlleyBatches(bool nodesOnly = false)
        {
            var batchManager = World.GetExistingSystemManaged<BatchManagerSystem>();
            var prefabs = World.GetExistingSystemManaged<PrefabSystem>();
            if (batchManager == null || prefabs == null) return 0;

            var targetMeshes = new HashSet<Entity>();
            var edgeMeshes = new HashSet<Entity>();
            var otherMeshes = new HashSet<Entity>();
            var targetPrefabs = new Dictionary<Entity, bool>();
            var roadEndOnlyPrefabs = new HashSet<Entity>();
            var roadEndOnlyMeshes = new HashSet<Entity>();
            var compatibleNodePrefabs = new Dictionary<Entity, bool>();
            var query = GetEntityQuery(ComponentType.ReadOnly<Edge>(), ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.ReadOnly<Composition>());
            var roads = query.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var road in roads)
                {
                    var prefabEntity = EntityManager.GetComponentData<PrefabRef>(road).m_Prefab;
                    if (!targetPrefabs.TryGetValue(prefabEntity, out bool isAlley))
                    {
                        bool found = prefabs.TryGetPrefab<PrefabBase>(prefabEntity, out var prefab) && prefab != null;
                        isAlley = found && IsTargetRoad(prefab);
                        if (!isAlley && found && nodesOnly && UsesRoadEndTiling && RoadBuilderSurfaceCompatibility.IsRoadBuilderRoad(prefab))
                        {
                            isAlley = true;
                            roadEndOnlyPrefabs.Add(prefabEntity);
                        }
                        targetPrefabs.Add(prefabEntity, isAlley);
                        compatibleNodePrefabs.Add(prefabEntity, IsAlleyAtlas && prefab != null &&
                            prefab.name != null && prefab.name.StartsWith("Alley", StringComparison.Ordinal) &&
                            prefab.name.IndexOf("Parking", StringComparison.OrdinalIgnoreCase) >= 0);
                    }
                    var composition = EntityManager.GetComponentData<Composition>(road);
                    var meshes = isAlley ? targetMeshes : otherMeshes;
                    AddAlleyCompositionMesh(composition.m_Edge, meshes);
                    AddAlleyCompositionMesh(composition.m_Edge, edgeMeshes);
                    if (isAlley || !compatibleNodePrefabs[prefabEntity])
                    {
                        AddAlleyCompositionMesh(composition.m_StartNode, meshes);
                        AddAlleyCompositionMesh(composition.m_EndNode, meshes);
                    }
                    if (roadEndOnlyPrefabs.Contains(prefabEntity))
                    {
                        AddAlleyCompositionMesh(composition.m_StartNode, roadEndOnlyMeshes);
                        AddAlleyCompositionMesh(composition.m_EndNode, roadEndOnlyMeshes);
                    }
                }
            }
            finally { roads.Dispose(); }

            // One mesh can be shared at a junction.
            int shared = 0;
            foreach (var mesh in targetMeshes) if (otherMeshes.Contains(mesh)) shared++;
            targetMeshes.ExceptWith(otherMeshes);
            if (IsAlleyAtlas) targetMeshes.RemoveWhere(SidewalkJunctionMeshSystem.IsRuntimeMesh);

            var groups = batchManager.GetNativeBatchGroups(readOnly: false, out var dependencies);
            dependencies.Complete();
            var batches = batchManager.GetManagedBatches();
            var active = new HashSet<CustomBatch>();
            int applied = 0;
            for (int groupIndex = 0; groupIndex < groups.GetGroupCount(); groupIndex++)
            {
                if (!groups.IsValidGroup(groupIndex)) continue;
                var group = groups.GetGroupData(groupIndex);
                if (group.m_MeshType != MeshType.Net || !targetMeshes.Contains(group.m_Mesh)) continue;
                bool node = !edgeMeshes.Contains(group.m_Mesh);
                if (nodesOnly && !node) continue;
                bool deadEnd = node && IsDeadEndMesh(group.m_Mesh);
                bool largeRoad = node && UsesLargeRoadTiling && IsLargeRoadMesh(group.m_Mesh);
                // Only "Round road ends" set: leave every other node mesh exactly as before (no swap).
                if (nodesOnly && !deadEnd && (!(largeRoad ? LargeEndcapActive : NormalEndcapActive) || roadEndOnlyMeshes.Contains(group.m_Mesh))) continue;
                for (int batchIndex = 0; batchIndex < groups.GetBatchCount(groupIndex); batchIndex++)
                {
                    int index = groups.GetManagedBatchIndex(groupIndex, batchIndex);
                    if (index < 0 || index >= batches.BatchCount) continue;
                    var batch = batches.GetBatch(index) as CustomBatch;
                    if (batch == null || batch.loadedMaterial == null ||
                        !m_AlleyCurrentMaterialSources.Contains(batch.loadedMaterial) ||
                        !m_AlleyMaterialCopies.TryGetValue(batch.loadedMaterial, out var copy)) continue;
                    if (node) copy = GetEndcapMaterial(copy, deadEnd, largeRoad);
                    // Respect loading placeholders and replacements owned by another mod.
                    if (batch.material != batch.loadedMaterial && batch.material != copy) continue;
                    if (!m_AlleyBatchBindings.TryGetValue(batch, out var binding))
                    {
                        binding = new AlleyBatchBinding { Index = index, Original = batch.loadedMaterial, Copy = copy };
                        m_AlleyBatchBindings.Add(batch, binding);
                    }
                    binding.Index = index;
                    binding.Original = batch.loadedMaterial;
                    binding.Copy = copy;
                    batches.SetMaterial(index, copy, groups);
                    active.Add(batch);
                    applied++;
                }
            }

            var removed = new List<CustomBatch>();
            foreach (var entry in m_AlleyBatchBindings)
            {
                if (active.Contains(entry.Key)) continue;
                var binding = entry.Value;
                // Managed indices can be recycled as roads are demolished or rebuilt.
                if (binding.Index >= 0 && binding.Index < batches.BatchCount &&
                    ReferenceEquals(batches.GetBatch(binding.Index), entry.Key) &&
                    entry.Key.material == binding.Copy && binding.Original != null)
                    batches.SetMaterial(binding.Index, binding.Original, groups);
                removed.Add(entry.Key);
            }
            foreach (var batch in removed) m_AlleyBatchBindings.Remove(batch);

            if (applied != m_LastIsolatedBatches || shared != m_LastSharedAlleyMeshes)
            {
                // Keep change tracking independent of optional diagnostic logging.
                m_LastIsolatedBatches = applied;
                m_LastSharedAlleyMeshes = shared;
                if (Mod.DebugBackendEnabled)
                {
                    Mod.LogLater(IsolatedSurfaceLabel + " batch isolation: " + applied + " batches adjusted; " + shared +
                        " shared composition meshes kept original. Other road types excluded.");
                }
            }
            return applied;
        }

        private bool IsLargeRoadMesh(Entity mesh) => mesh != Entity.Null && EntityManager.HasComponent<NetCompositionMeshData>(mesh) &&
            EntityManager.GetComponentData<NetCompositionMeshData>(mesh).m_Width >= 32f;

        private bool IsDeadEndMesh(Entity mesh) => mesh != Entity.Null && EntityManager.HasComponent<NetCompositionMeshData>(mesh) &&
            (EntityManager.GetComponentData<NetCompositionMeshData>(mesh).m_Flags.m_General & CompositionFlags.General.DeadEnd) != 0;

        private void AddAlleyCompositionMesh(Entity composition, HashSet<Entity> meshes)
        {
            if (composition == Entity.Null || !EntityManager.HasComponent<NetCompositionMeshRef>(composition)) return;
            var mesh = EntityManager.GetComponentData<NetCompositionMeshRef>(composition).m_Mesh;
            if (mesh != Entity.Null) meshes.Add(mesh);
        }

        protected void RestoreIsolatedAlleyBatches()
        {
            if (m_AlleyBatchBindings.Count > 0 && World != null && World.IsCreated)
            {
                var batchManager = World.GetExistingSystemManaged<BatchManagerSystem>();
                if (batchManager != null)
                {
                    var groups = batchManager.GetNativeBatchGroups(readOnly: false, out var dependencies);
                    dependencies.Complete();
                    var batches = batchManager.GetManagedBatches();
                    foreach (var entry in m_AlleyBatchBindings)
                    {
                        var binding = entry.Value;
                        if (binding.Index >= 0 && binding.Index < batches.BatchCount &&
                            ReferenceEquals(batches.GetBatch(binding.Index), entry.Key) &&
                            entry.Key.material == binding.Copy && binding.Original != null)
                            batches.SetMaterial(binding.Index, binding.Original, groups);
                    }
                }
            }
            m_AlleyBatchBindings.Clear();
            ReleaseEndcapScaling();
            foreach (var copy in m_AlleyMaterialCopies.Values)
                if (copy != null) UnityEngine.Object.Destroy(copy);
            m_AlleyMaterialCopies.Clear();
            m_AlleyCurrentMaterialSources.Clear();
            m_LastIsolatedBatches = -1;
            m_LastSharedAlleyMeshes = -1;
        }
    }
}





