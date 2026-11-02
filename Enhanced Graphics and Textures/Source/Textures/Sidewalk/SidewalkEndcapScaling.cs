using System.Collections.Generic;
using UnityEngine;

namespace Advanced_Graphics
{
    public partial class SidewalkRoadAtlasSystem
    {
        private float m_EndcapU = 1f, m_EndcapV = 1f;
        private float m_RoadEndU = 1f, m_RoadEndV = 1f;
        private float m_LargeEndcapU = 1f, m_LargeEndcapV = 1f;
        private readonly Dictionary<(Material, bool, bool), Material> m_EndcapMaterials = new Dictionary<(Material, bool, bool), Material>();
        private readonly Dictionary<(Texture, bool, bool, float, float), Texture2D> m_EndcapTextures = new Dictionary<(Texture, bool, bool, float, float), Texture2D>();

        private bool UsesRoadEndTiling => !IsAlleyAtlas && !(this is ParkingRoadAtlasSystem);
        private bool UsesLargeRoadTiling => !IsAlleyAtlas;
        private bool LargeEndcapActive => !Mathf.Approximately(m_LargeEndcapU, 1f) || !Mathf.Approximately(m_LargeEndcapV, 1f);
        private bool NormalEndcapActive => !Mathf.Approximately(m_EndcapU, 1f) || !Mathf.Approximately(m_EndcapV, 1f);
        protected bool EndcapScaleActive => !Mathf.Approximately(m_EndcapU, 1f) || !Mathf.Approximately(m_EndcapV, 1f) ||
            (UsesLargeRoadTiling && LargeEndcapActive) ||
            (UsesRoadEndTiling && (!Mathf.Approximately(m_RoadEndU, 1f) || !Mathf.Approximately(m_RoadEndV, 1f)));

        protected bool RefreshEndcapScale()
        {
            var settings = Mod.ActiveSetting;
            bool parking = this is ParkingRoadAtlasSystem;
            float u = (parking ? settings?.ParkingEndcapU : IsAlleyAtlas ? settings?.AlleyEndcapU : settings?.SidewalkEndcapU) ?? 1f;
            float v = (parking ? settings?.ParkingEndcapV : IsAlleyAtlas ? settings?.AlleyEndcapV : settings?.SidewalkEndcapV) ?? 1f;
            float ru = UsesRoadEndTiling ? settings?.RoadEndTilingU ?? 1f : 1f;
            float rv = UsesRoadEndTiling ? settings?.RoadEndTilingV ?? 1f : 1f;
            float lu = UsesLargeRoadTiling ? settings?.LargeRoadEndcapU ?? 1f : 1f;
            float lv = UsesLargeRoadTiling ? settings?.LargeRoadEndcapV ?? 1f : 1f;
            bool changed = !Mathf.Approximately(lu, m_LargeEndcapU) || !Mathf.Approximately(lv, m_LargeEndcapV) || !Mathf.Approximately(u, m_EndcapU) || !Mathf.Approximately(v, m_EndcapV) ||
                !Mathf.Approximately(ru, m_RoadEndU) || !Mathf.Approximately(rv, m_RoadEndV);
            m_LargeEndcapU = lu;
            m_LargeEndcapV = lv;
            m_EndcapU = u;
            m_EndcapV = v;
            m_RoadEndU = ru;
            m_RoadEndV = rv;
            return changed;
        }

        private Material GetEndcapMaterial(Material source, bool roadEnd = false, bool largeRoad = false)
        {
            roadEnd &= UsesRoadEndTiling;
            largeRoad &= UsesLargeRoadTiling;
            // Large roads own their node/end values; the other categories retain their existing values.
            float scaleU = largeRoad ? m_LargeEndcapU : roadEnd ? m_RoadEndU : m_EndcapU;
            float scaleV = largeRoad ? m_LargeEndcapV : roadEnd ? m_RoadEndV : m_EndcapV;
            if (Mathf.Approximately(scaleU, 1f) && Mathf.Approximately(scaleV, 1f)) return source;
            if (m_EndcapMaterials.TryGetValue((source, roadEnd, largeRoad), out var result)) return result;
            var mask = source.HasProperty("_MaskMap") ? source.GetTexture("_MaskMap") : null;
            bool parking = mask != null && mask.name.StartsWith("ParkingLane7_", System.StringComparison.Ordinal);
            bool road = mask != null && mask.name.StartsWith("RoadEU_", System.StringComparison.Ordinal);
            if (!parking && !road) return source;
            result = new Material(source) { name = "Enhanced Graphics and Textures endcap " + source.name,
                hideFlags = HideFlags.HideAndDontSave };
            m_EndcapMaterials.Add((source, roadEnd, largeRoad), result);
            if (source.HasProperty(ColorSlot) && source.GetTexture(ColorSlot) != null)
                result.SetTexture(ColorSlot, ScaleEndcapAtlas(source.GetTexture(ColorSlot), false, parking, scaleU, scaleV));
            if (source.HasProperty(NormalSlot) && source.GetTexture(NormalSlot) != null)
                result.SetTexture(NormalSlot, ScaleEndcapAtlas(source.GetTexture(NormalSlot), true, parking, scaleU, scaleV));
            return result;
        }

