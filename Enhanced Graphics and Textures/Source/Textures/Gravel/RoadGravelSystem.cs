using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Advanced_Graphics
{
    // Edits only the dedicated GravelWorldspace maps on gravel road materials.
    public partial class RoadGravelSystem : RoadSurfaceSystemBase
    {
        public static string RoadGravelTextureFolder => TextureStorage.RoadGravel;

        protected override string TextureFolder => RoadGravelTextureFolder;
        protected override string TextureFileName => "RoadGravel_BaseColor.png";
        protected override string SurfaceName => "Road gravel";
        protected override bool BakeUvSizeIntoTexture => false;
        protected override bool NeedsNormalAtZeroDepth => true;
        protected override bool ReuseColorForDepthChanges => CurrentLevel <= 1;
        private const float DepthGain = 8f;
        internal static float GravelNormalStrength(int depth) =>
            (float)Math.Pow(1f + DepthGain * 10f, depth / 1000f);
        protected override float AuthoredNormalStrength => GravelNormalStrength(CurrentDepth);

        private const string AtlasColorSlot = "_BaseColorMap";
        private const string AtlasNormalSlot = "_NormalMap";
        private const string WorldspaceScaleSlot = "_WorldspaceUVScale";
        private sealed class AtlasPair
        {
            public Texture OriginalColor, OriginalNormal;
            public Texture2D FlatColor, FlatNormal;
            public float OriginalUvScale, AppliedUvScale;
        }
        private readonly Dictionary<Material, AtlasPair> m_Atlases = new Dictionary<Material, AtlasPair>();
        private readonly HashSet<Material> m_UnavailableAtlases = new HashSet<Material>();
        private Texture m_OriginalWorldspaceNormal;
        internal Texture OriginalGravelNormal => m_OriginalWorldspaceNormal;
        internal Texture2D CopyOriginalGravelColor() => LoadOriginalColor();
        protected virtual Texture OriginalNormalSource => m_OriginalWorldspaceNormal;
        private Texture2D m_DecodedNormalSource, m_RepeatedNormalSource;

        protected override bool MatchesShader(Material material) => material.shader != null &&
            material.shader.name == "BH/NetCompositionMeshLitShader" &&
            material.name.StartsWith("Batch (Gravel_", StringComparison.Ordinal);

        protected override bool TryGetOriginal(Material material, out string colorSlot, out string normalSlot,
            out Texture color, out Texture normal)
        {
            colorSlot = "_WorldspaceAlbedo";
            normalSlot = "_WorldspaceNormalMap";
            color = material.HasProperty(colorSlot) ? material.GetTexture(colorSlot) : null;
            normal = material.HasProperty(normalSlot) ? material.GetTexture(normalSlot) : null;
            bool matched = color != null && normal != null &&
                color.name.StartsWith("GravelWorldspace_BaseColor_", StringComparison.Ordinal) &&
                normal.name.StartsWith("GravelWorldspace_Normal_", StringComparison.Ordinal);
            if (matched && m_OriginalWorldspaceNormal == null) m_OriginalWorldspaceNormal = normal;
            return matched;
        }

        protected override Texture2D GenerateNeutralAsphalt()
        {
            var texture = LoadOriginalColor();
            if (texture == null) return null;
            var pixels = RepeatGravelTwice(texture.GetPixels32(), texture.width);
            for (int i = 0; i < pixels.Length; i++)
            {
                var pixel = pixels[i];
                float brightness = pixel.r * 0.2126f + pixel.g * 0.7152f + pixel.b * 0.0722f;
                float shade = Mathf.Clamp(132f + (brightness - 70f) * 1.18f, 70f, 220f);
                pixel.r = (byte)Mathf.Clamp(shade + 2f, 0f, 255f);
                pixel.g = (byte)Mathf.Clamp(shade + 1f, 0f, 255f);
                pixel.b = (byte)Mathf.Clamp(shade, 0f, 255f);
                pixels[i] = pixel;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            texture.name = "Advanced Graphics Generated Road Gravel Source";
            return texture;
        }

        private static Color32[] RepeatGravelTwice(Color32[] source, int width)
        {
            var result = new Color32[source.Length];
            for (int y = 0; y < width; y++)
            for (int x = 0; x < width; x++)
                result[y * width + x] = source[((y * 2) % width) * width + (x * 2) % width];
            return result;
        }

        protected override Texture2D BuildSurfaceNormal(Texture2D color, int depth, int uvSize, byte[] crackMask)
        {
            if (CurrentLevel <= 1)
            {
                var original = BuildOriginalNormal();
                if (original != null) return original;
            }
            // Custom PNGs have no authored normal; derive one from their color.
            int boundedDepth = Mathf.RoundToInt(DepthGain * 100f * Mathf.Sqrt(depth / 100f));
            return base.BuildSurfaceNormal(color, boundedDepth, 100, crackMask);
        }

        private Texture2D BuildOriginalNormal()
        {
            Texture2D transformed = null;
            try
            {
                var decoded = GetCachedNormalSource();
                if (decoded == null) return null;
                transformed = Transform(decoded, true, true);
                var pixels = transformed.GetPixels32();
                EncodeNormalPixels(pixels);
                transformed.SetPixels32(pixels);
                transformed.Apply(true, !KeepTileReadable);
                transformed.name = "Advanced Graphics Road Gravel Original Normal";
                return transformed;
            }
            catch (Exception ex)
            {
                if (transformed != null) UnityEngine.Object.Destroy(transformed);
                Mod.log.Error(ex, "Road gravel original normal could not be adjusted.");
                return null;
            }
        }

        private Texture2D GetCachedNormalSource()
        {
            if (m_DecodedNormalSource == null)
            {
                Texture source = OriginalNormalSource;
                if (source == null)
                foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
                {
                    if (material == null || !MatchesShader(material)) continue;
                    var candidate = material.GetTexture("_WorldspaceNormalMap");
                    if (candidate != null && candidate.name.StartsWith("GravelWorldspace_Normal_", StringComparison.Ordinal))
                    { source = candidate; break; }
                }
                if (source == null || source.width != source.height || source.width > 4096) return null;
                var target = RenderTexture.GetTemporary(source.width, source.height, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                var previous = RenderTexture.active;
                Texture2D decoded = null;
                try
                {
                    UnityEngine.Graphics.Blit(source, target);
                    RenderTexture.active = target;
                    decoded = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
                    decoded.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
                    var pixels = decoded.GetPixels32();
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        var pixel = pixels[i];
                        float x = pixel.a / 255f * 2f - 1f, y = pixel.g / 255f * 2f - 1f;
                        float z = Mathf.Sqrt(Mathf.Max(0.001f, 1f - x * x - y * y));
                        pixel.r = pixel.a;
                        pixel.b = (byte)Mathf.RoundToInt(z * 127.5f + 127.5f);
                        pixel.a = 255;
                        pixels[i] = pixel;
                    }
                    decoded.SetPixels32(pixels);
                    decoded.Apply(false, false);
                    m_DecodedNormalSource = decoded;
                }
                catch { if (decoded != null) UnityEngine.Object.Destroy(decoded); throw; }
                finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
            }
            if (CurrentLevel != 1) return m_DecodedNormalSource;
            if (m_RepeatedNormalSource == null)
            {
                int size = m_DecodedNormalSource.width;
                m_RepeatedNormalSource = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
                m_RepeatedNormalSource.SetPixels32(RepeatGravelTwice(m_DecodedNormalSource.GetPixels32(), size));
                m_RepeatedNormalSource.Apply(false, false);
            }
            return m_RepeatedNormalSource;
        }

        protected override void OnSurfaceMaterialApplied(Material material)
        {
            if (!m_Atlases.TryGetValue(material, out var pair))
            {
                if (m_UnavailableAtlases.Contains(material)) return;
                var originalColor = material.GetTexture(AtlasColorSlot);
                var originalNormal = material.GetTexture(AtlasNormalSlot);
                if (originalColor == null || originalNormal == null || !material.HasProperty(WorldspaceScaleSlot) ||
                    !originalColor.name.StartsWith("Gravel_BaseColor_", StringComparison.Ordinal) ||
                    !originalNormal.name.StartsWith("Gravel_Normal_", StringComparison.Ordinal) ||
                    originalColor.width * 8 != originalColor.height || originalColor.height > 4096 ||
                    originalNormal.width != originalColor.width || originalNormal.height != originalColor.height)
                {
                    m_UnavailableAtlases.Add(material);
                    return;
                }
                try
                {
                    pair = new AtlasPair { OriginalColor = originalColor, OriginalNormal = originalNormal,
                        OriginalUvScale = material.GetFloat(WorldspaceScaleSlot) };
                    pair.FlatColor = FlattenBallastAtlas(originalColor, false);
                    pair.FlatNormal = FlattenBallastAtlas(originalNormal, true);
                    m_Atlases.Add(material, pair);
                }
                catch (Exception ex)
                {
                    if (pair?.FlatColor != null) UnityEngine.Object.Destroy(pair.FlatColor);
                    if (pair?.FlatNormal != null) UnityEngine.Object.Destroy(pair.FlatNormal);
                    m_UnavailableAtlases.Add(material);
                    Mod.log.Error(ex, "Road gravel base atlas could not be prepared.");
                    return;
                }
            }

            bool useFlat = CurrentLevel != 0 || CurrentUvSize != 100 || CurrentDepth != 0 ||
                CurrentOffsetX != 0 || CurrentOffsetY != 0 || CurrentRotation != 0;
            if (!useFlat)
            {
                OnSurfaceMaterialRestored(material);
                return;
            }
            float currentScale = material.GetFloat(WorldspaceScaleSlot);
            float desiredScale = pair.OriginalUvScale * 100f / CurrentUvSize;
            if (Mathf.Approximately(currentScale, pair.OriginalUvScale) ||
                Mathf.Approximately(currentScale, pair.AppliedUvScale))
            {
                material.SetFloat(WorldspaceScaleSlot, desiredScale);
                pair.AppliedUvScale = desiredScale;
            }
            var currentColor = material.GetTexture(AtlasColorSlot);
            var currentNormal = material.GetTexture(AtlasNormalSlot);
            if ((currentColor != pair.OriginalColor && currentColor != pair.FlatColor) ||
                (currentNormal != pair.OriginalNormal && currentNormal != pair.FlatNormal)) return;
            material.SetTexture(AtlasColorSlot, pair.FlatColor);
            material.SetTexture(AtlasNormalSlot, pair.FlatNormal);
        }

        protected override void OnSurfaceMaterialRestored(Material material)
        {
            if (!m_Atlases.TryGetValue(material, out var pair)) return;
            if (Mathf.Approximately(material.GetFloat(WorldspaceScaleSlot), pair.AppliedUvScale))
                material.SetFloat(WorldspaceScaleSlot, pair.OriginalUvScale);
            if (material.GetTexture(AtlasColorSlot) == pair.FlatColor)
                material.SetTexture(AtlasColorSlot, pair.OriginalColor);
            if (material.GetTexture(AtlasNormalSlot) == pair.FlatNormal)
                material.SetTexture(AtlasNormalSlot, pair.OriginalNormal);
        }

        protected override void OnSurfaceReset()
        {
            foreach (var pair in m_Atlases.Values)
            {
                if (pair.FlatColor != null) UnityEngine.Object.Destroy(pair.FlatColor);
                if (pair.FlatNormal != null) UnityEngine.Object.Destroy(pair.FlatNormal);
            }
            m_Atlases.Clear();
            m_UnavailableAtlases.Clear();
            m_OriginalWorldspaceNormal = null;
            if (m_DecodedNormalSource != null) UnityEngine.Object.Destroy(m_DecodedNormalSource);
            if (m_RepeatedNormalSource != null) UnityEngine.Object.Destroy(m_RepeatedNormalSource);
            m_DecodedNormalSource = m_RepeatedNormalSource = null;
        }

        internal override void UpdateTextureSampling()
        {
            base.UpdateTextureSampling();
            foreach (var pair in m_Atlases.Values)
            {
                SurfaceTextureSampling.Apply(pair.FlatColor);
                SurfaceTextureSampling.Apply(pair.FlatNormal);
            }
        }

        private static Texture2D FlattenBallastAtlas(Texture source, bool normal)
        {
            var target = RenderTexture.GetTemporary(source.width, source.height, 0,
                RenderTextureFormat.ARGB32, normal ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Texture2D copy = null;
            try
            {
                UnityEngine.Graphics.Blit(source, target);
                RenderTexture.active = target;
                copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true, normal);
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
                var pixels = copy.GetPixels32();
                int width = source.width, band = Mathf.RoundToInt(width * 432f / 512f);
                var flat = pixels[width - 1];
                for (int y = 0; y < source.height; y++)
                for (int x = 0; x < band; x++)
                {
                    int i = y * width + x;
                    float edge = Mathf.Clamp01((band - x) / 8f);
                    var old = pixels[i];
                    old.r = (byte)Mathf.Lerp(old.r, flat.r, edge);
                    old.g = (byte)Mathf.Lerp(old.g, flat.g, edge);
                    old.b = (byte)Mathf.Lerp(old.b, flat.b, edge);
                    if (normal) old.a = (byte)Mathf.Lerp(old.a, flat.a, edge);
                    pixels[i] = old;
                }
                copy.SetPixels32(pixels);
                copy.Apply(true, true);
                copy.name = "Advanced Graphics Road Gravel Base " + (normal ? "Normal" : "Color");
                copy.filterMode = source.filterMode;
                copy.wrapMode = source.wrapMode;
                SurfaceTextureSampling.Apply(copy);
                return copy;
            }
            catch
            {
                if (copy != null) UnityEngine.Object.Destroy(copy);
                throw;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        protected override void EncodeNormalPixels(Color32[] pixels)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                var value = pixels[i];
                value.a = value.r;
                value.r = 255;
                value.b = value.g;
                pixels[i] = value;
            }
        }

    }
}


