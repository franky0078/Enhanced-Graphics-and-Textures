using System;
using System.Collections.Generic;
using System.IO;
using Game;
using UnityEngine;

namespace Advanced_Graphics
{
    public partial class RailCrossingSurfaceSystem : GameSystemBase
    {
        private const string ColorSlot = "_BaseColorMap";
        private const string NormalSlot = "_NormalMap";
        private const string MaskSlot = "_MaskMap";
        private const string MaterialPrefix = "Batch (TrainRailsCover_";
        private const string TexturePrefix = "TrainRailsCover_BaseColor_";

        private sealed class Cover
        {
            internal Texture OriginalColor, OriginalNormal, OriginalMask;
            internal Texture2D Color, Normal, Mask;
        }

        private readonly Dictionary<Material, Cover> m_Applied = new Dictionary<Material, Cover>();
        private readonly List<Material> m_DestroyedMaterials = new List<Material>();
        private (int Mode, int Depth, int Gloss, int Uv, int X, int Y, int Rotation, int Color, int Opacity) m_Config;
        private Texture2D m_CustomColor, m_CustomNormal;
        private float m_NextScan;
        private bool m_Refresh = true, m_Failed;

        internal void ReloadCustomTexture() => m_Refresh = true;

        protected override void OnUpdate()
        {
            var settings = Mod.ActiveSetting;
            if (settings?.HasPendingApply == true) return;
            if (settings?.TexturesEnabled != true)
            {
                if (m_Applied.Count > 0 || m_CustomColor != null || m_CustomNormal != null || m_Failed)
                    RestoreNow();
                return;
            }

            var config = (
                Mathf.Clamp(settings.RailCrossingMode, Setting.CrossingThrough, Setting.CrossingCustom),
                Mathf.Clamp(settings.RailCrossingDepth, 0, Setting.MaxTextureDepthStrength),
                Mathf.Clamp(settings.RailCrossingGloss, 0, 100),
                Mathf.Clamp(settings.RailCrossingUv, 10, Setting.MaxTextureUvSize),
                Mathf.Clamp(settings.RailCrossingOffsetX, 0, Setting.MaxTextureUvOffset),
                Mathf.Clamp(settings.RailCrossingOffsetY, 0, Setting.MaxTextureUvOffset),
                Mathf.Clamp(settings.RailCrossingRotation, 0, 360),
                Mathf.Clamp(settings.RailCrossingColor, 0, 0xffffff),
                Mathf.Clamp(settings.RailCrossingColorOpacity, 0, 100));
            bool changed = m_Refresh || config != m_Config;
            float now = UnityEngine.Time.unscaledTime;
            if (!changed && (m_Failed || now < m_NextScan)) return;

            try
            {
                if (changed)
                {
                    RestoreMaterials();
                    if (m_Refresh || config.Item1 != m_Config.Mode)
                    {
                        ReleaseCustom();
                        if (config.Item1 == Setting.CrossingCustom)
                        {
                            m_CustomColor = LoadCustom("RailCrossing_BaseColor.png");
                            if (m_CustomColor != null) m_CustomNormal = LoadCustom("RailCrossing_Normal.png");
                        }
                    }
                    m_Config = config;
                    m_Refresh = m_Failed = false;
                }
                m_NextScan = now + 5f;
                foreach (var entry in m_Applied)
                {
                    if (entry.Key != null) continue;
                    ReleaseCover(entry.Key, entry.Value);
                    m_DestroyedMaterials.Add(entry.Key);
                }
                foreach (var material in m_DestroyedMaterials) m_Applied.Remove(material);
                m_DestroyedMaterials.Clear();

                foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
                {
                    if (material == null || !material.name.StartsWith(MaterialPrefix, StringComparison.Ordinal)) continue;
                    ApplyCover(material);
                }
            }
            catch (Exception ex)
            {
                RestoreMaterials();
                m_Config = config;
                m_Refresh = false;
                m_Failed = true;
                Mod.ErrorLater(ex, "Rail crossing cover texture could not be applied.");
            }
        }

