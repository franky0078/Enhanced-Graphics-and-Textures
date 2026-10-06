using System;
using System.Globalization;
using System.Text;
using Colossal.IO.AssetDatabase;
using Colossal.UI.Binding;
using Game;
using Game.UI;

namespace Advanced_Graphics
{
    public partial class GraphicsPanelUISystem : UISystemBase
    {
        private const string Group = "Advanced_Graphics";
        private string m_State = "{}";
        private bool m_SavePending;
        private float m_SaveAt;

        public override GameMode gameMode => GameMode.GameOrEditor;

        protected override void OnCreate()
        {
            base.OnCreate();
            AddUpdateBinding(new GetterValueBinding<string>(Group, "graphicsSettings", () => m_State));
            AddUpdateBinding(new GetterValueBinding<bool>(Group, "luminaNumberFix",
                () => Mod.ActiveSetting?.LuminaNumberFix ?? false));
            AddBinding(new TriggerBinding<string>(Group, "setGraphicsSetting", ChangeSetting));
            AddBinding(new TriggerBinding<string>(Group, "setGraphicsWindowPosition", ChangePosition));
        }

        protected override void OnUpdate()
        {
            m_State = BuildState(Mod.ActiveSetting);
            base.OnUpdate();
            if (m_SavePending && UnityEngine.Time.unscaledTime >= m_SaveAt) SavePending();
        }

        protected override void OnDestroy()
        {
            if (m_SavePending) SavePending();
            base.OnDestroy();
        }

        private void SavePending()
        {
            m_SavePending = false;
            try { _ = AssetDatabase.global.SaveSettings(); }
            catch (Exception ex) { Mod.log.Error(ex, "Graphics panel settings could not be saved."); }
        }

        private void QueueSave()
        {
            m_SavePending = true;
            m_SaveAt = UnityEngine.Time.unscaledTime + 0.6f;
        }

        private void ChangePosition(string request)
        {
            var s = Mod.ActiveSetting;
            if (s == null || string.IsNullOrEmpty(request)) return;
            var parts = request.Split(':');
            if (parts.Length != 2 || !TryNumber(parts[0], out float x) || !TryNumber(parts[1], out float y) ||
                x < 0 || y < 0 || x > 100000 || y > 100000) return;
            s.GraphicsWindowX = x;
            s.GraphicsWindowY = y;
            QueueSave();
        }

        private void ChangeSetting(string request)
        {
            var s = Mod.ActiveSetting;
            if (s == null || string.IsNullOrEmpty(request)) return;
            int separator = request.IndexOf(':');
            if (separator <= 0) return;
            string key = request.Substring(0, separator);
            string input = request.Substring(separator + 1);
            if (key == "reset")
            {
                if (input == "all")
                {
                    foreach (var name in ResetKeys) ResetValue(s, name);
                }
                else if (!ResetValue(s, input)) return;
            }
            else
            {
                if (!TryNumber(input, out float value)) return;
                switch (key)
                {
                    case "TreeLodDistance": s.TreeLodDistance = ClampInt(value, 10, 500); break;
                    case "GlobalDetailScalePercent": s.GlobalDetailScalePercent = ClampInt(value, 10, 500); break;
                    case "TerrainTessellationLevel": s.TerrainTessellationLevel = ClampInt(value, 1, 16); break;
                    case "TerrainPatchSizeUnits": s.TerrainPatchSizeUnits = ClampInt(value, 4, 128); break;
                    case "ShadowResolution": s.ShadowResolution = ClampInt(value, 0, 8192); break;
                    case "ShadowDistanceMeters": s.ShadowDistanceMeters = ClampInt(value, 0, 2000); break;
                    case "SmallShadowCasterThreshold": s.SmallShadowCasterThreshold = ClampInt(value, 0, 10); break;
                    case "TaaSharpenStrength": s.TaaSharpenStrength = ClampFloat(value, 0, 2); break;
                    case "TaaStabilityEnabled": s.TaaStabilityEnabled = value != 0; break;
                    case "TaaStabilityStrength": s.TaaStabilityStrength = ClampFloat(value, 0, 1); break;
                    case "BloomMode": s.BloomMode = (Setting.BloomControl)ClampInt(value, 0, 2); break;
                    case "BloomIntensity": s.BloomIntensity = ClampFloat(value, 0, 1); break;
                    case "SunBloomStrength": s.SunBloomStrength = ClampFloat(value, 0, 5); break;
                    case "SkyExposure": s.SkyExposure = ClampFloat(value, -5, 5); break;
                    default: return;
                }
            }
            // Graphics edits do not reconfigure or regenerate any road textures.
            if (Mod.TreeLod != null) Mod.TreeLod.DistancePercent = s.TreeLodDistance;
            Mod.Graphics?.Apply(s);
            m_State = BuildState(s);
            QueueSave();
        }

