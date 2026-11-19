using System;
using System.Collections.Generic;
using Game;
using UnityEngine;

namespace Advanced_Graphics
{
    public partial class RoadWearSystem : GameSystemBase
    {
        private sealed class Original
        {
            internal Texture Color, Normal;
            internal float Gloss, Depth;
            internal string DepthSlot;
            internal string ColorSlot, NormalSlot;
            internal bool HasGloss, Virtual;
        }

        private readonly Dictionary<Material, Original> materials = new Dictionary<Material, Original>();
        private readonly Dictionary<Texture, Texture2D> colors = new Dictionary<Texture, Texture2D>();
        private readonly MaterialScanGate gate = new MaterialScanGate();
        private readonly Dictionary<(Texture, Texture), Texture2D> normals = new Dictionary<(Texture, Texture), Texture2D>();
        internal int MatchCount => active ? materials.Count : -1;
        private int tint = -1, opacity = 100, gloss = -1, depth = 100;
        private float nextPoll;
        private bool active;

        protected override void OnUpdate()
        {
            var s = Mod.ActiveSetting;
            if (s == null || s.HasPendingApply) return;
            int selected = !s.WearHidden && (s.WearColorSelected || s.WearColor != 0xffffff) ? s.WearColor : -1;
            int nextOpacity = s.WearHidden ? 0 : s.WearOpacity;
            int nextGloss = s.WearHidden ? -1 : s.WearGloss;
            int nextDepth = s.WearHidden ? 0 : s.WearDepth;
            if (!s.GraphicsEnabled || !s.WearEnabled || (selected < 0 && nextOpacity == 100 && nextGloss < 0 && nextDepth == 100))
            {
                if (active) Restore();
                return;
            }

            bool recolor = tint != selected || opacity != nextOpacity;
            bool changed = !active || recolor || gloss != nextGloss || depth != nextDepth;
            if (!changed && UnityEngine.Time.unscaledTime < nextPoll) return;
            nextPoll = UnityEngine.Time.unscaledTime + 2f;
            bool scan = gate.ShouldScan(World, !active || (changed && materials.Count == 0));
            if (!changed && !scan) return;
            active = true;
            if (recolor) RestoreColors();
            if (depth != nextDepth) RestoreNormals();
            tint = selected;
            opacity = nextOpacity;
            gloss = nextGloss;
            depth = nextDepth;
            if (scan)
            {
                var expired = new List<Material>();
                foreach (var entry in materials)
                    if (entry.Key == null || entry.Value.Color == null) expired.Add(entry.Key);
                foreach (var m in expired) materials.Remove(m);
                foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
                {
                    if (m == null || materials.ContainsKey(m)) continue;
                    bool namedLane = m.name.StartsWith("Batch (CarLane", StringComparison.OrdinalIgnoreCase) ||
                        m.name.StartsWith("Batch (GravelLane", StringComparison.OrdinalIgnoreCase);
                    bool wearShader = m.shader != null && m.shader.name == "BH/Decals/CurvedDecalDeteriorationShader";
                    string slot = FindLaneColorSlot(m, namedLane || wearShader);
                    if (slot == null) continue;
                    string normalSlot = slot == "_BaseColorMap" ? "_NormalMap" : "_WorldspaceNormalMap";
                    if (!m.HasProperty(normalSlot)) normalSlot = null;
                    string normal = m.HasProperty("_NormalScale") ? "_NormalScale" :
                        m.HasProperty("_NormalStrength") ? "_NormalStrength" : null;
                    materials.Add(m, new Original {
                        ColorSlot = slot, Color = m.GetTexture(slot), Virtual = m.IsKeywordEnabled("ENABLE_VT"),
                        NormalSlot = normalSlot, Normal = normalSlot == null ? null : m.GetTexture(normalSlot),
                        HasGloss = m.HasProperty("_Smoothness"), Gloss = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 0f,
                        DepthSlot = normal, Depth = normal == null ? 0f : m.GetFloat(normal)
                    });
                }
                gate.Passed(World, materials.Count == 0);
            }
            foreach (var pair in materials)
            {
                var m = pair.Key;
                var original = pair.Value;
                if (m == null || original.Color == null) continue;
                if (original.HasGloss) m.SetFloat("_Smoothness", gloss < 0 ? original.Gloss : Mathf.Clamp01(gloss / 100f));
                if (opacity != 0) ApplyDepth(m, original);
                else if (original.DepthSlot != null) m.SetFloat(original.DepthSlot, 0f);
                if (tint < 0 && opacity == 100) continue;
                if (!colors.TryGetValue(original.Color, out var copy))
                {
                    try { copy = TintTexture(original.Color); }
                    catch (Exception ex) { Mod.log.Error(ex, "Road wear texture could not be tinted."); }
                    colors.Add(original.Color, copy);
                }
                if (copy != null && m.GetTexture(original.ColorSlot) == original.Color)
                {
                    m.SetTexture(original.ColorSlot, copy);
                    if (original.Virtual) m.DisableKeyword("ENABLE_VT");
                }
            }
        }

