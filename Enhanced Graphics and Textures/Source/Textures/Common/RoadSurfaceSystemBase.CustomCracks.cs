using System;
using System.IO;
using UnityEngine;

namespace Advanced_Graphics
{
    public abstract partial class RoadSurfaceSystemBase
    {
        private const int CrackMaskSize = 256;
        private bool m_CustomCracks, m_CrackLoadAttempted;
        private byte[] m_CustomCrackMask;
        private Color32[] m_CustomCrackNormals, m_StampedCrackNormals, m_ColorBeforeCracks;
        private byte[] m_CustomCrackWidthMask;
        private int m_CachedCrackWidth = -1;
        private float m_CustomCrackAspect = 1f;
        // 0 = not loaded, 1 = loaded, 2 = missing, 3 = invalid (procedural fallback).
        internal int CustomCrackStatus { get; private set; }

        private void ClearCrackCache()
        {
            m_CustomCrackMask = null;
            m_CustomCrackNormals = m_StampedCrackNormals = m_ColorBeforeCracks = null;
            m_CustomCrackWidthMask = null;
            m_CachedCrackWidth = -1;
            m_CustomCrackAspect = 1f;
            m_CrackLoadAttempted = false;
            CustomCrackStatus = 0;
        }

        private byte[] LoadCrackMask()
        {
            if (m_CrackLoadAttempted) return m_CustomCrackMask;
            m_CrackLoadAttempted = true;
            string path = Path.Combine(TextureStorage.RoadCracks, "Road_Cracks.png");
            if (!File.Exists(path)) { CustomCrackStatus = 2; return null; }
            Texture2D texture = null;
            try
            {
                if (new FileInfo(path).Length > 32 * 1024 * 1024)
                    throw new InvalidDataException("Crack PNG exceeds 32 MB.");
                byte[] data = File.ReadAllBytes(path);
                byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
                if (data.Length < 24) throw new InvalidDataException("Invalid crack PNG header.");
                for (int i = 0; i < signature.Length; i++)
                    if (data[i] != signature[i]) throw new InvalidDataException("Crack texture must be a PNG.");
                // Validate dimensions before Unity allocates the decoded image.
                uint width = ReadPngDimension(data, 16), height = ReadPngDimension(data, 20);
                if (width == 0 || height == 0 || width > 4096 || height > 4096)
                    throw new InvalidDataException("Crack PNG dimensions must be 1–4096 pixels.");
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                if (!texture.LoadImage(data, false)) throw new InvalidDataException("Could not decode crack PNG.");
                var pixels = texture.GetPixels32();
                var normalPixels = LoadCrackNormalPixels(texture.width, texture.height);
                var normalMask = normalPixels != null ? new Color32[CrackMaskSize * CrackMaskSize] : null;
                var mask = new byte[CrackMaskSize * CrackMaskSize];
                bool hasCracks = false;
                for (int y = 0; y < CrackMaskSize; y++)
                for (int x = 0; x < CrackMaskSize; x++)
                {
                    // Preserve narrow cracks when a large image is reduced to the cached mask.
                    int x0 = x * texture.width / CrackMaskSize;
                    int y0 = y * texture.height / CrackMaskSize;
                    int x1 = Math.Min(texture.width, Math.Max(x0 + 1, (x + 1) * texture.width / CrackMaskSize));
                    int y1 = Math.Min(texture.height, Math.Max(y0 + 1, (y + 1) * texture.height / CrackMaskSize));
                    byte value = 0;
                    Color32 normalValue = new Color32(128, 128, 255, 255);
                    for (int py = y0; py < y1; py++)
                    for (int px = x0; px < x1; px++)
                    {
                        var p = pixels[py * texture.width + px];
                        float darkness = 1f - (p.r * 0.299f + p.g * 0.587f + p.b * 0.114f) / 255f;
                        byte strength = (byte)Mathf.Clamp(darkness * p.a, 0f, 255f);
                        if (strength > value)
                        {
                            value = strength;
                            if (normalPixels != null) normalValue = normalPixels[py * texture.width + px];
                        }
                    }
                    mask[y * CrackMaskSize + x] = value;
                    if (normalMask != null) normalMask[y * CrackMaskSize + x] = normalValue;
                    hasCracks |= value > 0;
                }
                if (!hasCracks) throw new InvalidDataException("Crack PNG has no visible dark pixels.");
                m_CustomCrackMask = mask;
                m_CustomCrackNormals = normalMask;
                m_CustomCrackAspect = (float)texture.width / texture.height;
                CustomCrackStatus = 1;
            }
            catch (Exception ex)
            {
                CustomCrackStatus = 3;
                Mod.log.Error(ex, "Custom road cracks could not be loaded; using generated cracks.");
            }
            finally { if (texture != null) UnityEngine.Object.Destroy(texture); }
            return m_CustomCrackMask;
        }

