using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Game.Rendering;
using Unity.Entities;
using UnityEngine;

namespace Advanced_Graphics
{
    internal sealed class MaterialScanGate
    {
        internal const float SafetyInterval = 300f;
        private const float FirstRetry = 2f;

        private int m_Signature;
        private bool m_HasSignature;
        private float m_SafetyAt;
        private float m_Retry = FirstRetry;

        // Signature of the current frame, shared by all gates; any finished pass invalidates it (its own SetMaterial calls).
        private static int s_FrameSignature;
        private static int s_SignatureFrame = -1;
        private static int s_Failures;
        private const string NetShaderName = "BH/NetCompositionMeshLitShader";
        // Per batch object: uses the net shader (decided once its material is loaded).
        private static readonly Dictionary<CustomBatch, bool> s_IsNet = new Dictionary<CustomBatch, bool>();
        private static int s_Checks, s_Skipped, s_Passes, s_Computations;
        private static long s_ComputeTicks, s_ComputeMaxTicks;
        private static float s_StatsSince = -1f;
        internal bool ShouldScan(World world, bool forced)
        {
            float now = UnityEngine.Time.unscaledTime;
            s_Checks++;
            bool changed = m_HasSignature && CurrentSignature(world) != m_Signature;
            if (changed) m_Retry = FirstRetry;
            bool run = forced || changed || !m_HasSignature || now >= m_SafetyAt;
            if (run) s_Passes++;
            else s_Skipped++;
            ReportStats(now);
            return run;
        }
        internal void Passed(World world, bool waiting)
        {
            s_SignatureFrame = -1;
            m_Signature = CurrentSignature(world);
            m_HasSignature = true;
            if (waiting)
            {
                m_SafetyAt = UnityEngine.Time.unscaledTime + m_Retry;
                m_Retry = Math.Min(m_Retry * 2f, SafetyInterval);
            }
            else
            {
                m_SafetyAt = UnityEngine.Time.unscaledTime + SafetyInterval;
                m_Retry = FirstRetry;
            }
        }

        // Next check runs the pass (restore, failure, reset of the owner's state).
        internal void Reset()
        {
            m_HasSignature = false;
            m_Retry = FirstRetry;
            s_SignatureFrame = -1;
        }

        private static int CurrentSignature(World world)
        {
            int frame = UnityEngine.Time.frameCount;
            if (s_SignatureFrame == frame) return s_FrameSignature;
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            s_FrameSignature = Compute(world);
            s_SignatureFrame = frame;
            long ticks = System.Diagnostics.Stopwatch.GetTimestamp() - start;
            s_ComputeTicks += ticks;
            if (ticks > s_ComputeMaxTicks) s_ComputeMaxTicks = ticks;
            s_Computations++;
            return s_FrameSignature;
        }

        private static int Compute(World world)
        {
            try
            {
                var batchManager = world?.GetExistingSystemManaged<BatchManagerSystem>();
                if (batchManager == null) return 0;
                var batches = batchManager.GetManagedBatches();
                int count = batches.BatchCount;
                if (s_IsNet.Count > count * 2 + 1024) s_IsNet.Clear();
                unchecked
                {
                    uint hash = 2166136261u;
                    uint netBatches = 0;
                    for (int i = 0; i < count; i++)
                    {
                        var batch = batches.GetBatch(i) as CustomBatch;
                        if (batch == null || !IsNetBatch(batch)) continue;
                        netBatches++;
                        hash = (hash ^ (uint)i) * 16777619u;
                        hash = (hash ^ (uint)RuntimeHelpers.GetHashCode(batch)) * 16777619u;
                        hash = (hash ^ (uint)Identity(batch.loadedMaterial)) * 16777619u;
                        hash = (hash ^ (uint)Identity(batch.material)) * 16777619u;
                    }
                    return (int)((hash ^ netBatches) * 16777619u);
                }
            }
            catch (Exception ex)
            {
                if (s_Failures++ == 0) Mod.ErrorLater(ex, "Material scan gate could not read the batch list; textures are checked on every poll.");
                return s_Failures;
            }
        }

        // A batch whose material is not loaded yet counts (its loading must be seen); the decision is cached once known.
        private static bool IsNetBatch(CustomBatch batch)
        {
            if (s_IsNet.TryGetValue(batch, out bool net)) return net;
            var material = batch.loadedMaterial;
            if (material == null) return true;
            var shader = material.shader;
            net = shader != null && shader.name == NetShaderName;
            s_IsNet[batch] = net;
            return net;
        }

        private static int Identity(Material material) => ReferenceEquals(material, null) || material == null ? 0 : RuntimeHelpers.GetHashCode(material);

        private static void ReportStats(float now)
        {
            if (s_StatsSince < 0f) s_StatsSince = now;
            if (now - s_StatsSince < SafetyInterval) return;
            double ms = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            int computations = Math.Max(1, s_Computations);
            Mod.LogLater("Material scan gate (" + SafetyInterval.ToString("0") + " s): " + s_Checks + " checks, " + s_Passes + " passes run, " +
                s_Skipped + " skipped; batch signature " + s_Computations + "x avg " + (s_ComputeTicks * ms / computations).ToString("0.00") +
                " ms max " + (s_ComputeMaxTicks * ms).ToString("0.00") + " ms.");
            s_Checks = s_Skipped = s_Passes = s_Computations = 0;
            s_ComputeTicks = s_ComputeMaxTicks = 0;
            s_StatsSince = now;
        }
    }
}
