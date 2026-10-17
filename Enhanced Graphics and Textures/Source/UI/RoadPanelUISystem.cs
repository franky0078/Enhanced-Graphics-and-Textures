using System;
using System.Globalization;
using Colossal.IO.AssetDatabase;
using Colossal.UI.Binding;
using Game;
using Game.UI;
using UnityEngine;

namespace Advanced_Graphics
{
    // Keeps the floating road editor and the normal options page on the same settings.
    public partial class RoadPanelUISystem : UISystemBase
    {
        private const string Group = "Advanced_Graphics";
        private string m_State = "{}";
        private string m_SidewalkState = "{}";
        private string m_AlleySidewalkState = "{}";
        private string m_RailState = "{}";
        private string m_GravelState = "{}", m_ShoulderState = "{}";
        private string m_GrassState = "{}";
        private bool m_SavePending;
        private float m_SaveAt;

        public override GameMode gameMode => GameMode.GameOrEditor;

        protected override void OnCreate()
        {
            base.OnCreate();
            AddBinding(new TriggerBinding<string>(Group, "setSurfaceFilterColor", ChangeFilterColor));
            AddUpdateBinding(new GetterValueBinding<string>(Group, "colorWindowPosition", () =>
                "{\"x\":" + (Mod.ActiveSetting?.ColorWindowX ?? -1f).ToString("R", CultureInfo.InvariantCulture) +
                ",\"y\":" + (Mod.ActiveSetting?.ColorWindowY ?? -1f).ToString("R", CultureInfo.InvariantCulture) + "}"));
            AddBinding(new TriggerBinding<string>(Group, "setColorWindowPosition", ChangeColorWindowPosition));
            AddUpdateBinding(new GetterValueBinding<string>(Group, "textureGeneralWindowPosition", () =>
                "{\"x\":" + (Mod.ActiveSetting?.TextureGeneralWindowX ?? -1f).ToString("R", CultureInfo.InvariantCulture) +
                ",\"y\":" + (Mod.ActiveSetting?.TextureGeneralWindowY ?? -1f).ToString("R", CultureInfo.InvariantCulture) + "}"));
            AddBinding(new TriggerBinding<string>(Group, "setTextureGeneralWindowPosition", ChangeTextureGeneralWindowPosition));
            AddUpdateBinding(new GetterValueBinding<bool>(Group, "debugEnabled", () => Mod.DebugBackendEnabled));
            AddBinding(new TriggerBinding<string>(Group, "reloadGravelShoulderTexture", _ => Mod.GravelShoulderSurface?.ReloadCustomTexture()));
            AddUpdateBinding(new GetterValueBinding<string>(Group, "roadSettings", () => m_State));
            AddBinding(new TriggerBinding<string>(Group, "setRoadSetting", ChangeSetting));
            AddUpdateBinding(new GetterValueBinding<string>(Group, "sidewalkSettings", () => m_SidewalkState));
            AddBinding(new TriggerBinding<string>(Group, "setSidewalkSetting", ChangeSidewalkSetting));
            AddUpdateBinding(new GetterValueBinding<string>(Group, "alleySidewalkSettings", () => m_AlleySidewalkState));
            AddBinding(new TriggerBinding<string>(Group, "setAlleySidewalkSetting", ChangeAlleySidewalkSetting));
            AddUpdateBinding(new GetterValueBinding<string>(Group, "railSettings", () => m_RailState));
            if (Mod.DebugBackendEnabled)
            {
                AddBinding(new TriggerBinding<string>(Group, "inspectRailMaterials", _ => Mod.RailInspector?.RequestExport()));
                AddBinding(new TriggerBinding<string>(Group, "probeRailMaterials", _ => Mod.RailInspector?.ToggleColorProbe()));
            }
            AddBinding(new TriggerBinding<string>(Group, "setRailSetting", ChangeRailSetting));
            AddUpdateBinding(new GetterValueBinding<string>(Group, "gravelSettings", () => m_GravelState));
            AddUpdateBinding(new GetterValueBinding<string>(Group, "shoulderSettings", () => m_ShoulderState));
            AddBinding(new TriggerBinding<string>(Group, "setGravelSetting", request => ChangeGravelSetting(request, false)));
            AddBinding(new TriggerBinding<string>(Group, "setShoulderSetting", request => ChangeGravelSetting(request, true)));
            AddUpdateBinding(new GetterValueBinding<string>(Group, "grassSettings", () => m_GrassState));
            AddBinding(new TriggerBinding<string>(Group, "setGrassSetting", ChangeGrassSetting));
            AddBinding(new TriggerBinding<string>(Group, "reloadGrassTexture", _ => Mod.Grass?.ReloadCustomTexture()));
            AddBinding(new TriggerBinding<string>(Group, "reloadGravelTexture", _ => Mod.RoadGravel?.ReloadCustomTexture()));
        }

