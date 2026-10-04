using System;
using System.Collections.Generic;
using System.IO;
using Game;
using UnityEngine;

namespace Advanced_Graphics
{
    // Shared surface texture implementation; never registered as an ECS system.
    public abstract partial class RoadSurfaceSystemBase : GameSystemBase
    {
        private const string RoadShader = "BH/NetCompositionMeshLitShader";
        private const string OriginalColorPrefix = "RoadEUWorldspace_BaseColor_";
        private const string OriginalNormalPrefix = "RoadEUWorldspace_Normal_";
        public static string CustomTextureFolder => TextureStorage.Road;
        protected virtual string TextureFolder => CustomTextureFolder;
        protected virtual string TextureFileName => "Road_BaseColor.png";
        protected virtual string SurfaceName => "Road asphalt";
        protected virtual bool MatchesColor(Texture texture) => texture != null &&
            texture.name.StartsWith(OriginalColorPrefix, StringComparison.Ordinal);
        protected virtual bool MatchesNormal(Texture texture) => texture != null &&
            texture.name.StartsWith(OriginalNormalPrefix, StringComparison.Ordinal);
        protected virtual bool MatchesShader(Material material) => material.shader != null && material.shader.name == RoadShader;

        protected virtual bool ApplyToSourceMaterials => true;
        protected virtual bool TryGetOriginal(Material material, out string colorSlot, out string normalSlot,
            out Texture color, out Texture normal)
        {
            colorSlot = "_WorldspaceAlbedo";
            normalSlot = "_WorldspaceNormalMap";
            color = material.GetTexture(ColorSlot);
            normal = material.GetTexture(NormalSlot);
            return MatchesColor(color) && MatchesNormal(normal);
        }
        private string CustomTexturePath => Path.Combine(TextureFolder, TextureFileName);
        private static readonly int ColorSlot = Shader.PropertyToID("_WorldspaceAlbedo");
        private static readonly int NormalSlot = Shader.PropertyToID("_WorldspaceNormalMap");

        private struct Original
        {
            public Texture Color;
            public Texture Normal;
            public string ColorSlot;
            public string NormalSlot;
            public Vector2 ColorScale;
            public Vector2 NormalScale;
        }

        private readonly Dictionary<Material, Original> m_Original = new Dictionary<Material, Original>();
        private Texture2D m_Color, m_Normal;
        private Texture2D m_CachedSourceColor;
        private int m_CachedSourceLevel = -1;
        internal Texture2D ActiveColorTexture => m_Enabled ? m_Color : null;
        internal Texture2D ActiveNormalTexture => m_Enabled ? m_Normal : null;
        private int m_Level, m_UvSize = 100, m_Depth = 100, m_OffsetX, m_OffsetY, m_Rotation;
        private float m_Tiling = 1f;
        private int m_FilterColor = 0xffffff, m_FilterOpacityPercent = 100;
        private int m_CrackCount, m_CrackSize = 100, m_CrackWidth = 100, m_CrackOpacity = 100, m_CrackRandomness = 50;
        private Setting.RoadTextureSource m_Source;
        private int m_Frames, m_PollInterval = 120;
        private bool m_Dirty;
        private bool m_TextureDirty;
        private bool m_NormalOnly;
        private bool m_Enabled;
        private bool m_HasLoggedRoadConfiguration;
        protected int CurrentUvSize => m_UvSize;
        protected int CurrentLevel => m_Level;
        protected int CurrentDepth => m_Depth;
        protected int CurrentOffsetX => m_OffsetX;
        protected int CurrentOffsetY => m_OffsetY;
        protected int CurrentRotation => m_Rotation;
        protected virtual bool BakeUvSizeIntoTexture => true;
        protected virtual int SeamBlendDivisor => 8;
        protected virtual bool NeedsNormalAtZeroDepth => false;
        protected virtual bool ReuseColorForDepthChanges => false;
        protected virtual float AuthoredNormalStrength => m_Depth / 100f;

        public void ReloadCustomTexture()
        {
            ClearSourceCache();
            m_NormalOnly = false;
            m_TextureDirty = true;
            m_Dirty = true;
        }

