using System;
using UnityEngine;

namespace Advanced_Graphics
{
    public partial class FootpathSurfaceSystem : RoadSurfaceSystemBase
    {
        protected override string TextureFolder => TextureStorage.Footpath;
        protected override string TextureFileName => "Footpath_BaseColor.png";
        protected override string SurfaceName => "Footpath";
        protected override int MinimumUvSize => 10;
        protected override bool CacheSourceColor => true;
        protected override bool ReuseColorForDepthChanges => true;
        protected override bool AlwaysBuildSurfaceNormal => true;
        protected override int SeamBlendDivisor => 64;
        protected override float AuthoredNormalStrength => CurrentLevel == 1 ? CurrentDepth / 100f : Mathf.Pow(5f, CurrentDepth / 1000f);
        private Texture m_OriginalNormal;
        private Texture2D m_DecodedNormal;
        private Texture2D m_GeneratedNormal;

        protected override bool MatchesShader(Material material) => material.shader != null &&
            material.shader.name == "BH/NetCompositionMeshLitShader" &&
            material.name.StartsWith("Batch (PathwayPavement", StringComparison.Ordinal);

        protected override bool TryGetOriginal(Material material, out string colorSlot, out string normalSlot,
            out Texture color, out Texture normal)
        {
            colorSlot = "_WorldspaceAlbedo";
            normalSlot = "_WorldspaceNormalMap";
            color = material.HasProperty(colorSlot) ? material.GetTexture(colorSlot) : null;
            normal = material.HasProperty(normalSlot) ? material.GetTexture(normalSlot) : null;
            bool matched = color != null && normal != null &&
                color.name.StartsWith("PathwayPavementWorldspace_BaseColor_", StringComparison.Ordinal) &&
                normal.name.StartsWith("PathwayPavementWorldspace_Normal_", StringComparison.Ordinal);
            if (matched && m_OriginalNormal == null) m_OriginalNormal = normal;
            return matched;
        }

        protected override Texture2D GenerateNeutralAsphalt() => RailGravelSystem.LoadBundledBallast(false);

        protected override Texture2D BuildSurfaceNormal(Texture2D color, int depth, int uvSize, byte[] crackMask)
        {
            if (CurrentLevel == 1)
            {
                if (m_GeneratedNormal == null)
                    m_GeneratedNormal = RailGravelSystem.LoadBundledBallast(true);
                Texture2D transformed = null;
                try
                {
                    transformed = Transform(m_GeneratedNormal, true, true);
                    var pixels = transformed.GetPixels32();
                    EncodeNormalPixels(pixels);
                    transformed.SetPixels32(pixels);
                    transformed.Apply(true, true);
                    return transformed;
                }
                catch
                {
                    if (transformed != null) UnityEngine.Object.Destroy(transformed);
                    throw;
                }
            }
            // Original mode preserves the game's authored footpath relief, including at depth 0.
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
            if (m_GeneratedNormal != null) UnityEngine.Object.Destroy(m_GeneratedNormal);
            m_GeneratedNormal = null;
            m_OriginalNormal = null;
        }
    }
}
