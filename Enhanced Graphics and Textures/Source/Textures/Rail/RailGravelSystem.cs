using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Advanced_Graphics
{
    // Edits only the dedicated TrainGravelWorldspace maps on track materials.
    public partial class RailGravelSystem : RoadSurfaceSystemBase
    {
        public static string RailTextureFolder => TextureStorage.Rail;

        protected override string TextureFolder => RailTextureFolder;
        protected override string TextureFileName => "RailGravel_BaseColor.png";
        protected override string SurfaceName => "Rail gravel";
        protected override bool BakeUvSizeIntoTexture => false;
        protected override bool NeedsNormalAtZeroDepth => true;
        protected override bool AlwaysBuildSurfaceNormal => CurrentLevel != 0;

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
        private Texture2D m_GeneratedNormal;

        // Preserve the custom texture without blurring its borders. Rotation remains free.
        protected override bool BlendTextureSeams => CurrentLevel == 0;
        protected override float TextureSampleOffset => CurrentLevel != 0 ? 0f : 0.5f;

        protected override bool MatchesShader(Material material) => material.shader != null &&
            material.shader.name == "BH/NetCompositionMeshLitShader" &&
            material.name.StartsWith("Batch (TrainSubwayTrack_", StringComparison.Ordinal);

        protected override bool TryGetOriginal(Material material, out string colorSlot, out string normalSlot,
            out Texture color, out Texture normal)
        {
            colorSlot = "_WorldspaceAlbedo";
            normalSlot = "_WorldspaceNormalMap";
            color = material.HasProperty(colorSlot) ? material.GetTexture(colorSlot) : null;
            normal = material.HasProperty(normalSlot) ? material.GetTexture(normalSlot) : null;
            bool matched = color != null && normal != null &&
                color.name.StartsWith("TrainGravelWorldspace_BaseColor_", StringComparison.Ordinal) &&
                normal.name.StartsWith("TrainGravelWorldspace_Normal_", StringComparison.Ordinal);
            if (matched && m_OriginalWorldspaceNormal == null) m_OriginalWorldspaceNormal = normal;
            return matched;
        }

        protected override Texture2D GenerateNeutralAsphalt() => LoadBundledBallast(false);

        private static Texture2D LoadBundledBallast(bool normal)
        {
            string resource = "Advanced_Graphics.RailGravel_" + (normal ? "Normal" : "BaseColor") + ".png";
            Texture2D texture = null;
            try
            {
                using (var stream = typeof(RailGravelSystem).Assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null) throw new InvalidDataException("Missing embedded texture: " + resource);
                    using (var buffer = new MemoryStream())
                    {
                        stream.CopyTo(buffer);
                        texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, normal);
                        if (!texture.LoadImage(buffer.ToArray(), false) || texture.width != 2048 || texture.height != 2048)
                            throw new InvalidDataException("Invalid embedded ballast texture: " + resource);
                    }
                }
                texture.name = "Advanced Graphics Generated Rail Ballast " + (normal ? "Normal" : "Color");
                return texture;
            }
            catch
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
                throw;
            }
        }

        protected override Texture2D BuildSurfaceNormal(Texture2D color, int depth, int uvSize, byte[] crackMask)
        {
            if (CurrentLevel == 1)
            {
                if (m_GeneratedNormal == null)
                    m_GeneratedNormal = LoadBundledBallast(true);
                var source = m_GeneratedNormal;
                if (source != null)
                {
                    Texture2D transformed = null;
                    try
                    {
                        transformed = Transform(source, true, true);
                        var pixels = transformed.GetPixels32();
                        EncodeNormalPixels(pixels);
                        transformed.SetPixels32(pixels);
                        transformed.Apply(true, true);
                        transformed.name = "Advanced Graphics Rail Custom Normal";
                        return transformed;
                    }
                    catch
                    {
                        if (transformed != null) UnityEngine.Object.Destroy(transformed);
                        throw;
                    }
                }
            }
            if (CurrentLevel == 0)
            {
                var original = BuildOriginalNormal();
                if (original != null) return original;
            }
            int boundedDepth = Mathf.RoundToInt(100f * Mathf.Sqrt(depth / 100f));
            return base.BuildSurfaceNormal(color, boundedDepth, 100, crackMask);
        }

        private Texture2D BuildOriginalNormal()
        {
            Texture source = m_OriginalWorldspaceNormal;
            if (source == null)
            foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (material == null || !MatchesShader(material)) continue;
                var candidate = material.GetTexture("_WorldspaceNormalMap");
                if (candidate != null && candidate.name.StartsWith("TrainGravelWorldspace_Normal_", StringComparison.Ordinal))
                {
                    source = candidate;
                    break;
                }
            }
            if (source == null || source.width != source.height || source.width > 4096) return null;

            var target = RenderTexture.GetTemporary(source.width, source.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            Texture2D decoded = null, transformed = null;
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
                    // The game stores tangent X in alpha and Y in green.
                    float x = pixel.a / 255f * 2f - 1f;
                    float y = pixel.g / 255f * 2f - 1f;
                    float z = Mathf.Sqrt(Mathf.Max(0.001f, 1f - x * x - y * y));
                    pixel.r = pixel.a;
                    pixel.b = (byte)Mathf.RoundToInt(z * 127.5f + 127.5f);
                    pixel.a = 255;
                    pixels[i] = pixel;
                }
                decoded.SetPixels32(pixels);
                decoded.Apply(false, false);
                transformed = Transform(decoded, true, true);
                pixels = transformed.GetPixels32();
                EncodeNormalPixels(pixels);
                transformed.SetPixels32(pixels);
                transformed.Apply(true, true);
                transformed.name = "Advanced Graphics Rail Original Normal";
                return transformed;
            }
            catch (Exception ex)
            {
                if (transformed != null) UnityEngine.Object.Destroy(transformed);
                Mod.log.Error(ex, "Rail original normal could not be adjusted.");
                return null;
            }
            finally
            {
                if (decoded != null) UnityEngine.Object.Destroy(decoded);
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        protected override void OnSurfaceMaterialApplied(Material material)
        {
            if (!m_Atlases.TryGetValue(material, out var pair))
            {
                if (m_UnavailableAtlases.Contains(material)) return;
                var originalColor = material.GetTexture(AtlasColorSlot);
                var originalNormal = material.GetTexture(AtlasNormalSlot);
                if (originalColor == null || originalNormal == null || !material.HasProperty(WorldspaceScaleSlot) ||
                    !originalColor.name.StartsWith("TrainSubwayTrack_BaseColor_", StringComparison.Ordinal) ||
                    !originalNormal.name.StartsWith("TrainSubwayTrack_Normal_", StringComparison.Ordinal) ||
                    originalColor.width != 2048 || originalColor.height != 4096 ||
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
                    Mod.log.Error(ex, "Rail ballast base atlas could not be prepared.");
                    return;
                }
            }

            bool useFlat = CurrentLevel != 0 || CurrentUvSize != 100 || CurrentDepth != 0;
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
            if (m_GeneratedNormal != null) UnityEngine.Object.Destroy(m_GeneratedNormal);
            m_GeneratedNormal = null;
            foreach (var pair in m_Atlases.Values)
            {
                if (pair.FlatColor != null) UnityEngine.Object.Destroy(pair.FlatColor);
                if (pair.FlatNormal != null) UnityEngine.Object.Destroy(pair.FlatNormal);
            }
            m_Atlases.Clear();
            m_UnavailableAtlases.Clear();
            m_OriginalWorldspaceNormal = null;
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
                int width = source.width, band = width / 4;
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
                copy.name = "Advanced Graphics Rail Base " + (normal ? "Normal" : "Color");
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