        private void ApplyCover(Material material)
        {
            if (material.shader == null || material.shader.name != "BH/Decals/CurvedDecalShader" ||
                material.IsKeywordEnabled("ENABLE_VT") || !material.HasProperty(ColorSlot)) return;
            var current = material.GetTexture(ColorSlot);
            if (m_Applied.TryGetValue(material, out var existing))
            {
                if (Matches(material, ColorSlot, existing.OriginalColor, existing.Color) &&
                    Matches(material, NormalSlot, existing.OriginalNormal, existing.Normal) &&
                    Matches(material, MaskSlot, existing.OriginalMask, existing.Mask))
                {
                    ApplySlot(material, ColorSlot, existing.Color);
                    ApplySlot(material, NormalSlot, existing.Normal);
                    ApplySlot(material, MaskSlot, existing.Mask);
                    return;
                }
                ReleaseCover(material, existing);
                m_Applied.Remove(material);
                current = material.GetTexture(ColorSlot);
            }

            if (!(current is Texture2D source) || source == null ||
                !source.name.StartsWith(TexturePrefix, StringComparison.Ordinal) ||
                source.width > 2048 || source.height > 2048) return;

            var cover = new Cover
            {
                OriginalColor = source,
                OriginalNormal = OriginalSlot(material, NormalSlot, "TrainRailsCover_Normal_"),
                OriginalMask = OriginalSlot(material, MaskSlot, "TrainRailsCover_MaskMap_")
            };
            m_Applied.Add(material, cover);
            BuildCover(source, cover);
            ApplySlot(material, ColorSlot, cover.Color);
            ApplySlot(material, NormalSlot, cover.Normal);
            ApplySlot(material, MaskSlot, cover.Mask);
        }

        private static Texture OriginalSlot(Material material, string slot, string prefix)
        {
            if (!material.HasProperty(slot)) return null;
            var texture = material.GetTexture(slot);
            return texture is Texture2D && texture != null && texture.width <= 2048 && texture.height <= 2048 &&
                texture.name.StartsWith(prefix, StringComparison.Ordinal) ? texture : null;
        }

        private static bool Matches(Material material, string slot, Texture original, Texture2D replacement) =>
            replacement == null || (material.HasProperty(slot) &&
                (material.GetTexture(slot) == replacement || material.GetTexture(slot) == original));

        private static void ApplySlot(Material material, string slot, Texture2D replacement)
        {
            if (replacement != null && material.GetTexture(slot) != replacement) material.SetTexture(slot, replacement);
        }

        private static void RestoreSlot(Material material, string slot, Texture original, Texture2D replacement)
        {
            if (replacement == null) return;
            if (material != null && material.HasProperty(slot) && material.GetTexture(slot) == replacement)
                material.SetTexture(slot, original);
            UnityEngine.Object.Destroy(replacement);
        }

        private static void ReleaseCover(Material material, Cover cover)
        {
            RestoreSlot(material, ColorSlot, cover.OriginalColor, cover.Color);
            RestoreSlot(material, NormalSlot, cover.OriginalNormal, cover.Normal);
            RestoreSlot(material, MaskSlot, cover.OriginalMask, cover.Mask);
        }

        private void RestoreMaterials()
        {
            foreach (var entry in m_Applied) ReleaseCover(entry.Key, entry.Value);
            m_Applied.Clear();
        }

        private void ReleaseCustom()
        {
            if (m_CustomColor != null) UnityEngine.Object.Destroy(m_CustomColor);
            if (m_CustomNormal != null) UnityEngine.Object.Destroy(m_CustomNormal);
            m_CustomColor = m_CustomNormal = null;
        }

        internal void RestoreNow()
        {
            RestoreMaterials();
            ReleaseCustom();
            m_Refresh = true;
            m_Failed = false;
            m_NextScan = 0f;
        }

        protected override void OnDestroy()
        {
            RestoreNow();
            base.OnDestroy();
        }

        private static Texture2D LoadCustom(string filename)
        {
            string path = Path.Combine(TextureStorage.RailCrossing, filename);
            if (!File.Exists(path)) return null;
            Texture2D texture = null;
            try
            {
                if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new InvalidDataException("PNG exceeds 32 MB.");
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, filename.EndsWith("_Normal.png", StringComparison.Ordinal));
                if (!texture.LoadImage(File.ReadAllBytes(path), false) || texture.width > 4096 || texture.height > 4096)
                    throw new InvalidDataException("Expected a PNG of up to 4096 pixels per side.");
                texture.wrapMode = TextureWrapMode.Repeat;
                return texture;
            }
            catch (Exception ex)
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
                Mod.ErrorLater(ex, "Could not load " + filename);
                return null;
            }
        }
    }
}
