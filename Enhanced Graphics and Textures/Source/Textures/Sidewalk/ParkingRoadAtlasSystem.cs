using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Prefabs;
using UnityEngine;

namespace Advanced_Graphics
{
    // Parking roads (ParkingLane7 atlas): asphalt and sidewalk strips filled with the selected textures.
    public sealed partial class ParkingRoadAtlasSystem : SidewalkRoadAtlasSystem
    {
        private const string ParkingColorPrefix = "ParkingLane7_BaseColor_";
        private const string ParkingNormalPrefix = "ParkingLane7_Normal_";
        private const string ParkingColorSlot = "_BaseColorMap";
        private const string ParkingNormalSlot = "_NormalMap";
        private const string ParkingMaskSlot = "_MaskMap";
        private readonly Dictionary<Texture, Texture2D> m_RepeatMasks = new Dictionary<Texture, Texture2D>();
        private bool m_RepeatMaskFailed;
        private const int ReferenceWidth = 2048;
        private const int ParkingEnd = 482;
        private const int SidewalkStart = 516;
        private const int SidewalkEnd = 942;
        private const int AsphaltRepeatsPerAtlas = 8;
        private const float SidewalkTileMetres = 441f / 64f;
        private const float ParkingUPerMetre = 385f / 1024f / 9f;
        private const float ParkingVPerMetre = 0.0326f;
        private const float SidewalkRepeatsU = 1f / (SidewalkTileMetres * ParkingUPerMetre);
        private const float SidewalkRepeatsV = 1f / (SidewalkTileMetres * ParkingVPerMetre);
        private float m_SidewalkScaleU = 1f, m_SidewalkScaleV = 1f;
        private bool m_SidewalkCustom;
        // Slab pattern of the parking sidewalk strip (repeats per atlas U and V), used by the junction meshes.
        internal static float SidewalkPatternRepeatsU, SidewalkPatternRepeatsV;
        // Pattern repeat shift at both ends of the sidewalk strip (atlas U), used instead of mirroring in corners.
        internal static float SidewalkRepeatShiftRight, SidewalkRepeatShiftLeft;

        private sealed class ParkingMaterial
        {
            public Texture Color;
            public Texture Normal;
            public bool VirtualTexturing;
        }

        private readonly Dictionary<Material, ParkingMaterial> m_ParkingMaterials = new Dictionary<Material, ParkingMaterial>();
        private readonly Dictionary<Texture, Texture2D> m_ParkingReadbacks = new Dictionary<Texture, Texture2D>();
        private readonly Dictionary<Texture, Texture2D> m_ParkingColors = new Dictionary<Texture, Texture2D>();
        private readonly Dictionary<(Texture, Texture), Texture2D> m_ParkingNormals = new Dictionary<(Texture, Texture), Texture2D>();
        private Texture2D m_AsphaltColor, m_AsphaltNormal, m_SidewalkColor, m_SidewalkNormal;
        private int m_ParkingFrames;
        private int m_CornerVersion = -1;
        private float m_ParkingScaleFactor = 1f, m_ParkingDepthFactor = 1f;

        protected override string IsolatedSurfaceLabel => "Parking road";
        protected override bool IsTargetRoad(string name) => name != null &&
            (name.StartsWith("Alley", StringComparison.Ordinal) || name.StartsWith("Small Road", StringComparison.Ordinal) ||
             name.StartsWith("Medium Road", StringComparison.Ordinal)) &&
            name.IndexOf(" - ", StringComparison.Ordinal) >= 0 &&
            name.IndexOf("Parking", StringComparison.OrdinalIgnoreCase) >= 0;

        protected override bool IsTargetRoad(PrefabBase prefab) => prefab != null &&
            (IsTargetRoad(prefab.name) || (RoadBuilderSurfaceCompatibility.IsRoadBuilderRoad(prefab) &&
                RoadBuilderSurfaceCompatibility.HasParkingLanes(prefab)));

