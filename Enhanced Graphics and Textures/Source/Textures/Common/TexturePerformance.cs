using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Advanced_Graphics
{
    internal enum TextureComputeMode
    {
        Burst,
        Parallel,
        Single
    }

    // Fixed settings: Burst with automatic fallback, all optimizations on, no timing log.
    internal static class TexturePerformance
    {
        internal static TextureComputeMode Mode = TextureComputeMode.Burst;
        internal static bool CacheAsphaltSource = true;
        internal static bool KeepAsphaltReadable = true;
        internal static bool FilterOnlyRebuild = true;
        internal static bool KeepAtlasReadable = true;
        internal static bool ReuseTextures = true;
        internal static bool BurstMipmaps = true;
        internal static bool LogTimings = false;

        private static bool s_BurstFailed, s_BurstProbed;

        // Logs once if the jobs do not run Burst-compiled.
        private static void ProbeBurst()
        {
            s_BurstProbed = true;
            var result = new NativeArray<int>(1, Allocator.TempJob);
            try
            {
                new BurstProbeJob { Result = result }.Schedule(1, 1).Complete();
                if (result[0] != 1)
                    Mod.log.Info("Texture jobs run without Burst; Burst compiler enabled " + Unity.Burst.BurstCompiler.IsEnabled + ".");
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex, "Burst check could not run.");
            }
            finally { result.Dispose(); }
        }

        internal static void LogBatchIsolation(string label, double milliseconds, bool nodesOnly, int applied, int endcapTextures)
        {
            if (!LogTimings || (milliseconds < 10.0 && endcapTextures == 0)) return;
            Mod.log.Info("[Timing] " + label + " batch isolation: " + milliseconds.ToString("0") + " ms (mode " + Mode +
                (nodesOnly ? ", nodes only" : "") + ", " + applied + " batches, " + endcapTextures + " endcap textures built)");
        }

        // Runs a kernel over count work items in the selected mode.
        internal static void Run<T>(T job, int count) where T : struct, IJobParallelFor
        {
            if (count <= 0) return;
            var mode = Mode;
            if (mode == TextureComputeMode.Burst && !s_BurstFailed)
            {
                if (!s_BurstProbed) ProbeBurst();
                try
                {
                    job.Schedule(count, 8).Complete();
                    return;
                }
                catch (Exception ex)
                {
                    s_BurstFailed = true;
                    Mod.log.Error(ex, "Texture job could not be scheduled; using Parallel mode for this session.");
                }
                mode = TextureComputeMode.Parallel;
            }
            if (mode == TextureComputeMode.Single)
            {
                for (int i = 0; i < count; i++) job.Execute(i);
                return;
            }
            Parallel.For(0, count, i => job.Execute(i));
        }

        // Uploads a texture; with burstMipmaps the mip chain is computed by a kernel instead of Unity's CPU path.
        internal static void ApplyWithMips(Texture2D texture, bool makeNoLongerReadable, Timer timer = null)
        {
            if (!BurstMipmaps || texture == null || texture.mipmapCount <= 1 || !texture.isReadable ||
                texture.format != TextureFormat.RGBA32)
            {
                texture.Apply(true, makeNoLongerReadable);
                return;
            }
            int width = texture.width, height = texture.height;
            for (int mip = 1; mip < texture.mipmapCount; mip++)
            {
                int targetWidth = Math.Max(1, width >> 1), targetHeight = Math.Max(1, height >> 1);
                var source = texture.GetPixelData<Color32>(mip - 1);
                var target = texture.GetPixelData<Color32>(mip);
                if (source.Length != width * height || target.Length != targetWidth * targetHeight)
                {
                    texture.Apply(true, makeNoLongerReadable);
                    return;
                }
                Run(new MipDownsampleRowsJob
                {
                    Source = source,
                    Target = target,
                    SourceWidth = width,
                    SourceHeight = height,
                    TargetWidth = targetWidth
                }, targetHeight);
                width = targetWidth;
                height = targetHeight;
            }
            timer?.Mark("mips");
            texture.Apply(false, makeNoLongerReadable);
        }

        internal static Timer StartTimer(string label) => new Timer(label);

        // Collects the step durations of one rebuild and logs them as one line.
        internal sealed class Timer
        {
            private readonly string m_Label;
            private readonly Stopwatch m_Total = Stopwatch.StartNew();
            private readonly Stopwatch m_Step = Stopwatch.StartNew();
            private readonly List<KeyValuePair<string, double>> m_Steps = new List<KeyValuePair<string, double>>();

            internal Timer(string label) { m_Label = label; }

            internal void Mark(string step)
            {
                m_Steps.Add(new KeyValuePair<string, double>(step, m_Step.Elapsed.TotalMilliseconds));
                m_Step.Reset();
                m_Step.Start();
            }

            internal void Log(string detail = null)
            {
                if (!LogTimings) return;
                var text = new StringBuilder();
                text.Append("[Timing] ").Append(m_Label).Append(": ")
                    .Append(m_Total.Elapsed.TotalMilliseconds.ToString("0")).Append(" ms (mode ").Append(Mode);
                if (!string.IsNullOrEmpty(detail)) text.Append(", ").Append(detail);
                text.Append(")");
                foreach (var step in m_Steps)
                    text.Append(" | ").Append(step.Key).Append(' ').Append(step.Value.ToString("0")).Append(" ms");
                Mod.log.Info(text.ToString());
            }
        }
    }

    internal static class TexturePool
    {
        private const float MaxIdleSeconds = 5f;
        private const int MaxPerSize = 2;
        private static readonly Dictionary<(int, int, bool), List<KeyValuePair<Texture2D, float>>> s_Free =
            new Dictionary<(int, int, bool), List<KeyValuePair<Texture2D, float>>>();
        private static float s_NextTrim;
        internal static int Reused, Created;

        internal static Texture2D Get(int width, int height, bool linear) => Get(width, height, linear, out _);

        internal static Texture2D Get(int width, int height, bool linear, out bool reused)
        {
            reused = false;
            if (TexturePerformance.ReuseTextures && s_Free.TryGetValue((width, height, linear), out var list))
            {
                while (list.Count > 0)
                {
                    var texture = list[list.Count - 1].Key;
                    list.RemoveAt(list.Count - 1);
                    if (texture != null && texture.isReadable)
                    {
                        Reused++;
                        reused = true;
                        return texture;
                    }
                    if (texture != null) UnityEngine.Object.Destroy(texture);
                }
            }
            Created++;
            return new Texture2D(width, height, TextureFormat.RGBA32, true, linear);
        }

        internal static void Release(Texture2D texture, bool linear)
        {
            if (texture == null) return;
            if (!TexturePerformance.ReuseTextures || !texture.isReadable || texture.format != TextureFormat.RGBA32 ||
                texture.mipmapCount <= 1)
            {
                UnityEngine.Object.Destroy(texture);
                return;
            }
            var key = (texture.width, texture.height, linear);
            if (!s_Free.TryGetValue(key, out var list)) s_Free.Add(key, list = new List<KeyValuePair<Texture2D, float>>());
            foreach (var entry in list) if (ReferenceEquals(entry.Key, texture)) return;
            if (list.Count >= MaxPerSize)
            {
                UnityEngine.Object.Destroy(list[0].Key);
                list.RemoveAt(0);
            }
            list.Add(new KeyValuePair<Texture2D, float>(texture, Time.realtimeSinceStartup));
        }

        // Destroys textures that were not reused within a few seconds; checks at most once per second.
        internal static void Trim()
        {
            float now = Time.realtimeSinceStartup;
            if (now < s_NextTrim) return;
            s_NextTrim = now + 1f;
            foreach (var list in s_Free.Values)
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (now - list[i].Value < MaxIdleSeconds && TexturePerformance.ReuseTextures) continue;
                    if (list[i].Key != null) UnityEngine.Object.Destroy(list[i].Key);
                    list.RemoveAt(i);
                }
        }

        internal static void Clear()
        {
            foreach (var list in s_Free.Values)
                foreach (var entry in list)
                    if (entry.Key != null) UnityEngine.Object.Destroy(entry.Key);
            s_Free.Clear();
        }
    }

    internal struct PixelBuffer : IDisposable
    {
        internal NativeArray<Color32> Pixels;
        private bool m_Owned;

        internal bool IsView => !m_Owned;
        internal int Length => Pixels.Length;

        // View into mip 0 of a readable RGBA32 texture, otherwise an owned copy.
        internal static PixelBuffer Read(Texture2D texture)
        {
            if (CanView(texture))
                return new PixelBuffer { Pixels = texture.GetPixelData<Color32>(0), m_Owned = false };
            return new PixelBuffer { Pixels = new NativeArray<Color32>(texture.GetPixels32(), Allocator.TempJob), m_Owned = true };
        }

        internal static PixelBuffer Copy(Color32[] pixels) =>
            new PixelBuffer { Pixels = new NativeArray<Color32>(pixels, Allocator.TempJob), m_Owned = true };

        internal static PixelBuffer Allocate(int length) =>
            new PixelBuffer { Pixels = new NativeArray<Color32>(length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory), m_Owned = true };

        internal static bool TryWrite(Texture2D texture, out NativeArray<Color32> view)
        {
            if (CanView(texture))
            {
                view = texture.GetPixelData<Color32>(0);
                return true;
            }
            view = default;
            return false;
        }

        private static bool CanView(Texture2D texture)
        {
            if (texture == null || !texture.isReadable || texture.format != TextureFormat.RGBA32) return false;
            var view = texture.GetPixelData<Color32>(0);
            return view.IsCreated && view.Length == texture.width * texture.height;
        }

        public void Dispose()
        {
            if (m_Owned && Pixels.IsCreated) Pixels.Dispose();
            Pixels = default;
        }
    }
}
