using System.Linq;
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
        private string m_FootpathState = "{}";
        private bool m_SavePending;
        private float m_SaveAt;

        public override GameMode gameMode => GameMode.GameOrEditor;

        private void ApplyEndcap(string request)
        {
            var s = Mod.ActiveSetting;
            if (s == null || string.IsNullOrEmpty(request)) return;
            var parts = request.Split(':');
            if (parts.Length != 3 || (parts[0] != "sidewalk" && parts[0] != "alley" && parts[0] != "parking" && parts[0] != "roadend" && parts[0] != "large")) return;
            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float u) ||
                !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ||
                float.IsNaN(u) || float.IsInfinity(u) || float.IsNaN(v) || float.IsInfinity(v)) return;
            // Publish both axes together, only on the explicit Apply action.
            s.ApplyEndcapValues(parts[0], u, v);
            s.ApplyToSystem();
            m_State = BuildState(s);
            m_SavePending = true;
            m_SaveAt = UnityEngine.Time.unscaledTime + 0.6f;
        }

        private static void TextureFileAction(string surface, bool openFolder)
        {
            var s = Mod.ActiveSetting;
            if (s == null) return;
            // Map tab IDs to existing actions; never accept a path from the UI.
            switch (surface)
            {
                case "road":
                    if (openFolder) s.OpenRoadTextureFolder = true;
                    else s.ReloadRoadTexture = true;
                    break;
                case "sidewalk":
                    if (openFolder) s.OpenSidewalkTextureFolder = true;
                    else s.ReloadSidewalkTexture = true;
                    break;
                case "alley":
                    if (openFolder) s.OpenAlleySidewalkTextureFolder = true;
                    else s.ReloadAlleySidewalkTexture = true;
                    break;
                case "footpath":
                    if (openFolder) s.OpenFootpathTextureFolder = true;
                    else s.ReloadFootpathTexture = true;
                    break;
                case "grass":
                    if (openFolder) s.OpenGrassTextureFolder = true;
                    else s.ReloadGrassTexture = true;
                    break;
                case "gravel":
                    if (openFolder) s.OpenGravelTextureFolder = true;
                    else s.ReloadGravelTexture = true;
                    break;
                case "shoulder":
                    if (openFolder) s.OpenGravelShoulderTextureFolder = true;
                    else s.ReloadGravelShoulderTexture = true;
                    break;
                case "rail":
                    if (openFolder) s.OpenRailTextureFolder = true;
                    else s.ReloadRailTexture = true;
                    break;
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            AddBinding(new TriggerBinding<string>(Group, "reloadSurfaceTexture", surface => TextureFileAction(surface, false)));
            AddBinding(new TriggerBinding<string>(Group, "openSurfaceTextureFolder", surface => TextureFileAction(surface, true)));
            AddUpdateBinding(new GetterValueBinding<string>(Group, "normalMapFlags", () =>
                string.Join(",", new[] { "road", "sidewalk", "alley", "gravel", "shoulder", "rail", "grass", "footpath" }
                    .Select(key => Mod.ActiveSetting?.GetCustomNormalEnabled(key) == true ? "1" : "0"))));
            AddBinding(new TriggerBinding<string>(Group, "setNormalMapEnabled", ChangeNormalMapEnabled));
            AddBinding(new TriggerBinding<string>(Group, "applyEndcap", ApplyEndcap));
            AddBinding(new TriggerBinding<string>(Group, "reloadAllCustomTextures", _ => Mod.ActiveSetting?.ReloadAllCustomTexturesNow()));
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

            AddBinding(new TriggerBinding<string>(Group, "setRailSetting", ChangeRailSetting));
            AddUpdateBinding(new GetterValueBinding<string>(Group, "gravelSettings", () => m_GravelState));
            AddUpdateBinding(new GetterValueBinding<string>(Group, "shoulderSettings", () => m_ShoulderState));
            AddBinding(new TriggerBinding<string>(Group, "setGravelSetting", request => ChangeGravelSetting(request, false)));
            AddBinding(new TriggerBinding<string>(Group, "setShoulderSetting", request => ChangeGravelSetting(request, true)));
            AddUpdateBinding(new GetterValueBinding<string>(Group, "grassSettings", () => m_GrassState));
            AddUpdateBinding(new GetterValueBinding<string>(Group, "footpathSettings", () => m_FootpathState));
            AddBinding(new TriggerBinding<string>(Group, "setGrassSetting", ChangeGrassSetting));
            AddBinding(new TriggerBinding<string>(Group, "setFootpathSetting", ChangeFootpathSetting));
            AddBinding(new TriggerBinding<string>(Group, "reloadGrassTexture", _ => Mod.Grass?.ReloadCustomTexture()));
            AddBinding(new TriggerBinding<string>(Group, "reloadFootpathTexture", _ => Mod.Footpath?.ReloadCustomTexture()));
            AddBinding(new TriggerBinding<string>(Group, "reloadGravelTexture", _ => Mod.RoadGravel?.ReloadCustomTexture()));
        }

        protected override void OnUpdate()
        {
            Mod.ActiveSetting?.ProcessPendingApply();
            m_State = BuildState(Mod.ActiveSetting);
            m_SidewalkState = BuildSidewalkState(Mod.ActiveSetting);
            m_AlleySidewalkState = BuildAlleySidewalkState(Mod.ActiveSetting);
            m_RailState = BuildRailState(Mod.ActiveSetting);
            m_GrassState = BuildGrassState(Mod.ActiveSetting);
            m_FootpathState = BuildFootpathState(Mod.ActiveSetting);
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

        private void ChangeNormalMapEnabled(string request)
        {
            var s = Mod.ActiveSetting;
            var parts = request?.Split(':');
            if (s == null || parts == null || parts.Length != 2 ||
                (parts[1] != "0" && parts[1] != "1") || !s.SetCustomNormalEnabled(parts[0], parts[1] == "1")) return;
            s.ApplyToSystem();
            m_SavePending = true;
            m_SaveAt = UnityEngine.Time.unscaledTime + 0.6f;
        }

        private void ChangeFilterColor(string request)
        {
            var s = Mod.ActiveSetting;
            if (s == null || string.IsNullOrEmpty(request)) return;
            var parts = request.Split(':');
            int opacity = 100, mode = -1;
            if ((parts.Length < 2 || parts.Length > 4) || !int.TryParse(parts[1], out int color) ||
                (parts.Length >= 3 && !int.TryParse(parts[2], out opacity)) || opacity < 0 || opacity > 100 ||
                (parts.Length == 4 && (!int.TryParse(parts[3], out mode) || mode < 0 || mode > 2)) ||
                color < 0 || color > 0xffffff || !s.SetFilterColor(parts[0], color, opacity, mode)) return;
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
            return "{\"filterColor\":" + s.RoadFilterColor + ",\"filterOpacity\":" + s.RoadFilterOpacityPercent + ",\"show\":" + (s.ShowAppButton && s.TexturesEnabled ? "true" : "false") +
                ",\"enabled\":" + (s.RoadAsphaltEnabled ? "true" : "false") +
                ",\"detail\":" + s.RoadAsphaltDetail +
                ",\"uv\":" + s.RoadUvSizePercent +
                ",\"customCracks\":" + (s.UseCustomRoadCracks ? "true" : "false") +
                ",\"crackStatus\":" + (Mod.RoadAsphalt?.CustomCrackStatus ?? 0) +
                ",\"mipBias\":" + s.SurfaceTextureMipBias.ToString("0.##", CultureInfo.InvariantCulture) +
                ",\"endcapU\":" + s.SidewalkEndcapU.ToString("0.##", CultureInfo.InvariantCulture) +
                ",\"endcapV\":" + s.SidewalkEndcapV.ToString("0.##", CultureInfo.InvariantCulture) +
                ",\"alleyEndcapU\":" + s.AlleyEndcapU.ToString("0.##", CultureInfo.InvariantCulture) +
                ",\"alleyEndcapV\":" + s.AlleyEndcapV.ToString("0.##", CultureInfo.InvariantCulture) +
                ",\"parkingEndcapU\":" + s.ParkingEndcapU.ToString("0.##", CultureInfo.InvariantCulture) +
                ",\"parkingEndcapV\":" + s.ParkingEndcapV.ToString("0.##", CultureInfo.InvariantCulture) +
                ",\"largeEndcapU\":" + s.LargeRoadEndcapU.ToString("0.00", CultureInfo.InvariantCulture) +
                ",\"largeEndcapV\":" + s.LargeRoadEndcapV.ToString("0.00", CultureInfo.InvariantCulture) +
                ",\"roadEndU\":" + s.RoadEndTilingU.ToString("0.##", CultureInfo.InvariantCulture) +
                ",\"roadEndV\":" + s.RoadEndTilingV.ToString("0.##", CultureInfo.InvariantCulture) +
                ",\"parkingDepth\":" + s.ParkingDepthStrengthFactor.ToString("0.##", CultureInfo.InvariantCulture) +
                ",\"parkingScale\":" + s.ParkingTextureScaleFactor.ToString("0.##", CultureInfo.InvariantCulture) +
                ",\"depth\":" + s.RoadDepthStrengthPercent +
                ",\"tiling\":" + s.RoadTextureTiling.ToString("0.##", CultureInfo.InvariantCulture) +
                ",\"offsetX\":" + s.RoadUvOffsetX +
                ",\"offsetY\":" + s.RoadUvOffsetY +
                ",\"rotation\":" + s.RoadUvRotation +
                ",\"cracks\":" + s.RoadCrackCount +
                ",\"crackSize\":" + s.RoadCrackSizePercent +
                ",\"crackWidth\":" + s.RoadCrackWidthPercent +
                ",\"crackOffsetX\":" + s.RoadCrackOffsetX +
                ",\"crackOffsetY\":" + s.RoadCrackOffsetY +
                ",\"crackRotation\":" + s.RoadCrackRotation +
                ",\"crackDepth\":" + s.RoadCrackDepthStrengthPercent +
                ",\"crackOpacity\":" + s.RoadCrackOpacityPercent +
                ",\"randomness\":" + s.RoadCrackRandomnessPercent + "}";
        }

        private static string BuildSidewalkState(Setting s)
        {
            if (s == null) return "{}";
            return "{\"filterColor\":" + s.SidewalkFilterColor + ",\"filterOpacity\":" + s.SidewalkFilterOpacityPercent + ",\"show\":" + (s.ShowAppButton && s.TexturesEnabled ? "true" : "false") +
                ",\"enabled\":" + (s.SidewalkEnabled ? "true" : "false") +
                ",\"detail\":" + s.SidewalkDetail +
                ",\"uv\":" + s.SidewalkUvSizePercent +
                ",\"depth\":" + s.SidewalkDepthStrengthPercent +
                ",\"tiling\":" + s.SidewalkTextureTiling.ToString("0.##", CultureInfo.InvariantCulture) +
                ",\"parkingSidewalkU\":" + s.ParkingSidewalkScaleU.ToString("0.##", CultureInfo.InvariantCulture) +
                ",\"parkingSidewalkV\":" + s.ParkingSidewalkScaleV.ToString("0.##", CultureInfo.InvariantCulture) +
                ",\"shiftU\":" + s.SidewalkShiftU +
                ",\"shiftV\":" + s.SidewalkShiftV +
                ",\"offsetX\":" + s.SidewalkUvOffsetX +
                ",\"offsetY\":" + s.SidewalkUvOffsetY +
                ",\"rotation\":" + s.SidewalkUvRotation +
                ",\"cracks\":" + s.SidewalkCrackCount +
                ",\"crackSize\":" + s.SidewalkCrackSizePercent +
                ",\"crackWidth\":" + s.SidewalkCrackWidthPercent +
                ",\"crackDepth\":" + s.SidewalkCrackDepthStrengthPercent +
                ",\"crackRotation\":" + s.SidewalkCrackRotation +
                ",\"crackOffsetX\":" + s.SidewalkCrackOffsetX +
                ",\"crackOffsetY\":" + s.SidewalkCrackOffsetY +
                ",\"crackOpacity\":" + s.SidewalkCrackOpacityPercent +
                ",\"randomness\":" + s.SidewalkCrackRandomnessPercent + "}";
        }

        private static string BuildAlleySidewalkState(Setting s)
        {
            if (s == null) return "{}";
            return "{\"filterColor\":" + s.AlleySidewalkFilterColor + ",\"filterOpacity\":" + s.AlleySidewalkFilterOpacityPercent + ",\"show\":" + (s.ShowAppButton && s.TexturesEnabled ? "true" : "false") +
                ",\"enabled\":" + (s.AlleySidewalkEnabled ? "true" : "false") +
                ",\"detail\":" + s.AlleySidewalkDetail +
                ",\"uv\":" + s.AlleySidewalkUvSizePercent +
                ",\"depth\":" + s.AlleySidewalkDepthStrengthPercent +
                ",\"tiling\":" + s.AlleySidewalkTextureTiling.ToString("0.##", CultureInfo.InvariantCulture) +
                ",\"offsetX\":" + s.AlleySidewalkUvOffsetX +
                ",\"offsetY\":" + s.AlleySidewalkUvOffsetY +
                ",\"rotation\":" + s.AlleySidewalkUvRotation +
                ",\"cracks\":" + s.AlleySidewalkCrackCount +
                ",\"crackSize\":" + s.AlleySidewalkCrackSizePercent +
                ",\"crackWidth\":" + s.AlleySidewalkCrackWidthPercent +
                ",\"crackDepth\":" + s.AlleySidewalkCrackDepthStrengthPercent +
                ",\"crackRotation\":" + s.AlleySidewalkCrackRotation +
                ",\"crackOffsetX\":" + s.AlleySidewalkCrackOffsetX +
                ",\"crackOffsetY\":" + s.AlleySidewalkCrackOffsetY +
                ",\"crackOpacity\":" + s.AlleySidewalkCrackOpacityPercent +
                ",\"randomness\":" + s.AlleySidewalkCrackRandomnessPercent + "}";
        }

        private static string BuildRailState(Setting s)
        {
            if (s == null) return "{}";
            return "{\"filterColor\":" + s.RailFilterColor + ",\"filterOpacity\":" + s.RailFilterOpacityPercent + ",\"show\":" + (s.ShowAppButton && s.TexturesEnabled ? "true" : "false") +
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
            return "{\"filterColor\":" + (shoulder ? s.GravelShoulderFilterColor : s.RoadGravelFilterColor) + ",\"filterOpacity\":" + (shoulder ? s.GravelShoulderFilterOpacityPercent : s.RoadGravelFilterOpacityPercent) + ",\"show\":" + (s.ShowAppButton && s.TexturesEnabled ? "true" : "false") +
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
            return "{\"filterColor\":" + s.GrassFilterColor + ",\"filterOpacity\":" + s.GrassFilterOpacityPercent + ",\"show\":" + (s.ShowAppButton && s.TexturesEnabled ? "true" : "false") +
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

        private static string BuildFootpathState(Setting s)
        {
            if (s == null) return "{}";
            return "{\"filterColor\":" + s.FootpathFilterColor + ",\"filterOpacity\":" + s.FootpathFilterOpacityPercent + ",\"show\":" + (s.ShowAppButton && s.TexturesEnabled ? "true" : "false") +
                ",\"enabled\":" + (s.FootpathEnabled ? "true" : "false") +
                ",\"detail\":" + s.FootpathDetail +
                ",\"uv\":" + s.FootpathUvSizePercent +
                ",\"depth\":" + s.FootpathDepthStrengthPercent +
                ",\"offsetX\":" + s.FootpathUvOffsetX +
                ",\"offsetY\":" + s.FootpathUvOffsetY +
                ",\"rotation\":" + s.FootpathUvRotation + "}";
        }

        private void ChangeFootpathSetting(string request)
        {
            var s = Mod.ActiveSetting;
            if (s == null || string.IsNullOrEmpty(request)) return;
            if (request == "reset") { s.ResetFootpath = true; m_FootpathState = BuildFootpathState(s); return; }
            int separator = request.IndexOf(':');
            if (separator <= 0 || !int.TryParse(request.Substring(separator + 1), out int value)) return;
            switch (request.Substring(0, separator))
            {
                case "enabled": s.FootpathEnabled = value != 0; break;
                case "detail": s.FootpathDetail = Clamp(value, 0, 2); break;
                case "uv": s.FootpathUvSizePercent = Clamp(value, 10, Setting.MaxTextureUvSize); break;
                case "depth": s.FootpathDepthStrengthPercent = Clamp(value, 0, Setting.MaxTextureDepthStrength); break;
                case "offsetX": s.FootpathUvOffsetX = Clamp(value, 0, Setting.MaxTextureUvOffset); break;
                case "offsetY": s.FootpathUvOffsetY = Clamp(value, 0, Setting.MaxTextureUvOffset); break;
                case "rotation": s.FootpathUvRotation = Clamp(value, 0, 360); break;
                default: return;
            }
            s.ApplyToSystem();
            m_FootpathState = BuildFootpathState(s);
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
                if (key == "tiling" || key == "parkingSidewalkU" || key == "parkingSidewalkV")
                {
                    if (!float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out float tiling)) return;
                    if (float.IsNaN(tiling) || float.IsInfinity(tiling)) return;
                    if (key == "parkingSidewalkU") s.ParkingSidewalkScaleU = tiling;
                    else if (key == "parkingSidewalkV") s.ParkingSidewalkScaleV = tiling;
                    else s.SidewalkTextureTiling = Math.Max(0.1f, Math.Min(8f, (float)Math.Round(tiling, 2)));
                }
                else
                {
                    if (!int.TryParse(input, out int value)) return;
                    switch (key)
                    {
                        case "enabled": s.SidewalkEnabled = value != 0; break;
                        case "detail": s.SidewalkDetail = Clamp(value, 0, 2); break;
                        case "uv": s.SidewalkUvSizePercent = Clamp(value, 10, Setting.MaxTextureUvSize); break;
                        case "depth": s.SidewalkDepthStrengthPercent = Clamp(value, 0, Setting.MaxTextureDepthStrength); break;
                        case "offsetX": s.SidewalkUvOffsetX = Clamp(value, 0, Setting.MaxTextureUvOffset); break;
                        case "offsetY": s.SidewalkUvOffsetY = Clamp(value, 0, Setting.MaxTextureUvOffset); break;
                        case "rotation": s.SidewalkUvRotation = Clamp(value, 0, 360); break;
                        case "shiftU": s.SidewalkShiftU = Clamp(value, 0, 100); break;
                        case "shiftV": s.SidewalkShiftV = Clamp(value, 0, 100); break;
                        case "cracks": s.SidewalkCrackCount = Clamp(value, 0, 400); break;
                        case "crackSize": s.SidewalkCrackSizePercent = Clamp(value, 25, 600); break;
                        case "crackWidth": s.SidewalkCrackWidthPercent = Clamp(value, 25, 400); break;
                        case "crackDepth": s.SidewalkCrackDepthStrengthPercent = Clamp(value, 0, Setting.MaxTextureDepthStrength); break;
                        case "crackRotation": s.SidewalkCrackRotation = Clamp(value, 0, 360); break;
                        case "crackOffsetX": s.SidewalkCrackOffsetX = Clamp(value, 0, Setting.MaxTextureUvOffset); break;
                        case "crackOffsetY": s.SidewalkCrackOffsetY = Clamp(value, 0, Setting.MaxTextureUvOffset); break;
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
                    s.AlleySidewalkTextureTiling = Math.Max(0.1f, Math.Min(8f, (float)Math.Round(tiling, 2)));
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
                        case "crackDepth": s.AlleySidewalkCrackDepthStrengthPercent = Clamp(value, 0, Setting.MaxTextureDepthStrength); break;
                        case "crackRotation": s.AlleySidewalkCrackRotation = Clamp(value, 0, 360); break;
                        case "crackOffsetX": s.AlleySidewalkCrackOffsetX = Clamp(value, 0, Setting.MaxTextureUvOffset); break;
                        case "crackOffsetY": s.AlleySidewalkCrackOffsetY = Clamp(value, 0, Setting.MaxTextureUvOffset); break;
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
                else s.RoadTextureTiling = Math.Max(0.1f, Math.Min(8f, (float)Math.Round(tiling, 2)));
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
                case "crackOffsetX": s.RoadCrackOffsetX = Clamp(value, 0, Setting.MaxTextureUvOffset); break;
                case "crackOffsetY": s.RoadCrackOffsetY = Clamp(value, 0, Setting.MaxTextureUvOffset); break;
                case "crackRotation": s.RoadCrackRotation = Clamp(value, 0, 360); break;
                case "crackDepth": s.RoadCrackDepthStrengthPercent = Clamp(value, 0, Setting.MaxTextureDepthStrength); break;
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