        private static readonly string[] AlbedoSlots = { "_BaseColorMap", "_WorldspaceAlbedo" };

        private static string FindLaneColorSlot(Material m, bool knownLane)
        {
            string fallback = null;
            foreach (var slot in AlbedoSlots)
            {
                if (!m.HasProperty(slot)) continue;
                var texture = m.GetTexture(slot);
                if (texture == null || texture.name.StartsWith("EGT Road wear", StringComparison.Ordinal)) continue;
                if (texture.name.StartsWith("CarLane_BaseColor", StringComparison.OrdinalIgnoreCase) ||
                    texture.name.StartsWith("GravelLane_BaseColor", StringComparison.OrdinalIgnoreCase)) return slot;
                if (knownLane && (fallback == null || (m.GetTexture(fallback).width <= 4 && texture.width > 4)))
                    fallback = slot;
            }
            return fallback;
        }

        private void ApplyDepth(Material material, Original original)
        {
            bool bake = depth != 100 && original.NormalSlot != null &&
                (depth > 100 || original.DepthSlot == null || original.Depth <= 0f || original.Normal == null);
            if (!bake)
            {
                if (original.DepthSlot != null) material.SetFloat(original.DepthSlot, original.Depth * depth / 100f);
                return;
            }
            var key = (original.Color, original.Normal);
            if (!normals.TryGetValue(key, out var copy))
            {
                try { copy = BuildRelief(original); }
                catch (Exception ex) { Mod.log.Error(ex, "Road wear relief could not be generated."); }
                normals.Add(key, copy);
            }
            if (copy == null) return;
            var current = material.GetTexture(original.NormalSlot);
            if (current != original.Normal && current != copy) return;
            material.SetTexture(original.NormalSlot, copy);
            if (original.DepthSlot != null) material.SetFloat(original.DepthSlot, original.Depth > 0f ? original.Depth : 1f);
            if (original.Virtual) material.DisableKeyword("ENABLE_VT");
        }

        private static Texture2D MakeMap(Texture template, int w, int h, Color32[] pixels, bool linear)
        {
            var result = new Texture2D(w, h, TextureFormat.RGBA32, true, linear);
            try
            {
                result.name = "EGT Road wear " + (linear ? "Normal" : "Color");
                result.wrapModeU = template.wrapModeU;
                result.wrapModeV = template.wrapModeV;
                result.filterMode = template.filterMode;
                result.anisoLevel = template.anisoLevel;
                result.mipMapBias = template.mipMapBias;
                result.SetPixels32(pixels);
                result.Apply(true, true);
                return result;
            }
            catch { UnityEngine.Object.Destroy(result); throw; }
        }

        private Texture2D TintTexture(Texture source)
        {
            if (opacity == 0)
                return MakeMap(source, 1, 1, new[] { new Color32(0, 0, 0, 0) }, false);
            var read = SidewalkRoadAtlasSystem.Readback(source, false);
            try
            {
                var data = read.GetPixels32();
                int r = (tint >> 16) & 255, g = (tint >> 8) & 255, b = tint & 255;
                for (int i = 0; i < data.Length; i++)
                {
                    var c = data[i];
                    if (tint >= 0)
                    {
                        // Give dark wear texels the selected hue while retaining fine tonal variation.
                        int shade = 192 + ((54 * c.r + 183 * c.g + 19 * c.b) >> 10);
                        c.r = (byte)(r * shade / 255);
                        c.g = (byte)(g * shade / 255);
                        c.b = (byte)(b * shade / 255);
                    }
                    c.a = (byte)Mathf.Clamp((c.a * opacity + 50) / 100, 0, 255);
                    data[i] = c;
                }
                return MakeMap(source, read.width, read.height, data, false);
            }
            finally { UnityEngine.Object.Destroy(read); }
        }

