using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game;
using UnityEngine;

namespace Advanced_Graphics
{
    /// <summary>
    /// Material survey and temporary color probe for the visible track bed.
    /// </summary>
    public partial class RailMaterialInspector : GameSystemBase
    {
        private const int MaxMaterials = 200;
        private const int MaxTextures = 24;
        private bool m_Requested;
        private bool m_ProbeRequested;
        private bool m_ProbeActive;
        private int m_ProbeFrames;
        private readonly Dictionary<Material, Color> m_OriginalColors = new Dictionary<Material, Color>();
        public bool IsProbeActive => m_ProbeActive;

        public static string DiagnosticFolder => Path.Combine(TextureStorage.Rail, "Diagnostics");

        public void RequestExport() { m_Requested = true; }
        public void ToggleColorProbe() { m_ProbeRequested = true; }

        protected override void OnUpdate()
        {
            if (!Mod.DebugBackendEnabled)
            {
                m_ProbeRequested = m_Requested = false;
                if (m_ProbeActive) RestoreNow();
                return;
            }
            if (m_ProbeRequested)
            {
                m_ProbeRequested = false;
                if (m_ProbeActive) RestoreNow();
                else { m_ProbeActive = true; ApplyProbe(); }
            }
            if (m_ProbeActive && ++m_ProbeFrames >= 120)
            {
                m_ProbeFrames = 0;
                ApplyProbe();
            }
            if (m_Requested)
            {
                m_Requested = false;
                try { Export(); }
                catch (Exception ex) { Mod.log.Error(ex, "Rail material inspection failed."); }
            }
        }

        private static bool TryProbeColor(string name, out Color color)
        {
            if (name.StartsWith("Batch (TrainSubwayTrack_", StringComparison.Ordinal))
                color = new Color(4f, 0.05f, 4f, 1f); // magenta: target of gravel sliders
            else if (name.StartsWith("Batch (GravelShoulder", StringComparison.Ordinal))
                color = new Color(4f, 3f, 0f, 1f); // yellow
            else if (name.StartsWith("Batch (Gravel_", StringComparison.Ordinal))
                color = new Color(0f, 4f, 0f, 1f); // green
            else if (name.StartsWith("Batch (TrainRailsBase_", StringComparison.Ordinal) ||
                     name.StartsWith("Batch (TrainRailsCover_", StringComparison.Ordinal))
                color = new Color(4f, 0f, 0f, 1f); // red
            else if (name.StartsWith("Batch (TrainRails_", StringComparison.Ordinal))
                color = new Color(0f, 3f, 3f, 1f); // cyan
            else { color = default(Color); return false; }
            return true;
        }

        private void ApplyProbe()
        {
            int added = 0;
            foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (material == null || !material.HasProperty("_BaseColor") ||
                    !TryProbeColor(material.name, out var probe)) continue;
                if (!m_OriginalColors.TryGetValue(material, out var original))
                {
                    original = material.GetColor("_BaseColor");
                    m_OriginalColors.Add(material, original);
                    added++;
                }
                var current = material.GetColor("_BaseColor");
                if (current == original || current == probe)
                    material.SetColor("_BaseColor", probe);
            }
            if (added > 0) Mod.log.Info("Rail material color probe: " + added + " materials marked.");
        }

        public void RestoreNow()
        {
            m_ProbeActive = false;
            m_ProbeRequested = false;
            foreach (var pair in m_OriginalColors)
            {
                var material = pair.Key;
                if (material == null || !TryProbeColor(material.name, out var probe)) continue;
                if (material.GetColor("_BaseColor") == probe)
                    material.SetColor("_BaseColor", pair.Value);
            }
            m_OriginalColors.Clear();
        }

