using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Colossal.IO.AssetDatabase;
using Colossal.UI.Binding;
using Game;
using Game.UI;
using UnityEngine;

namespace Advanced_Graphics
{
    // In-game window with every debug tool, so testing needs no trip to the main menu.
    // Shown only while "Show debug window" is on in the options; with it off all tools are inactive.
    public partial class DebugPanelUISystem : UISystemBase
    {
        private const string Group = "Advanced_Graphics";
        private string m_State = "{}";
        private bool m_SavePending;
        private float m_SaveAt;

        public override GameMode gameMode => GameMode.GameOrEditor;

        internal static string DiagnosticsFolder =>
            Path.Combine(Application.persistentDataPath, "ModsData", "Enhanced_Graphics_and_Textures", "Diagnostics");

        protected override void OnCreate()
        {
            base.OnCreate();
            AddUpdateBinding(new GetterValueBinding<string>(Group, "debugState", () => m_State));
            AddBinding(new TriggerBinding<string>(Group, "debugAction", Action));
            AddBinding(new TriggerBinding<string>(Group, "setDebugWindowPosition", ChangePosition));
        }

        protected override void OnUpdate()
        {
            m_State = BuildState(Mod.ActiveSetting);
            Mod.FlushDeferredLog();
            base.OnUpdate();
            if (m_SavePending && UnityEngine.Time.unscaledTime >= m_SaveAt)
            {
                m_SavePending = false;
                try { _ = AssetDatabase.global.SaveSettings(); }
                catch (Exception ex) { Mod.log.Error(ex, "Debug window settings could not be saved."); }
            }
        }

        private void QueueSave()
        {
            m_SavePending = true;
            m_SaveAt = UnityEngine.Time.unscaledTime + 0.6f;
        }

        private void Action(string request)
        {
            var s = Mod.ActiveSetting;
            if (s == null || !Mod.DebugBackendEnabled || string.IsNullOrEmpty(request)) return;
            int separator = request.IndexOf(':');
            string key = separator < 0 ? request : request.Substring(0, separator);
            string input = separator < 0 ? "" : request.Substring(separator + 1);
            try
            {
                switch (key)
                {
                    case "atlasDiagnostic":
                        s.DiagnoseRoadEUAtlas = input == "1";
                        s.ApplyToSystem();
                        QueueSave();
                        break;
                    case "junctionMode":
                        if (!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int mode)) return;
                        s.JunctionSyncBlendMode = Math.Max(0, Math.Min(6, mode));
                        QueueSave();
                        break;
                    case "recomputeTangents":
                        s.JunctionRecomputeTangents = input == "1";
                        QueueSave();
                        break;
                    case "inspectRoad": s.InspectRoadMaterials = true; break;
                    case "exportGravel": s.ExportGravelRoadMaterials = true; break;
                    case "exportGrass": s.ExportGrassMaterials = true; break;
                    case "exportSidewalkAtlas": s.ExportSidewalkAtlas = true; break;
                    case "exportRail": Mod.RailInspector?.RequestExport(); break;
                    case "railProbe": Mod.RailInspector?.ToggleColorProbe(); break;
                    case "openGrassFolder": s.OpenGrassDiagnosticFolder = true; break;
                    case "openRailFolder": s.OpenRailDiagnosticFolder = true; break;
                    case "openDiagnostics": OpenFolder(DiagnosticsFolder); break;
                    case "sidewalkScaleReport": Mod.SidewalkJunctionMeshes?.WriteSidewalkScaleReport(); break;
                    case "resetModes":
                        s.ResetDebugModes();
                        s.ApplyToSystem();
                        QueueSave();
                        break;
                }
            }
            catch (Exception ex) { Mod.log.Error(ex, "Debug action failed: " + key); }
        }

        private static void OpenFolder(string folder)
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }

        private void ChangePosition(string request)
        {
            var s = Mod.ActiveSetting;
            if (s == null || string.IsNullOrEmpty(request)) return;
            var parts = request.Split(':');
            if (parts.Length != 2 ||
                !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                x < 0 || y < 0 || x > 100000 || y > 100000) return;
            s.DebugWindowX = x;
            s.DebugWindowY = y;
            QueueSave();
        }

        private static string BuildState(Setting s)
        {
            if (s == null) return "{}";
            var json = new StringBuilder("{\"show\":").Append(Mod.DebugBackendEnabled && s.ShowDebugWindow ? "true" : "false");
            json.Append(",\"atlasDiagnostic\":").Append(s.DiagnoseRoadEUAtlas ? "true" : "false");
            json.Append(",\"railProbe\":").Append(Mod.RailInspector?.IsProbeActive == true ? "true" : "false");
            json.Append(",\"recomputeTangents\":").Append(s.JunctionRecomputeTangents ? "true" : "false");
            json.Append(",\"junctionMode\":").Append(s.JunctionSyncBlendMode.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"x\":").Append(s.DebugWindowX.ToString("R", CultureInfo.InvariantCulture));
            json.Append(",\"y\":").Append(s.DebugWindowY.ToString("R", CultureInfo.InvariantCulture));
            return json.Append('}').ToString();
        }
    }
}


