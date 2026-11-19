using System;
using System.IO;
using Unity.Collections;
using UnityEngine;

namespace Advanced_Graphics
{
    internal static class CurbTexture
    {
        internal static string Folder => Path.Combine(TextureStorage.Sidewalk, "Curb");
        private static Color32[] pixels, normalPixels;
        private static int normalWidth, normalHeight;
        private static bool useCustomNormal;
        private static int width, height, currentMode;
        internal static int Revision { get; private set; }
        private static int size = 100, depth = 100, rotation, offsetX, offsetY, tint = 0xffffff, opacity = 100;
        private static int jointSpacingCm, jointOpacity = 100;
        private static bool Transformed => size != 100 || rotation != 0 || offsetX != 0 || offsetY != 0;
        internal static bool NeedsNormal => pixels != null || Transformed || depth != 100 || (jointSpacingCm > 0 && jointOpacity > 0);
        internal static bool Active => pixels != null || (currentMode == 0 &&
            (NeedsNormal || TextureColorFilter.IsActive(tint, opacity)));

        internal static void Configure(int mode, Setting settings = null, bool reload = false)
        {
            mode = settings == null ? 0 : Mathf.Clamp(mode, 0, 2);
            int newSize = Mathf.Clamp(settings?.CurbSize ?? 100, 10, 3000);
            int newDepth = Mathf.Clamp(settings?.CurbDepth ?? 100, 0, 3000);
            int newRotation = Mathf.Clamp(settings?.CurbRotation ?? 0, 0, 360) % 360;
            int newX = Mathf.Clamp(settings?.CurbOffsetX ?? 0, -100, 100);
            int newY = Mathf.Clamp(settings?.CurbOffsetY ?? 0, -100, 100);
            int newTint = Mathf.Clamp(settings?.CurbColor ?? 0xffffff, 0, 0xffffff);
            int newOpacity = Mathf.Clamp(settings?.CurbOpacity ?? 100, 0, 100);
            bool nextNormal = mode == 2 && settings.UseCurbNormalMap;
            int newSpacing = Mathf.Clamp(settings?.CurbJointSpacing ?? 0, 0, 500);
            int newJointOpacity = Mathf.Clamp(settings?.CurbJointOpacity ?? 100, 0, 100);
            bool loadTile = mode != currentMode || (mode == 1 && (newSpacing == 0) != (jointSpacingCm == 0)) || (mode == 2 && (reload || nextNormal != useCustomNormal));
            if (!loadTile && newSize == size && newDepth == depth && newRotation == rotation &&
                newX == offsetX && newY == offsetY && newTint == tint && newOpacity == opacity && newSpacing == jointSpacingCm && newJointOpacity == jointOpacity) return;
            jointSpacingCm = newSpacing;
            jointOpacity = newJointOpacity;
            size = newSize;
            depth = newDepth;
            rotation = newRotation;
            offsetX = newX;
            offsetY = newY;
            tint = newTint;
            opacity = newOpacity;
            currentMode = mode;
            useCustomNormal = nextNormal;
            Revision++;
            if (!loadTile) return;
            pixels = null;
            normalPixels = null;
            if (mode == 1)
            {
                width = 64;
                height = 256;
                pixels = new Color32[width * height];
                var random = new System.Random(5483);
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int joint = Math.Min(y, height - 1 - y);
                    int shade = 158 + random.Next(-14, 15) + (int)(8 * Mathf.PerlinNoise(x / 13f, y / 18f));
                    if (jointSpacingCm == 0 && joint < 3) shade -= (3 - joint) * 15;
                    if (x < 3) shade += 5 - x;
                    pixels[y * width + x] = new Color32((byte)(shade + 2), (byte)shade, (byte)(shade - 3), 255);
                }
            }
            else if (mode == 2)
            {
                pixels = ReadPng("Curb_BaseColor.png", false, out width, out height);
                if (pixels != null && useCustomNormal)
                {
                    normalPixels = ReadPng("Curb_Normal.png", true, out normalWidth, out normalHeight);
                    if (normalPixels != null) RoadSurfaceSystemBase.NormalizeCustomNormal(normalPixels);
                }
            }
        }

        private static Color32[] ReadPng(string name, bool normal, out int w, out int h)
        {
            w = h = 0;
            Texture2D image = null;
            try
            {
                var file = new FileInfo(Path.Combine(Folder, name));
                if (normal && !file.Exists) return null;
                if (!file.Exists || file.Length > 32 * 1024 * 1024)
                    throw new InvalidDataException("Missing or oversized " + name);
                var bytes = File.ReadAllBytes(file.FullName);
                if (bytes.Length < 24 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71 ||
                    !ValidDimension(bytes, 16) || !ValidDimension(bytes, 20))
                    throw new InvalidDataException("Expected PNG up to 4096 x 4096.");
                image = new Texture2D(2, 2, TextureFormat.RGBA32, false, normal);
                if (!image.LoadImage(bytes)) throw new InvalidDataException("Invalid PNG.");
                w = image.width;
                h = image.height;
                return image.GetPixels32();
            }
            catch (Exception ex)
            {
                Mod.log.Warn(name + ": " + ex.Message);
                return null;
            }
            finally { if (image != null) UnityEngine.Object.Destroy(image); }
        }