        private static void Export()
        {
            Directory.CreateDirectory(DiagnosticFolder);
            var report = new StringBuilder();
            report.AppendLine("Advanced Graphics rail material survey");
            report.AppendLine("Read-only export. Load a city with visible train/tram tracks before using the button.");
            report.AppendLine("Each PNG is named after the material index and shader texture slot listed below.");
            var exported = new HashSet<Texture>();
            int matches = 0, images = 0;
            foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (material == null || material.shader == null) continue;
                string[] slots;
                try { slots = material.GetTexturePropertyNames(); }
                catch (Exception) { continue; }
                bool relevant = IsRailRelated(material.name);
                var textures = new List<KeyValuePair<string, Texture>>();
                foreach (var slot in slots)
                {
                    Texture texture;
                    try { texture = material.GetTexture(slot); }
                    catch (Exception) { continue; }
                    if (texture == null) continue;
                    textures.Add(new KeyValuePair<string, Texture>(slot, texture));
                    if (IsRailRelated(texture.name)) relevant = true;
                }
                if (!relevant) continue;
                if (matches >= MaxMaterials)
                {
                    report.AppendLine("Material limit reached; remaining candidates omitted.");
                    break;
                }
                int index = ++matches;
                report.AppendLine();
                report.AppendLine(index + ". Material: " + material.name + " | shader: " + material.shader.name +
                    " | ENABLE_VT: " + material.IsKeywordEnabled("ENABLE_VT"));
                if (material.name.StartsWith("Batch (TrainRails_", StringComparison.Ordinal) ||
                    material.name.StartsWith("Batch (TrainSubwayTrack_", StringComparison.Ordinal))
                {
                    report.AppendLine("  Active keywords: " + string.Join(", ", material.shaderKeywords));
                    for (int propertyIndex = 0; propertyIndex < material.shader.GetPropertyCount(); propertyIndex++)
                    {
                        string property = material.shader.GetPropertyName(propertyIndex);
                        string kind = material.shader.GetPropertyType(propertyIndex).ToString();
                        if (kind == "Color")
                            report.AppendLine("  Shader color " + property + " = " + material.GetColor(property));
                        else if (kind == "Float" || kind == "Range")
                            report.AppendLine("  Shader number " + property + " = " + material.GetFloat(property));
                        else if (kind == "Texture")
                            report.AppendLine("  Shader texture slot " + property);
                    }
                }
                foreach (var item in textures)
                {
                    var texture = item.Value;
                    report.Append("  ").Append(item.Key).Append(" = ").Append(texture.name)
                        .Append(" (").Append(texture.width).Append('x').Append(texture.height).Append(")")
                        .Append(" scale=").Append(material.GetTextureScale(item.Key))
                        .Append(" offset=").Append(material.GetTextureOffset(item.Key)).AppendLine();
                    if (images >= MaxTextures || !IsColorOrMask(item.Key) || !(texture is Texture2D) ||
                        texture.width > 4096 || texture.height > 4096 || !exported.Add(texture)) continue;
                    string file = index + "_" + SafeName(item.Key) + ".png";
                    try
                    {
                        SaveTexture(texture, Path.Combine(DiagnosticFolder, file));
                        images++;
                        report.AppendLine("    Exported: " + file);
                    }
                    catch (Exception ex)
                    {
                        report.AppendLine("    Could not export " + item.Key + ": " + ex.Message);
                    }
                }
            }
            report.AppendLine();
            report.AppendLine("Matched materials: " + matches + "; exported textures: " + images);
            File.WriteAllText(Path.Combine(DiagnosticFolder, "RailMaterials.txt"), report.ToString());
            Mod.log.Info("Rail survey: " + matches + " materials, " + images +
                " textures exported to " + DiagnosticFolder);
        }

        private static bool IsColorOrMask(string slot) =>
            Contains(slot, "Color") || Contains(slot, "Albedo") ||
            Contains(slot, "Mask") || Contains(slot, "Normal");

        private static bool IsRailRelated(string name) =>
            Contains(name, "rail") || Contains(name, "track") || Contains(name, "ballast") ||
            Contains(name, "gravel") || Contains(name, "sleeper") || Contains(name, "railway") ||
            Contains(name, "schiene") || Contains(name, "gleis") || Contains(name, "kies");

        private static bool Contains(string text, string fragment) =>
            text != null && text.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string SafeName(string input)
        {
            var value = new StringBuilder();
            foreach (char character in input)
                value.Append(char.IsLetterOrDigit(character) ? character : '_');
            return value.ToString();
        }

        private static void SaveTexture(Texture source, string path)
        {
            var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Texture2D copy = null;
            try
            {
                UnityEngine.Graphics.Blit(source, target);
                RenderTexture.active = target;
                copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                copy.Apply();
                File.WriteAllBytes(path, copy.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                if (copy != null) UnityEngine.Object.Destroy(copy);
            }
        }
    }
}