        public void Configure(bool enabled, int level, Setting.RoadTextureSource source, int uvSize, int depth, float tiling,
            int offsetX, int offsetY, int rotation, int crackCount, int crackSize, int crackWidth,
            int crackOpacity, int crackRandomness, int filterColor = 0xffffff, int filterOpacityPercent = 100)
        {
            filterOpacityPercent = Math.Max(0, Math.Min(100, filterOpacityPercent));
            filterColor = Math.Max(0, Math.Min(0xffffff, filterColor));
            level = Math.Max(0, Math.Min(2, level));
            uvSize = Math.Max(50, Math.Min(Setting.MaxTextureUvSize, uvSize));
            depth = Math.Max(0, Math.Min(Setting.MaxTextureDepthStrength, depth));
            tiling = Mathf.Clamp(Mathf.Round(tiling * 10f) / 10f, 0.1f, 8f);
            offsetX = Math.Max(0, Math.Min(Setting.MaxTextureUvOffset, offsetX));
            offsetY = Math.Max(0, Math.Min(Setting.MaxTextureUvOffset, offsetY));
            rotation = Math.Max(0, Math.Min(360, rotation));
            if (rotation == 360) rotation = 0;
            crackCount = Math.Max(0, Math.Min(400, crackCount));
            crackSize = Math.Max(25, Math.Min(600, crackSize));
            crackWidth = Math.Max(25, Math.Min(400, crackWidth));
            crackOpacity = Math.Max(0, Math.Min(200, crackOpacity));
            crackRandomness = Math.Max(0, Math.Min(100, crackRandomness));
            if (GetType() == typeof(RoadAsphaltSystem) && (!m_HasLoggedRoadConfiguration || m_Enabled != enabled))
            {
                Mod.log.Info($"Road asphalt configuration: enabled {enabled}, level {level}, custom PNG present {File.Exists(CustomTexturePath)}");
                m_HasLoggedRoadConfiguration = true;
            }
            if (m_Enabled == enabled && m_Level == level && m_Source == source && m_UvSize == uvSize && m_Depth == depth && m_Tiling == tiling && m_OffsetX == offsetX &&
                m_OffsetY == offsetY && m_Rotation == rotation && m_CrackCount == crackCount &&
                m_CrackSize == crackSize && m_CrackWidth == crackWidth && m_CrackOpacity == crackOpacity &&
                m_CrackRandomness == crackRandomness && m_FilterColor == filterColor && m_FilterOpacityPercent == filterOpacityPercent) return;
            bool depthOnly = !TextureColorFilter.IsActive(m_FilterColor, m_FilterOpacityPercent) && !TextureColorFilter.IsActive(filterColor, filterOpacityPercent) && ReuseColorForDepthChanges && m_Color != null && !m_TextureDirty &&
                m_Enabled == enabled && m_Level == level && m_Source == source && m_UvSize == uvSize &&
                m_Tiling == tiling && m_OffsetX == offsetX && m_OffsetY == offsetY && m_Rotation == rotation &&
                m_CrackCount == crackCount && m_CrackSize == crackSize && m_CrackWidth == crackWidth &&
                m_CrackOpacity == crackOpacity && m_CrackRandomness == crackRandomness;
            if (m_FilterOpacityPercent != filterOpacityPercent || m_FilterColor != filterColor || m_Enabled != enabled || m_Level != level || m_Source != source ||
                (BakeUvSizeIntoTexture && m_UvSize != uvSize) || m_Depth != depth ||
                m_OffsetX != offsetX || m_OffsetY != offsetY || m_Rotation != rotation ||
                m_Tiling != tiling || m_CrackCount != crackCount || m_CrackSize != crackSize ||
                m_CrackWidth != crackWidth || m_CrackOpacity != crackOpacity || m_CrackRandomness != crackRandomness)
            {
                m_TextureDirty = true;
                m_NormalOnly = depthOnly;
            }
            m_FilterOpacityPercent = filterOpacityPercent;
            m_FilterColor = filterColor;
            m_Enabled = enabled;
            m_Level = level;
            m_Source = source;
            m_UvSize = uvSize;
            m_Depth = depth;
            m_Tiling = tiling;
            m_OffsetX = offsetX;
            m_OffsetY = offsetY;
            m_Rotation = rotation;
            m_CrackCount = crackCount;
            m_CrackSize = crackSize;
            m_CrackWidth = crackWidth;
            m_CrackOpacity = crackOpacity;
            m_CrackRandomness = crackRandomness;
            m_Dirty = true;
        }