        private Texture2D BuildRelief(Original original)
        {
            var wear = SidewalkRoadAtlasSystem.Readback(original.Color, false);
            Texture2D normal = null;
            try
            {
                if (original.Normal != null) normal = SidewalkRoadAtlasSystem.Readback(original.Normal, true);
                var input = wear.GetPixels32();
                var packed = normal != null ? normal.GetPixels32() : null;
                int w = wear.width, h = wear.height;
                var output = new Color32[input.Length];
                float strength = Mathf.Clamp(depth, 0, 1000) / 100f;
                float extra = Mathf.Max(0f, strength - 1f) * 4f;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float nx = 0f, ny = 0f, nz = 1f;
                    if (packed != null)
                    {
                        var p = packed[(y * normal.height / h) * normal.width + x * normal.width / w];
                        nx = 2f * p.r * p.a / 65025f - 1f;
                        ny = p.g / 127.5f - 1f;
                        nz = Mathf.Sqrt(Mathf.Max(0.001f, 1f - nx * nx - ny * ny));
                    }
                    int row = y * w;
                    nx = nx / nz * strength + extra * (Height(input[row + Mathf.Max(0, x - 1)]) -
                        Height(input[row + Mathf.Min(w - 1, x + 1)]));
                    ny = ny / nz * strength + extra * (Height(input[Mathf.Max(0, y - 1) * w + x]) -
                        Height(input[Mathf.Min(h - 1, y + 1) * w + x]));
                    nz = 1f;
                    TextureMath.Normalize(ref nx, ref ny, ref nz);
                    byte px = (byte)Mathf.RoundToInt((nx + 1f) * 127.5f);
                    byte py = (byte)Mathf.RoundToInt((ny + 1f) * 127.5f);
                    output[row + x] = new Color32(255, py, py, px);
                }
                return MakeMap(original.Normal ?? original.Color, w, h, output, true);
            }
            finally
            {
                UnityEngine.Object.Destroy(wear);
                if (normal != null) UnityEngine.Object.Destroy(normal);
            }
        }

        private static float Height(Color32 p) =>
            p.a / 255f * (1f - (p.r + p.g + p.b) / 1530f);

        private void RestoreNormals()
        {
            foreach (var pair in materials)
            {
                var m = pair.Key;
                var o = pair.Value;
                if (m == null || o.NormalSlot == null || !normals.TryGetValue((o.Color, o.Normal), out var copy) ||
                    copy == null || m.GetTexture(o.NormalSlot) != copy) continue;
                m.SetTexture(o.NormalSlot, o.Normal);
                if (o.Virtual && m.GetTexture(o.ColorSlot) == o.Color) m.EnableKeyword("ENABLE_VT");
            }
            foreach (var copy in normals.Values) if (copy != null) UnityEngine.Object.Destroy(copy);
            normals.Clear();
        }

        private void RestoreColors()
        {
            foreach (var pair in materials)
            {
                var m = pair.Key;
                var original = pair.Value;
                if (m == null || original.Color == null || !colors.TryGetValue(original.Color, out var copy) ||
                    copy == null || m.GetTexture(original.ColorSlot) != copy) continue;
                m.SetTexture(original.ColorSlot, original.Color);
                if (original.Virtual && (original.NormalSlot == null || m.GetTexture(original.NormalSlot) == original.Normal)) m.EnableKeyword("ENABLE_VT");
            }
            foreach (var copy in colors.Values) if (copy != null) UnityEngine.Object.Destroy(copy);
            colors.Clear();
        }

        internal void Restore()
        {
            RestoreColors();
            RestoreNormals();
            foreach (var pair in materials)
            {
                if (pair.Key == null) continue;
                if (pair.Value.HasGloss) pair.Key.SetFloat("_Smoothness", pair.Value.Gloss);
                if (pair.Value.DepthSlot != null) pair.Key.SetFloat(pair.Value.DepthSlot, pair.Value.Depth);
            }
            materials.Clear();
            active = false;
            gate.Reset();
        }

        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            if (active) Restore();
            base.OnGamePreload(purpose, mode);
        }

        protected override void OnDestroy()
        {
            Restore();
            base.OnDestroy();
        }
    }
}
