using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game;
using UnityEngine;

namespace Advanced_Graphics
{
    /// <summary>
    /// One-shot, read-only survey of road material bindings. Road shaders and
    /// virtual textures differ between game versions, so texture replacement
    /// must be based on the materials observed in a loaded city.
    /// </summary>
    public partial class RoadMaterialInspector : GameSystemBase
    {
        private bool m_Requested;
        private bool m_ExportRequested;
        private bool m_GravelRequested;
        private bool m_GrassRequested;

        public void RequestGrassSurvey()
        {
            m_GrassRequested = true;
            Mod.log.Info("Roadside grass survey requested; load a city with grass sidewalks.");
        }

        public void RequestGravelSurvey()
        {
            m_GravelRequested = true;
            Mod.log.Info("Gravel road material export requested; load a city with gravel roads and sidewalks.");
        }

        public void RequestSidewalkAtlasExport()
        {
            m_ExportRequested = true;
            Mod.log.Info("Sidewalk atlas export requested; load a city before exporting.");
        }

        public void RequestSurvey()
        {
            m_Requested = true;
            Mod.log.Info("Road material inspection requested; open a city before reading the result.");
        }

        protected override void OnUpdate()
        {
            if (!m_Requested && !m_ExportRequested && !m_GravelRequested && !m_GrassRequested) return;
            if (m_GrassRequested)
            {
                m_GrassRequested = false;
                try { ExportGrassMaterials(); }
                catch (Exception ex) { Mod.log.Error(ex, "Roadside grass material export failed."); }
            }
            if (!Mod.DebugBackendEnabled) return;
            if (m_GravelRequested)
            {
                m_GravelRequested = false;
                try { ExportGravelMaterials(); }
                catch (Exception ex) { Mod.log.Error(ex, "Gravel road material export failed."); }
            }
            if (m_ExportRequested)
            {
                m_ExportRequested = false;
                try { ExportSidewalkAtlas(); }
                catch (Exception ex) { Mod.log.Error(ex, "Sidewalk atlas export failed."); }
            }
            if (!m_Requested) return;
            m_Requested = false;
            try { Survey(); }
            catch (Exception ex) { Mod.log.Error(ex, "Road material inspection failed."); }
        }

