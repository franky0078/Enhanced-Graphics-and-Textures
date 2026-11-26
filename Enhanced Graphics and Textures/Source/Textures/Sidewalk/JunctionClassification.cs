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
    // Which roads and nodes get straight sidewalk slabs (alleys, parking roads, crossing arms).
    public partial class SidewalkJunctionMeshSystem
    {
        private readonly Dictionary<Entity, bool> m_SidewalkFreeMeshes = new Dictionary<Entity, bool>();
        private readonly Dictionary<Entity, bool> m_RoadSidewalkMeshes = new Dictionary<Entity, bool>();

        private bool HasRoadSidewalk(Entity edge)
        {
            if (!EntityManager.HasComponent<Composition>(edge)) return false;
            var composed = EntityManager.GetComponentData<Composition>(edge).m_Edge;
            if (composed == Entity.Null || !EntityManager.HasComponent<NetCompositionMeshRef>(composed)) return false;
            var mesh = EntityManager.GetComponentData<NetCompositionMeshRef>(composed).m_Mesh;
            if (mesh == Entity.Null || !EntityManager.HasBuffer<NetCompositionPiece>(mesh)) return false;
            if (m_RoadSidewalkMeshes.TryGetValue(mesh, out bool found)) return found;
            found = false;
            var pieces = EntityManager.GetBuffer<NetCompositionPiece>(mesh, true);
            for (int i = 0; i < pieces.Length && !found; i++)
            {
                if (!m_Prefabs.TryGetPrefab<PrefabBase>(pieces[i].m_Piece, out var prefab) || prefab == null || prefab.name == null) continue;
                string name = prefab.name;
                found = name.IndexOf("Sidewalk", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    name.IndexOf("Side Piece", StringComparison.OrdinalIgnoreCase) < 0 && !IsParkingAtlasPiece(name);
            }
            m_RoadSidewalkMeshes[mesh] = found;
            return found;
        }

        private bool IsAlley(Entity edge)
        {
            if (!EntityManager.HasComponent<Game.Net.Road>(edge) || !EntityManager.HasComponent<Composition>(edge)) return false;
            var composed = EntityManager.GetComponentData<Composition>(edge).m_Edge;
            if (composed == Entity.Null || !EntityManager.HasComponent<NetCompositionMeshRef>(composed)) return false;
            var mesh = EntityManager.GetComponentData<NetCompositionMeshRef>(composed).m_Mesh;
            if (mesh == Entity.Null || !EntityManager.HasBuffer<NetCompositionPiece>(mesh)) return false;
            if (m_SidewalkFreeMeshes.TryGetValue(mesh, out bool free)) return free;
            free = true;
            var pieces = EntityManager.GetBuffer<NetCompositionPiece>(mesh, true);
            for (int i = 0; i < pieces.Length && free; i++)
            {
                if (m_Prefabs.TryGetPrefab<PrefabBase>(pieces[i].m_Piece, out var prefab) && prefab != null && prefab.name != null &&
                    prefab.name.IndexOf("Sidewalk", StringComparison.OrdinalIgnoreCase) >= 0)
                    free = false;
            }
            m_SidewalkFreeMeshes[mesh] = free;
            return free;
        }

        private bool AtAlleyJunction(Entity node)
        {
            if (!EntityManager.HasBuffer<ConnectedEdge>(node)) return false;
            var edges = EntityManager.GetBuffer<ConnectedEdge>(node, true);
            if (edges.Length != 3 && edges.Length != 2) return false;
            int alleys = 0;
            for (int i = 0; i < edges.Length; i++) if (IsAlley(edges[i].m_Edge)) alleys++;
            return alleys >= 1 && alleys < edges.Length;
        }

        private bool AtAlleyJunction(Entity road, Entity node, out int sides)
        {
            sides = 3;
            if (!EntityManager.HasBuffer<ConnectedEdge>(node)) return false;
            var edges = EntityManager.GetBuffer<ConnectedEdge>(node, true);
            if (edges.Length <= 3) return AtAlleyJunction(node);
            sides = 0;
            var arms = new List<(float, Entity)>(edges.Length);
            for (int i = 0; i < edges.Length; i++)
            {
                var other = edges[i].m_Edge;
                if (!EntityManager.HasComponent<Edge>(other)) return false;
                bool start = EntityManager.GetComponentData<Edge>(other).m_Start == node;
                if (!EdgeDirection(other, start, out var direction)) return false;
                arms.Add((math.atan2(direction.y, direction.x), other));
            }
            arms.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            int index = arms.FindIndex(a => a.Item2 == road);
            if (index < 0) return false;
            var previous = arms[(index + arms.Count - 1) % arms.Count].Item2;
            var next = arms[(index + 1) % arms.Count].Item2;
            if (previous == road || next == road || IsAlley(road)) return false;
            sides = (IsAlley(previous) ? 1 : 0) | (IsAlley(next) ? 2 : 0);
            return sides != 0;
        }

        private int m_SideMask = 3;
        private float2 m_SideNode, m_SideDirection;

        private void SetSideFilter(Entity road, bool start, Entity node, int mask)
        {
            m_SideMask = 3;
            if (mask == 3 || !EntityManager.HasComponent<Game.Net.Node>(node) || !EdgeDirection(road, start, out var direction)) return;
            m_SideMask = mask;
            m_SideNode = EntityManager.GetComponentData<Game.Net.Node>(node).m_Position.xz;
            m_SideDirection = direction;
        }

        private bool SideAllowed(float2 center)
        {
            if (m_SideMask == 3) return true;
            if (math.any(math.isnan(center))) return false;
            float side = Cross2(m_SideDirection, center - m_SideNode);
            return side > 0f ? (m_SideMask & 2) != 0 : (m_SideMask & 1) != 0;
        }

        private static bool IsParkingRoad(string name) => name != null &&
            (name.StartsWith("Alley", StringComparison.Ordinal) || name.StartsWith("Small Road", StringComparison.Ordinal) ||
             name.StartsWith("Medium Road", StringComparison.Ordinal)) &&
            name.IndexOf(" - ", StringComparison.Ordinal) >= 0 &&
            name.IndexOf("Parking", StringComparison.OrdinalIgnoreCase) >= 0;

        // Parking road by name, or a Road Builder road with parking lanes (same rule as the parking atlas).
        private readonly Dictionary<PrefabBase, bool> m_ParkingPrefabs = new Dictionary<PrefabBase, bool>();

        private bool IsParkingPrefab(PrefabBase prefab)
        {
            if (prefab == null) return false;
            if (m_ParkingPrefabs.TryGetValue(prefab, out bool parking)) return parking;
            parking = IsParkingRoad(prefab.name);
            if (!parking)
            {
                try { parking = RoadBuilderSurfaceCompatibility.IsRoadBuilderRoad(prefab) && RoadBuilderSurfaceCompatibility.HasParkingLanes(prefab); }
                catch (Exception) { parking = false; }
            }
            m_ParkingPrefabs[prefab] = parking;
            return parking;
        }

        // Junction of a parking road with other roads (or the transition into a road of another kind).
        private bool AtParkingJunction(Entity road, Entity prefabEntity, Entity node)
        {
            if (!EntityManager.HasBuffer<ConnectedEdge>(node)) return false;
            var edges = EntityManager.GetBuffer<ConnectedEdge>(node, true);
            if (edges.Length >= 3) return true;
            if (edges.Length != 2) return false;
            var other = edges[0].m_Edge == road ? edges[1].m_Edge : edges[0].m_Edge;
            if (other == road || !EntityManager.HasComponent<PrefabRef>(other)) return false;
            var otherPrefab = EntityManager.GetComponentData<PrefabRef>(other).m_Prefab;
            if (otherPrefab == prefabEntity) return false;
            if (!m_Prefabs.TryGetPrefab<PrefabBase>(otherPrefab, out var prefab) || prefab == null || !IsParkingPrefab(prefab)) return true;
            bool otherAlley = prefab.name.StartsWith("Alley", StringComparison.Ordinal);
            bool ownAlley = m_Prefabs.TryGetPrefab<PrefabBase>(prefabEntity, out var own) && own != null &&
                own.name.StartsWith("Alley", StringComparison.Ordinal);
            return otherAlley != ownAlley;
        }

        // A road with its own sidewalk meeting a parking road (2 edges of different kind or a junction).
        private bool HasParkingNeighbour(Entity road, Entity node)
        {
            if (!EntityManager.HasBuffer<ConnectedEdge>(node)) return false;
            var edges = EntityManager.GetBuffer<ConnectedEdge>(node, true);
            if (edges.Length < 2) return false;
            for (int i = 0; i < edges.Length; i++)
            {
                var other = edges[i].m_Edge;
                if (other == road || !EntityManager.HasComponent<PrefabRef>(other)) continue;
                if (m_Prefabs.TryGetPrefab<PrefabBase>(EntityManager.GetComponentData<PrefabRef>(other).m_Prefab, out var prefab) &&
                    prefab != null && IsParkingPrefab(prefab)) return true;
            }
            return false;
        }

        private static bool IsParkingAtlasPiece(string name) =>
            !string.IsNullOrEmpty(name) && name.EndsWith(" Mesh", StringComparison.Ordinal);

        private static void SetParkingMode(bool parking)
        {
            s_ParkingMode = parking;
            s_WalkLeft = parking ? ParkingLeftUv : LeftUv;
            s_WalkRight = parking ? ParkingRightUv : RightUv;
        }

        private bool EdgeDirection(Entity edge, bool start, out float2 direction)
        {
            direction = default;
            if (!EntityManager.HasComponent<Game.Net.Curve>(edge)) return false;
            var bezier = EntityManager.GetComponentData<Game.Net.Curve>(edge).m_Bezier;
            float2 d = start ? (bezier.b - bezier.a).xz : (bezier.c - bezier.d).xz;
            if (math.lengthsq(d) < 1e-6f) d = start ? (bezier.d - bezier.a).xz : (bezier.a - bezier.d).xz;
            if (math.lengthsq(d) < 1e-6f) return false;
            direction = math.normalize(d);
            return true;
        }

        private bool HasAlleyArm(Entity node)
        {
            var edges = EntityManager.GetBuffer<ConnectedEdge>(node, true);
            for (int i = 0; i < edges.Length; i++) if (IsAlley(edges[i].m_Edge)) return true;
            return false;
        }
    }
}
