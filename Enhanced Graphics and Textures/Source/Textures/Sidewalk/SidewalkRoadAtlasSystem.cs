using System;
using System.Collections.Generic;
using System.IO;
using Game;
using UnityEngine;

namespace Advanced_Graphics
{

    public partial class SidewalkRoadAtlasSystem : GameSystemBase
    {
        private const string ShaderName = "BH/NetCompositionMeshLitShader";
        private const string ColorSlot = "_BaseColorMap";
        private const string NormalSlot = "_NormalMap";
        private const string ColorPrefix = "RoadEU_BaseColor_";
        private const string NormalPrefix = "RoadEU_Normal_";
        private const string AlleyColorPrefix = "ParkingLane7_BaseColor_";
        private const string AlleyNormalPrefix = "ParkingLane7_Normal_";
        private const int X0 = 318, X1 = 759, Y0 = 0, Y1 = 2048;
        internal static bool RoadPatternValid;
        internal static float RoadPatternRepeatsV, RoadPatternShiftU, RoadPatternShiftV;
        protected virtual SidewalkSurfaceSystem Surface => Mod.Sidewalk;
        protected virtual bool IsAlleyAtlas => false;
        private string SurfaceLabel => IsAlleyAtlas ? "Alley sidewalk" : "RoadEU sidewalk";
        private readonly Dictionary<Texture, Texture2D> m_Readbacks = new Dictionary<Texture, Texture2D>();

        private struct Original
        {
            public Texture Color;
            public Texture Normal;
            public bool VirtualTexturing;
            public bool AlleyShoulder;
        }

        private readonly Dictionary<Material, Original> m_Materials = new Dictionary<Material, Original>();
        private readonly Dictionary<Texture, Texture2D> m_ColorCopies = new Dictionary<Texture, Texture2D>();
        private readonly Dictionary<(Texture, Texture), Texture2D> m_NormalCopies = new Dictionary<(Texture, Texture), Texture2D>();
        private Texture2D m_SourceColor, m_SourceNormal;
        private bool m_Enabled, m_Diagnostic, m_BuiltDiagnostic, m_BuiltCustom, m_Dirty;
        private bool m_LoggedMissing;
        private int m_BuiltShiftU, m_BuiltShiftV;
        private bool UsesSidewalkShift => !IsAlleyAtlas && !(this is ParkingRoadAtlasSystem);
        private int CurrentShiftU => UsesSidewalkShift ? Mod.ActiveSetting?.SidewalkShiftU ?? 0 : 0;
        private int CurrentShiftV => UsesSidewalkShift ? Mod.ActiveSetting?.SidewalkShiftV ?? 0 : 0;
        private int m_Frames;
        private bool m_RoadEuAtlasChanged;

        internal bool IsEnabled => m_Enabled;
        internal void NotifyRoadEuAtlasChanged() => m_RoadEuAtlasChanged = true;

        // Own atlases stay readable while endcaps or the Alley atlas read them again.
        protected bool KeepOwnAtlasReadable => TexturePerformance.KeepAtlasReadable &&
            (EndcapScaleActive || (!IsAlleyAtlas && !(this is ParkingRoadAtlasSystem) &&
                Mod.AlleySidewalkAtlas != null && Mod.AlleySidewalkAtlas.IsEnabled));

        private void NotifyAlleyAtlas()
        {
            if (!IsAlleyAtlas && !(this is ParkingRoadAtlasSystem)) Mod.AlleySidewalkAtlas?.NotifyRoadEuAtlasChanged();
        }

        internal bool OwnsAppliedAtlas(Texture texture, bool normal)
        {
            if (texture == null) return false;
            if (normal)
            {
                foreach (var copy in m_NormalCopies.Values) if (copy == texture) return true;
            }
            else
            {
                foreach (var copy in m_ColorCopies.Values) if (copy == texture) return true;
            }
            return false;
        }

        internal Texture FindOriginalColorAtlas()
        {
            foreach (var entry in m_Materials.Values)
                if (entry.Color != null) return entry.Color;
            foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (material == null || material.shader == null || material.shader.name != ShaderName ||
                    !material.HasProperty(ColorSlot)) continue;
                var color = material.GetTexture(ColorSlot);
                if (color != null && (IsAlleyAtlas
                    ? material.name != null && material.name.StartsWith("Batch (", StringComparison.Ordinal) &&
                        color.name.StartsWith(AlleyColorPrefix, StringComparison.Ordinal)
                    : color.name.StartsWith(ColorPrefix, StringComparison.Ordinal))) return color;
            }
            return null;
        }