        private static readonly string[] ResetKeys = {
            "TreeLodDistance", "GlobalDetailScalePercent", "TerrainTessellationLevel", "TerrainPatchSizeUnits",
            "ShadowResolution", "ShadowDistanceMeters", "SmallShadowCasterThreshold", "TaaSharpenStrength", "TaaStabilityEnabled", "TaaStabilityStrength",
            "BloomMode", "SunBloomStrength", "SkyExposure"
        };

        private static bool ResetValue(Setting s, string key)
        {
            switch (key)
            {
                case "TreeLodDistance": s.TreeLodDistance = 100; break;
                case "GlobalDetailScalePercent": s.GlobalDetailScalePercent = 100; break;
                case "TerrainTessellationLevel": s.TerrainTessellationLevel = Setting.DefaultTerrainTessellation; break;
                case "TerrainPatchSizeUnits": s.TerrainPatchSizeUnits = Setting.DefaultTerrainPatchSize; break;
                case "ShadowResolution": s.ShadowResolution = 0; break;
                case "ShadowDistanceMeters": s.ShadowDistanceMeters = 0; break;
                case "SmallShadowCasterThreshold": s.SmallShadowCasterThreshold = 0; break;
                case "TaaSharpenStrength": s.TaaSharpenStrength = 0; break;
                case "TaaStabilityEnabled": s.TaaStabilityEnabled = false; break;
                case "TaaStabilityStrength": s.TaaStabilityStrength = 0.5f; break;
                case "BloomMode":
                case "BloomIntensity": s.BloomMode = Setting.BloomControl.GameDefault; s.BloomIntensity = 0.2f; break;
                case "SunBloomStrength": s.SunBloomStrength = 1f; break;
                case "SkyExposure": s.SkyExposure = 0; break;
                default: return false;
            }
            return true;
        }

        private static bool TryNumber(string input, out float value) =>
            float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static int ClampInt(float value, int min, int max) =>
            (int)Math.Round(Math.Max(min, Math.Min(max, value)));

        private static float ClampFloat(float value, float min, float max) =>
            (float)Math.Round(Math.Max(min, Math.Min(max, value)), 2);

        private static string BuildState(Setting s)
        {
            if (s == null) return "{}";
            var json = new StringBuilder("{\"show\":").Append(s.ShowGraphicsAppButton ? "true" : "false");
            Add(json, "x", s.GraphicsWindowX);
            Add(json, "y", s.GraphicsWindowY);
            Add(json, "TreeLodDistance", s.TreeLodDistance);
            Add(json, "GlobalDetailScalePercent", s.GlobalDetailScalePercent);
            Add(json, "TerrainTessellationLevel", s.TerrainTessellationLevel);
            Add(json, "TerrainPatchSizeUnits", s.TerrainPatchSizeUnits);
            Add(json, "ShadowResolution", s.ShadowResolution);
            Add(json, "ShadowDistanceMeters", s.ShadowDistanceMeters);
            Add(json, "SmallShadowCasterThreshold", s.SmallShadowCasterThreshold);
            Add(json, "TaaSharpenStrength", s.TaaSharpenStrength);
            Add(json, "TaaStabilityEnabled", s.TaaStabilityEnabled ? 1 : 0);
            Add(json, "TaaStabilityStrength", s.TaaStabilityStrength);
            Add(json, "DefaultShadowResolution", Mod.Graphics?.DefaultShadowResolution ?? 0);
            Add(json, "BloomMode", (int)s.BloomMode);
            Add(json, "BloomIntensity", s.BloomIntensity);
            Add(json, "SunBloomStrength", s.SunBloomStrength);
            Add(json, "SkyExposure", s.SkyExposure);
            return json.Append('}').ToString();
        }

        private static void Add(StringBuilder json, string key, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) value = 0;
            json.Append(",\"").Append(key).Append("\":").Append(value.ToString("R", CultureInfo.InvariantCulture));
        }
    }
}