        private static bool ValidDimension(byte[] data, int index)
        {
            int size = (data[index + 2] << 8) | data[index + 3];
            return data[index] == 0 && data[index + 1] == 0 && size > 0 && size <= 4096;
        }

        private static int Wrap(int i, int length)
        {
            int r = i % length;
            return r < 0 ? r + length : r;
        }

        private static float Luminance(Color32 p) => (p.r * 0.299f + p.g * 0.587f + p.b * 0.114f) / 255f;

        internal static void Paint(NativeArray<Color32> atlas, NativeArray<Color32> source,
            int w, int h, int start, int end, int reference, bool normal, float vPerMetre)
        {
            if (!Active || (normal && !NeedsNormal)) return;
            int left = Mathf.RoundToInt(w * (start / (float)reference));
            int span = Mathf.RoundToInt(w * (end / (float)reference)) - left;
            if (span <= 0) return;

            bool original = currentMode == 0;
            int tw = original ? span : width;
            int th = original ? h : height;
            int repeats = original ? 1 : Mathf.Max(1, Mathf.RoundToInt(h * (tw / (float)th) / span));
            float angle = rotation * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            float scale = 100f / size;
            float strength = depth / 100f;
            // Whole repeats keep the groove continuous across the atlas wrap.
            float jointWeight = jointOpacity / 100f;
            int joints = jointSpacingCm > 0 && jointOpacity > 0 ? Mathf.Max(1, Mathf.RoundToInt(100f / (jointSpacingCm * vPerMetre))) : 0;
            float halfWidth = Mathf.Max(1.5f, 0.014f * h * vPerMetre);
            for (int y = 0; y < h; y++)
            {
                float shade = 1f, jointSlope = 0f;
                if (joints > 0)
                {
                    float phase = (y + 0.5f) * joints / h;
                    float distance = (phase - Mathf.Round(phase)) * h / joints;
                    float t = Mathf.Clamp01(Mathf.Abs(distance) / halfWidth);
                    shade = Mathf.Lerp(1f - 0.40f * jointWeight, 1f, t * t * (3f - 2f * t));
                    jointSlope = -Mathf.Sign(distance) * 3.6f * jointWeight * t * (1f - t);
                }
                float v = ((y + 0.5f) * repeats / h - 0.5f) * th * scale;
                for (int x = 0; x < span; x++)
                {
                    float u = ((x + 0.5f) / span - 0.5f) * tw * scale;
                    float tileX = cos * u + sin * v + (0.5f + offsetX / 100f) * tw;
                    float tileY = -sin * u + cos * v + (0.5f + offsetY / 100f) * th;
                    int tx = Wrap(Mathf.FloorToInt(tileX), tw);
                    int ty = Wrap(Mathf.FloorToInt(tileY), th);
                    int i = y * w + left + x;
                    var dest = atlas[i];
                    var sample = original ? source[ty * w + left + tx] : pixels[ty * tw + tx];
                    if (!normal)
                    {
                        sample = TextureColorFilter.Apply(sample, tint, opacity);
                        if (shade < 1f)
                        {
                            sample.r = (byte)Mathf.RoundToInt(sample.r * shade);
                            sample.g = (byte)Mathf.RoundToInt(sample.g * shade);
                            sample.b = (byte)Mathf.RoundToInt(sample.b * shade);
                        }
                        sample.a = dest.a;
                        atlas[i] = sample;
                        continue;
                    }

                    // Road atlases store tangent X in alpha and tangent Y in green.
                    // Preserve the independent red and blue channels of the destination.
                    float nx, ny, nz;
                    if (original)
                    {
                        nx = sample.a / 127.5f - 1f;
                        ny = sample.g / 127.5f - 1f;
                        nz = Mathf.Sqrt(Mathf.Max(0.0001f, 1f - nx * nx - ny * ny));
                    }
                    else if (normalPixels != null)
                    {
                        int normalX = Wrap(Mathf.FloorToInt(tileX / tw * normalWidth), normalWidth);
                        int normalY = Wrap(Mathf.FloorToInt(tileY / th * normalHeight), normalHeight);
                        var n = normalPixels[normalY * normalWidth + normalX];
                        nx = n.r / 127.5f - 1f;
                        ny = n.g / 127.5f - 1f;
                        nz = Mathf.Max(0.001f, n.b / 127.5f - 1f);
                    }
                    else
                    {
                        nx = Luminance(pixels[ty * tw + Wrap(tx - 1, tw)]) - Luminance(pixels[ty * tw + Wrap(tx + 1, tw)]);
                        ny = Luminance(pixels[Wrap(ty - 1, th) * tw + tx]) - Luminance(pixels[Wrap(ty + 1, th) * tw + tx]);
                        nz = 1f;
                    }
                    float dx = cos * nx - sin * ny;
                    float dy = sin * nx + cos * ny + jointSlope * nz;
                    float k = strength / Mathf.Sqrt((dx * dx + dy * dy) * strength * strength + nz * nz);
                    dest.a = Encode(dx * k);
                    dest.g = Encode(dy * k);
                    atlas[i] = dest;
                }
            }
        }

        private static byte Encode(float component) =>
            (byte)Mathf.Clamp(Mathf.RoundToInt((component + 1f) * 127.5f), 0, 255);
    }
}
