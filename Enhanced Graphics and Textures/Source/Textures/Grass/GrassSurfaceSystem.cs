using System;
using UnityEngine;

namespace Advanced_Graphics
{
    // The survey identifies RoadGrass as a dedicated worldspace material.
    // Deliberately exclude AreaDecalShader and terrain, even when they use Grass01.
    public partial class GrassSurfaceSystem : RoadSurfaceSystemBase
    {
        protected override string TextureFolder => TextureStorage.Grass;
        protected override string TextureFileName => "Grass_BaseColor.png";
        protected override string SurfaceName => "Roadside grass";
        protected override bool CacheSourceColor => true;
        protected override bool ReuseColorForDepthChanges => true;
        protected override bool AlwaysBuildSurfaceNormal => true;
        protected override int SeamBlendDivisor => 64;
        protected override float AuthoredNormalStrength => Mathf.Pow(5f, CurrentDepth / 1000f);
        private Texture m_OriginalNormal;
        private Texture2D m_DecodedNormal;

        protected override bool MatchesShader(Material material) => material.shader != null &&
            material.shader.name == "BH/NetCompositionMeshLitShader" &&
            material.name.StartsWith("Batch (RoadGrass_", StringComparison.Ordinal);

        protected override bool TryGetOriginal(Material material, out string colorSlot, out string normalSlot,
            out Texture color, out Texture normal)
        {
            colorSlot = "_WorldspaceAlbedo";
            normalSlot = "_WorldspaceNormalMap";
            color = material.HasProperty(colorSlot) ? material.GetTexture(colorSlot) : null;
            normal = material.HasProperty(normalSlot) ? material.GetTexture(normalSlot) : null;
            bool matched = color != null && normal != null &&
                color.name.StartsWith("Grass01_BaseColor", StringComparison.Ordinal) &&
                normal.name.StartsWith("Grass01_Normal", StringComparison.Ordinal);
            if (matched && m_OriginalNormal == null) m_OriginalNormal = normal;
            return matched;
        }

        protected override Texture2D GenerateNeutralAsphalt()
        {
            // Periodic low-frequency variation and fine blades avoid large color patches.
            const int size = 1024;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                uint h;
                unchecked
                {
                    h = (uint)(x * 374761393 + y * 668265263);
                    h = (h ^ (h >> 13)) * 1274126177u;
                    h ^= h >> 16;
                }
                float fine = (h & 255u) / 255f - 0.5f;
                float soft = Mathf.Sin(x * 2f * Mathf.PI / size) * Mathf.Sin(y * 4f * Mathf.PI / size);
                float blade = ((x + y * 3) % 11 < 2 ? 3f : 0f);
                float brightness = fine * 14f + soft * 3f + blade;
                pixels[y * size + x] = new Color32(
                    (byte)Mathf.Clamp(78f + brightness, 0f, 255f),
                    (byte)Mathf.Clamp(92f + brightness, 0f, 255f),
                    (byte)Mathf.Clamp(48f + brightness * 0.7f, 0f, 255f), 255);
            }
            var result = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            result.SetPixels32(pixels);
            result.Apply(false, false);
            return result;
        }

        protected override Texture2D BuildSurfaceNormal(Texture2D color, int depth, int uvSize, byte[] crackMask)
        {
            // Original mode preserves the game's authored grass relief, including at depth 0.
            // Generated/custom modes derive relief from the selected color texture.
            if (CurrentLevel != 0) return base.BuildSurfaceNormal(color, depth, uvSize, crackMask);
            if (m_DecodedNormal == null && m_OriginalNormal != null)
            {
                var source = m_OriginalNormal;
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
                        var p = pixels[i];
                        float x = p.a / 255f * 2f - 1f, y = p.g / 255f * 2f - 1f;
                        float z = Mathf.Sqrt(Mathf.Max(0.001f, 1f - x * x - y * y));
                        pixels[i] = new Color32(p.a, p.g, (byte)Mathf.RoundToInt(z * 127.5f + 127.5f), 255);
                    }
                    decoded.SetPixels32(pixels);
                    decoded.Apply(false, false);
                    m_DecodedNormal = decoded;
                }
                catch { if (decoded != null) UnityEngine.Object.Destroy(decoded); throw; }
                finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
            }
            if (m_DecodedNormal == null) return base.BuildSurfaceNormal(color, depth, uvSize, crackMask);
            var normal = Transform(m_DecodedNormal, true, true);
            try
            {
                var pixels = normal.GetPixels32();
                EncodeNormalPixels(pixels);
                normal.SetPixels32(pixels);
                normal.Apply(true, true);
                return normal;
            }
            catch { UnityEngine.Object.Destroy(normal); throw; }
        }

        protected override void EncodeNormalPixels(Color32[] pixels)
        {
            // Same packed AG layout used by the existing NetComposition gravel worker.
            for (int i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                p.a = p.r;
                p.r = 255;
                p.b = p.g;
                pixels[i] = p;
            }
        }

        protected override void OnSurfaceReset()
        {
            if (m_DecodedNormal != null) UnityEngine.Object.Destroy(m_DecodedNormal);
            m_DecodedNormal = null;
            m_OriginalNormal = null;
        }
    }
}
