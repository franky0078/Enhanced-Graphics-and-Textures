using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game;
using UnityEngine;

namespace Advanced_Graphics
{
    // Resamples only the authored gravel strip, preserving the rest of the shoulder atlas.
    public partial class GravelShoulderSystem : GameSystemBase
    {

        private const float DepthGain = 4f;
        private sealed class Maps
        {
            public Texture Color, Normal;
            public bool VirtualTexturing;
            public Texture2D AppliedColor, AppliedNormal;
            public Color32[] SourceColor, SourceNormal;
            public Color32[] ColorWork, NormalWork;
            public int Width, Height, Band;
            public int AppliedVersion;
            public int AppliedSize, AppliedOffsetX, AppliedOffsetY, AppliedRotation;
            public Texture AppliedTile;
        }
        private sealed class Result
        {
            public Maps Maps;
            public Color32[] Color, Normal;
            public int Size, OffsetX, OffsetY, Rotation;
            public Texture Tile;
        }
        private readonly Dictionary<Material, Maps> m_Maps = new Dictionary<Material, Maps>();
        private bool m_Enabled, m_Dirty;
        private int m_Size = 100, m_Depth, m_Frames, m_Detail, m_OffsetX, m_OffsetY, m_Rotation;
        private Texture2D m_Tile, m_NormalTile;
        private Color32[] m_TilePixels, m_NormalTilePixels;
        private bool IsOriginal => m_Detail == 0 && m_Size == 100 && m_Depth == 0 &&
            m_OffsetX == 0 && m_OffsetY == 0 && m_Rotation == 0;
        private int m_Version, m_WorkVersion;
        private Task<Result[]> m_Work;

        public void Configure(bool enabled, int size, int depth, int detail, int offsetX, int offsetY, int rotation)
        {
            size = Mathf.Clamp(size, 100, 1000);
            depth = Mathf.Clamp(depth, 0, 1000);
            detail = Mathf.Clamp(detail, 0, 2);
            offsetX = Mathf.Clamp(offsetX, 0, 100); offsetY = Mathf.Clamp(offsetY, 0, 100);
            rotation = Mathf.Clamp(rotation, 0, 360);
            if (m_Enabled == enabled && m_Size == size && m_Depth == depth && m_Detail == detail &&
                m_OffsetX == offsetX && m_OffsetY == offsetY && m_Rotation == rotation) return;
            m_Detail = detail; m_OffsetX = offsetX; m_OffsetY = offsetY; m_Rotation = rotation;
            m_Enabled = enabled;
            m_Size = size;
            m_Depth = depth;
            m_Version++;
            if (!enabled || IsOriginal) RestoreNow();
            m_Dirty = true;
        }