        internal override void UpdateTextureSampling()
        {
            base.UpdateTextureSampling();
            foreach (var texture in m_ParkingColors.Values) SurfaceTextureSampling.Apply(texture);
            foreach (var texture in m_ParkingNormals.Values) SurfaceTextureSampling.Apply(texture);
        }

        protected override void OnUpdate()
        {
            TexturePool.Trim();
            var settings = Mod.ActiveSetting;
            var asphaltColor = Mod.RoadAsphalt?.ActiveColorTexture;
            var asphaltNormal = asphaltColor != null ? Mod.RoadAsphalt?.ActiveNormalTexture : null;
            float parkingScaleFactor = settings?.ParkingTextureScaleFactor ?? 1f;
            float parkingDepthFactor = settings?.ParkingDepthStrengthFactor ?? 1f;
            bool sidewalkOverride = settings != null && settings.TexturesEnabled && settings.SidewalkEnabled &&
                (settings.SidewalkDetail != 0 || settings.SidewalkUvSizePercent != 100 ||
                 settings.SidewalkDepthStrengthPercent != 0 || settings.SidewalkUvOffsetX != 0 ||
                 settings.SidewalkUvOffsetY != 0 || settings.SidewalkUvRotation != 0 || settings.SidewalkCrackCount != 0 || TextureColorFilter.IsActive(settings.SidewalkFilterColor, settings.SidewalkFilterOpacityPercent));
            var sidewalkColor = sidewalkOverride ? Mod.Sidewalk?.ActiveColorTexture : null;
            var sidewalkNormal = sidewalkColor != null ? Mod.Sidewalk?.ActiveNormalTexture : null;
            bool scaleChanged = RefreshEndcapScale();
            float sidewalkScaleU = settings?.ParkingSidewalkScaleU ?? 1f, sidewalkScaleV = settings?.ParkingSidewalkScaleV ?? 1f;
            bool sidewalkCustom = Mod.Sidewalk?.UsesCustomTexture == true;
            bool changed = !Mathf.Approximately(sidewalkScaleU, m_SidewalkScaleU) || !Mathf.Approximately(sidewalkScaleV, m_SidewalkScaleV) ||
                sidewalkCustom != m_SidewalkCustom || m_AsphaltColor != asphaltColor || m_AsphaltNormal != asphaltNormal ||
                m_SidewalkColor != sidewalkColor || m_SidewalkNormal != sidewalkNormal ||
                (asphaltColor != null && (!Mathf.Approximately(m_ParkingScaleFactor, parkingScaleFactor) ||
                    !Mathf.Approximately(m_ParkingDepthFactor, parkingDepthFactor)));
            int cornerVersion = SidewalkJunctionMeshSystem.ParkingCornerVersion;
            if (cornerVersion != m_CornerVersion)
            {
                m_CornerVersion = cornerVersion;
                m_ParkingFrames = Mathf.Max(m_ParkingFrames, 110);
            }
            if (!changed && !scaleChanged && ++m_ParkingFrames < 120) return;
            m_ParkingFrames = 0;
            if (scaleChanged && !changed) RestoreIsolatedAlleyBatches();
            if (changed)
            {
                RestoreParkingNow(false);
                m_AsphaltColor = asphaltColor;
                m_AsphaltNormal = asphaltNormal;
                m_SidewalkColor = sidewalkColor;
                m_SidewalkNormal = sidewalkNormal;
                m_ParkingScaleFactor = parkingScaleFactor;
                m_ParkingDepthFactor = parkingDepthFactor;
                m_SidewalkScaleU = sidewalkScaleU;
                m_SidewalkScaleV = sidewalkScaleV;
                m_SidewalkCustom = sidewalkCustom;
            }
            if (asphaltColor == null && sidewalkColor == null)
            {
                RestoreParkingNow();
                return;
            }

            try
            {
                BeginIsolatedMaterials();
                int added = 0;
                foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
                {
                    if (material == null || material.shader == null ||
                        material.shader.name != "BH/NetCompositionMeshLitShader" ||
                        material.name == null || !material.name.StartsWith("Batch (", StringComparison.Ordinal) ||
                        !material.HasProperty(ParkingColorSlot)) continue;
                    if (!m_ParkingMaterials.TryGetValue(material, out var original))
                    {
                        var color = material.GetTexture(ParkingColorSlot);
                        if (color == null || !color.name.StartsWith(ParkingColorPrefix, StringComparison.Ordinal)) continue;
                        var normal = material.HasProperty(ParkingNormalSlot) ? material.GetTexture(ParkingNormalSlot) : null;
                        original = new ParkingMaterial
                        {
                            Color = color,
                            Normal = normal != null && normal.name.StartsWith(ParkingNormalPrefix, StringComparison.Ordinal) ? normal : null,
                            VirtualTexturing = material.IsKeywordEnabled("ENABLE_VT")
                        };
                        m_ParkingMaterials.Add(material, original);
                        added++;
                    }
                    if (material.GetTexture(ParkingColorSlot) != original.Color ||
                        (original.Normal != null && material.GetTexture(ParkingNormalSlot) != original.Normal)) continue;
                    if (!m_ParkingColors.TryGetValue(original.Color, out var colorCopy))
                    {
                        colorCopy = BuildParkingAtlas(original.Color, false);
                        m_ParkingColors.Add(original.Color, colorCopy);
                    }
                    Texture2D normalCopy = null;
                    if (original.Normal != null && !m_ParkingNormals.TryGetValue((original.Normal, original.Color), out normalCopy))
                    {
                        normalCopy = BuildParkingAtlas(original.Normal, true, original.Color);
                        m_ParkingNormals.Add((original.Normal, original.Color), normalCopy);
                    }
                    PrepareIsolatedAlleyMaterial(material, colorCopy, normalCopy, original.VirtualTexturing);
                    var isolated = IsolatedCopyOf(material);
                    if (isolated != null && isolated.HasProperty(ParkingMaskSlot))
                    {
                        var repeatMask = RepeatingMask(isolated.GetTexture(ParkingMaskSlot));
                        if (repeatMask != null) isolated.SetTexture(ParkingMaskSlot, repeatMask);
                    }
                }
                int applied = ApplyIsolatedAlleyBatches();
                if (added > 0)
                    Mod.LogLater("Parking road atlas: " + added + " new materials, " + applied +
                        " isolated batches; parking uses Asphalt, outer sidewalk uses Sidewalk; " +
                        "asphalt active " + (asphaltColor != null) + ", sidewalk active " + (sidewalkColor != null) + ".");
            }
            catch (Exception ex)
            {
                RestoreParkingNow();
                Mod.log.Error(ex, "Parking road atlas could not be applied.");
            }
        }

