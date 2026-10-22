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

        protected virtual bool IsTargetRoad(string name) => name != null &&
            name.StartsWith("Alley", StringComparison.Ordinal) &&
            name.IndexOf("Parking", StringComparison.OrdinalIgnoreCase) < 0;

        protected virtual bool IsTargetRoad(PrefabBase prefab) => prefab != null &&
            (IsTargetRoad(prefab.name) || (RoadBuilderSurfaceCompatibility.IsRoadBuilderRoad(prefab) &&
                !RoadBuilderSurfaceCompatibility.HasParkingLanes(prefab)));

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

        protected int ApplyIsolatedAlleyBatches()
        {
            var batchManager = World.GetExistingSystemManaged<BatchManagerSystem>();
            var prefabs = World.GetExistingSystemManaged<PrefabSystem>();
            if (batchManager == null || prefabs == null) return 0;

            var targetMeshes = new HashSet<Entity>();
            var otherMeshes = new HashSet<Entity>();
            var targetPrefabs = new Dictionary<Entity, bool>();
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
                        isAlley = prefabs.TryGetPrefab<PrefabBase>(prefabEntity, out var prefab) &&
                            prefab != null && IsTargetRoad(prefab);
                        targetPrefabs.Add(prefabEntity, isAlley);
                    }
                    var composition = EntityManager.GetComponentData<Composition>(road);
                    var meshes = isAlley ? targetMeshes : otherMeshes;
                    AddAlleyCompositionMesh(composition.m_Edge, meshes);
                    AddAlleyCompositionMesh(composition.m_StartNode, meshes);
                    AddAlleyCompositionMesh(composition.m_EndNode, meshes);
                }
            }
            finally { roads.Dispose(); }

            // One mesh can be shared at a junction.
            int shared = 0;
            foreach (var mesh in targetMeshes) if (otherMeshes.Contains(mesh)) shared++;
            targetMeshes.ExceptWith(otherMeshes);

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
                for (int batchIndex = 0; batchIndex < groups.GetBatchCount(groupIndex); batchIndex++)
                {
                    int index = groups.GetManagedBatchIndex(groupIndex, batchIndex);
                    if (index < 0 || index >= batches.BatchCount) continue;
                    var batch = batches.GetBatch(index) as CustomBatch;
                    if (batch == null || batch.loadedMaterial == null ||
                        !m_AlleyCurrentMaterialSources.Contains(batch.loadedMaterial) ||
                        !m_AlleyMaterialCopies.TryGetValue(batch.loadedMaterial, out var copy)) continue;
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
                    Mod.log.Info(IsolatedSurfaceLabel + " batch isolation: " + applied + " batches adjusted; " + shared +
                        " shared composition meshes kept original. Other road types excluded.");
                }
            }
            return applied;
        }

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
            foreach (var copy in m_AlleyMaterialCopies.Values)
                if (copy != null) UnityEngine.Object.Destroy(copy);
            m_AlleyMaterialCopies.Clear();
            m_AlleyCurrentMaterialSources.Clear();
            m_LastIsolatedBatches = -1;
            m_LastSharedAlleyMeshes = -1;
        }
    }
}


