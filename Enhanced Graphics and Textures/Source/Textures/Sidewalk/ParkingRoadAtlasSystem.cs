using System;
using System.Collections.Generic;
using Game.Prefabs;
using UnityEngine;

namespace Advanced_Graphics
{
    public sealed partial class ParkingRoadAtlasSystem : SidewalkRoadAtlasSystem
    {
        private const string ParkingColorPrefix = "ParkingLane7_BaseColor_";
        private const string ParkingNormalPrefix = "ParkingLane7_Normal_";
        private const string ParkingColorSlot = "_BaseColorMap";
        private const string ParkingNormalSlot = "_NormalMap";

        private const int ReferenceWidth = 2048;
        private const int ParkingEnd = 482;
        private const int SidewalkStart = 516;
        private const int SidewalkEnd = 942;
        private const int AsphaltRepeatsPerAtlas = 8;
        private const int SidewalkRepeatsPerAtlas = 8;

        private sealed class ParkingMaterial
        {
            public Texture Color;
            public Texture Normal;
            public bool VirtualTexturing;
        }

        private readonly Dictionary<Material, ParkingMaterial> m_ParkingMaterials = new Dictionary<Material, ParkingMaterial>();
        private readonly Dictionary<Texture, Texture2D> m_ParkingReadbacks = new Dictionary<Texture, Texture2D>();
        private readonly Dictionary<Texture, Texture2D> m_ParkingColors = new Dictionary<Texture, Texture2D>();
        private readonly Dictionary<Texture, Texture2D> m_ParkingNormals = new Dictionary<Texture, Texture2D>();
        private Texture2D m_AsphaltColor, m_AsphaltNormal, m_SidewalkColor, m_SidewalkNormal;
        private int m_ParkingFrames;
        private float m_ParkingScaleFactor = 1f, m_ParkingDepthFactor = 1f;

