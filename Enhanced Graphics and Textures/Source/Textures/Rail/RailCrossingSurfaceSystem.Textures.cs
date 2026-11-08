using UnityEngine;

namespace Advanced_Graphics
{
    public partial class RailCrossingSurfaceSystem
    {
        private void BuildCover(Texture2D source, Cover cover)
        {
            if (m_Config.Mode == Setting.CrossingThrough)
            {
                cover.Color = WriteTexture(source, 1, 1, new[] { new Color32(0, 0, 0, 0) }, false, "Transparent");
                return;
            }

            int width = source.width, height = source.height;
            var colors = ReadPixels(source, false);
            var heights = new byte[colors.Length];
            bool custom = m_Config.Mode == Setting.CrossingCustom && m_CustomColor != null;
            bool useCustomNormal = custom && m_CustomNormal != null;
            var normals = cover.OriginalNormal != null ? new Color32[colors.Length] : null;
            float radians = m_Config.Rotation * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians), cos = Mathf.Cos(radians), scale = 100f / m_Config.Uv;
            float offsetX = m_Config.X / 100f, offsetY = m_Config.Y / 100f;
            float opacity = m_Config.Opacity / 100f;
            float red = Mathf.Lerp(1f, ((m_Config.Color >> 16) & 255) / 255f, opacity);
            float green = Mathf.Lerp(1f, ((m_Config.Color >> 8) & 255) / 255f, opacity);
            float blue = Mathf.Lerp(1f, (m_Config.Color & 255) / 255f, opacity);
            float depth = m_Config.Depth / 100f;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                var original = colors[index];
                float centeredX = (x + 0.5f) / width - 0.5f, centeredY = (y + 0.5f) / height - 0.5f;
                float u = (centeredX * cos - centeredY * sin) * scale + 0.5f + offsetX;
                float v = (centeredX * sin + centeredY * cos) * scale + 0.5f + offsetY;
                Color32 color;
                if (custom)
                    color = m_CustomColor.GetPixelBilinear(Mathf.Repeat(u, 1f), Mathf.Repeat(v, 1f));
                else
                {
                    float luminance = Luminance(original) / 255f;
                    byte shade = (byte)(RoadSurfaceSystemBase.NeutralAsphaltShade(
                        Mathf.FloorToInt(u * width), Mathf.FloorToInt(v * height)) * Mathf.Lerp(0.5f, 1f, luminance));
                    color = new Color32(shade, (byte)(shade + 1), (byte)(shade + 2), 255);
                }
                heights[index] = (byte)Luminance(color);
                colors[index] = new Color32((byte)(color.r * red), (byte)(color.g * green), (byte)(color.b * blue),
                    (byte)((original.a * color.a + 127) / 255));

                if (normals == null || !useCustomNormal) continue;
                Color32 normal = m_CustomNormal.GetPixelBilinear(Mathf.Repeat(u, 1f), Mathf.Repeat(v, 1f));
                float nx = normal.r / 127.5f - 1f, ny = normal.g / 127.5f - 1f;
                normals[index] = PackNormal((nx * cos + ny * sin) * depth, (-nx * sin + ny * cos) * depth,
                    Mathf.Max(0.01f, normal.b / 127.5f - 1f));
            }

            cover.Color = WriteTexture(source, width, height, colors, false, "Color");
            if (normals != null)
            {
                if (!useCustomNormal)
                {
                    float relief = 0.025f * depth;
                    for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        int row = y * width;
                        int left = row + Mathf.Max(0, x - 1), right = row + Mathf.Min(width - 1, x + 1);
                        int down = Mathf.Max(0, y - 1) * width + x, up = Mathf.Min(height - 1, y + 1) * width + x;
                        normals[row + x] = PackNormal((heights[left] - heights[right]) * relief,
                            (heights[down] - heights[up]) * relief, 1f);
                    }
                }
                cover.Normal = WriteTexture(cover.OriginalNormal, width, height, normals, true, "Normal");
            }
            if (cover.OriginalMask != null && m_Config.Gloss != 100)
            {
                var mask = ReadPixels(cover.OriginalMask, true);
                for (int i = 0; i < mask.Length; i++)
                {
                    mask[i].r = (byte)(mask[i].r * m_Config.Gloss / 100);
                    mask[i].g = (byte)(mask[i].g * m_Config.Gloss / 100);
                    mask[i].a = (byte)(mask[i].a * m_Config.Gloss / 100);
                }
                cover.Mask = WriteTexture(cover.OriginalMask, cover.OriginalMask.width, cover.OriginalMask.height,
                    mask, true, "Mask");
            }
        }

        private static float Luminance(Color32 pixel) => pixel.r * 0.2126f + pixel.g * 0.7152f + pixel.b * 0.0722f;

        private static Color32 PackNormal(float x, float y, float z)
        {
            TextureMath.Normalize(ref x, ref y, ref z);
            byte packedX = (byte)Mathf.Clamp(Mathf.RoundToInt(127.5f * (x + 1f)), 0, 255);
            byte packedY = (byte)Mathf.Clamp(Mathf.RoundToInt(127.5f * (y + 1f)), 0, 255);
            return new Color32(255, packedY, packedY, packedX);
        }

        private static Color32[] ReadPixels(Texture source, bool linear)
        {
            var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32,
                linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Texture2D copy = null;
            try
            {
                UnityEngine.Graphics.Blit(source, target);
                RenderTexture.active = target;
                copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, linear);
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
                return copy.GetPixels32();
            }
            finally
            {
                if (copy != null) UnityEngine.Object.Destroy(copy);
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static Texture2D WriteTexture(Texture source, int width, int height, Color32[] pixels, bool linear, string name)
        {
            bool mipmaps = width > 1 && ((Texture2D)source).mipmapCount > 1;
            var result = new Texture2D(width, height, TextureFormat.RGBA32, mipmaps, linear);
            try
            {
                result.name = "EGT Rail Crossing " + name;
                result.filterMode = source.filterMode;
                result.wrapModeU = source.wrapModeU;
                result.wrapModeV = source.wrapModeV;
                result.anisoLevel = source.anisoLevel;
                result.mipMapBias = source.mipMapBias;
                result.SetPixels32(pixels);
                result.Apply(mipmaps, true);
                return result;
            }
            catch
            {
                UnityEngine.Object.Destroy(result);
                throw;
            }
        }
    }
}
