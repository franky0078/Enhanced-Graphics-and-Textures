using System;
using System.IO;
using UnityEngine;

namespace Advanced_Graphics
{
    public abstract partial class RoadSurfaceSystemBase
    {
        private Texture2D m_CustomNormal;
        private bool m_CustomNormalLoaded;
        internal bool HasCustomNormal => m_UseCustomNormal && CurrentLevel == 2 && m_CustomNormal != null;
        protected virtual float CustomNormalStrength => CurrentDepth / 100f;
        private bool ShouldBuildSurfaceNormal => CurrentDepth > 0 ||
            (m_CrackCount > 0 && m_CrackOpacity > 0 && CrackDepth > 0) || AlwaysBuildSurfaceNormal ||
            (CurrentLevel == 1 && NeedsNormalAtZeroDepth) ||
            (CurrentLevel == 2 && LoadCustomNormal() != null);

        private void ClearCustomNormal()
        {
            if (m_CustomNormal != null) UnityEngine.Object.Destroy(m_CustomNormal);
            m_CustomNormal = null;
            m_CustomNormalLoaded = false;
        }

        // Optional, unpacked RGB tangent-space normal (OpenGL/+Y), loaded as linear data.
        private Texture2D LoadCustomNormal()
        {
            if (!m_UseCustomNormal) return null;
            if (m_CustomNormalLoaded) return m_CustomNormal;
            m_CustomNormalLoaded = true;
            string path = Path.Combine(TextureFolder, TextureFileName.Replace("_BaseColor.png", "_Normal.png"));
            if (!File.Exists(path)) return null;
            Texture2D loaded = null;
            try
            {
                if (new FileInfo(path).Length > 32 * 1024 * 1024)
                    throw new InvalidDataException("Custom normal PNG exceeds 32 MB.");
                loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                if (!loaded.LoadImage(File.ReadAllBytes(path), false) ||
                    loaded.width != loaded.height || loaded.width > 4096)
                    throw new InvalidDataException("Custom normal must be a square PNG, at most 4096 pixels wide.");
                var pixels = loaded.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    var p = pixels[i];
                    var n = new Vector3(p.r / 127.5f - 1f, p.g / 127.5f - 1f,
                        Mathf.Max(0.001f, p.b / 127.5f - 1f)).normalized;
                    pixels[i] = new Color32(
                        (byte)Mathf.Clamp((n.x + 1f) * 127.5f, 0f, 255f),
                        (byte)Mathf.Clamp((n.y + 1f) * 127.5f, 0f, 255f),
                        (byte)Mathf.Clamp((n.z + 1f) * 127.5f, 0f, 255f), 255);
                }
                loaded.SetPixels32(pixels);
                loaded.Apply(false, false);
                m_CustomNormal = loaded;
                return loaded;
            }
            catch (Exception ex)
            {
                if (loaded != null) UnityEngine.Object.Destroy(loaded);
                Mod.log.Error(ex, SurfaceName + " custom normal could not be loaded; using the previous relief calculation.");
                return null;
            }
        }


        // Select the optional map before calling surface-specific fallback calculations.
        private Texture2D BuildSelectedNormal(Texture2D color, int depth, int uvSize, byte[] crackMask)
        {
            var source = CurrentLevel == 2 ? LoadCustomNormal() : null;
            if (source == null) return BuildSurfaceNormal(color, depth, uvSize, crackMask);
            Texture2D normal = null;
            try
            {
                normal = Transform(source, true, true, CustomNormalStrength, color.width);
                var pixels = normal.GetPixels32();
                // Preserve the additional relief of the existing crack overlay.
                if (crackMask != null && CrackDepth > 0 && m_StampedCrackNormals == null)
                {
                    int width = normal.width, height = normal.height;
                    float strength = 0.008f * CrackDepth / 100f;
                    for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        int i = y * width + x;
                        int left = y * width + (x + width - 1) % width;
                        int right = y * width + (x + 1) % width;
                        int down = ((y + height - 1) % height) * width + x;
                        int up = ((y + 1) % height) * width + x;
                        var p = pixels[i];
                        float z = Mathf.Max(0.001f, p.b / 127.5f - 1f);
                        var n = new Vector3((p.r / 127.5f - 1f) / z + (crackMask[right] - crackMask[left]) * strength,
                            (p.g / 127.5f - 1f) / z + (crackMask[up] - crackMask[down]) * strength, 1f).normalized;
                        pixels[i] = new Color32((byte)Mathf.Clamp((n.x + 1f) * 127.5f, 0f, 255f),
                            (byte)Mathf.Clamp((n.y + 1f) * 127.5f, 0f, 255f),
                            (byte)Mathf.Clamp((n.z + 1f) * 127.5f, 0f, 255f), 255);
                    }
                }
                ApplyAuthoredCrackNormals(pixels, crackMask, CrackDepth);
                EncodeNormalPixels(pixels);
                normal.SetPixels32(pixels);
                normal.Apply(true, !KeepTileReadable);
                normal.name = "Advanced Graphics " + SurfaceName + " Custom Normal";
                return normal;
            }
            catch
            {
                if (normal != null) UnityEngine.Object.Destroy(normal);
                throw;
            }
        }
    }
}