        // GPU copy of the mask atlas with V repeat (tiled parking edges run past V 1).
        private Texture RepeatingMask(Texture mask)
        {
            if (mask == null || mask.wrapModeV == TextureWrapMode.Repeat || m_RepeatMaskFailed) return null;
            if (m_RepeatMasks.TryGetValue(mask, out var cached) && cached != null) return cached;
            if (!(mask is Texture2D source)) return null;
            Texture2D copy = null;
            try
            {
                copy = new Texture2D(source.width, source.height, source.graphicsFormat, source.mipmapCount,
                    source.mipmapCount > 1 ? UnityEngine.Experimental.Rendering.TextureCreationFlags.MipChain
                        : UnityEngine.Experimental.Rendering.TextureCreationFlags.None)
                {
                    name = "Enhanced Graphics and Textures Parking road Mask",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = source.filterMode,
                    anisoLevel = source.anisoLevel,
                    wrapModeU = source.wrapModeU,
                    wrapModeV = TextureWrapMode.Repeat
                };
                UnityEngine.Graphics.CopyTexture(source, copy);
                m_RepeatMasks[mask] = copy;
                return copy;
            }
            catch (Exception ex)
            {
                if (copy != null) UnityEngine.Object.Destroy(copy);
                m_RepeatMaskFailed = true;
                Mod.log.Warn("Parking road mask could not be copied (" + ex.Message + "); the original mask is kept.");
                return null;
            }
        }