        public void Configure(bool enabled, bool diagnostic)
        {
            diagnostic = diagnostic && Mod.DebugBackendEnabled;
            if (m_Enabled == enabled && m_Diagnostic == diagnostic) return;
            m_Enabled = enabled;
            m_Diagnostic = diagnostic;
            m_Dirty = true;
        }

        internal virtual void UpdateTextureSampling()
        {
            foreach (var texture in m_ColorCopies.Values) SurfaceTextureSampling.Apply(texture);
            foreach (var texture in m_NormalCopies.Values) SurfaceTextureSampling.Apply(texture);
            foreach (var texture in m_EndcapTextures.Values) SurfaceTextureSampling.Apply(texture);
        }

        protected override void OnUpdate()
        {
            TexturePool.Trim();
            if (Mod.ActiveSetting?.HasPendingApply == true) return;
            bool scaleChanged = RefreshEndcapScale();
            bool tileChanged = m_SourceColor != Surface?.ActiveColorTexture ||
                m_SourceNormal != Surface?.ActiveNormalTexture ||
                m_BuiltShiftU != CurrentShiftU || m_BuiltShiftV != CurrentShiftV;
            bool roadEuChanged = IsAlleyAtlas && m_RoadEuAtlasChanged;
            if (!m_Dirty && !tileChanged && !scaleChanged && !roadEuChanged && ++m_Frames < 120) return;
            m_Frames = 0;
            m_Dirty = false;
            m_RoadEuAtlasChanged = false;
            if (!m_Enabled)
            {
                RestoreNow();
                return;
            }

            var color = Surface?.ActiveColorTexture;
            var normal = Surface?.ActiveNormalTexture;
            bool customTexture = Surface?.UsesCustomTexture == true;
            if (color == null)
            {
                if (m_SourceColor != null) RestoreNow();
                return;
            }

            try
            {
                if (scaleChanged) RestoreIsolatedAlleyBatches();
                int shiftU = CurrentShiftU, shiftV = CurrentShiftV;
                if (m_SourceColor != color || m_SourceNormal != normal ||
                    m_BuiltDiagnostic != m_Diagnostic || m_BuiltCustom != customTexture ||
                    m_BuiltShiftU != shiftU || m_BuiltShiftV != shiftV || roadEuChanged)
                {
                    RestoreNow(false);
                    m_BuiltShiftU = shiftU;
                    m_BuiltShiftV = shiftV;
                    m_SourceColor = color;
                    m_SourceNormal = normal;
                    m_BuiltDiagnostic = m_Diagnostic;
                    m_BuiltCustom = customTexture;
                    m_LoggedMissing = false;
                }

                int added = 0;
                int applied = 0;
                int virtualMaterials = 0;
                m_AlleyCurrentMaterialSources.Clear();
                foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
                {
                    if (material == null || material.shader == null || material.shader.name != ShaderName ||
                        !material.HasProperty(ColorSlot)) continue;
                    if (material.name.StartsWith("Enhanced Graphics and Textures", StringComparison.Ordinal)) continue;
                    if (IsAlleyAtlas && m_Materials.TryGetValue(material, out var previous) &&
                        (material.GetTexture(ColorSlot) != previous.Color ||
                         (material.HasProperty(NormalSlot) && material.GetTexture(NormalSlot) != previous.Normal)))
                        m_Materials.Remove(material);
                    if (!m_Materials.TryGetValue(material, out var original))
                    {
                        Texture baseColor = material.GetTexture(ColorSlot);
                        if (baseColor == null) continue;
                        bool alleyShoulder = material.name != null &&
                            material.name.StartsWith("Batch (", StringComparison.Ordinal) &&
                            baseColor.name.StartsWith(AlleyColorPrefix, StringComparison.Ordinal);
                        bool roadEndAtlas = baseColor.name.StartsWith(ColorPrefix, StringComparison.Ordinal) ||
                            (IsAlleyAtlas && Mod.SidewalkAtlas?.OwnsAppliedAtlas(baseColor, false) == true);
                        if (IsAlleyAtlas ? !(alleyShoulder || roadEndAtlas) : !roadEndAtlas) continue;
                        Texture baseNormal = material.HasProperty(NormalSlot) ? material.GetTexture(NormalSlot) : null;
                        original = new Original
                        {
                            Color = baseColor,
                            AlleyShoulder = alleyShoulder,
                            VirtualTexturing = material.IsKeywordEnabled("ENABLE_VT"),
                            Normal = baseNormal != null && (baseNormal.name.StartsWith(
                                alleyShoulder ? AlleyNormalPrefix : NormalPrefix, StringComparison.Ordinal) ||
                                (IsAlleyAtlas && Mod.SidewalkAtlas?.OwnsAppliedAtlas(baseNormal, true) == true))
                                ? baseNormal : null
                        };
                        m_Materials.Add(material, original);
                        added++;
                        if (alleyShoulder)
                            Mod.LogLater("Alley sidewalk matched: " + material.name + " | " + baseColor.name);
                    }

                    if (!m_ColorCopies.TryGetValue(original.Color, out var colorCopy))
                    {
                        colorCopy = BuildAtlas(original.Color, color, false, m_Diagnostic, customTexture,
                            original.AlleyShoulder, original.Color);
                        m_ColorCopies.Add(original.Color, colorCopy);
                    }
                    Texture2D normalCopy = null;
                    if (!m_Diagnostic && normal != null && original.Normal != null)
                    {
                        if (!m_NormalCopies.TryGetValue((original.Normal, original.Color), out normalCopy))
                        {
                            normalCopy = BuildAtlas(original.Normal, normal, true, false, customTexture,
                                original.AlleyShoulder, original.Color);
                            m_NormalCopies.Add((original.Normal, original.Color), normalCopy);
                        }
                    }
                    var currentColor = material.GetTexture(ColorSlot);
                    if (currentColor != original.Color && currentColor != colorCopy) continue;
                    if (normalCopy != null && material.GetTexture(NormalSlot) != original.Normal &&
                        material.GetTexture(NormalSlot) != normalCopy) continue;
                    if (IsAlleyAtlas)
                    {
                        PrepareIsolatedAlleyMaterial(material, colorCopy, normalCopy, original.VirtualTexturing);
                        continue;
                    }
                    if (EndcapScaleActive) PrepareIsolatedAlleyMaterial(material, colorCopy, normalCopy, original.VirtualTexturing);
                    material.SetTexture(ColorSlot, colorCopy);
                    if (normalCopy != null) material.SetTexture(NormalSlot, normalCopy);
                    if (original.VirtualTexturing) { material.DisableKeyword("ENABLE_VT"); virtualMaterials++; }
                    applied++;
                }
                if (IsAlleyAtlas) applied = ApplyIsolatedAlleyBatches();
                else if (EndcapScaleActive) ApplyIsolatedAlleyBatches(nodesOnly: true);
                string region = m_Diagnostic ? "color regions red/yellow/blue/cyan/magenta/green"
                    : (IsAlleyAtlas ? "ParkingLane7 atlas on isolated Alley batches" : "sidewalk region x=318..758");
                if (added > 0) NotifyAlleyAtlas();
                if (added > 0) Mod.LogLater($"{SurfaceLabel} atlas: {m_Materials.Count} matched materials ({added} new), {applied} applied, {virtualMaterials} switched from ENABLE_VT; {region}; source tile {color.width}x{color.height}.");
                if (m_Materials.Count == 0 && !m_LoggedMissing)
                {
                    m_LoggedMissing = true;
                    Mod.LogLater("RoadEU atlas diagnostic: 0 matching materials. Load a city and retry.");
                }
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex, "RoadEU sidewalk atlas could not be applied.");
            }
        }

        private Texture2D BuildAtlas(Texture source, Texture overlay, bool linear, bool diagnostic,
            bool customTexture, bool alleyShoulder = false, Texture referenceColor = null)
        {
            if (source.width > 4096 || source.height > 4096)
                throw new InvalidOperationException("RoadEU atlas exceeds 4096 pixels per side.");

            var timer = TexturePerformance.StartTimer(SurfaceLabel + " atlas " + (linear ? "normal" : "color"));
            Texture2D cached;
            if (IsDirectlyReadable(source)) cached = (Texture2D)source;
            else if (!m_Readbacks.TryGetValue(source, out cached))
            {
                cached = Readback(source, linear);
                m_Readbacks.Add(source, cached);
                timer.Mark("atlas readback");
            }
            var detail = diagnostic ? null : overlay as Texture2D;
            bool destroyDetail = false;
            if (!diagnostic && (detail == null || !detail.isReadable))
            {
                detail = Readback(overlay, linear);
                destroyDetail = true;
                timer.Mark("tile readback");
            }
            var original = TexturePool.Get(cached.width, cached.height, linear, out bool reused);
            timer.Mark(reused ? "texture reused" : "texture new");
            try
            {
                int width = original.width, height = original.height;
                int left = diagnostic || alleyShoulder ? 0 : Mathf.RoundToInt(width * (X0 / 1024f));
                int right = diagnostic || alleyShoulder ? width : Mathf.RoundToInt(width * (X1 / 1024f));
                int bottom = diagnostic ? 0 : Mathf.RoundToInt(height * (1f - Y1 / 2048f));
                int top = diagnostic ? height : Mathf.RoundToInt(height * (1f - Y0 / 2048f));
                float referenceAspect = referenceColor != null
                    ? referenceColor.height / (float)referenceColor.width : 2f;
                float stripFraction = alleyShoulder ? 1f / 8f : (X1 - X0) / 1024f;
                float verticalRepeats = referenceAspect / stripFraction;
                if (customTexture && !diagnostic)
                    verticalRepeats = Mathf.Max(1, Mathf.RoundToInt(verticalRepeats));
                if (UsesSidewalkShift && !alleyShoulder && !linear)
                {
                    RoadPatternValid = !diagnostic;
                    RoadPatternRepeatsV = verticalRepeats;
                    RoadPatternShiftU = m_BuiltShiftU / 100f;
                    RoadPatternShiftV = m_BuiltShiftV / 100f;
                }
                var atlas = PixelBuffer.Read(cached);
                var tile = detail != null ? PixelBuffer.Read(detail) : PixelBuffer.Allocate(1);
                var copy = default(PixelBuffer);
                try
                {
                    bool direct = PixelBuffer.TryWrite(original, out var pixels);
                    if (!direct)
                    {
                        copy = PixelBuffer.Allocate(atlas.Length);
                        pixels = copy.Pixels;
                    }
                    pixels.CopyFrom(atlas.Pixels);
                    TexturePerformance.Run(new SidewalkStripRowsJob
                    {
                        Pixels = pixels,
                        Tile = tile.Pixels,
                        Diagnostic = diagnostic,
                        AlleyShoulder = alleyShoulder,
                        Linear = linear,
                        Width = width,
                        Height = height,
                        Bottom = bottom,
                        Left = left,
                        Right = right,
                        TileWidth = detail != null ? detail.width : 1,
                        TileHeight = detail != null ? detail.height : 1,
                        OriginU = alleyShoulder ? 0f : X0 / 1024f,
                        ShiftU = alleyShoulder ? 0f : m_BuiltShiftU / 100f,
                        ShiftV = alleyShoulder ? 0f : m_BuiltShiftV / 100f,
                        StripFraction = stripFraction,
                        VerticalRepeats = verticalRepeats
                    }, Math.Max(0, top - bottom));
                    if (!direct) original.SetPixels32(pixels.ToArray());
                }
                finally
                {
                    atlas.Dispose();
                    tile.Dispose();
                    copy.Dispose();
                }
                timer.Mark("strip");
                TexturePerformance.ApplyWithMips(original, !KeepOwnAtlasReadable && !TexturePerformance.ReuseTextures, timer);
                timer.Mark("upload");
                string surface = alleyShoulder ? "Alley Sidewalk" : "RoadEU Sidewalk";
                original.name = diagnostic ? "Advanced Graphics " + surface + " Diagnostic" :
                    "Advanced Graphics " + surface + " " + (linear ? "Normal" : "Color");
                original.wrapMode = source.wrapMode;
                SurfaceTextureSampling.Apply(original);
                timer.Log(width + "x" + height);
                return original;
            }
            catch
            {
                UnityEngine.Object.Destroy(original);
                throw;
            }
            finally
            {
                if (destroyDetail && detail != null) UnityEngine.Object.Destroy(detail);
            }
        }

        private static Texture2D Readback(Texture source, bool linear, int maximumSize = 4096)
        {
            float scale = Mathf.Min(1f, maximumSize / (float)Mathf.Max(source.width, source.height));
            int width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
            var target = RenderTexture.GetTemporary(width, height, 0,
                RenderTextureFormat.ARGB32, linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            try
            {
                UnityEngine.Graphics.Blit(source, target);
                RenderTexture.active = target;
                var copy = new Texture2D(width, height, TextureFormat.RGBA32, false, linear);
                copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                copy.Apply();
                return copy;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        public void ExportAppliedAtlases(string folder)
        {
            if (!Mod.DebugBackendEnabled) return;
            var tile = Surface?.ActiveColorTexture;
            if (tile != null) SaveTexture(tile, Path.Combine(folder, "Sidewalk_ActiveTile.png"));
            var normalTile = Surface?.ActiveNormalTexture;
            if (normalTile != null) SaveTexture(normalTile, Path.Combine(folder, "Sidewalk_ActiveNormal.png"), true);
            foreach (var pair in m_Materials)
            {
                
                if (pair.Key == null || !m_ColorCopies.TryGetValue(pair.Value.Color, out var copy) ||
                    pair.Key.GetTexture(ColorSlot) != copy) continue;
                SaveTexture(copy, Path.Combine(folder, "RoadEU_AppliedSidewalk_Atlas.png"));
                if (pair.Value.Normal != null && m_NormalCopies.TryGetValue((pair.Value.Normal, pair.Value.Color), out var normalCopy) &&
                    pair.Key.GetTexture(NormalSlot) == normalCopy)
                {
                    SaveTexture(pair.Value.Normal, Path.Combine(folder, "RoadEU_OriginalNormal_Atlas.png"), true);
                    SaveTexture(normalCopy, Path.Combine(folder, "RoadEU_AppliedNormal_Atlas.png"), true);
                }
                Mod.log.Info("Sidewalk diagnostic: active tiles and applied RoadEU atlases exported to " + folder);
                return;
            }
            Mod.log.Info("Sidewalk diagnostic: active tile exported; no applied RoadEU atlas is currently bound to a material.");
        }

        private static void SaveTexture(Texture source, string path, bool linear = false)
        {
            var copy = Readback(source, linear);
            try { File.WriteAllBytes(path, copy.EncodeToPNG()); }
            finally { UnityEngine.Object.Destroy(copy); }
        }

        public void RestoreNow(bool releaseSources = true)
        {
            bool hadMaterials = m_Materials.Count > 0;
            RestoreIsolatedAlleyBatches();
            foreach (var pair in m_Materials)
            {
                var material = pair.Key;
                if (material == null) continue;
                if (m_ColorCopies.TryGetValue(pair.Value.Color, out var colorCopy) &&
                    material.GetTexture(ColorSlot) == colorCopy)
                {
                    material.SetTexture(ColorSlot, pair.Value.Color);
                    if (pair.Value.VirtualTexturing) material.EnableKeyword("ENABLE_VT");
                }
                if (pair.Value.Normal != null && m_NormalCopies.TryGetValue((pair.Value.Normal, pair.Value.Color), out var normalCopy) &&
                    material.GetTexture(NormalSlot) == normalCopy)
                    material.SetTexture(NormalSlot, pair.Value.Normal);
            }
            m_Materials.Clear();
            foreach (var copy in m_ColorCopies.Values) TexturePool.Release(copy, false);
            foreach (var copy in m_NormalCopies.Values) TexturePool.Release(copy, true);
            m_ColorCopies.Clear();
            m_NormalCopies.Clear();
            m_SourceColor = m_SourceNormal = null;
            if (releaseSources)
            {
                foreach (var copy in m_Readbacks.Values) if (copy != null) UnityEngine.Object.Destroy(copy);
                m_Readbacks.Clear();
                TexturePool.Clear();
            }
            else PurgeStaleReadbacks();
            if (hadMaterials) NotifyAlleyAtlas();
        }

        private void PurgeStaleReadbacks()
        {
            List<Texture> stale = null;
            foreach (var pair in m_Readbacks)
            {
                if (pair.Key != null) continue;
                if (stale == null) stale = new List<Texture>();
                stale.Add(pair.Key);
            }
            if (stale == null) return;
            foreach (var key in stale)
            {
                var copy = m_Readbacks[key];
                if (copy != null) UnityEngine.Object.Destroy(copy);
                m_Readbacks.Remove(key);
            }
        }
    }
}
