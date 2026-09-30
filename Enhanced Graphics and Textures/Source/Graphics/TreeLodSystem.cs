using System;
using System.Collections.Generic;
using Colossal.Rendering;
using Game;
using Game.Prefabs;
using Game.Rendering;
using Unity.Collections;
using Unity.Entities;

namespace Advanced_Graphics
{
    /// <summary>
    /// Adjusts the LOD thresholds actually read by BatchDataSystem. The game
    /// copies mesh thresholds into NativeBatchGroups when it builds render
    /// batches; changing MeshData after that copy does not change the picture.
    /// </summary>
    public partial class TreeLodSystem : GameSystemBase
    {
        private struct OriginalBatch
        {
            public Entity Mesh;
            public byte MinLod;
        }

        private EntityQuery m_Trees;
        private BatchManagerSystem m_BatchManager;
        private readonly Dictionary<long, OriginalBatch> m_Original = new Dictionary<long, OriginalBatch>();
        private int m_Percent = 100;
        private int m_LastTreeCount = -1;
        private int m_LastGroupCount = -1;
        private bool m_Dirty = true;
        private int m_Frame;

        public int DistancePercent
        {
            get => m_Percent;
            set
            {
                var percent = Math.Max(10, Math.Min(300, value));
                if (m_Percent == percent) return;
                m_Percent = percent;
                m_Dirty = true;
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Trees = GetEntityQuery(
                ComponentType.ReadOnly<TreeData>(),
                ComponentType.ReadOnly<SubMesh>());
            m_BatchManager = World.GetExistingSystemManaged<BatchManagerSystem>();
        }

        protected override void OnUpdate()
        {
            if (!m_Dirty && ++m_Frame % 60 != 0) return;
            ApplyChanges();
        }

        private void ApplyChanges()
        {
            if (m_BatchManager == null)
                m_BatchManager = World.GetExistingSystemManaged<BatchManagerSystem>();
            if (m_BatchManager == null) return;

            var groups = m_BatchManager.GetNativeBatchGroups(readOnly: false, out var dependencies);
            dependencies.Complete();
            var treeCount = m_Trees.CalculateEntityCount();
            var groupCount = groups.GetGroupCount();
            if (!m_Dirty && treeCount == m_LastTreeCount && groupCount == m_LastGroupCount) return;

            m_Dirty = false;
            m_LastTreeCount = treeCount;
            m_LastGroupCount = groupCount;

            var treeMeshes = new HashSet<Entity>();
            var treeGroups = new HashSet<int>();
            var trees = m_Trees.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var tree in trees)
                {
                    var meshes = EntityManager.GetBuffer<SubMesh>(tree, true);
                    foreach (var subMesh in meshes)
                    {
                        var mesh = subMesh.m_SubMesh;
                        if (!treeMeshes.Add(mesh) || !EntityManager.HasBuffer<BatchGroup>(mesh)) continue;
                        var batchGroups = EntityManager.GetBuffer<BatchGroup>(mesh, true);
                        foreach (var batchGroup in batchGroups)
                        {
                            if (batchGroup.m_Type == MeshType.Object)
                                treeGroups.Add(batchGroup.m_GroupIndex);
                        }
                    }
                }
            }
            finally
            {
                trees.Dispose();
            }

            // CalculateDistanceFactor(lod) = 2^((128-lod)/6): six LOD
            var offset = (int)Math.Round(6.0 * Math.Log(m_Percent / 100.0, 2.0));
            foreach (var groupIndex in treeGroups)
            {
                if (groupIndex < 0 || groupIndex >= groupCount || !groups.IsValidGroup(groupIndex)) continue;
                var group = groups.GetGroupData(groupIndex);
                if (group.m_MeshType != MeshType.Object) continue;

                var batchCount = groups.GetBatchCount(groupIndex);
                for (var batchIndex = 0; batchIndex < batchCount; batchIndex++)
                {
                    var batch = groups.GetBatchData(groupIndex, batchIndex);
                    var key = ((long)groupIndex << 32) | (uint)batchIndex;
                    if (!m_Original.TryGetValue(key, out var original) || original.Mesh != group.m_Mesh)
                    {
                        original = new OriginalBatch { Mesh = group.m_Mesh, MinLod = batch.m_MinLod };
                        m_Original[key] = original;
                    }

                    var target = (byte)Math.Max(0, Math.Min(255, original.MinLod - offset));
                    if (batch.m_MinLod == target) continue;
                    batch.m_MinLod = target;
                    groups.SetBatchData(groupIndex, batchIndex, batch);
                }
            }
        }

        public void RestoreNow()
        {
            m_Percent = 100;
            m_Dirty = true;
            ApplyChanges();
            m_Original.Clear();
        }
    }
}