        protected override void OnUpdate()
        {
            m_State = BuildState(Mod.ActiveSetting);
            m_SidewalkState = BuildSidewalkState(Mod.ActiveSetting);
            m_AlleySidewalkState = BuildAlleySidewalkState(Mod.ActiveSetting);
            m_RailState = BuildRailState(Mod.ActiveSetting);
            m_GrassState = BuildGrassState(Mod.ActiveSetting);
            m_GravelState = BuildGravelState(Mod.ActiveSetting, false);
            m_ShoulderState = BuildGravelState(Mod.ActiveSetting, true);
            base.OnUpdate();
            if (m_SavePending && UnityEngine.Time.unscaledTime >= m_SaveAt)
            {
                m_SavePending = false;
                try { _ = AssetDatabase.global.SaveSettings(); }
                catch (Exception ex) { Mod.log.Error(ex, "Road panel settings could not be saved."); }
            }
        }

        protected override void OnDestroy()
        {
            if (m_SavePending)
            {
                try { _ = AssetDatabase.global.SaveSettings(); }
                catch (Exception ex) { Mod.log.Error(ex, "Road panel settings could not be saved."); }
            }
            base.OnDestroy();
        }

        private void ChangeFilterColor(string request)
        {
            var s = Mod.ActiveSetting;
            if (s == null || string.IsNullOrEmpty(request)) return;
            var parts = request.Split(':');
            int opacity = 100;
            if ((parts.Length != 2 && parts.Length != 3) || !int.TryParse(parts[1], out int color) ||
                (parts.Length == 3 && !int.TryParse(parts[2], out opacity)) || opacity < 0 || opacity > 100 ||
                color < 0 || color > 0xffffff || !s.SetFilterColor(parts[0], color, opacity)) return;
            s.ApplyToSystem();
            m_SavePending = true;
            m_SaveAt = UnityEngine.Time.unscaledTime + 0.6f;
        }

        private void ChangeColorWindowPosition(string request)
        {
            var s = Mod.ActiveSetting;
            if (s == null || string.IsNullOrEmpty(request)) return;
            var parts = request.Split(':');
            if (parts.Length != 2 ||
                !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y) ||
                x < 0 || y < 0 || x > 100000 || y > 100000) return;
            s.ColorWindowX = x;
            s.ColorWindowY = y;
            m_SavePending = true;
            m_SaveAt = UnityEngine.Time.unscaledTime + 0.6f;
        }

