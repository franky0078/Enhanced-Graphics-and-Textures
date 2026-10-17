using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Advanced_Graphics
{
    public partial class RoadMaterialInspector
    {
        public static string GrassDiagnosticFolder =>
            Path.GetFullPath(Path.Combine(TextureStorage.Sidewalk, "..", "Grass", "Diagnostics"));

        private static bool IsGrassCandidate(string name) => name != null &&
            (Contains(name, "grass") || Contains(name, "lawn") || Contains(name, "turf"));

        private static bool IsRoadCandidate(string name) => name != null &&
            (Contains(name, "road") || Contains(name, "sidewalk") || Contains(name, "pavement") ||
             Contains(name, "pathway") || Contains(name, "pedestrian") || Contains(name, "curb"));

        private static string GrassNumber(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static void ExportGrassMaterials()
        {
            Directory.CreateDirectory(GrassDiagnosticFolder);
            var report = new StringBuilder("Enhanced Graphics and Textures - roadside grass survey\n");
            report.AppendLine("UTC: " + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            report.AppendLine("Read-only. Candidates do not prove which material renders the visible grass strip.");
            report.AppendLine("Grass candidates first, then road/network materials (including shared atlases).");
            report.AppendLine("PNG previews: at most 24 unique textures, downsampled to at most 2048 pixels per side.");
            report.AppendLine("Normal/mask/height previews use linear readback. Alpha is preserved.");
            report.AppendLine("Textures visible only through virtual texturing may not be exportable. Shader properties are still listed.");

            var grass = new List<Material>();
            var roads = new List<Material>();
            int scanErrors = 0;
            foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (material == null || material.shader == null) continue;
                try
                {
                    bool grassMatch = IsGrassCandidate(material.name) || IsGrassCandidate(material.shader.name);
                    bool roadMatch = IsRoadCandidate(material.name) || Contains(material.shader.name, "NetComposition");
                    foreach (string slot in material.GetTexturePropertyNames())
                    {
                        var texture = material.GetTexture(slot);
                        if (texture == null) continue;
                        grassMatch |= IsGrassCandidate(texture.name);
                        roadMatch |= IsRoadCandidate(texture.name);
                    }
                    if (grassMatch) grass.Add(material);
                    else if (roadMatch) roads.Add(material);
                }
                catch (Exception ex)
                {
                    scanErrors++;
                    report.AppendLine("Scan failed for " + material.name + ": " + ex.Message);
                }
            }

            var exported = new Dictionary<Texture, string>();
            var attempted = new HashSet<Texture>();
            var signatures = new HashSet<string>(StringComparer.Ordinal);
            int count = 0, duplicates = 0, omitted = 0;
            grass.AddRange(roads);
            foreach (var material in grass)
            {
                if (material == null || material.shader == null) continue;
                try
                {
                    string[] slots = material.GetTexturePropertyNames();
                    var entry = new StringBuilder();
                    entry.AppendLine("Material: " + material.name);
                    entry.AppendLine("Shader: " + material.shader.name);
                    entry.AppendLine("Keywords: " + string.Join(", ", material.shaderKeywords));
                    entry.AppendLine("Render queue: " + material.renderQueue);
                    for (int i = 0; i < material.shader.GetPropertyCount(); i++)
                    {
                        string property = material.shader.GetPropertyName(i);
                        string kind = material.shader.GetPropertyType(i).ToString();
                        if (kind == "Float" || kind == "Range")
                            entry.AppendLine(property + " = " + GrassNumber(material.GetFloat(property)));
                        else if (kind == "Color")
                        {
                            Color c = material.GetColor(property);
                            entry.AppendLine(property + " = RGBA(" + GrassNumber(c.r) + ", " + GrassNumber(c.g) +
                                ", " + GrassNumber(c.b) + ", " + GrassNumber(c.a) + ")");
                        }
                        else if (kind == "Vector")
                        {
                            Vector4 v = material.GetVector(property);
                            entry.AppendLine(property + " = (" + GrassNumber(v.x) + ", " + GrassNumber(v.y) +
                                ", " + GrassNumber(v.z) + ", " + GrassNumber(v.w) + ")");
                        }
                    }
                    foreach (string slot in slots)
                    {
                        Texture texture = material.GetTexture(slot);
                        if (texture == null) { entry.AppendLine(slot + " = <empty>"); continue; }
                        Vector2 scale = material.GetTextureScale(slot), offset = material.GetTextureOffset(slot);
                        entry.AppendLine(slot + " = " + texture.name + " (" + texture.width + "x" + texture.height +
                            ", " + texture.GetType().Name + ", id=" + texture.GetInstanceID() + ") scale=(" +
                            GrassNumber(scale.x) + ", " + GrassNumber(scale.y) + ") offset=(" +
                            GrassNumber(offset.x) + ", " + GrassNumber(offset.y) + ")");
                    }
                    if (!signatures.Add(entry.ToString())) { duplicates++; continue; }
                    // Bound the report as well as GPU readbacks in large, heavily modded cities.
                    if (count >= 300) { omitted++; continue; }
                    report.AppendLine("\n--- Candidate " + (++count) + " ---");
                    report.Append(entry);
                    foreach (string slot in slots)
                    {
                        Texture texture = material.GetTexture(slot);
                        if (texture == null) continue;
                        if (exported.TryGetValue(texture, out string filename))
                        {
                            report.AppendLine("Preview " + slot + ": " + filename);
                            continue;
                        }
                        // Cubemaps, arrays, volume textures and virtual texture bindings are report-only.
                        if (!(texture is Texture2D) || attempted.Count >= 24 || texture.width <= 0 || texture.height <= 0)
                            continue;
                        if (!attempted.Add(texture)) continue;
                        bool linear = Contains(slot, "normal") || Contains(slot, "mask") || Contains(slot, "height") ||
                            Contains(texture.name, "normal") || Contains(texture.name, "mask") || Contains(texture.name, "height");
                        filename = "Texture_" + attempted.Count.ToString("D2") + (linear ? "_Linear.png" : "_Color.png");
                        try
                        {
                            SaveGrassPreview(texture, Path.Combine(GrassDiagnosticFolder, filename), linear);
                            exported.Add(texture, filename);
                            report.AppendLine("Preview " + slot + ": " + filename);
                        }
                        catch (Exception ex) { report.AppendLine("Preview failed for " + slot + ": " + ex.Message); }
                    }
                }
                catch (Exception ex) { report.AppendLine("Material inspection failed for " + material.name + ": " + ex.Message); }
            }
            report.AppendLine("\nCandidates recorded: " + count + "; duplicates skipped: " + duplicates +
                "; omitted by limit: " + omitted + "; scan errors: " + scanErrors + "; previews: " + exported.Count);
            File.WriteAllText(Path.Combine(GrassDiagnosticFolder, "GrassMaterials.txt"), report.ToString(), Encoding.UTF8);
            Mod.log.Info("Roadside grass survey saved: " + count + " materials, " + exported.Count +
                " previews in " + GrassDiagnosticFolder);
        }

        private static void SaveGrassPreview(Texture source, string path, bool linear)
        {
            float scale = Mathf.Min(1f, 2048f / Mathf.Max(source.width, source.height));
            int width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32,
                linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Texture2D copy = null;
            try
            {
                UnityEngine.Graphics.Blit(source, target);
                RenderTexture.active = target;
                copy = new Texture2D(width, height, TextureFormat.RGBA32, false, linear);
                copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                copy.Apply(false, false);
                byte[] bytes = copy.EncodeToPNG();
                if (bytes == null || bytes.Length == 0) throw new InvalidOperationException("PNG encoding returned no data.");
                File.WriteAllBytes(path, bytes);
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