        protected override string IsolatedSurfaceLabel => "Parking road";
        protected override bool IsTargetRoad(string name) => name != null &&
            (name.StartsWith("Alley", StringComparison.Ordinal) || name.StartsWith("Small Road", StringComparison.Ordinal)) &&
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
            var settings = Mod.ActiveSetting;
            var asphaltColor = Mod.RoadAsphalt?.ActiveColorTexture;
            var asphaltNormal = asphaltColor != null ? Mod.RoadAsphalt?.ActiveNormalTexture : null;
            float parkingScaleFactor = settings?.ParkingTextureScaleFactor ?? 1f;
            float parkingDepthFactor = settings?.ParkingDepthStrengthFactor ?? 1f;
            bool sidewalkOverride = settings != null && settings.SidewalkEnabled &&
                (settings.SidewalkDetail != 0 || settings.SidewalkUvSizePercent != 100 ||
                 settings.SidewalkDepthStrengthPercent != 0 || settings.SidewalkUvOffsetX != 0 ||
                 settings.SidewalkUvOffsetY != 0 || settings.SidewalkUvRotation != 0 || settings.SidewalkCrackCount != 0 || TextureColorFilter.IsActive(settings.SidewalkFilterColor, settings.SidewalkFilterOpacityPercent));
            var sidewalkColor = sidewalkOverride ? Mod.Sidewalk?.ActiveColorTexture : null;
            var sidewalkNormal = sidewalkColor != null ? Mod.Sidewalk?.ActiveNormalTexture : null;
            bool changed = m_AsphaltColor != asphaltColor || m_AsphaltNormal != asphaltNormal ||
                m_SidewalkColor != sidewalkColor || m_SidewalkNormal != sidewalkNormal ||
                (asphaltColor != null && (!Mathf.Approximately(m_ParkingScaleFactor, parkingScaleFactor) ||
                    !Mathf.Approximately(m_ParkingDepthFactor, parkingDepthFactor)));
            if (!changed && ++m_ParkingFrames < 120) return;
            m_ParkingFrames = 0;
            if (changed)
            {
                RestoreParkingNow(false);
                m_AsphaltColor = asphaltColor;
                m_AsphaltNormal = asphaltNormal;
                m_SidewalkColor = sidewalkColor;
                m_SidewalkNormal = sidewalkNormal;
                m_ParkingScaleFactor = parkingScaleFactor;
                m_ParkingDepthFactor = parkingDepthFactor;
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
                    // Never overwrite texture bindings changed by another owner.
                    if (material.GetTexture(ParkingColorSlot) != original.Color ||
                        (original.Normal != null && material.GetTexture(ParkingNormalSlot) != original.Normal)) continue;
                    if (!m_ParkingColors.TryGetValue(original.Color, out var colorCopy))
                    {
                        colorCopy = BuildParkingAtlas(original.Color, false);
                        m_ParkingColors.Add(original.Color, colorCopy);
                    }
                    Texture2D normalCopy = null;
                    if (original.Normal != null && !m_ParkingNormals.TryGetValue(original.Normal, out normalCopy))
                    {
                        normalCopy = BuildParkingAtlas(original.Normal, true);
                        m_ParkingNormals.Add(original.Normal, normalCopy);
                    }
                    PrepareIsolatedAlleyMaterial(material, colorCopy, normalCopy, original.VirtualTexturing);
                }
                int applied = ApplyIsolatedAlleyBatches();
                if (added > 0)
                    Mod.log.Info("Parking road atlas: " + added + " new materials, " + applied +
                        " isolated batches; parking uses Asphalt, outer sidewalk uses Sidewalk; " +
                        "asphalt active " + (asphaltColor != null) + ", sidewalk active " + (sidewalkColor != null) + ".");
            }
            catch (Exception ex)
            {
                // Unbind copies before discarding partially built textures.
                RestoreParkingNow();
                Mod.log.Error(ex, "Parking road atlas could not be applied.");
            }
        }

        private Texture2D BuildParkingAtlas(Texture source, bool linear)
        {
            if (!m_ParkingReadbacks.TryGetValue(source, out var cached))
            {
                cached = ReadParkingTexture(source, linear);
                m_ParkingReadbacks.Add(source, cached);
            }
            var result = new Texture2D(cached.width, cached.height, TextureFormat.RGBA32, true, linear);
            try
            {
                var pixels = cached.GetPixels32();
                if (m_AsphaltColor != null)
                    FillParkingStrip(pixels, cached.width, cached.height, 0, ParkingEnd,
                        linear ? m_AsphaltNormal : m_AsphaltColor, linear, AsphaltRepeatsPerAtlas * m_ParkingScaleFactor, m_ParkingDepthFactor);
                if (m_SidewalkColor != null)
                    FillParkingStrip(pixels, cached.width, cached.height, SidewalkStart, SidewalkEnd,
                        linear ? m_SidewalkNormal : m_SidewalkColor, linear, SidewalkRepeatsPerAtlas);
                result.SetPixels32(pixels);
                result.Apply(true, true);
                result.name = "Enhanced Graphics and Textures Parking road " + (linear ? "Normal" : "Color");
                result.wrapMode = source.wrapMode;
                SurfaceTextureSampling.Apply(result);
                return result;
            }
            catch { UnityEngine.Object.Destroy(result); throw; }
        }

        private static void FillParkingStrip(Color32[] pixels, int width, int height,
            int referenceLeft, int referenceRight, Texture2D tile, bool linear, float repeatsPerAtlas, float normalStrength = 1f)
        {
            Texture2D readable = tile;
            bool releaseTile = tile != null && !tile.isReadable;
            if (releaseTile) readable = ReadParkingTexture(tile, linear);
            try
            {
                var detail = readable != null ? readable.GetPixels32() : null;
                int left = Mathf.RoundToInt(width * referenceLeft / (float)ReferenceWidth);
                int right = Mathf.RoundToInt(width * referenceRight / (float)ReferenceWidth);
                float period = width / repeatsPerAtlas;
                for (int y = 0; y < height; y++)
                for (int x = left; x < right; x++)
                {
                    int index = y * width + x;
                    var original = pixels[index];
                    Color32 replacement = new Color32(128, 128, 255, 255);
                    if (detail != null)
                    {
                        int u = (int)((long)(x - left) * readable.width / period) % readable.width;
                        int v = (int)((long)y * readable.height / period) % readable.height;
                        replacement = detail[v * readable.width + u];
                    }
                    if (linear)
                    {
                        if (normalStrength == 0f)
                        {
                            replacement.r = replacement.g = 128;
                        }
                        else if (normalStrength != 1f)
                        {
                            // Scale the tangent-space normal before packing it for the parking atlas.
                            var normal = new Vector3(
                                (replacement.r / 255f * 2f - 1f) * normalStrength,
                                (replacement.g / 255f * 2f - 1f) * normalStrength,
                                Mathf.Max(0.001f, replacement.b / 255f * 2f - 1f)).normalized;
                            replacement.r = (byte)Mathf.Clamp((normal.x * 0.5f + 0.5f) * 255f, 0f, 255f);
                            replacement.g = (byte)Mathf.Clamp((normal.y * 0.5f + 0.5f) * 255f, 0f, 255f);
                        }
                        // Match the existing RoadEU atlas's packed normal channels.
                        replacement.a = replacement.r;
                        replacement.r = original.r;
                        replacement.b = original.b;
                    }
                    else replacement.a = original.a;
                    pixels[index] = replacement;
                }
            }
            finally { if (releaseTile && readable != null) UnityEngine.Object.Destroy(readable); }
        }

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

        public void RestoreParkingNow(bool releaseSources = true)
        {
            RestoreIsolatedAlleyBatches();
            m_ParkingMaterials.Clear();
            foreach (var copy in m_ParkingColors.Values) if (copy != null) UnityEngine.Object.Destroy(copy);
            foreach (var copy in m_ParkingNormals.Values) if (copy != null) UnityEngine.Object.Destroy(copy);
            m_ParkingColors.Clear();
            m_ParkingNormals.Clear();
            m_AsphaltColor = m_AsphaltNormal = m_SidewalkColor = m_SidewalkNormal = null;
            if (releaseSources)
            {
                foreach (var copy in m_ParkingReadbacks.Values) if (copy != null) UnityEngine.Object.Destroy(copy);
                m_ParkingReadbacks.Clear();
            }
        }

        protected override void OnDestroy()
        {
            RestoreParkingNow();
            base.OnDestroy();
        }
    }
}