        protected override void OnUpdate()
        {
            if (!m_Dirty && (!m_Enabled || ++m_Frames < m_PollInterval)) return;
            m_Frames = 0;
            if (!m_Enabled)
            {
                RestoreNow();
                m_Dirty = false;
                return;
            }

            try
            {
                if (m_TextureDirty) BuildTextures();
                if (m_Color == null || ((m_Depth > 0 || (m_Level == 1 && NeedsNormalAtZeroDepth)) && m_Normal == null))
                {
                    m_TextureDirty = true;
                    m_Dirty = false;
                    return;
                }
                m_TextureDirty = false;
                if (!ApplyToSourceMaterials)
                {
                    RestoreMaterials();
                    m_Dirty = false;
                    return;
                }
                int added = 0;
                var missing = new List<Material>();
                foreach (var pair in m_Original)
                    if (pair.Key == null) missing.Add(pair.Key);
                foreach (var material in missing) m_Original.Remove(material);
                foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
                {
                    if (material == null || !MatchesShader(material))
                        continue;
                    if (!m_Original.TryGetValue(material, out var original))
                    {
                        if (!TryGetOriginal(material, out string colorSlot, out string normalSlot,
                            out Texture color, out Texture normal))
                            continue;
                        original = new Original
                        {
                            Color = color, Normal = normal,
                            ColorSlot = colorSlot, NormalSlot = normalSlot,
                            ColorScale = material.GetTextureScale(colorSlot),
                            NormalScale = normalSlot != null ? material.GetTextureScale(normalSlot) : Vector2.one
                        };
                        m_Original.Add(material, original);
                        added++;
                    }
                    var currentColor = material.GetTexture(original.ColorSlot);
                    var currentNormal = original.NormalSlot != null ? material.GetTexture(original.NormalSlot) : null;
                    if (currentColor != original.Color && currentColor != m_Color)
                        continue;
                    if (currentNormal != original.Normal && currentNormal != m_Normal)
                        continue;
                    material.SetTexture(original.ColorSlot, m_Color);
                    if (original.NormalSlot != null)
                        material.SetTexture(original.NormalSlot, m_Normal != null ? m_Normal : original.Normal);

                    material.SetTextureScale(original.ColorSlot, original.ColorScale);
                    if (original.NormalSlot != null)
                        material.SetTextureScale(original.NormalSlot, original.NormalScale);
                    OnSurfaceMaterialApplied(material);
                }
                if (m_Dirty || added > 0)
                    Mod.log.Info($"{SurfaceName}: source {m_Source}, level {m_Level}, UV size {m_UvSize}%, tiling {m_Tiling:0.0}x, " +
                        $"UV {m_OffsetX}/{m_OffsetY}% rotation {m_Rotation}°, depth {m_Depth}%, " +
                        $"cracks {m_CrackCount}/{m_CrackSize}%/{m_CrackWidth}%/{m_CrackOpacity}%/{m_CrackRandomness}%, " +
                        $"{m_Original.Count} materials ({added} new)");
                m_PollInterval = m_Original.Count == 0 ? 120 : 3600;
                m_Dirty = false;
            }
            catch (Exception ex)
            {
                m_Dirty = false;
                Mod.log.Error(ex, SurfaceName + " could not be applied.");
            }
        }