        private Texture2D BuildParkingAtlas(Texture source, bool linear, Texture referenceColor = null)
        {
            var timer = TexturePerformance.StartTimer("Parking road atlas " + (linear ? "normal" : "color"));
            if (!m_ParkingReadbacks.TryGetValue(source, out var cached))
            {
                cached = ReadParkingTexture(source, linear);
                m_ParkingReadbacks.Add(source, cached);
                timer.Mark("atlas readback");
            }
            var result = TexturePool.Get(cached.width, cached.height, linear, out bool reused);
            timer.Mark(reused ? "texture reused" : "texture new");
            try
            {
                var pixels = PixelBuffer.Read(cached);
                var copy = default(PixelBuffer);
                bool direct = PixelBuffer.TryWrite(result, out var target);
                try
                {
                    if (!direct)
                    {
                        copy = PixelBuffer.Allocate(pixels.Length);
                        target = copy.Pixels;
                    }
                    target.CopyFrom(pixels.Pixels);
                    float referenceAspect = (referenceColor ?? source).height / (float)(referenceColor ?? source).width;
                    if (m_AsphaltColor != null)
                        FillParkingStrip(target, cached.width, cached.height, 0, ParkingEnd,
                            linear ? m_AsphaltNormal : m_AsphaltColor, linear, AsphaltRepeatsPerAtlas * m_ParkingScaleFactor, referenceAspect, m_ParkingDepthFactor,
                            AsphaltRepeatsPerAtlas * m_ParkingScaleFactor * ParkingUPerMetre / ParkingVPerMetre);
                    if (m_SidewalkColor == null) SidewalkPatternRepeatsU = SidewalkPatternRepeatsV = 0f;
                    if (m_SidewalkColor != null)
                    {
                        float repeatsV = m_SidewalkCustom ? 1f / ((32f / 5f) * ParkingVPerMetre) : SidewalkRepeatsV;
                        SidewalkPatternRepeatsU = SidewalkRepeatsU * m_SidewalkScaleU;
                        SidewalkPatternRepeatsV = repeatsV * m_SidewalkScaleV;
                        FillParkingStrip(target, cached.width, cached.height, SidewalkStart, SidewalkEnd,
                            linear ? m_SidewalkNormal : m_SidewalkColor, linear, SidewalkRepeatsU * m_SidewalkScaleU, 1f, 1f, repeatsV * m_SidewalkScaleV);
                        if (!linear) MeasureRepeatShifts(target, cached.width, cached.height);
                    }
                    else SidewalkRepeatShiftRight = SidewalkRepeatShiftLeft = 0f;
                    if (!direct) result.SetPixels32(target.ToArray());
                }
                finally
                {
                    pixels.Dispose();
                    copy.Dispose();
                }
                timer.Mark("strips");
                TexturePerformance.ApplyWithMips(result, !KeepOwnAtlasReadable && !TexturePerformance.ReuseTextures, timer);
                timer.Mark("upload");
                result.name = "Enhanced Graphics and Textures Parking road " + (linear ? "Normal" : "Color");
                result.wrapModeU = source.wrapModeU;
                result.wrapModeV = TextureWrapMode.Repeat;
                SurfaceTextureSampling.Apply(result);
                timer.Log(cached.width + "x" + cached.height);
                return result;
            }
            catch { UnityEngine.Object.Destroy(result); throw; }
        }