        private Color32[] LoadCrackNormalPixels(int width, int height)
        {
            if (!m_UseCustomNormal) return null;
            string path = Path.Combine(TextureStorage.RoadCracks, "Road_Cracks_Normal.png");
            if (!File.Exists(path)) return null;
            Texture2D normal = null;
            try
            {
                if (new FileInfo(path).Length > 32 * 1024 * 1024)
                    throw new InvalidDataException("Crack normal PNG exceeds 32 MB.");
                byte[] data = File.ReadAllBytes(path);
                byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
                if (data.Length < 24) throw new InvalidDataException("Invalid crack normal PNG.");
                for (int i = 0; i < signature.Length; i++)
                    if (data[i] != signature[i]) throw new InvalidDataException("Crack normal must be a PNG.");
                if (ReadPngDimension(data, 16) != width || ReadPngDimension(data, 20) != height)
                    throw new InvalidDataException("Road_Cracks_Normal.png must have the same dimensions as Road_Cracks.png.");
                normal = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                if (!normal.LoadImage(data, false)) throw new InvalidDataException("Could not decode crack normal PNG.");
                return normal.GetPixels32();
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex, "Custom crack normal could not be loaded; using automatic crack relief.");
                return null;
            }
            finally { if (normal != null) UnityEngine.Object.Destroy(normal); }
        }

        private static Color32 EncodeCrackNormal(Vector3 n) => new Color32(
            (byte)Mathf.Clamp((n.x + 1f) * 127.5f, 0f, 255f),
            (byte)Mathf.Clamp((n.y + 1f) * 127.5f, 0f, 255f),
            (byte)Mathf.Clamp((n.z + 1f) * 127.5f, 0f, 255f), 255);