        private void BuildTextures()
        {
            if (m_NormalOnly && m_Color != null)
            {
                var normalOnly = m_Depth > 0 || (m_Level == 1 && NeedsNormalAtZeroDepth)
                    ? BuildSurfaceNormal(m_Color, m_Depth, m_UvSize, null) : null;
                RestoreMaterials();
                if (m_Normal != null) UnityEngine.Object.Destroy(m_Normal);
                m_Normal = normalOnly;
                m_NormalOnly = false;
                return;
            }

            var sourceColor = CacheSourceColor && m_CachedSourceLevel == m_Level ? m_CachedSourceColor : null;
            if (sourceColor == null) sourceColor = m_Level == 0 ? LoadOriginalColor() :
                m_Level == 2 ? LoadCustomTexture() : GenerateNeutralAsphalt();
            if (sourceColor == null)
            {
                RestoreMaterials();
                DestroyTextures();
                return;
            }
            Texture2D color = null, normal = null;
            if (CacheSourceColor && m_CachedSourceColor != sourceColor)
            {
                ClearSourceCache();
                m_CachedSourceColor = sourceColor;
                m_CachedSourceLevel = m_Level;
            }
            try
            {
                color = Transform(sourceColor, false, true);
                PostprocessSurfaceColor(color, sourceColor);
                byte[] crackMask = m_CrackCount > 0 && m_CrackOpacity > 0 ? AddCrackOverlay(color) : null;
                if (m_Depth > 0 || (m_Level == 1 && NeedsNormalAtZeroDepth))
                    normal = BuildSurfaceNormal(color, m_Depth, m_UvSize, crackMask);
                TextureColorFilter.Apply(color, m_FilterColor, m_FilterOpacityPercent);
                color.Apply(true, !KeepTileReadable);
            }
            catch
            {
                if (color != null) UnityEngine.Object.Destroy(color);
                if (normal != null) UnityEngine.Object.Destroy(normal);
                throw;
            }
            finally
            {
                if (!CacheSourceColor && sourceColor != null) UnityEngine.Object.Destroy(sourceColor);
            }

            RestoreMaterials();
            DestroyTextures();
            m_Color = color;
            m_Normal = normal;
        }

        protected virtual Texture2D LoadOriginalColor()
        {
            Texture source = null;
            foreach (var original in m_Original.Values)
            {
                if (original.Color != null) { source = original.Color; break; }
            }
            if (source == null)
            foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (material == null || !MatchesShader(material)) continue;
                if (TryGetOriginal(material, out string colorSlot, out string normalSlot,
                    out Texture candidate, out Texture candidateNormal))
                {
                    source = candidate;
                    break;
                }
            }
            if (source == null || source.width != source.height || source.width > 4096) return null;