        private void ChangeTextureGeneralWindowPosition(string request)
        {
            var s = Mod.ActiveSetting;
            if (s == null || string.IsNullOrEmpty(request)) return;
            var parts = request.Split(':');
            if (parts.Length != 2 ||
                !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y) ||
                x < 0 || y < 0 || x > 100000 || y > 100000) return;
            s.TextureGeneralWindowX = x;
            s.TextureGeneralWindowY = y;
            m_SavePending = true;
            m_SaveAt = UnityEngine.Time.unscaledTime + 0.6f;
        }

        private static string BuildState(Setting s)
        {
            if (s == null) return "{}";
            return "{\"filterColor\":" + s.RoadFilterColor + ",\"filterOpacity\":" + s.RoadFilterOpacityPercent + ",\"show\":" + (s.ShowAppButton ? "true" : "false") +
                ",\"enabled\":" + (s.RoadAsphaltEnabled ? "true" : "false") +
                ",\"detail\":" + s.RoadAsphaltDetail +
                ",\"uv\":" + s.RoadUvSizePercent +
                ",\"customCracks\":" + (s.UseCustomRoadCracks ? "true" : "false") +
                ",\"crackStatus\":" + (Mod.RoadAsphalt?.CustomCrackStatus ?? 0) +
                ",\"mipBias\":" + s.SurfaceTextureMipBias.ToString("0.0", CultureInfo.InvariantCulture) +
                ",\"parkingDepth\":" + s.ParkingDepthStrengthFactor.ToString("0.0", CultureInfo.InvariantCulture) +
                ",\"parkingScale\":" + s.ParkingTextureScaleFactor.ToString("0.0", CultureInfo.InvariantCulture) +
                ",\"depth\":" + s.RoadDepthStrengthPercent +
                ",\"tiling\":" + s.RoadTextureTiling.ToString("0.0", CultureInfo.InvariantCulture) +
                ",\"offsetX\":" + s.RoadUvOffsetX +
                ",\"offsetY\":" + s.RoadUvOffsetY +
                ",\"rotation\":" + s.RoadUvRotation +
                ",\"cracks\":" + s.RoadCrackCount +
                ",\"crackSize\":" + s.RoadCrackSizePercent +
                ",\"crackWidth\":" + s.RoadCrackWidthPercent +
                ",\"crackOpacity\":" + s.RoadCrackOpacityPercent +
                ",\"randomness\":" + s.RoadCrackRandomnessPercent + "}";
        }

        private static string BuildSidewalkState(Setting s)
        {
            if (s == null) return "{}";
            return "{\"filterColor\":" + s.SidewalkFilterColor + ",\"filterOpacity\":" + s.SidewalkFilterOpacityPercent + ",\"show\":" + (s.ShowAppButton ? "true" : "false") +
                ",\"enabled\":" + (s.SidewalkEnabled ? "true" : "false") +
                ",\"detail\":" + s.SidewalkDetail +
                ",\"uv\":" + s.SidewalkUvSizePercent +
                ",\"depth\":" + s.SidewalkDepthStrengthPercent +
                ",\"tiling\":" + s.SidewalkTextureTiling.ToString("0.0", CultureInfo.InvariantCulture) +
                ",\"offsetX\":" + s.SidewalkUvOffsetX +
                ",\"offsetY\":" + s.SidewalkUvOffsetY +
                ",\"rotation\":" + s.SidewalkUvRotation +
                ",\"cracks\":" + s.SidewalkCrackCount +
                ",\"crackSize\":" + s.SidewalkCrackSizePercent +
                ",\"crackWidth\":" + s.SidewalkCrackWidthPercent +
                ",\"crackOpacity\":" + s.SidewalkCrackOpacityPercent +
                ",\"randomness\":" + s.SidewalkCrackRandomnessPercent + "}";
        }

        private static string BuildAlleySidewalkState(Setting s)
        {
            if (s == null) return "{}";
            return "{\"filterColor\":" + s.AlleySidewalkFilterColor + ",\"filterOpacity\":" + s.AlleySidewalkFilterOpacityPercent + ",\"show\":" + (s.ShowAppButton ? "true" : "false") +
                ",\"enabled\":" + (s.AlleySidewalkEnabled ? "true" : "false") +
                ",\"detail\":" + s.AlleySidewalkDetail +
                ",\"uv\":" + s.AlleySidewalkUvSizePercent +
                ",\"depth\":" + s.AlleySidewalkDepthStrengthPercent +
                ",\"tiling\":" + s.AlleySidewalkTextureTiling.ToString("0.0", CultureInfo.InvariantCulture) +
                ",\"offsetX\":" + s.AlleySidewalkUvOffsetX +
                ",\"offsetY\":" + s.AlleySidewalkUvOffsetY +
                ",\"rotation\":" + s.AlleySidewalkUvRotation +
                ",\"cracks\":" + s.AlleySidewalkCrackCount +
                ",\"crackSize\":" + s.AlleySidewalkCrackSizePercent +
                ",\"crackWidth\":" + s.AlleySidewalkCrackWidthPercent +
                ",\"crackOpacity\":" + s.AlleySidewalkCrackOpacityPercent +
                ",\"randomness\":" + s.AlleySidewalkCrackRandomnessPercent + "}";
        }

        private static string BuildRailState(Setting s)
        {
            if (s == null) return "{}";
            return "{\"filterColor\":" + s.RailFilterColor + ",\"filterOpacity\":" + s.RailFilterOpacityPercent + ",\"show\":" + (s.ShowAppButton ? "true" : "false") +
                ",\"enabled\":" + (s.RailGravelEnabled ? "true" : "false") +
                ",\"detail\":" + s.RailGravelDetail +
                ",\"uv\":" + s.RailGravelUvSizePercent +
                ",\"depth\":" + s.RailGravelDepthStrengthPercent +
                ",\"offsetX\":" + s.RailGravelUvOffsetX +
                ",\"offsetY\":" + s.RailGravelUvOffsetY +
                ",\"rotation\":" + s.RailGravelUvRotation +
                ",\"probe\":" + (Mod.RailInspector?.IsProbeActive == true ? "true" : "false") + "}";
        }

        private static string BuildGravelState(Setting s, bool shoulder)
        {
            if (s == null) return "{}";
            return "{\"filterColor\":" + (shoulder ? s.GravelShoulderFilterColor : s.RoadGravelFilterColor) + ",\"filterOpacity\":" + (shoulder ? s.GravelShoulderFilterOpacityPercent : s.RoadGravelFilterOpacityPercent) + ",\"show\":" + (s.ShowAppButton ? "true" : "false") +
                ",\"enabled\":" + ((shoulder ? s.GravelShoulderEnabled : s.RoadGravelEnabled) ? "true" : "false") +
                ",\"detail\":" + (shoulder ? s.GravelShoulderDetail : s.RoadGravelDetail) +
                ",\"uv\":" + (shoulder ? s.GravelShoulderUvSizePercent : s.RoadGravelUvSizePercent) +
                ",\"depth\":" + (shoulder ? s.GravelShoulderDepthStrengthPercent : s.RoadGravelDepthStrengthPercent) +
                ",\"offsetX\":" + (shoulder ? s.GravelShoulderUvOffsetX : s.RoadGravelUvOffsetX) +
                ",\"offsetY\":" + (shoulder ? s.GravelShoulderUvOffsetY : s.RoadGravelUvOffsetY) +
                ",\"rotation\":" + (shoulder ? s.GravelShoulderUvRotation : s.RoadGravelUvRotation) + "}";
        }

        private void ChangeGravelSetting(string request, bool shoulder)
        {
            var s = Mod.ActiveSetting;
            if (s == null || string.IsNullOrEmpty(request)) return;
            if (request == "reset")
            {
                if (shoulder) s.ResetGravelShoulder = true; else s.ResetRoadGravel = true;
                return;
            }
            int separator = request.IndexOf(':');
            if (separator <= 0 || !int.TryParse(request.Substring(separator + 1), out int value)) return;
            string key = request.Substring(0, separator);
            if (shoulder)
            {
                switch (key)
                {
                    case "detail": s.GravelShoulderDetail = Clamp(value, 0, 2); break;
                    case "offsetX": s.GravelShoulderUvOffsetX = Clamp(value, 0, Setting.MaxTextureUvOffset); break;
                    case "offsetY": s.GravelShoulderUvOffsetY = Clamp(value, 0, Setting.MaxTextureUvOffset); break;
                    case "rotation": s.GravelShoulderUvRotation = Clamp(value, 0, 360); break;
                    case "enabled": s.GravelShoulderEnabled = value != 0; break;
                    case "uv": s.GravelShoulderUvSizePercent = Clamp(value, 50, Setting.MaxTextureUvSize); break;
                    case "depth": s.GravelShoulderDepthStrengthPercent = Clamp(value, 0, Setting.MaxTextureDepthStrength); break;
                    default: return;
                }
            }
            else
            {
                switch (key)
                {
                    case "enabled": s.RoadGravelEnabled = value != 0; break;
                    case "detail": s.RoadGravelDetail = Clamp(value, 0, 2); break;
                    case "uv": s.RoadGravelUvSizePercent = Clamp(value, 50, Setting.MaxTextureUvSize); break;
                    case "depth": s.RoadGravelDepthStrengthPercent = Clamp(value, 0, Setting.MaxTextureDepthStrength); break;
                    case "offsetX": s.RoadGravelUvOffsetX = Clamp(value, 0, Setting.MaxTextureUvOffset); break;
                    case "offsetY": s.RoadGravelUvOffsetY = Clamp(value, 0, Setting.MaxTextureUvOffset); break;
                    case "rotation": s.RoadGravelUvRotation = Clamp(value, 0, 360); break;
                    default: return;
                }
            }
            s.ApplyToSystem();
            m_GravelState = BuildGravelState(s, false);
            m_ShoulderState = BuildGravelState(s, true);
            m_SavePending = true;
            m_SaveAt = UnityEngine.Time.unscaledTime + 0.6f;
        }

        private void ChangeRailSetting(string request)
        {
            var s = Mod.ActiveSetting;
            if (s == null || string.IsNullOrEmpty(request)) return;
            if (request == "reset") { s.ResetRailGravel = true; m_RailState = BuildRailState(s); return; }
            int separator = request.IndexOf(':');
            if (separator <= 0 || !int.TryParse(request.Substring(separator + 1), out int value)) return;
            switch (request.Substring(0, separator))
            {
                case "enabled": s.RailGravelEnabled = value != 0; break;
                case "detail": s.RailGravelDetail = Clamp(value, 0, 2); break;
                case "uv": s.RailGravelUvSizePercent = Clamp(value, 50, Setting.MaxTextureUvSize); break;
                case "depth": s.RailGravelDepthStrengthPercent = Clamp(value, 0, Setting.MaxTextureDepthStrength); break;
                case "offsetX": s.RailGravelUvOffsetX = Clamp(value, 0, Setting.MaxTextureUvOffset); break;
                case "offsetY": s.RailGravelUvOffsetY = Clamp(value, 0, Setting.MaxTextureUvOffset); break;
                case "rotation": s.RailGravelUvRotation = Clamp(value, 0, 360); break;
                default: return;
            }
            s.ApplyToSystem();
            m_RailState = BuildRailState(s);
            m_SavePending = true;
            m_SaveAt = UnityEngine.Time.unscaledTime + 0.6f;
        }

        private static string BuildGrassState(Setting s)
        {
            if (s == null) return "{}";
            return "{\"filterColor\":" + s.GrassFilterColor + ",\"filterOpacity\":" + s.GrassFilterOpacityPercent + ",\"show\":" + (s.ShowAppButton ? "true" : "false") +
                ",\"enabled\":" + (s.GrassEnabled ? "true" : "false") +
                ",\"detail\":" + s.GrassDetail +
                ",\"uv\":" + s.GrassUvSizePercent +
                ",\"depth\":" + s.GrassDepthStrengthPercent +
                ",\"offsetX\":" + s.GrassUvOffsetX +
                ",\"offsetY\":" + s.GrassUvOffsetY +
                ",\"rotation\":" + s.GrassUvRotation + "}";
        }

        private void ChangeGrassSetting(string request)
        {
            var s = Mod.ActiveSetting;
            if (s == null || string.IsNullOrEmpty(request)) return;
            if (request == "reset") { s.ResetGrass = true; m_GrassState = BuildGrassState(s); return; }
            int separator = request.IndexOf(':');
            if (separator <= 0 || !int.TryParse(request.Substring(separator + 1), out int value)) return;
            switch (request.Substring(0, separator))
            {
                case "enabled": s.GrassEnabled = value != 0; break;
                case "detail": s.GrassDetail = Clamp(value, 0, 2); break;
                case "uv": s.GrassUvSizePercent = Clamp(value, 50, Setting.MaxTextureUvSize); break;
                case "depth": s.GrassDepthStrengthPercent = Clamp(value, 0, Setting.MaxTextureDepthStrength); break;
                case "offsetX": s.GrassUvOffsetX = Clamp(value, 0, Setting.MaxTextureUvOffset); break;
                case "offsetY": s.GrassUvOffsetY = Clamp(value, 0, Setting.MaxTextureUvOffset); break;
                case "rotation": s.GrassUvRotation = Clamp(value, 0, 360); break;
                default: return;
            }
            s.ApplyToSystem();
            m_GrassState = BuildGrassState(s);
            m_SavePending = true;
            m_SaveAt = UnityEngine.Time.unscaledTime + 0.6f;
        }

        private void ChangeSidewalkSetting(string request)
        {
            var s = Mod.ActiveSetting;
            if (s == null || string.IsNullOrEmpty(request)) return;
            if (request == "reset") s.ResetSidewalk = true;
            else
            {
                int separator = request.IndexOf(':');
                if (separator <= 0) return;
                string key = request.Substring(0, separator);
                string input = request.Substring(separator + 1);
                if (key == "tiling")
                {
                    if (!float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out float tiling)) return;
                    s.SidewalkTextureTiling = Math.Max(0.1f, Math.Min(8f, (float)Math.Round(tiling, 1)));
                }
                else
                {
                    if (!int.TryParse(input, out int value)) return;
                    switch (key)
                    {
                        case "enabled": s.SidewalkEnabled = value != 0; break;
                        case "detail": s.SidewalkDetail = Clamp(value, 0, 2); break;
                        case "uv": s.SidewalkUvSizePercent = Clamp(value, 50, Setting.MaxTextureUvSize); break;
                        case "depth": s.SidewalkDepthStrengthPercent = Clamp(value, 0, Setting.MaxTextureDepthStrength); break;
                        case "offsetX": s.SidewalkUvOffsetX = Clamp(value, 0, Setting.MaxTextureUvOffset); break;
                        case "offsetY": s.SidewalkUvOffsetY = Clamp(value, 0, Setting.MaxTextureUvOffset); break;
                        case "rotation": s.SidewalkUvRotation = Clamp(value, 0, 360); break;
                        case "cracks": s.SidewalkCrackCount = Clamp(value, 0, 400); break;
                        case "crackSize": s.SidewalkCrackSizePercent = Clamp(value, 25, 600); break;
                        case "crackWidth": s.SidewalkCrackWidthPercent = Clamp(value, 25, 400); break;
                        case "crackOpacity": s.SidewalkCrackOpacityPercent = Clamp(value, 0, 200); break;
                        case "randomness": s.SidewalkCrackRandomnessPercent = Clamp(value, 0, 100); break;
                        default: return;
                    }
                }
                s.ApplyToSystem();
                m_SavePending = true;
                m_SaveAt = UnityEngine.Time.unscaledTime + 0.6f;
            }
            m_SidewalkState = BuildSidewalkState(s);
        }

        private void ChangeAlleySidewalkSetting(string request)
        {
            var s = Mod.ActiveSetting;
            if (s == null || string.IsNullOrEmpty(request)) return;
            if (request == "reset") s.ResetAlleySidewalk = true;
            else
            {
                int separator = request.IndexOf(':');
                if (separator <= 0) return;
                string key = request.Substring(0, separator);
                string input = request.Substring(separator + 1);
                if (key == "tiling")
                {
                    if (!float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out float tiling)) return;
                    s.AlleySidewalkTextureTiling = Math.Max(0.1f, Math.Min(8f, (float)Math.Round(tiling, 1)));
                }
                else
                {
                    if (!int.TryParse(input, out int value)) return;
                    switch (key)
                    {
                        case "enabled": s.AlleySidewalkEnabled = value != 0; break;
                        case "detail": s.AlleySidewalkDetail = Clamp(value, 0, 2); break;
                        case "uv": s.AlleySidewalkUvSizePercent = Clamp(value, 50, Setting.MaxTextureUvSize); break;
                        case "depth": s.AlleySidewalkDepthStrengthPercent = Clamp(value, 0, Setting.MaxTextureDepthStrength); break;
                        case "offsetX": s.AlleySidewalkUvOffsetX = Clamp(value, 0, Setting.MaxTextureUvOffset); break;
                        case "offsetY": s.AlleySidewalkUvOffsetY = Clamp(value, 0, Setting.MaxTextureUvOffset); break;
                        case "rotation": s.AlleySidewalkUvRotation = Clamp(value, 0, 360); break;
                        case "cracks": s.AlleySidewalkCrackCount = Clamp(value, 0, 400); break;
                        case "crackSize": s.AlleySidewalkCrackSizePercent = Clamp(value, 25, 600); break;
                        case "crackWidth": s.AlleySidewalkCrackWidthPercent = Clamp(value, 25, 400); break;
                        case "crackOpacity": s.AlleySidewalkCrackOpacityPercent = Clamp(value, 0, 200); break;
                        case "randomness": s.AlleySidewalkCrackRandomnessPercent = Clamp(value, 0, 100); break;
                        default: return;
                    }
                }
                s.ApplyToSystem();
                m_SavePending = true;
                m_SaveAt = UnityEngine.Time.unscaledTime + 0.6f;
            }
            m_AlleySidewalkState = BuildAlleySidewalkState(s);
        }

        private void ChangeSetting(string request)
        {
            var s = Mod.ActiveSetting;
            if (s == null || string.IsNullOrEmpty(request)) return;
            if (request == "reset")
            {
                s.ResetRoadAsphalt = true;
                m_State = BuildState(s);
                return;
            }
            int separator = request.IndexOf(':');
            if (separator > 0 && (request.Substring(0, separator) == "tiling" || request.Substring(0, separator) == "parkingScale" || request.Substring(0, separator) == "parkingDepth" || request.Substring(0, separator) == "mipBias"))
            {
                if (!float.TryParse(request.Substring(separator + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float tiling)) return;
                if (float.IsNaN(tiling) || float.IsInfinity(tiling)) return;
                if (request.Substring(0, separator) == "parkingScale") s.ParkingTextureScaleFactor = tiling;
                else if (request.Substring(0, separator) == "mipBias") s.SurfaceTextureMipBias = tiling;
                else if (request.Substring(0, separator) == "parkingDepth") s.ParkingDepthStrengthFactor = tiling;
                else s.RoadTextureTiling = Math.Max(0.1f, Math.Min(8f, (float)Math.Round(tiling, 1)));
                s.ApplyToSystem();
                m_State = BuildState(s);
                m_SavePending = true;
                m_SaveAt = UnityEngine.Time.unscaledTime + 0.6f;
                return;
            }
            if (separator <= 0 || !int.TryParse(request.Substring(separator + 1), out int value)) return;
            switch (request.Substring(0, separator))
            {
                case "enabled": s.RoadAsphaltEnabled = value != 0; break;
                case "detail": s.RoadAsphaltDetail = Clamp(value, 0, 2); break;
                case "uv": s.RoadUvSizePercent = Clamp(value, 50, Setting.MaxTextureUvSize); break;
                case "depth": s.RoadDepthStrengthPercent = Clamp(value, 0, Setting.MaxTextureDepthStrength); break;
                case "offsetX": s.RoadUvOffsetX = Clamp(value, 0, Setting.MaxTextureUvOffset); break;
                case "offsetY": s.RoadUvOffsetY = Clamp(value, 0, Setting.MaxTextureUvOffset); break;
                case "rotation": s.RoadUvRotation = Clamp(value, 0, 360); break;
                case "customCracks": s.UseCustomRoadCracks = value != 0; break;
                case "cracks": s.RoadCrackCount = Clamp(value, 0, Setting.MaxRoadCrackCount); break;
                case "crackSize": s.RoadCrackSizePercent = Clamp(value, 25, Setting.MaxRoadCrackSize); break;
                case "crackWidth": s.RoadCrackWidthPercent = Clamp(value, 25, Setting.MaxRoadCrackWidth); break;
                case "crackOpacity": s.RoadCrackOpacityPercent = Clamp(value, 0, Setting.MaxRoadCrackOpacity); break;
                case "randomness": s.RoadCrackRandomnessPercent = Clamp(value, 0, 100); break;
                default: return;
            }
            s.ApplyToSystem();
            m_State = BuildState(s);
            m_SavePending = true;
            m_SaveAt = UnityEngine.Time.unscaledTime + 0.6f;
        }

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
    }
}