        protected override void OnUpdate()
        {
            var tile = m_Detail == 0 ? null : Mod.GravelShoulderSurface?.ActiveColorTexture;
            var normalTile = m_Detail == 0 ? null : Mod.GravelShoulderSurface?.ActiveNormalTexture;
            if (m_Tile != tile || m_NormalTile != normalTile)
            {
                m_Tile = tile; m_NormalTile = normalTile;
                m_TilePixels = tile != null ? tile.GetPixels32() : null;
                m_NormalTilePixels = normalTile != null ? normalTile.GetPixels32() : null;
                m_Version++; m_Dirty = true;
            }
            CompleteWork();
            if (m_Enabled && m_Detail != 0 && (m_Tile == null || m_NormalTile == null))
            {
                if (m_Maps.Count > 0) RestoreNow();
                m_Dirty = true;
                return;
            }
            if (!m_Enabled || IsOriginal) { m_Dirty = false; return; }
            if (m_Dirty && m_Work == null && m_Maps.Count > 0) StartWork();
            if (!m_Dirty && ++m_Frames < (m_Maps.Count == 0 ? 120 : 3600)) return;
            m_Frames = 0;
            int added = 0;
            foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (material == null || material.shader == null ||
                    material.shader.name != "BH/NetCompositionMeshLitShader" ||
                    !material.name.StartsWith("Batch (GravelShoulder", StringComparison.Ordinal)) continue;
                if (!m_Maps.TryGetValue(material, out var pair))
                {
                    Texture color = material.GetTexture("_BaseColorMap");
                    Texture normal = material.GetTexture("_NormalMap");
                    if (color == null || normal == null || color.width != color.height || color.width > 4096 ||
                        normal.width != color.width || normal.height != color.height ||
                        !color.name.StartsWith("GravelShoulder", StringComparison.Ordinal) ||
                        !normal.name.StartsWith("GravelShoulder", StringComparison.Ordinal)) continue;
                    pair = new Maps { Color = color, Normal = normal, VirtualTexturing = material.IsKeywordEnabled("ENABLE_VT") };
                    try
                    {
                        pair.AppliedColor = ReadSource(color, false, out pair.SourceColor);
                        pair.AppliedNormal = ReadSource(normal, true, out pair.SourceNormal);
                        pair.Width = color.width;
                        pair.Height = color.height;
                        pair.Band = Mathf.RoundToInt(color.width * 952f / 4096f);
                        pair.ColorWork = new Color32[pair.Band * pair.Height];
                        pair.NormalWork = new Color32[pair.Band * pair.Height];
                        pair.AppliedVersion = -1;
                        pair.AppliedSize = -1;
                        m_Maps.Add(material, pair);
                        added++;
                    }
                    catch (Exception ex)
                    {
                        if (pair.AppliedColor != null) UnityEngine.Object.Destroy(pair.AppliedColor);
                        if (pair.AppliedNormal != null) UnityEngine.Object.Destroy(pair.AppliedNormal);
                        Mod.log.Error(ex, "Gravel shoulder atlas could not be prepared.");
                        continue;
                    }
                }
                var currentColor = material.GetTexture("_BaseColorMap");
                var currentNormal = material.GetTexture("_NormalMap");
                if ((currentColor != pair.Color && currentColor != pair.AppliedColor) ||
                    (currentNormal != pair.Normal && currentNormal != pair.AppliedNormal)) continue;
                if (pair.AppliedVersion == m_Version)
                {
                    material.DisableKeyword("ENABLE_VT");
                    material.SetTexture("_BaseColorMap", pair.AppliedColor);
                    material.SetTexture("_NormalMap", pair.AppliedNormal);
                }
            }
            if (m_Dirty || added > 0)
                Mod.log.Info($"Gravel shoulder: size {m_Size}, depth {m_Depth}, {m_Maps.Count} materials ({added} new)");
            m_Dirty = false;
            if (added > 0 && m_Work == null) StartWork();
        }