        private static void ExportSidewalkAtlas()
        {
            // A street sidewalk may be part of the RoadEU atlas rather than a
            // separate PathwayPavement world-space map. Exporting the source
            // images lets us identify the exact region before changing it.
            string folder = Path.Combine(SidewalkSurfaceSystem.SidewalkTextureFolder, "Diagnostics");
            Directory.CreateDirectory(folder);
            var names = new[] { "RoadEU_BaseColor_", "RoadEU_MaskMap_" };
            var slots = new[] { "_BaseColorMap", "_MaskMap" };
            var exported = new bool[names.Length];
            foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (material == null || material.shader == null ||
                    !Contains(material.shader.name, "NetComposition")) continue;
                for (int i = 0; i < names.Length; i++)
                {
                    if (exported[i] || !material.HasProperty(slots[i])) continue;
                    Texture source = material.GetTexture(slots[i]);
                    if (source == null || !source.name.StartsWith(names[i], StringComparison.Ordinal) ||
                        source.width > 4096 || source.height > 4096) continue;
                    string path = Path.Combine(folder, i == 0 ? "RoadEU_BaseColor_Atlas.png" : "RoadEU_MaskMap_Atlas.png");
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
                        exported[i] = true;
                        Mod.log.Info("Sidewalk atlas diagnostic saved: " + path);
                    }
                    finally
                    {
                        RenderTexture.active = previous;
                        RenderTexture.ReleaseTemporary(target);
                        if (copy != null) UnityEngine.Object.Destroy(copy);
                    }
                }
                if (exported[0] && exported[1]) break;
            }
            if (!exported[0]) Mod.log.Info("RoadEU color atlas was not found in the loaded city.");
            if (!exported[1]) Mod.log.Info("RoadEU mask atlas was not found in the loaded city.");
            Mod.SidewalkAtlas?.ExportAppliedAtlases(folder);
        }

        private static void Survey()
        {
            // This allocates and walks all loaded Unity materials. It runs only
            // on a button click, never continuously in the render loop.
            var materials = Resources.FindObjectsOfTypeAll<Material>();
            var namedRoads = new List<string>();
            var networkMaterials = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var matching = 0;
            var withTexture = 0;
            string roadShaderProperties = null;
            foreach (var material in materials)
            {
                if (material == null || material.shader == null) continue;
                var shader = material.shader.name ?? "";
                var name = material.name ?? "";
                if (!Contains(shader, "NetComposition") && !Contains(name, "asphalt") &&
                    !Contains(name, "road") && !Contains(name, "carriageway") &&
                    !Contains(name, "sidewalk") && !Contains(name, "pavement") &&
                    !Contains(name, "footpath") && !Contains(name, "pedestrian")) continue;

                matching++;
                string[] properties;
                try { properties = material.GetTexturePropertyNames(); }
                catch (Exception) { continue; }
                var detail = new StringBuilder();
                foreach (var property in properties)
                {
                    Texture texture;
                    try { texture = material.GetTexture(property); }
                    catch (Exception) { continue; }
                    if (texture != null)
                    {
                        withTexture++;
                        detail.Append(property).Append('=')
                            .Append(texture.name).Append('(')
                            .Append(texture.width).Append('x').Append(texture.height).Append(") ");
                    }
                    else detail.Append(property).Append("=<empty> ");
                }

                // Include the shader's declared texture slots even when no
                // ordinary Texture object is bound (virtual texturing).
                var signature = shader + " | " + name + " | " + detail;
                if (roadShaderProperties == null &&
                    (detail.ToString().IndexOf("RoadEUWorldspace_BaseColor_", StringComparison.Ordinal) >= 0 ||
                     detail.ToString().IndexOf("Advanced Graphics Road Asphalt Color", StringComparison.Ordinal) >= 0))
                {
                    var names = new List<string>();
                    for (int i = 0; i < material.shader.GetPropertyCount(); i++)
                        names.Add(material.shader.GetPropertyName(i));
                    roadShaderProperties = string.Join(", ", names);
                    Mod.log.Info("RoadEU worldspace UV scale: " +
                        material.GetTextureScale("_WorldspaceAlbedo") +
                        "; texture offset: " + material.GetTextureOffset("_WorldspaceAlbedo"));
                }
                if (!seen.Add(signature)) continue;
                if (Contains(name, "asphalt") || Contains(name, "road") || Contains(name, "carriageway") ||
                    Contains(name, "sidewalk") || Contains(name, "pavement") ||
                    Contains(name, "footpath") || Contains(name, "pedestrian"))
                    namedRoads.Add(signature);
                else networkMaterials.Add(signature);
            }

            var lines = new List<string>();
            foreach (var line in namedRoads)
            {
                if (lines.Count == 60) break;
                lines.Add(line);
            }
            foreach (var line in networkMaterials)
            {
                if (lines.Count == 60) break;
                lines.Add(line);
            }
            Mod.log.Info($"Road material inspection: {materials.Length} loaded materials, " +
                $"{matching} road candidates, {withTexture} assigned texture slots; " +
                $"showing {lines.Count} distinct examples.");
            foreach (var line in lines) Mod.log.Info("Road material: " + line);
            if (roadShaderProperties != null)
                Mod.log.Info("RoadEU shader properties: " + roadShaderProperties);
            if (matching == 0)
                Mod.log.Info("Road material inspection found no candidates. Load a city, then press the button again.");
        }

        private static bool Contains(string text, string fragment) =>
            text.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;

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

        private static void ExportGravelMaterials()
        {
            string folder = Path.Combine(TextureStorage.RoadGravel, "Diagnostics");
            Directory.CreateDirectory(folder);
            var report = new StringBuilder("Advanced Graphics gravel road materials\n");
            report.AppendLine("Read-only survey. Green: Gravel road surface; yellow: GravelShoulder edge (color probe).");
            int gravel = 0, shoulder = 0, images = 0;
            foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (material == null || material.shader == null) continue;
                bool isShoulder = material.name.StartsWith("Batch (GravelShoulder", StringComparison.Ordinal);
                bool isGravel = material.name.StartsWith("Batch (Gravel_", StringComparison.Ordinal);
                if ((!isShoulder && !isGravel) || (isShoulder ? shoulder >= 4 : gravel >= 4)) continue;
                string label = isShoulder ? "Shoulder" + (++shoulder) : "Gravel" + (++gravel);
                report.AppendLine("\n" + label + ": " + material.name + " | shader: " + material.shader.name);
                report.AppendLine("  Keywords: " + string.Join(", ", material.shaderKeywords));
                for (int i = 0; i < material.shader.GetPropertyCount(); i++)
                {
                    string property = material.shader.GetPropertyName(i);
                    string kind = material.shader.GetPropertyType(i).ToString();
                    if (kind == "Float" || kind == "Range")
                        report.AppendLine("  " + property + " = " + material.GetFloat(property));
                    else if (kind == "Color")
                        report.AppendLine("  " + property + " = " + material.GetColor(property));
                }
                foreach (var slot in material.GetTexturePropertyNames())
                {
                    Texture texture = material.GetTexture(slot);
                    if (texture == null) { report.AppendLine("  " + slot + " = <empty>"); continue; }
                    report.AppendLine("  " + slot + " = " + texture.name + " (" + texture.width + "x" +
                        texture.height + ") scale=" + material.GetTextureScale(slot) +
                        " offset=" + material.GetTextureOffset(slot));
                    if (images >= 16 || texture.width > 4096 || texture.height > 4096 ||
                        !(Contains(slot, "Color") || Contains(slot, "Albedo") || Contains(slot, "Normal") || Contains(slot, "Mask"))) continue;
                    string path = Path.Combine(folder, label + "_" + slot.TrimStart('_') + ".png");
                    try { SaveTexture(texture, path); images++; report.AppendLine("    Exported: " + Path.GetFileName(path)); }
                    catch (Exception ex) { report.AppendLine("    Export failed: " + ex.Message); }
                }
            }
            report.AppendLine("\nGravel materials: " + gravel + "; shoulders: " + shoulder + "; textures: " + images);
            File.WriteAllText(Path.Combine(folder, "GravelRoadMaterials.txt"), report.ToString());
            Mod.log.Info("Gravel road survey: " + gravel + " surfaces, " + shoulder + " shoulders, " + images + " textures in " + folder);
        }
    }
}