        // Measures the repeat shifts from the column profile of the filled sidewalk strip.
        private static void MeasureRepeatShifts(Unity.Collections.NativeArray<Color32> pixels, int width, int height)
        {
            try
            {
                int left = Mathf.RoundToInt(width * SidewalkStart / (float)ReferenceWidth);
                int right = Mathf.RoundToInt(width * SidewalkEnd / (float)ReferenceWidth);
                int count = right - left;
                if (count < 40 || height < 8 || pixels.Length < width * height) return;
                var profile = new float[count];
                int step = Mathf.Max(1, height / 512);
                int rows = 0;
                for (int y = 0; y < height; y += step)
                {
                    int row = y * width;
                    for (int x = 0; x < count; x++)
                    {
                        var c = pixels[row + left + x];
                        profile[x] += c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                    }
                    rows++;
                }
                for (int x = 0; x < count; x++) profile[x] /= rows;
                int margin = Mathf.Max(2, count / 100);
                int window = Mathf.Max(16, count / 5);
                int best = 0;
                float bestScore = 0.5f;
                for (int shift = window; shift <= count - window - 2 * margin; shift++)
                {
                    float score = Correlation(profile, count - margin - window, count - margin - window - shift, window);
                    if (score > bestScore) { bestScore = score; best = shift; }
                }
                float measuredRight = best / (float)width;
                best = 0;
                bestScore = 0.5f;
                for (int shift = window; shift <= count - window - 2 * margin; shift++)
                {
                    float score = Correlation(profile, margin, margin + shift, window);
                    if (score > bestScore) { bestScore = score; best = shift; }
                }
                float measuredLeft = best / (float)width;
                // Every parking material builds its own atlas: keep the value unless it really changed (corners rebuild on change).
                SidewalkRepeatShiftRight = StableShift(SidewalkRepeatShiftRight, measuredRight);
                SidewalkRepeatShiftLeft = StableShift(SidewalkRepeatShiftLeft, measuredLeft);
                if (Mod.DebugBackendEnabled) Mod.LogLater("Parking sidewalk strip: repeat shift right " + (SidewalkRepeatShiftRight * width).ToString("0") +
                    " px, left " + (SidewalkRepeatShiftLeft * width).ToString("0") + " px of " + count + " px (" + width + " px atlas).");
            }
            catch (Exception ex)
            {
                SidewalkRepeatShiftRight = SidewalkRepeatShiftLeft = 0f;
                Mod.log.Warn("Parking sidewalk repeat shift could not be measured: " + ex.Message);
            }
        }

        private static float StableShift(float current, float measured)
        {
            float snapped = Mathf.Round(measured * ReferenceWidth) / ReferenceWidth;
            return current > 0f && snapped > 0f && Mathf.Abs(current - snapped) <= 2f / ReferenceWidth ? current : snapped;
        }

        private static float Correlation(float[] profile, int a, int b, int length)
        {
            float meanA = 0f, meanB = 0f;
            for (int i = 0; i < length; i++) { meanA += profile[a + i]; meanB += profile[b + i]; }
            meanA /= length;
            meanB /= length;
            float ab = 0f, aa = 0f, bb = 0f;
            for (int i = 0; i < length; i++)
            {
                float da = profile[a + i] - meanA, db = profile[b + i] - meanB;
                ab += da * db; aa += da * da; bb += db * db;
            }
            return aa > 1e-6f && bb > 1e-6f ? ab / Mathf.Sqrt(aa * bb) : 0f;
        }

        private static void FillParkingStrip(Unity.Collections.NativeArray<Color32> pixels, int width, int height,
            int referenceLeft, int referenceRight, Texture2D tile, bool linear, float repeatsPerAtlas, float referenceAspect, float normalStrength = 1f,
            float repeatsPerV = float.NaN)
        {
            Texture2D readable = tile;
            bool releaseTile = tile != null && !tile.isReadable;
            if (releaseTile) readable = ReadParkingTexture(tile, linear);
            var detail = readable != null ? PixelBuffer.Read(readable) : PixelBuffer.Allocate(1);
            try
            {
                int left = Mathf.RoundToInt(width * referenceLeft / (float)ReferenceWidth);
                int right = Mathf.RoundToInt(width * referenceRight / (float)ReferenceWidth);
                float vRepeats = float.IsNaN(repeatsPerV) ? referenceAspect * repeatsPerAtlas : repeatsPerV;
                TexturePerformance.Run(new ParkingStripRowsJob
                {
                    Pixels = pixels,
                    Detail = detail.Pixels,
                    HasDetail = readable != null,
                    Linear = linear,
                    Width = width,
                    Height = height,
                    Left = left,
                    Right = right,
                    DetailWidth = readable != null ? readable.width : 1,
                    DetailHeight = readable != null ? readable.height : 1,
                    ReferenceLeftU = referenceLeft / (float)ReferenceWidth,
                    RepeatsPerAtlas = repeatsPerAtlas,
                    VRepeats = vRepeats,
                    NormalStrength = normalStrength
                }, height);
            }
            finally
            {
                detail.Dispose();
                if (releaseTile && readable != null) UnityEngine.Object.Destroy(readable);
            }
        }