        private static Texture2D ReadSource(Texture source, bool normal, out Color32[] pixels)
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
                pixels = copy.GetPixels32();
                copy.Apply(true, false);
                copy.name = "Advanced Graphics Gravel Shoulder " + (normal ? "Normal" : "Color");
                copy.wrapMode = source.wrapMode;
                copy.filterMode = source.filterMode;
                copy.anisoLevel = source.anisoLevel;
                return copy;
            }
            catch { if (copy != null) UnityEngine.Object.Destroy(copy); throw; }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
        }

        private void StartWork()
        {
            var maps = new List<Maps>(m_Maps.Values).ToArray();
            int size = m_Size, depth = m_Depth;
            int offsetX = m_OffsetX, offsetY = m_OffsetY, rotation = m_Rotation, detail = m_Detail;
            var tile = m_Tile;
            var overlayColor = m_TilePixels;
            var overlayNormal = m_NormalTilePixels;
            int tileWidth = tile != null ? tile.width : 0, tileHeight = tile != null ? tile.height : 0;
            int version = m_Version;
            m_WorkVersion = version;
            m_Dirty = false;
            m_Work = Task.Run(() =>
            {
                var results = new Result[maps.Length];
                for (int i = 0; i < maps.Length; i++)
                {
                    if (System.Threading.Volatile.Read(ref m_Version) != version) return null;
                    var pair = maps[i];
                    bool needsColor = pair.AppliedSize != size || pair.AppliedOffsetX != offsetX ||
                        pair.AppliedOffsetY != offsetY || pair.AppliedRotation != rotation || !ReferenceEquals(pair.AppliedTile, tile);
                    var color = needsColor ? ResampleStrip(pair.SourceColor, pair.ColorWork, pair.Width, pair.Height, pair.Band, size, depth, false, version, offsetX, offsetY, rotation, overlayColor, tileWidth, tileHeight, detail) : null;
                    if (needsColor && color == null) return null;
                    var normal = ResampleStrip(pair.SourceNormal, pair.NormalWork, pair.Width, pair.Height, pair.Band, size, depth, true, version, offsetX, offsetY, rotation, overlayNormal, tileWidth, tileHeight, detail);
                    if (normal == null) return null;
                    results[i] = new Result { Maps = pair, Color = color, Normal = normal, Size = size, OffsetX = offsetX, OffsetY = offsetY, Rotation = rotation, Tile = tile };
                }
                return results;
            });
        }

        private void CompleteWork()
        {
            if (m_Work == null || !m_Work.IsCompleted) return;
            var work = m_Work;
            m_Work = null;
            if (work.IsFaulted) { Mod.log.Error(work.Exception, "Gravel shoulder processing failed."); return; }
            if (m_WorkVersion != m_Version || !m_Enabled || work.Result == null)
            {
                m_Dirty = m_Enabled;
                return;
            }
            foreach (var result in work.Result)
            {
                var pair = result.Maps;
                if (pair.AppliedColor == null || pair.AppliedNormal == null) continue;
                if (result.Color != null)
                {
                    pair.AppliedColor.SetPixels32(0, 0, pair.Band, pair.Height, result.Color, 0);
                    pair.AppliedColor.Apply(true, false);
                }
                pair.AppliedNormal.SetPixels32(0, 0, pair.Band, pair.Height, result.Normal, 0);
                pair.AppliedNormal.Apply(true, false);
                pair.AppliedVersion = m_Version;
                pair.AppliedSize = result.Size;
                pair.AppliedOffsetX = result.OffsetX; pair.AppliedOffsetY = result.OffsetY;
                pair.AppliedRotation = result.Rotation; pair.AppliedTile = result.Tile;
            }
            foreach (var item in m_Maps)
            {
                if (item.Key == null || item.Value.AppliedVersion != m_Version) continue;
                var pair = item.Value;
                var color = item.Key.GetTexture("_BaseColorMap");
                var normal = item.Key.GetTexture("_NormalMap");
                if ((color != pair.Color && color != pair.AppliedColor) ||
                    (normal != pair.Normal && normal != pair.AppliedNormal)) continue;
                item.Key.DisableKeyword("ENABLE_VT");
                item.Key.SetTexture("_BaseColorMap", pair.AppliedColor);
                item.Key.SetTexture("_NormalMap", pair.AppliedNormal);
            }
            foreach (var pair in m_Maps.Values)
                if (pair.AppliedVersion != m_Version) m_Dirty = true;
        }

        private Color32[] ResampleStrip(Color32[] original, Color32[] pixels, int width, int height, int band,
            int size, int depth, bool normal, int version, int offsetX, int offsetY, int rotation,
            Color32[] overlay, int tileWidth, int tileHeight, int detail)
        {
            float scale = 100f / size, strength = 1f + DepthGain * depth / 100f;
            float angle = rotation * (float)Math.PI / 180f;
            float cos = (float)Math.Cos(angle), sin = (float)Math.Sin(angle);
            for (int y = 0; y < height; y++)
            {
                if ((y & 31) == 0 && System.Threading.Volatile.Read(ref m_Version) != version) return null;
                for (int x = 0; x < band; x++)
                {
                    float u = (x - band * 0.5f) * scale;
                    float v = (y - height * 0.5f) * scale;
                    float sx = u * cos - v * sin + band * 0.5f + band * offsetX / 100f;
                    float sy = u * sin + v * cos + height * 0.5f + height * offsetY / 100f;
                    var value = overlay == null ? Sample(original, width, height, band, sx, sy) :
                        Sample(overlay, tileWidth, tileHeight, tileWidth, sx * tileWidth / band, sy * tileWidth / band);
                    if (normal)
                    {
                        double nx = (overlay == null ? value.a : value.r) / 127.5 - 1.0, ny = value.g / 127.5 - 1.0;
                        double rotatedX = nx * cos + ny * sin;
                        ny = -nx * sin + ny * cos; nx = rotatedX;
                        double nz = Math.Sqrt(Math.Max(0.001, 1.0 - nx * nx - ny * ny));
                        double gain = overlay == null ? strength : detail == 1
                            ? RoadGravelSystem.GravelNormalStrength(depth) : Math.Sqrt(depth / 100.0);
                        nx *= gain; ny *= gain;
                        double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                        value.a = (byte)Math.Round((nx / length + 1.0) * 127.5);
                        value.g = (byte)Math.Round((ny / length + 1.0) * 127.5);
                        value.r = original[y * width + x].r;
                        value.b = original[y * width + x].b;
                    }
                    var old = original[y * width + x];
                    float blend = Math.Min(1f, Math.Max(0f, (band - 1 - x) / 16f));
                    pixels[y * band + x] = new Color32((byte)(old.r + (value.r - old.r) * blend),
                        (byte)(old.g + (value.g - old.g) * blend), (byte)(old.b + (value.b - old.b) * blend),
                        normal ? (byte)(old.a + (value.a - old.a) * blend) : old.a);
                }
            }
            return pixels;
        }

        private static Color32 Sample(Color32[] pixels, int width, int height, int band, float x, float y)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            float fx = x - ix, fy = y - iy;
            int x0 = (ix % band + band) % band, x1 = (x0 + 1) % band;
            int y0 = (iy % height + height) % height, y1 = (y0 + 1) % height;
            var a = pixels[y0 * width + x0]; var b = pixels[y0 * width + x1];
            var c = pixels[y1 * width + x0]; var d = pixels[y1 * width + x1];
            float wa = (1f - fx) * (1f - fy), wb = fx * (1f - fy), wc = (1f - fx) * fy, wd = fx * fy;
            return new Color32((byte)(a.r * wa + b.r * wb + c.r * wc + d.r * wd),
                (byte)(a.g * wa + b.g * wb + c.g * wc + d.g * wd),
                (byte)(a.b * wa + b.b * wb + c.b * wc + d.b * wd),
                (byte)(a.a * wa + b.a * wb + c.a * wc + d.a * wd));
        }

        public void RestoreNow()
        {
            m_Version++;
            foreach (var item in m_Maps)
            {
                var pair = item.Value;
                if (item.Key != null)
                {
                    bool ownsColor = item.Key.GetTexture("_BaseColorMap") == pair.AppliedColor;
                    bool ownsNormal = item.Key.GetTexture("_NormalMap") == pair.AppliedNormal;
                    if (ownsColor) item.Key.SetTexture("_BaseColorMap", pair.Color);
                    if (ownsNormal) item.Key.SetTexture("_NormalMap", pair.Normal);
                    if (ownsColor && ownsNormal && pair.VirtualTexturing) item.Key.EnableKeyword("ENABLE_VT");
                }
                if (pair.AppliedColor != null) UnityEngine.Object.Destroy(pair.AppliedColor);
                if (pair.AppliedNormal != null) UnityEngine.Object.Destroy(pair.AppliedNormal);
            }
            m_Maps.Clear();
            m_Frames = 0;
        }

        protected override void OnDestroy() { RestoreNow(); base.OnDestroy(); }
    }
}

