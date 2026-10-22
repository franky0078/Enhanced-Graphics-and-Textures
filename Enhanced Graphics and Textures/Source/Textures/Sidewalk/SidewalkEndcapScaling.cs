using System.Collections.Generic;
using UnityEngine;

namespace Advanced_Graphics
{
    public partial class SidewalkRoadAtlasSystem
    {
        private float m_EndcapU = 1f, m_EndcapV = 1f;
        private readonly Dictionary<Material, Material> m_EndcapMaterials = new Dictionary<Material, Material>();
        private readonly Dictionary<(Texture, bool, bool), Texture2D> m_EndcapTextures = new Dictionary<(Texture, bool, bool), Texture2D>();

        protected bool EndcapScaleActive => !Mathf.Approximately(m_EndcapU, 1f) || !Mathf.Approximately(m_EndcapV, 1f);

        protected bool RefreshEndcapScale()
        {
            var settings = Mod.ActiveSetting;
            bool parking = this is ParkingRoadAtlasSystem;
            float u = (parking ? settings?.ParkingEndcapU : IsAlleyAtlas ? settings?.AlleyEndcapU : settings?.SidewalkEndcapU) ?? 1f;
            float v = (parking ? settings?.ParkingEndcapV : IsAlleyAtlas ? settings?.AlleyEndcapV : settings?.SidewalkEndcapV) ?? 1f;
            bool changed = !Mathf.Approximately(u, m_EndcapU) || !Mathf.Approximately(v, m_EndcapV);
            m_EndcapU = u;
            m_EndcapV = v;
            return changed;
        }

        private Material GetEndcapMaterial(Material source)
        {
            if (Mathf.Approximately(m_EndcapU, 1f) && Mathf.Approximately(m_EndcapV, 1f)) return source;
            if (m_EndcapMaterials.TryGetValue(source, out var result)) return result;
            var mask = source.HasProperty("_MaskMap") ? source.GetTexture("_MaskMap") : null;
            bool parking = mask != null && mask.name.StartsWith("ParkingLane7_", System.StringComparison.Ordinal);
            bool road = mask != null && mask.name.StartsWith("RoadEU_", System.StringComparison.Ordinal);
            if (!parking && !road) return source;
            result = new Material(source) { name = "Enhanced Graphics and Textures endcap " + source.name,
                hideFlags = HideFlags.HideAndDontSave };
            m_EndcapMaterials.Add(source, result);
            if (source.HasProperty(ColorSlot) && source.GetTexture(ColorSlot) != null)
                result.SetTexture(ColorSlot, ScaleEndcapAtlas(source.GetTexture(ColorSlot), false, parking));
            if (source.HasProperty(NormalSlot) && source.GetTexture(NormalSlot) != null)
                result.SetTexture(NormalSlot, ScaleEndcapAtlas(source.GetTexture(NormalSlot), true, parking));
            return result;
        }

        private Texture2D ScaleEndcapAtlas(Texture source, bool linear, bool parking)
        {
            var key = (source, linear, parking);
            if (m_EndcapTextures.TryGetValue(key, out var result)) return result;
            var readable = Readback(source, linear);
            try
            {
                int width = readable.width, height = readable.height;
                var original = readable.GetPixels32();
                var pixels = (Color32[])original.Clone();
                // The isolated Alley ParkingLane atlas is wholly covered in sidewalk.
                // Parking roads retain their asphalt and markings outside the sidewalk strip.
                bool whole = parking && IsAlleyAtlas;
                int left = whole ? 0 : Mathf.RoundToInt(width * (parking ? 516f / 2048f : X0 / 1024f));
                int right = whole ? width : Mathf.RoundToInt(width * (parking ? 942f / 2048f : X1 / 1024f));
                int span = Mathf.Max(1, right - left);
                for (int y = 0; y < height; y++)
                {
                    int sy = Mathf.Min(height - 1, Mathf.FloorToInt(Mathf.Repeat((y + 0.5f) / height * m_EndcapV, 1f) * height));
                    for (int x = left; x < right; x++)
                    {
                        int sx = left + Mathf.Min(span - 1, Mathf.FloorToInt(Mathf.Repeat((x - left + 0.5f) / span * m_EndcapU, 1f) * span));
                        int i = y * width + x;
                        var pixel = original[sy * width + sx];
                        // Keep non-normal channels and the color material mask at their original UVs.
                        if (linear) { pixel.r = original[i].r; pixel.b = original[i].b; }
                        else pixel.a = original[i].a;
                        pixels[i] = pixel;
                    }
                }
                result = new Texture2D(width, height, TextureFormat.RGBA32, true, linear)
                    { name = "Enhanced Graphics and Textures endcap atlas" };
                result.SetPixels32(pixels);
                result.Apply(true, true);
                SurfaceTextureSampling.Apply(result);
                m_EndcapTextures.Add(key, result);
                return result;
            }
            finally { UnityEngine.Object.Destroy(readable); }
        }

        private void ReleaseEndcapScaling()
        {
            foreach (var material in m_EndcapMaterials.Values) if (material != null) UnityEngine.Object.Destroy(material);
            foreach (var texture in m_EndcapTextures.Values) if (texture != null) UnityEngine.Object.Destroy(texture);
            m_EndcapMaterials.Clear();
            m_EndcapTextures.Clear();
        }
    }
}