            var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                UnityEngine.Graphics.Blit(source, target);
                RenderTexture.active = target;
                var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true, false);
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
                copy.Apply(false, false);
                return copy;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        protected virtual Texture2D GenerateNeutralAsphalt()
        {
            const int size = 1024;
            var colors = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float grain = (Hash(x, y, 17) - 0.5f) * 5f;
                float soft = (Hash(x / 4, y / 4, 29) - 0.5f) * 2f;
                byte shade = (byte)Mathf.Clamp(84f + grain + soft, 0f, 255f);
                colors[y * size + x] = new Color32(shade, (byte)(shade + 1), (byte)(shade + 2), 255);
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            texture.SetPixels32(colors);
            texture.Apply(false, false);
            return texture;
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint value = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                value = (value ^ (value >> 13)) * 1274126177u;
                return ((value ^ (value >> 16)) & 65535u) / 65535f;
            }
        }

        private Texture2D LoadCustomTexture()
        {
            if (!File.Exists(CustomTexturePath))
            {
                Mod.log.Info("Custom surface texture missing: copy " + TextureFileName + " into " + TextureFolder);
                return null;
            }
            try
            {
                var file = new FileInfo(CustomTexturePath);
                if (file.Length > 32 * 1024 * 1024) throw new InvalidDataException("Road PNG exceeds 32 MB.");
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, false);
                if (texture.LoadImage(File.ReadAllBytes(CustomTexturePath), false))
                {
                    if (texture.width == texture.height && texture.width <= 4096) return texture;
                    UnityEngine.Object.Destroy(texture);
                    throw new InvalidDataException(TextureFileName + " must be square and at most 4096 pixels wide.");
                }
                UnityEngine.Object.Destroy(texture);
                throw new InvalidDataException(TextureFileName + " is not a valid PNG.");
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex, "Custom road texture could not be loaded.");
                return null;
            }
        }

        protected virtual int MaximumTileResolution => 4096;
        protected virtual bool KeepTileReadable => false;
        protected virtual bool CacheSourceColor => false;

        protected virtual void EncodeNormalPixels(Color32[] pixels) { }

        protected virtual void PostprocessSurfaceColor(Texture2D color, Texture2D source) { }

        protected virtual void OnSurfaceMaterialApplied(Material material) { }
        protected virtual void OnSurfaceMaterialRestored(Material material) { }
        protected virtual void OnSurfaceReset() { }

        protected virtual Texture2D BuildSurfaceNormal(Texture2D color, int depth, int uvSize, byte[] crackMask) =>
            CreateNormalFromColor(color, depth, uvSize, crackMask);

        private Texture2D CreateNormalFromColor(Texture2D source, int depth, int uvSize, byte[] crackMask)
        {
            int width = source.width, height = source.height;
            var colors = source.GetPixels32();
            var normals = new Color32[colors.Length];

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int left = y * width + (x + width - 1) % width;
                int right = y * width + (x + 1) % width;
                int down = ((y + height - 1) % height) * width + x;
                int up = ((y + 1) % height) * width + x;
                float relief = 0.025f * depth / 100f * uvSize / 100f;
                float nx = (colors[left].r - colors[right].r) * relief;
                float ny = (colors[down].r - colors[up].r) * relief;
                if (crackMask != null)
                {
                    float crackRelief = 0.008f * depth / 100f;
                    nx += (crackMask[right] - crackMask[left]) * crackRelief;
                    ny += (crackMask[up] - crackMask[down]) * crackRelief;
                }
                var n = new Vector3(nx, ny, 1f).normalized;
                normals[y * width + x] = new Color32(
                    (byte)Mathf.Clamp((n.x * 0.5f + 0.5f) * 255f, 0f, 255f),
                    (byte)Mathf.Clamp((n.y * 0.5f + 0.5f) * 255f, 0f, 255f),
                    (byte)Mathf.Clamp((n.z * 0.5f + 0.5f) * 255f, 0f, 255f), 255);
            }
            EncodeNormalPixels(normals);
            var result = new Texture2D(width, height, TextureFormat.RGBA32, true, true);
            result.SetPixels32(normals);
            result.Apply(true, !KeepTileReadable);
            result.name = "Advanced Graphics Road Asphalt Normal";
            result.wrapMode = TextureWrapMode.Repeat;
            result.filterMode = FilterMode.Trilinear;
            result.anisoLevel = 8;
            return result;
        }

        private byte[] AddCrackOverlay(Texture2D source)
        {
            int size = source.width;
            if (size != source.height)
                throw new InvalidDataException("Road asphalt detail texture must be square.");
            var pixels = source.GetPixels32();
            var mask = new byte[pixels.Length];
            var random = new System.Random(73129 + m_CrackRandomness * 97);
            int count = Math.Min(800, Mathf.RoundToInt(m_CrackCount * m_Tiling));
            if (count == 0) return mask;
            int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt((float)count / columns);
            float cellWidth = (float)size / columns;
            float cellHeight = (float)size / rows;
            float jitter = m_CrackRandomness / 100f;
            float scale = m_CrackSize / 100f;
            float radius = Math.Max(0.25f, scale * m_CrackWidth / 100f);
            for (int i = 0; i < count; i++)
            {
                float x = ((i % columns) + 0.5f + ((float)random.NextDouble() - 0.5f) * jitter) * cellWidth;
                float y = ((i / columns) + 0.5f + ((float)random.NextDouble() - 0.5f) * jitter) * cellHeight;
                float angle = (float)(random.NextDouble() * Math.PI * 2.0);
                int segments = 8 + random.Next(6);
                for (int segment = 0; segment < segments; segment++)
                {
                    angle += ((float)random.NextDouble() - 0.5f) * (0.3f + 2f * jitter);
                    float length = (5f + (float)random.NextDouble() * 7f) * scale;
                    float nextX = x + Mathf.Cos(angle) * length;
                    float nextY = y + Mathf.Sin(angle) * length;
                    PaintCrack(mask, size, x, y, nextX, nextY, radius);
                    x = nextX;
                    y = nextY;
                }
            }
            for (int i = 0; i < pixels.Length; i++)
            {
                if (mask[i] == 0) continue;
                float strength = m_CrackOpacity / 100f;
                float shade = Mathf.Max(0f, 1f - mask[i] / 255f * 0.67f * strength);
                mask[i] = (byte)Mathf.Min(255f, mask[i] * strength);
                var color = pixels[i];
                color.r = (byte)(color.r * shade);
                color.g = (byte)(color.g * shade);
                color.b = (byte)(color.b * shade);
                pixels[i] = color;
            }
            source.SetPixels32(pixels);
            source.Apply(false, false);
            return mask;
        }

        private static void PaintCrack(byte[] mask, int size, float x0, float y0, float x1, float y1, float radius)
        {
            int steps = Math.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0))));
            for (int step = 0; step <= steps; step++)
            {
                float t = (float)step / steps;
                int cx = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                int cy = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                int reach = Mathf.CeilToInt(radius);
                for (int dy = -reach; dy <= reach; dy++)
                for (int dx = -reach; dx <= reach; dx++)
                {
                    int distance = dx * dx + dy * dy;
                    if (distance > radius * radius) continue;
                    int px = ((cx + dx) % size + size) % size;
                    int py = ((cy + dy) % size + size) % size;
                    byte opacity = (byte)(distance == 0 ? 225 : 100);
                    int index = py * size + px;
                    if (mask[index] < opacity) mask[index] = opacity;
                }
            }
        }

        protected Texture2D Transform(Texture2D source, bool linear, bool keepReadable)
        {
            float normalStrength = AuthoredNormalStrength;
            int size = source.width;
            int outputSize = Math.Min(size, MaximumTileResolution);
            if (size != source.height) throw new InvalidDataException("Road asphalt detail texture must be square.");
            var input = source.GetPixels32();
            var output = new Color32[outputSize * outputSize];
            float shiftX = m_OffsetX * size / 100f;
            float shiftY = m_OffsetY * size / 100f;
            float tiling = 1f;
            float period = size * tiling * 100f / (BakeUvSizeIntoTexture ? m_UvSize : 100);
            float seamWidth = outputSize / (float)Mathf.Max(1, SeamBlendDivisor);
            float angle = m_Rotation * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(angle), sine = Mathf.Sin(angle);
            for (int y = 0; y < outputSize; y++)
            for (int x = 0; x < outputSize; x++)
            {
                float u = (x + 0.5f) * period / outputSize + shiftX;
                float v = (y + 0.5f) * period / outputSize + shiftY;
                float dx = x < seamWidth ? period : x >= outputSize - seamWidth ? -period : 0f;
                float dy = y < seamWidth ? period : y >= outputSize - seamWidth ? -period : 0f;
                float wx = dx == 0f ? 0f : 0.5f * (1f - Math.Min(x, outputSize - 1 - x) / seamWidth);
                float wy = dy == 0f ? 0f : 0.5f * (1f - Math.Min(y, outputSize - 1 - y) / seamWidth);
                var pixel = SampleRotated(input, size, u, v, m_Rotation, cosine, sine);
                if (wx > 0f) pixel = Blend(pixel, SampleRotated(input, size, u + dx, v, m_Rotation, cosine, sine), wx);
                if (wy > 0f)
                {
                    var other = SampleRotated(input, size, u, v + dy, m_Rotation, cosine, sine);
                    if (wx > 0f) other = Blend(other, SampleRotated(input, size, u + dx, v + dy, m_Rotation, cosine, sine), wx);
                    pixel = Blend(pixel, other, wy);
                }
                if (linear && m_Rotation != 0)
                {
                    float nx = pixel.r - 128f, ny = pixel.g - 128f;
                    pixel.r = (byte)Mathf.Clamp(128f + nx * cosine - ny * sine, 0f, 255f);
                    pixel.g = (byte)Mathf.Clamp(128f + nx * sine + ny * cosine, 0f, 255f);
                }
                if (linear && normalStrength != 1f)
                {
                    var nx = (pixel.r / 255f * 2f - 1f) * normalStrength;
                    var ny = (pixel.g / 255f * 2f - 1f) * normalStrength;
                    var n = new Vector3(nx, ny, Mathf.Max(0.001f, pixel.b / 255f * 2f - 1f)).normalized;
                    pixel.r = (byte)Mathf.Clamp((n.x * 0.5f + 0.5f) * 255f, 0f, 255f);
                    pixel.g = (byte)Mathf.Clamp((n.y * 0.5f + 0.5f) * 255f, 0f, 255f);
                    pixel.b = (byte)Mathf.Clamp((n.z * 0.5f + 0.5f) * 255f, 0f, 255f);
                }
                output[y * outputSize + x] = pixel;
            }
            var texture = new Texture2D(outputSize, outputSize, TextureFormat.RGBA32, true, linear);
            texture.SetPixels32(output);
            texture.Apply(true, !keepReadable);
            texture.name = "Advanced Graphics Road Asphalt " + (linear ? "Normal" : "Color");
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = 8;
            return texture;
        }

        private static Color32 SampleRotated(Color32[] pixels, int size, float u, float v,
            int rotation, float cosine, float sine)
        {
            if (rotation == 0) return Sample(pixels, size, u, v);
            float center = size * 0.5f;
            float x = u - center, y = v - center;
            return Sample(pixels, size, center + x * cosine + y * sine,
                center - x * sine + y * cosine);
        }

        private static Color32 Blend(Color32 a, Color32 b, float weight)
        {
            return new Color32((byte)Mathf.Lerp(a.r, b.r, weight), (byte)Mathf.Lerp(a.g, b.g, weight),
                (byte)Mathf.Lerp(a.b, b.b, weight), (byte)Mathf.Lerp(a.a, b.a, weight));
        }

        private static Color32 Sample(Color32[] pixels, int size, float x, float y)
        {
            x %= size;
            y %= size;
            if (x < 0) x += size;
            if (y < 0) y += size;
            int x0 = (int)x, y0 = (int)y;
            int x1 = (x0 + 1) % size, y1 = (y0 + 1) % size;
            float fx = x - x0, fy = y - y0;
            Color32 a = pixels[y0 * size + x0], b = pixels[y0 * size + x1];
            Color32 c = pixels[y1 * size + x0], d = pixels[y1 * size + x1];
            float w0 = (1f - fx) * (1f - fy), w1 = fx * (1f - fy);
            float w2 = (1f - fx) * fy, w3 = fx * fy;
            return new Color32((byte)(a.r * w0 + b.r * w1 + c.r * w2 + d.r * w3),
                (byte)(a.g * w0 + b.g * w1 + c.g * w2 + d.g * w3),
                (byte)(a.b * w0 + b.b * w1 + c.b * w2 + d.b * w3),
                (byte)(a.a * w0 + b.a * w1 + c.a * w2 + d.a * w3));
        }

        private void RestoreMaterials()
        {
            foreach (var pair in m_Original)
            {
                var material = pair.Key;
                if (material == null) continue;
                if (material.GetTexture(pair.Value.ColorSlot) == m_Color)
                {
                    material.SetTexture(pair.Value.ColorSlot, pair.Value.Color);
                    material.SetTextureScale(pair.Value.ColorSlot, pair.Value.ColorScale);
                }
                if (pair.Value.NormalSlot != null && material.GetTexture(pair.Value.NormalSlot) == m_Normal)
                {
                    material.SetTexture(pair.Value.NormalSlot, pair.Value.Normal);
                    material.SetTextureScale(pair.Value.NormalSlot, pair.Value.NormalScale);
                }
                OnSurfaceMaterialRestored(material);
            }
        }

        private void DestroyTextures()
        {
            if (m_Color != null) UnityEngine.Object.Destroy(m_Color);
            if (m_Normal != null) UnityEngine.Object.Destroy(m_Normal);
            m_Color = m_Normal = null;
        }

        public void RestoreNow()
        {
            m_NormalOnly = false;
            RestoreMaterials();
            m_Original.Clear();
            DestroyTextures();
            ClearSourceCache();
            m_PollInterval = 120;
            OnSurfaceReset();
        }

        private void ClearSourceCache()
        {
            if (m_CachedSourceColor != null) UnityEngine.Object.Destroy(m_CachedSourceColor);
            m_CachedSourceColor = null;
            m_CachedSourceLevel = -1;
        }
    }
}


