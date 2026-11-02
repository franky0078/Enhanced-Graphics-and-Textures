using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Advanced_Graphics
{
    // RoadSurfaceSystemBase.Transform: UV size, offset, rotation, seam blending, normal strength.
    [BurstCompile]
    internal struct TransformRowsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Color32> Input;
        [NativeDisableParallelForRestriction] public NativeArray<Color32> Output;
        public int Size, OutputSize, Rotation;
        public float ShiftX, ShiftY, Period, SeamWidth, SampleOffset, Cosine, Sine, NormalStrength;
        public bool Linear;

        public void Execute(int y)
        {
            int outputSize = OutputSize;
            float period = Period, seamWidth = SeamWidth;
            for (int x = 0; x < outputSize; x++)
            {
                float u = (x + SampleOffset) * period / outputSize + ShiftX;
                float v = (y + SampleOffset) * period / outputSize + ShiftY;
                float dx = x < seamWidth ? period : x >= outputSize - seamWidth ? -period : 0f;
                float dy = y < seamWidth ? period : y >= outputSize - seamWidth ? -period : 0f;
                float wx = dx == 0f ? 0f : 0.5f * (1f - math.min(x, outputSize - 1 - x) / seamWidth);
                float wy = dy == 0f ? 0f : 0.5f * (1f - math.min(y, outputSize - 1 - y) / seamWidth);
                var pixel = SampleRotated(u, v);
                if (wx > 0f) pixel = Blend(pixel, SampleRotated(u + dx, v), wx);
                if (wy > 0f)
                {
                    var other = SampleRotated(u, v + dy);
                    if (wx > 0f) other = Blend(other, SampleRotated(u + dx, v + dy), wx);
                    pixel = Blend(pixel, other, wy);
                }
                if (Linear && Rotation != 0)
                {
                    float nx = pixel.r - 128f, ny = pixel.g - 128f;
                    pixel.r = (byte)math.clamp(128f + nx * Cosine - ny * Sine, 0f, 255f);
                    pixel.g = (byte)math.clamp(128f + nx * Sine + ny * Cosine, 0f, 255f);
                }
                if (Linear && NormalStrength != 1f)
                {
                    float nx = (pixel.r / 255f * 2f - 1f) * NormalStrength;
                    float ny = (pixel.g / 255f * 2f - 1f) * NormalStrength;
                    float nz = math.max(0.001f, pixel.b / 255f * 2f - 1f);
                    TextureMath.Normalize(ref nx, ref ny, ref nz);
                    pixel.r = (byte)math.clamp((nx * 0.5f + 0.5f) * 255f, 0f, 255f);
                    pixel.g = (byte)math.clamp((ny * 0.5f + 0.5f) * 255f, 0f, 255f);
                    pixel.b = (byte)math.clamp((nz * 0.5f + 0.5f) * 255f, 0f, 255f);
                }
                Output[y * outputSize + x] = pixel;
            }
        }

        private Color32 SampleRotated(float u, float v)
        {
            if (Rotation == 0) return Sample(u, v);
            float center = Size * 0.5f;
            float x = u - center, y = v - center;
            return Sample(center + x * Cosine + y * Sine, center - x * Sine + y * Cosine);
        }

        private Color32 Sample(float x, float y)
        {
            int size = Size;
            x %= size;
            y %= size;
            if (x < 0) x += size;
            if (y < 0) y += size;
            if (x >= size) x -= size;
            if (y >= size) y -= size;
            int x0 = (int)x, y0 = (int)y;
            int x1 = (x0 + 1) % size, y1 = (y0 + 1) % size;
            float fx = x - x0, fy = y - y0;
            Color32 a = Input[y0 * size + x0], b = Input[y0 * size + x1];
            Color32 c = Input[y1 * size + x0], d = Input[y1 * size + x1];
            float w0 = (1f - fx) * (1f - fy), w1 = fx * (1f - fy);
            float w2 = (1f - fx) * fy, w3 = fx * fy;
            return new Color32((byte)(a.r * w0 + b.r * w1 + c.r * w2 + d.r * w3),
                (byte)(a.g * w0 + b.g * w1 + c.g * w2 + d.g * w3),
                (byte)(a.b * w0 + b.b * w1 + c.b * w2 + d.b * w3),
                (byte)(a.a * w0 + b.a * w1 + c.a * w2 + d.a * w3));
        }

        private static Color32 Blend(Color32 a, Color32 b, float weight) =>
            new Color32((byte)TextureMath.Lerp(a.r, b.r, weight), (byte)TextureMath.Lerp(a.g, b.g, weight),
                (byte)TextureMath.Lerp(a.b, b.b, weight), (byte)TextureMath.Lerp(a.a, b.a, weight));
    }

    // RoadSurfaceSystemBase.CreateNormalFromColor: relief from the red channel plus optional crack relief.
    [BurstCompile]
    internal struct NormalFromColorRowsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Color32> Colors;
        [ReadOnly] public NativeArray<byte> CrackMask;
        [NativeDisableParallelForRestriction] public NativeArray<Color32> Normals;
        public int Width, Height;
        public float Relief, CrackRelief;
        public bool UseCrackMask;

        public void Execute(int y)
        {
            int width = Width, height = Height;
            for (int x = 0; x < width; x++)
            {
                int left = y * width + (x + width - 1) % width;
                int right = y * width + (x + 1) % width;
                int down = ((y + height - 1) % height) * width + x;
                int up = ((y + 1) % height) * width + x;
                float nx = (Colors[left].r - Colors[right].r) * Relief;
                float ny = (Colors[down].r - Colors[up].r) * Relief;
                if (UseCrackMask)
                {
                    nx += (CrackMask[right] - CrackMask[left]) * CrackRelief;
                    ny += (CrackMask[up] - CrackMask[down]) * CrackRelief;
                }
                float nz = 1f;
                TextureMath.Normalize(ref nx, ref ny, ref nz);
                Normals[y * width + x] = new Color32(
                    (byte)math.clamp((nx * 0.5f + 0.5f) * 255f, 0f, 255f),
                    (byte)math.clamp((ny * 0.5f + 0.5f) * 255f, 0f, 255f),
                    (byte)math.clamp((nz * 0.5f + 0.5f) * 255f, 0f, 255f), 255);
            }
        }
    }

    // TextureColorFilter.Apply: RGB multiplier blended with neutral, alpha unchanged.
    [BurstCompile]
    internal struct ColorFilterBlocksJob : IJobParallelFor
    {
        internal const int BlockSize = 16384;
        [NativeDisableParallelForRestriction] public NativeArray<Color32> Pixels;
        public int R, G, B, OpacityPercent;

        public void Execute(int block)
        {
            int start = block * BlockSize;
            int end = math.min(Pixels.Length, start + BlockSize);
            int neutral = 255 * (100 - OpacityPercent);
            int mr = neutral + R * OpacityPercent, mg = neutral + G * OpacityPercent, mb = neutral + B * OpacityPercent;
            for (int i = start; i < end; i++)
            {
                var pixel = Pixels[i];
                pixel.r = (byte)((pixel.r * mr + 12750) / 25500);
                pixel.g = (byte)((pixel.g * mg + 12750) / 25500);
                pixel.b = (byte)((pixel.b * mb + 12750) / 25500);
                Pixels[i] = pixel;
            }
        }

        internal static int Blocks(int length) => (length + BlockSize - 1) / BlockSize;
    }

    // Last loop of RoadSurfaceSystemBase.AddCrackOverlay: darken cracks and scale the mask.
    [BurstCompile]
    internal struct CrackShadeBlocksJob : IJobParallelFor
    {
        internal const int BlockSize = 16384;
        [NativeDisableParallelForRestriction] public NativeArray<Color32> Pixels;
        [NativeDisableParallelForRestriction] public NativeArray<byte> Mask;
        public float Strength;

        public void Execute(int block)
        {
            int start = block * BlockSize;
            int end = math.min(Pixels.Length, start + BlockSize);
            for (int i = start; i < end; i++)
            {
                byte value = Mask[i];
                if (value == 0) continue;
                float shade = math.max(0f, 1f - value / 255f * 0.67f * Strength);
                Mask[i] = (byte)math.min(255f, value * Strength);
                var color = Pixels[i];
                color.r = (byte)(color.r * shade);
                color.g = (byte)(color.g * shade);
                color.b = (byte)(color.b * shade);
                Pixels[i] = color;
            }
        }

        internal static int Blocks(int length) => (length + BlockSize - 1) / BlockSize;
    }

    // ParkingRoadAtlasSystem.FillParkingStrip: one horizontal band of the parking atlas.
    [BurstCompile]
    internal struct ParkingStripRowsJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] public NativeArray<Color32> Pixels;
        [ReadOnly] public NativeArray<Color32> Detail;
        public bool HasDetail, Linear;
        public int Width, Height, Left, Right, DetailWidth, DetailHeight;
        public float ReferenceLeftU, RepeatsPerAtlas, VRepeats, NormalStrength;

        public void Execute(int y)
        {
            for (int x = Left; x < Right; x++)
            {
                int index = y * Width + x;
                var original = Pixels[index];
                Color32 replacement = new Color32(128, 128, 255, 255);
                if (HasDetail)
                {
                    int u = (int)math.floor((x / (float)Width - ReferenceLeftU) * RepeatsPerAtlas * DetailWidth);
                    int v = (int)math.floor(y / (float)Height * VRepeats * DetailHeight);
                    u = (u % DetailWidth + DetailWidth) % DetailWidth;
                    v = (v % DetailHeight + DetailHeight) % DetailHeight;
                    replacement = Detail[v * DetailWidth + u];
                }
                if (Linear)
                {
                    if (NormalStrength == 0f)
                    {
                        replacement.r = replacement.g = 128;
                    }
                    else if (NormalStrength != 1f)
                    {
                        float nx = (replacement.r / 255f * 2f - 1f) * NormalStrength;
                        float ny = (replacement.g / 255f * 2f - 1f) * NormalStrength;
                        float nz = math.max(0.001f, replacement.b / 255f * 2f - 1f);
                        TextureMath.Normalize(ref nx, ref ny, ref nz);
                        replacement.r = (byte)math.clamp((nx * 0.5f + 0.5f) * 255f, 0f, 255f);
                        replacement.g = (byte)math.clamp((ny * 0.5f + 0.5f) * 255f, 0f, 255f);
                    }
                    replacement = TextureMath.StabilizeNormal(replacement);
                    replacement.a = replacement.r;
                    replacement.r = original.r;
                    replacement.b = original.b;
                }
                else replacement.a = original.a;
                Pixels[index] = replacement;
            }
        }
    }

    // SidewalkRoadAtlasSystem.BuildAtlas: sidewalk band (or diagnostic colors) of the RoadEU/Alley atlas.
    [BurstCompile]
    internal struct SidewalkStripRowsJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] public NativeArray<Color32> Pixels;
        [ReadOnly] public NativeArray<Color32> Tile;
        public bool Diagnostic, AlleyShoulder, Linear;
        public int Width, Height, Bottom, Left, Right, TileWidth, TileHeight;
        public float OriginU, ShiftU, ShiftV, StripFraction, VerticalRepeats;

        public void Execute(int row)
        {
            int y = Bottom + row;
            for (int x = Left; x < Right; x++)
            {
                int index = y * Width + x;
                Color32 replacement;
                if (Diagnostic)
                {
                    int atlasX = (int)math.floor(x * 1024f / Width);
                    int atlasY = (int)math.floor((Height - 1 - y) * 2048f / Height);
                    replacement = atlasX < 273 ? new Color32(255, 20, 20, 255) :
                        atlasX < 318 ? new Color32(255, 225, 20, 255) :
                        atlasX < 759 ? new Color32(30, 90, 255, 255) :
                        atlasX < 808 ? new Color32(20, 230, 230, 255) :
                        atlasX < 912 && atlasY < 468 ? new Color32(255, 25, 230, 255) :
                        new Color32(30, 245, 30, 255);
                    if (AlleyShoulder) replacement = new Color32(30, 90, 255, 255);
                }
                else
                {
                    int u = (int)math.floor(((x / (float)Width - OriginU) / StripFraction + ShiftU) * TileWidth);
                    int v = (int)math.floor((y / (float)Height * VerticalRepeats + ShiftV) * TileHeight);
                    u = (u % TileWidth + TileWidth) % TileWidth;
                    v = (v % TileHeight + TileHeight) % TileHeight;
                    replacement = Tile[v * TileWidth + u];
                }
                var current = Pixels[index];
                if (Linear)
                {
                    replacement.a = replacement.r;
                    replacement.r = current.r;
                    replacement.b = current.b;
                }
                else
                {
                    // BaseColor alpha may carry a separate material mask.
                    replacement.a = current.a;
                }
                Pixels[index] = replacement;
            }
        }
    }

    // SidewalkEndcapScaling.ScaleEndcapAtlas: repeats the sidewalk band with the endcap scale.
    [BurstCompile]
    internal struct EndcapScaleRowsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Color32> Original;
        [NativeDisableParallelForRestriction] public NativeArray<Color32> Pixels;
        public int Width, Height, Left, Right, Span;
        public float ScaleU, ScaleV;
        public bool Linear;

        public void Execute(int y)
        {
            int sy = math.min(Height - 1, (int)math.floor(TextureMath.Repeat01((y + 0.5f) / Height * ScaleV) * Height));
            for (int x = Left; x < Right; x++)
            {
                int sx = Left + math.min(Span - 1, (int)math.floor(TextureMath.Repeat01((x - Left + 0.5f) / Span * ScaleU) * Span));
                int i = y * Width + x;
                var pixel = Original[sy * Width + sx];
                var current = Original[i];
                if (Linear)
                {
                    pixel.r = current.r;
                    pixel.b = current.b;
                }
                else pixel.a = current.a;
                Pixels[i] = pixel;
            }
        }
    }

    // Writes 1 when Burst-compiled, 0 when running as managed code.
    [BurstCompile]
    internal struct BurstProbeJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] public NativeArray<int> Result;

        public void Execute(int index)
        {
            bool burst = true;
            MarkManaged(ref burst);
            Result[0] = burst ? 1 : 0;
        }

        [BurstDiscard]
        private static void MarkManaged(ref bool burst) { burst = false; }
    }

    // One mip level from the level above: 2x2 box filter, edges clamped.
    [BurstCompile]
    internal struct MipDownsampleRowsJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<Color32> Source;
        [NativeDisableParallelForRestriction, NativeDisableContainerSafetyRestriction] public NativeArray<Color32> Target;
        public int SourceWidth, SourceHeight, TargetWidth;

        public void Execute(int y)
        {
            int y0 = y * 2, y1 = math.min(y0 + 1, SourceHeight - 1);
            int row0 = y0 * SourceWidth, row1 = y1 * SourceWidth;
            for (int x = 0; x < TargetWidth; x++)
            {
                int x0 = x * 2, x1 = math.min(x0 + 1, SourceWidth - 1);
                Color32 a = Source[row0 + x0], b = Source[row0 + x1], c = Source[row1 + x0], d = Source[row1 + x1];
                Target[y * TargetWidth + x] = new Color32(
                    (byte)((a.r + b.r + c.r + d.r + 2) >> 2), (byte)((a.g + b.g + c.g + d.g + 2) >> 2),
                    (byte)((a.b + b.b + c.b + d.b + 2) >> 2), (byte)((a.a + b.a + c.a + d.a + 2) >> 2));
            }
        }
    }

    internal static class TextureMath
    {
        internal static float Repeat01(float t) => math.clamp(t - math.floor(t), 0f, 1f);

        // Same result as Vector3.normalized.
        internal static void Normalize(ref float x, ref float y, ref float z)
        {
            float magnitude = math.sqrt(x * x + y * y + z * z);
            if (magnitude > 1E-05f)
            {
                x /= magnitude;
                y /= magnitude;
                z /= magnitude;
            }
            else x = y = z = 0f;
        }

        internal static float Lerp(float a, float b, float t) => a + (b - a) * math.clamp(t, 0f, 1f);

        // RoadNormalEncoding.Stabilize for Burst jobs.
        internal static Color32 StabilizeNormal(Color32 pixel)
        {
            const float minimumZ = 0.15f;
            const float maximumXY2 = 1f - minimumZ * minimumZ;
            float x = pixel.r / 127.5f - 1f;
            float y = pixel.g / 127.5f - 1f;
            float xy2 = x * x + y * y;
            if (xy2 <= maximumXY2) return pixel;
            float scale = math.sqrt(maximumXY2 / xy2);
            x *= scale;
            y *= scale;
            return new Color32(
                (byte)math.clamp((int)math.round((x + 1f) * 127.5f), 0, 255),
                (byte)math.clamp((int)math.round((y + 1f) * 127.5f), 0, 255),
                (byte)(int)math.round((minimumZ + 1f) * 127.5f), pixel.a);
        }
    }
}