        protected override bool TryGetCachedReadback(Texture source, out Texture2D readback) =>
            m_ParkingReadbacks.TryGetValue(source, out readback) && readback != null;

        protected override void CacheReadback(Texture source, Texture2D readback) => m_ParkingReadbacks[source] = readback;

        private static Texture2D ReadParkingTexture(Texture source, bool linear)
        {
            int width = source.width, height = source.height;
            if (width > 4096 || height > 4096)
                throw new InvalidOperationException("Parking road texture exceeds 4096 pixels per side.");
            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32,
                linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Texture2D result = null;
            try
            {
                UnityEngine.Graphics.Blit(source, target);
                RenderTexture.active = target;
                result = new Texture2D(width, height, TextureFormat.RGBA32, false, linear);
                result.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                result.Apply(false, false);
                return result;
            }
            catch { if (result != null) UnityEngine.Object.Destroy(result); throw; }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
        }

        internal void ExportParkingAtlases(string folder)
        {
            Directory.CreateDirectory(folder);
            var report = new StringBuilder();
            report.AppendLine("Parking atlas diagnostic: original and applied texture bindings");
            int index = 0;
            foreach (var entry in m_ParkingMaterials)
            {
                var material = entry.Key;
                if (material == null) continue;
                var original = entry.Value;
                report.AppendLine(material.name);
                report.AppendLine("  Color: " + DescribeAtlas(original.Color));
                var boundNormal = material.HasProperty(ParkingNormalSlot) ? material.GetTexture(ParkingNormalSlot) : null;
                report.AppendLine("  Bound normal: " + DescribeAtlas(boundNormal));
                report.AppendLine("  Recognized normal: " + DescribeAtlas(original.Normal));
                if (material.HasProperty(ParkingNormalSlot))
                    report.AppendLine("  Normal scale/offset: " + material.GetTextureScale(ParkingNormalSlot) + " / " + material.GetTextureOffset(ParkingNormalSlot));
                report.AppendLine("  Color scale/offset: " + material.GetTextureScale(ParkingColorSlot) + " / " + material.GetTextureOffset(ParkingColorSlot));
            }
            foreach (var texture in m_ParkingColors.Values)
                SaveParkingDiagnostic(texture, Path.Combine(folder, "Parking_AppliedColor_" + index++ + ".png"), false);
            index = 0;
            foreach (var texture in m_ParkingNormals.Values)
                SaveParkingDiagnostic(texture, Path.Combine(folder, "Parking_AppliedNormal_" + index++ + ".png"), true);
            report.AppendLine("Applied color atlases: " + m_ParkingColors.Count + "; normal atlases: " + m_ParkingNormals.Count);
            File.WriteAllText(Path.Combine(folder, "Parking_Normal_Report.txt"), report.ToString());
            WriteMaterialProperties(folder);
        }

        private void WriteMaterialProperties(string folder)
        {
            var text = new StringBuilder();
            text.AppendLine("Shader properties of parking road batch materials (original and isolated copy) and one RoadEU batch material");
            var written = new HashSet<Material>();
            foreach (var entry in m_ParkingMaterials)
            {
                if (entry.Key == null || !written.Add(entry.Key)) continue;
                AppendMaterialProperties(text, "PARKING original", entry.Key);
            }
            foreach (var binding in IsolatedMaterialCopies)
                if (binding != null && written.Add(binding)) AppendMaterialProperties(text, "PARKING isolated copy", binding);
            foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (material == null || material.shader == null || material.shader.name != "BH/NetCompositionMeshLitShader" ||
                    material.name == null || !material.name.StartsWith("Batch (", StringComparison.Ordinal) ||
                    !material.HasProperty(ParkingColorSlot)) continue;
                var color = material.GetTexture(ParkingColorSlot);
                if (color == null || color.name == null || !color.name.StartsWith("RoadEU_BaseColor", StringComparison.Ordinal)) continue;
                AppendMaterialProperties(text, "ROADEU reference", material);
                break;
            }
            string path = Path.Combine(folder, "Parking_Material_Properties.txt");
            File.WriteAllText(path, text.ToString());
            Mod.log.Info("Parking material properties written to " + path);
        }