        private int m_EndcapTexturesBuilt;

        private Texture2D ScaleEndcapAtlas(Texture source, bool linear, bool parking, float scaleU, float scaleV)
        {
            var key = (source, linear, parking, scaleU, scaleV);
            if (m_EndcapTextures.TryGetValue(key, out var result)) return result;
            var timer = TexturePerformance.StartTimer(TimingLabel + " endcap atlas " + (linear ? "normal" : "color"));
            // No GPU readback when the pixels are already on the CPU.
            Texture2D readable;
            bool release = false;
            string origin;
            if (IsDirectlyReadable(source))
            {
                readable = (Texture2D)source;
                origin = "source direct";
            }
            else if (TryGetCachedReadback(source, out readable))
            {
                origin = "source cached";
            }
            else
            {
                readable = Readback(source, linear);
                origin = "source GPU readback";
                if (IsGameTexture(source)) CacheReadback(source, readable);
                else release = true;
            }
            timer.Mark(origin);
            try
            {
                int width = readable.width, height = readable.height;
                bool whole = parking && IsAlleyAtlas;
                int left = whole ? 0 : Mathf.RoundToInt(width * (parking ? 516f / 2048f : X0 / 1024f));
                int right = whole ? width : Mathf.RoundToInt(width * (parking ? 942f / 2048f : X1 / 1024f));
                int span = Mathf.Max(1, right - left);
                result = TexturePool.Get(width, height, linear, out bool reused);
                result.name = "Enhanced Graphics and Textures endcap atlas";
                timer.Mark(reused ? "texture reused" : "texture new");
                try
                {
                    var original = PixelBuffer.Read(readable);
                    var copy = default(PixelBuffer);
                    try
                    {
                        bool direct = PixelBuffer.TryWrite(result, out var pixels);
                        if (!direct)
                        {
                            copy = PixelBuffer.Allocate(original.Length);
                            pixels = copy.Pixels;
                        }
                        pixels.CopyFrom(original.Pixels);
                        TexturePerformance.Run(new EndcapScaleRowsJob
                        {
                            Original = original.Pixels,
                            Pixels = pixels,
                            Width = width,
                            Height = height,
                            Left = left,
                            Right = right,
                            Span = span,
                            ScaleU = scaleU,
                            ScaleV = scaleV,
                            Linear = linear
                        }, height);
                        if (!direct) result.SetPixels32(pixels.ToArray());
                    }
                    finally
                    {
                        original.Dispose();
                        copy.Dispose();
                    }
                    timer.Mark("scale");
                    TexturePerformance.ApplyWithMips(result, !TexturePerformance.ReuseTextures, timer);
                    timer.Mark("upload");
                    SurfaceTextureSampling.Apply(result);
                }
                catch
                {
                    UnityEngine.Object.Destroy(result);
                    throw;
                }
                m_EndcapTextures.Add(key, result);
                m_EndcapTexturesBuilt++;
                timer.Log(width + "x" + height + ", scale " + scaleU.ToString("0.##") + "x" + scaleV.ToString("0.##"));
                return result;
            }
            finally { if (release) UnityEngine.Object.Destroy(readable); }
        }

        private string TimingLabel => this is ParkingRoadAtlasSystem ? "Parking road" : SurfaceLabel;

        internal static bool IsDirectlyReadable(Texture texture) => texture is Texture2D tile && tile.isReadable &&
            tile.format == TextureFormat.RGBA32 && tile.width <= 4096 && tile.height <= 4096;

        private static bool IsGameTexture(Texture texture) => texture != null && texture.name != null &&
            !texture.name.StartsWith("Advanced Graphics", System.StringComparison.Ordinal) &&
            !texture.name.StartsWith("Enhanced Graphics and Textures", System.StringComparison.Ordinal);

        protected virtual bool TryGetCachedReadback(Texture source, out Texture2D readback) =>
            m_Readbacks.TryGetValue(source, out readback) && readback != null;

        protected virtual void CacheReadback(Texture source, Texture2D readback) => m_Readbacks[source] = readback;

        private void ReleaseEndcapScaling()
        {
            foreach (var material in m_EndcapMaterials.Values) if (material != null) UnityEngine.Object.Destroy(material);
            foreach (var pair in m_EndcapTextures) TexturePool.Release(pair.Value, pair.Key.Item2);
            m_EndcapMaterials.Clear();
            m_EndcapTextures.Clear();
        }
    }
}