        private void ApplyAuthoredCrackNormals(Color32[] pixels, byte[] mask, int depth)
        {
            if (m_StampedCrackNormals == null || mask == null || depth == 0) return;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (mask[i] == 0) continue;
                var crack = m_StampedCrackNormals[i];
                if (crack.a == 0) continue;
                var p = pixels[i];
                float baseZ = Mathf.Max(0.001f, p.b / 127.5f - 1f);
                float crackZ = Mathf.Max(0.001f, crack.b / 127.5f - 1f);
                float gain = depth / 100f * mask[i] / 255f;
                var n = new Vector3((p.r / 127.5f - 1f) / baseZ + (crack.r / 127.5f - 1f) / crackZ * gain,
                    (p.g / 127.5f - 1f) / baseZ + (crack.g / 127.5f - 1f) / crackZ * gain, 1f).normalized;
                pixels[i] = EncodeCrackNormal(n);
            }
        }

        private static uint ReadPngDimension(byte[] data, int offset) =>
            ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) |
            ((uint)data[offset + 2] << 8) | data[offset + 3];

        private byte[] GetCrackWidthMask()
        {
            if (m_CrackWidth == 100) return m_CustomCrackMask;
            if (m_CachedCrackWidth == m_CrackWidth && m_CustomCrackWidthMask != null)
                return m_CustomCrackWidthMask;
            float amount = Mathf.Abs(m_CrackWidth / 100f - 1f) * 0.75f;
            int radius = Mathf.CeilToInt(amount);
            float blend = Mathf.Min(1f, amount);
            bool grow = m_CrackWidth > 100;
            var horizontal = new byte[m_CustomCrackMask.Length];
            var result = new byte[m_CustomCrackMask.Length];
            for (int y = 0; y < CrackMaskSize; y++)
            for (int x = 0; x < CrackMaskSize; x++)
            {
                byte value = grow ? (byte)0 : (byte)255;
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int px = x + dx;
                    byte sample = px >= 0 && px < CrackMaskSize ? m_CustomCrackMask[y * CrackMaskSize + px] : (byte)0;
                    if (grow ? sample > value : sample < value) value = sample;
                }
                horizontal[y * CrackMaskSize + x] = value;
            }
            for (int y = 0; y < CrackMaskSize; y++)
            for (int x = 0; x < CrackMaskSize; x++)
            {
                byte value = grow ? (byte)0 : (byte)255;
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int py = y + dy;
                    byte sample = py >= 0 && py < CrackMaskSize ? horizontal[py * CrackMaskSize + x] : (byte)0;
                    if (grow ? sample > value : sample < value) value = sample;
                }
                int index = y * CrackMaskSize + x;
                result[index] = (byte)Mathf.RoundToInt(Mathf.Lerp(m_CustomCrackMask[index], value, blend));
            }
            m_CachedCrackWidth = m_CrackWidth;
            m_CustomCrackWidthMask = result;
            return result;
        }

        private void StampCrack(byte[] target, int size, byte[] pattern, float x, float y, float angle, float scale, bool mirror)
        {
            // Match the approximate length of the existing generated cracks.
            float height = Mathf.Clamp(96f * scale * size / 1024f, 4f, size * 2f);
            float width = height * m_CustomCrackAspect;
            if (m_StampedCrackNormals != null) width *= Mathf.Max(0.01f, m_CrackWidth / 100f);
            float fit = Mathf.Min(1f, size * 2f / Mathf.Max(width, height));
            height *= fit;
            width *= fit;
            float cosine = Mathf.Cos(angle), sine = Mathf.Sin(angle);
            int extentX = Mathf.CeilToInt((Mathf.Abs(cosine) * width + Mathf.Abs(sine) * height) * 0.5f);
            int extentY = Mathf.CeilToInt((Mathf.Abs(sine) * width + Mathf.Abs(cosine) * height) * 0.5f);
            int centerX = Mathf.RoundToInt(x), centerY = Mathf.RoundToInt(y);
            for (int dy = -extentY; dy <= extentY; dy++)
            for (int dx = -extentX; dx <= extentX; dx++)
            {
                float u = (cosine * dx + sine * dy) / width + 0.5f;
                float v = (-sine * dx + cosine * dy) / height + 0.5f;
                if (u < 0f || u >= 1f || v < 0f || v >= 1f) continue;
                if (mirror) u = 1f - u;
                int sx = Math.Min(CrackMaskSize - 1, (int)(u * CrackMaskSize));
                int sy = Math.Min(CrackMaskSize - 1, (int)(v * CrackMaskSize));
                byte value = pattern[sy * CrackMaskSize + sx];
                if (value == 0) continue;
                int px = ((centerX + dx) % size + size) % size;
                int py = ((centerY + dy) % size + size) % size;
                int index = py * size + px;
                if (value > target[index])
                {
                    target[index] = value;
                    if (m_StampedCrackNormals != null)
                    {
                        var p = m_CustomCrackNormals[sy * CrackMaskSize + sx];
                        float nx = (p.r / 127.5f - 1f) * (mirror ? -1f : 1f);
                        float ny = p.g / 127.5f - 1f;
                        var n = new Vector3(nx * cosine - ny * sine, nx * sine + ny * cosine,
                            Mathf.Max(0.001f, p.b / 127.5f - 1f)).normalized;
                        m_StampedCrackNormals[index] = EncodeCrackNormal(n);
                    }
                }
            }
        }
    }
}