        private static void AppendMaterialProperties(StringBuilder text, string label, Material material)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var shader = material.shader;
            text.AppendLine();
            text.AppendLine("== " + label + ": " + material.name + " | shader " + (shader != null ? shader.name : "none") +
                " | queue " + material.renderQueue);
            text.AppendLine("  keywords: " + string.Join(" ", material.shaderKeywords ?? new string[0]));
            if (shader == null) return;
            int count = shader.GetPropertyCount();
            for (int i = 0; i < count; i++)
            {
                string name = shader.GetPropertyName(i);
                var type = shader.GetPropertyType(i);
                string value;
                try
                {
                    switch (type)
                    {
                        case UnityEngine.Rendering.ShaderPropertyType.Float:
                        case UnityEngine.Rendering.ShaderPropertyType.Range:
                            value = material.GetFloat(name).ToString("0.######", inv); break;
                        case UnityEngine.Rendering.ShaderPropertyType.Int:
                            value = material.GetInteger(name).ToString(inv); break;
                        case UnityEngine.Rendering.ShaderPropertyType.Vector:
                            value = material.GetVector(name).ToString("0.######"); break;
                        case UnityEngine.Rendering.ShaderPropertyType.Color:
                            value = material.GetColor(name).ToString("0.######"); break;
                        case UnityEngine.Rendering.ShaderPropertyType.Texture:
                            var texture = material.GetTexture(name);
                            value = (texture != null ? texture.name + " (" + texture.width + "x" + texture.height + ")" : "none") +
                                " scale " + material.GetTextureScale(name).ToString("0.####") + " offset " + material.GetTextureOffset(name).ToString("0.####");
                            break;
                        default: value = "?"; break;
                    }
                }
                catch (Exception ex) { value = "error " + ex.GetType().Name; }
                text.AppendLine("  " + type + " " + name + " = " + value);
            }
        }

        private static string DescribeAtlas(Texture texture) => texture == null ? "none" :
            texture.name + " (" + texture.width + " x " + texture.height + ")";

        private static void SaveParkingDiagnostic(Texture texture, string path, bool linear)
        {
            var readable = ReadParkingTexture(texture, linear);
            try { File.WriteAllBytes(path, readable.EncodeToPNG()); }
            finally { UnityEngine.Object.Destroy(readable); }
        }

        public void RestoreParkingNow(bool releaseSources = true)
        {
            RestoreIsolatedAlleyBatches();
            m_ParkingMaterials.Clear();
            foreach (var copy in m_ParkingColors.Values) TexturePool.Release(copy, false);
            foreach (var copy in m_ParkingNormals.Values) TexturePool.Release(copy, true);
            m_ParkingColors.Clear();
            m_ParkingNormals.Clear();
            foreach (var mask in m_RepeatMasks.Values) if (mask != null) UnityEngine.Object.Destroy(mask);
            m_RepeatMasks.Clear();
            m_AsphaltColor = m_AsphaltNormal = m_SidewalkColor = m_SidewalkNormal = null;
            if (releaseSources)
            {
                foreach (var copy in m_ParkingReadbacks.Values) if (copy != null) UnityEngine.Object.Destroy(copy);
                m_ParkingReadbacks.Clear();
                TexturePool.Clear();
            }
        }

        protected override void OnDestroy()
        {
            RestoreParkingNow();
            base.OnDestroy();
        }
    }
}