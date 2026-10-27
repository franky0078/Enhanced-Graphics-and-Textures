using System;
using System.Globalization;
using Game.Settings;

namespace Advanced_Graphics
{
    public sealed partial class Setting
    {
        // Each road surface mode owns both generated and custom crack settings.
        [SettingsUIHidden]
        public string RoadCrackModeProfiles { get; set; } = "";

        private bool m_UseCustomRoadCracks;
        private double[][] m_RoadCrackProfiles;

        private double[] ReadRoadCrackSliders() => new double[] {
            RoadCrackCount, RoadCrackSizePercent, RoadCrackWidthPercent,
            RoadCrackOpacityPercent, RoadCrackRandomnessPercent, RoadTextureTiling };

        private void WriteRoadCrackSliders(double[] values)
        {
            RoadCrackCount = (int)values[0];
            RoadCrackSizePercent = (int)values[1];
            RoadCrackWidthPercent = (int)values[2];
            RoadCrackOpacityPercent = (int)values[3];
            RoadCrackRandomnessPercent = (int)values[4];
            RoadTextureTiling = (float)values[5];
        }

        private void EnsureRoadCrackProfiles()
        {
            if (m_RoadCrackProfiles != null) return;
            var encoded = (RoadCrackModeProfiles ?? "").Split(';');
            var profiles = new double[6][];
            for (int mode = 0; mode < 3; mode++)
            for (int source = 0; source < 2; source++)
            {
                int slot = mode * 2 + source;
                var values = new double[] { 0, 100, 100, 100, 50, 1 };
                // Seed generated cracks from existing surface-mode profiles on upgrade.
                if (source == 0 && m_TextureModeProfiles[0] != null)
                    Array.Copy(m_TextureModeProfiles[0][mode], 5, values, 0, values.Length);
                if (encoded.Length == profiles.Length)
                {
                    var parts = encoded[slot].Split(',');
                    var parsed = new double[values.Length];
                    bool valid = parts.Length == parsed.Length;
                    for (int i = 0; valid && i < parsed.Length; i++)
                        valid = double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out parsed[i]) &&
                            !double.IsNaN(parsed[i]) && !double.IsInfinity(parsed[i]) &&
                            IsValidTextureSlider(0, i + 5, parsed[i]);
                    if (valid) values = parsed;
                }
                profiles[slot] = values;
            }
            // Preserve the currently visible settings, including an already active custom PNG.
            profiles[RoadAsphaltDetail * 2 + (m_UseCustomRoadCracks ? 1 : 0)] = ReadRoadCrackSliders();
            m_RoadCrackProfiles = profiles;
        }

        private void CaptureRoadCrackProfile()
        {
            EnsureRoadCrackProfiles();
            m_RoadCrackProfiles[RoadAsphaltDetail * 2 + (m_UseCustomRoadCracks ? 1 : 0)] = ReadRoadCrackSliders();
            RoadCrackModeProfiles = SerializeTextureProfiles(m_RoadCrackProfiles);
        }

        private void SwitchRoadCrackSource(bool custom)
        {
            if (m_UseCustomRoadCracks == custom) return;
            if (m_TextureModeProfilesReady)
            {
                CaptureRoadCrackProfile();
                WriteRoadCrackSliders(m_RoadCrackProfiles[RoadAsphaltDetail * 2 + (custom ? 1 : 0)]);
            }
            m_UseCustomRoadCracks = custom;
        }

        // These strings are saved by the game's existing settings serializer.
        [SettingsUIHidden]
        public string RoadAsphaltModeProfiles { get; set; } = "";

        [SettingsUIHidden]
        public string SidewalkModeProfiles { get; set; } = "";

        [SettingsUIHidden]
        public string AlleySidewalkModeProfiles { get; set; } = "";

        [SettingsUIHidden]
        public string RoadGravelModeProfiles { get; set; } = "";

        [SettingsUIHidden]
        public string GravelShoulderModeProfiles { get; set; } = "";

        [SettingsUIHidden]
        public string RailGravelModeProfiles { get; set; } = "";

        [SettingsUIHidden]
        public string GrassModeProfiles { get; set; } = "";

        [SettingsUIHidden]
        public string FootpathModeProfiles { get; set; } = "";

        private bool m_TextureModeProfilesReady;
        private readonly double[][][] m_TextureModeProfiles = new double[8][][];

        // Called after LoadSettings and existing setting migrations.
        internal void InitializeTextureModeProfiles()
        {
            for (int surface = 0; surface < 8; surface++) EnsureTextureModeProfiles(surface);
            EnsureRoadCrackProfiles();
            m_TextureModeProfilesReady = true;
            CaptureTextureModeProfiles();
        }

        private void SwitchTextureMode(int surface, ref int currentMode, int nextMode)
        {
            nextMode = Math.Max(0, Math.Min(2, nextMode));
            if (currentMode == nextMode) return;
            if (surface == 0 && m_TextureModeProfilesReady) CaptureRoadCrackProfile();
            if (m_TextureModeProfilesReady && m_TextureModeProfiles[surface] != null)
            {
                m_TextureModeProfiles[surface][currentMode] = ReadTextureSliders(surface);
                WriteTextureSliders(surface, m_TextureModeProfiles[surface][nextMode]);
            }
            currentMode = nextMode;
            if (surface == 0 && m_TextureModeProfilesReady)
                WriteRoadCrackSliders(m_RoadCrackProfiles[nextMode * 2 + (m_UseCustomRoadCracks ? 1 : 0)]);
            if (m_TextureModeProfilesReady && m_TextureModeProfiles[surface] != null)
                SetSerializedProfiles(surface, SerializeTextureProfiles(m_TextureModeProfiles[surface]));
        }

        private void CaptureTextureModeProfiles()
        {
            if (!m_TextureModeProfilesReady) return;
            CaptureRoadCrackProfile();
            for (int surface = 0; surface < 8; surface++)
            {
                EnsureTextureModeProfiles(surface);
                m_TextureModeProfiles[surface][GetTextureMode(surface)] = ReadTextureSliders(surface);
                SetSerializedProfiles(surface, SerializeTextureProfiles(m_TextureModeProfiles[surface]));
            }
        }

        private void EnsureTextureModeProfiles(int surface)
        {
            if (m_TextureModeProfiles[surface] != null) return;
            var encoded = (GetSerializedProfiles(surface) ?? "").Split(';');
            var profiles = new double[3][];
            for (int mode = 0; mode < 3; mode++)
            {
                profiles[mode] = DefaultTextureSliders(surface, mode);
                if (encoded.Length != 3) continue;
                var values = encoded[mode].Split(',');
                // Older asphalt profiles have eleven or twelve sliders. Append parking
                // factors without losing the three existing profiles.
                if (values.Length != profiles[mode].Length && !(surface == 0 && (values.Length == 11 || values.Length == 12))) continue;
                var parsed = DefaultTextureSliders(surface, mode);
                bool valid = true;
                for (int i = 0; i < values.Length; i++)
                {
                    if (!double.TryParse(values[i], NumberStyles.Float, CultureInfo.InvariantCulture, out parsed[i]) ||
                        double.IsNaN(parsed[i]) || double.IsInfinity(parsed[i]) ||
                        !IsValidTextureSlider(surface, i, parsed[i]))
                    { valid = false; break; }
                }
                if (valid) profiles[mode] = parsed;
            }
            // Current live values are authoritative when upgrading existing settings.
            profiles[GetTextureMode(surface)] = ReadTextureSliders(surface);
            m_TextureModeProfiles[surface] = profiles;
        }

        private static bool IsValidTextureSlider(int surface, int index, double value)
        {
            switch (index)
            {
                case 0: return value >= (surface == 7 ? 10 : 50) && value <= MaxTextureUvSize;
                case 1: return value >= 0 && value <= MaxTextureDepthStrength;
                case 2: case 3: return value >= 0 && value <= MaxTextureUvOffset;
                case 9: return value >= 0 && value <= 100;
                case 4: return value >= 0 && value <= 360;
                case 5: return value >= 0 && value <= (surface == 0 ? MaxRoadCrackCount : 400);
                case 6: return value >= 25 && value <= (surface == 0 ? MaxRoadCrackSize : 600);
                case 7: return value >= 25 && value <= (surface == 0 ? MaxRoadCrackWidth : 400);
                case 8: return value >= 0 && value <= (surface == 0 ? MaxRoadCrackOpacity : 200);
                case 12: return surface == 0 && value >= 0 && value <= 8;
                case 10: case 11: return value >= 0.1 && value <= 8;
                default: return false;
            }
        }

        private static double[] DefaultTextureSliders(int surface, int mode) => surface == 5 && mode == 1
            ? new double[] { 150, 200, 0, 10, 53 }
            : surface == 0
            ? new double[] { 100, 0, 0, 0, 0, 0, 100, 100, 100, 50, 1, 1, 1 }
            : surface < 3
            ? new double[] { 100, 0, 0, 0, 0, 0, 100, 100, 100, 50, 1 }
            : new double[] { 100, 0, 0, 0, 0 };

        private static string SerializeTextureProfiles(double[][] profiles)
        {
            var modes = new string[profiles.Length];
            for (int mode = 0; mode < profiles.Length; mode++)
            {
                var values = new string[profiles[mode].Length];
                for (int i = 0; i < values.Length; i++)
                    values[i] = profiles[mode][i].ToString("R", CultureInfo.InvariantCulture);
                modes[mode] = string.Join(",", values);
            }
            return string.Join(";", modes);
        }

        private void ClearTextureModeProfiles(int surface)
        {
            if (surface == 0)
            {
                m_RoadCrackProfiles = null;
                RoadCrackModeProfiles = "";
            }
            m_TextureModeProfiles[surface] = null;
            SetSerializedProfiles(surface, "");
        }

        private void ClearAllTextureModeProfiles()
        {
            for (int surface = 0; surface < 8; surface++) ClearTextureModeProfiles(surface);
        }

        private int GetTextureMode(int surface)
        {
            switch (surface)
            {
                case 0: return RoadAsphaltDetail;
                case 1: return SidewalkDetail;
                case 2: return AlleySidewalkDetail;
                case 3: return RoadGravelDetail;
                case 4: return GravelShoulderDetail;
                case 5: return RailGravelDetail;
                case 6: return GrassDetail;
                case 7: return FootpathDetail;
                default: throw new ArgumentOutOfRangeException(nameof(surface));
            }
        }

        private string GetSerializedProfiles(int surface)
        {
            switch (surface)
            {
                case 0: return RoadAsphaltModeProfiles;
                case 1: return SidewalkModeProfiles;
                case 2: return AlleySidewalkModeProfiles;
                case 3: return RoadGravelModeProfiles;
                case 4: return GravelShoulderModeProfiles;
                case 5: return RailGravelModeProfiles;
                case 6: return GrassModeProfiles;
                case 7: return FootpathModeProfiles;
                default: throw new ArgumentOutOfRangeException(nameof(surface));
            }
        }

        private void SetSerializedProfiles(int surface, string value)
        {
            switch (surface)
            {
                case 0: RoadAsphaltModeProfiles = value; break;
                case 1: SidewalkModeProfiles = value; break;
                case 2: AlleySidewalkModeProfiles = value; break;
                case 3: RoadGravelModeProfiles = value; break;
                case 4: GravelShoulderModeProfiles = value; break;
                case 5: RailGravelModeProfiles = value; break;
                case 6: GrassModeProfiles = value; break;
                case 7: FootpathModeProfiles = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(surface));
            }
        }

        private double[] ReadTextureSliders(int surface)
        {
            switch (surface)
            {
                case 0: return new double[] { RoadUvSizePercent, RoadDepthStrengthPercent, RoadUvOffsetX, RoadUvOffsetY, RoadUvRotation, RoadCrackCount, RoadCrackSizePercent, RoadCrackWidthPercent, RoadCrackOpacityPercent, RoadCrackRandomnessPercent, RoadTextureTiling, ParkingTextureScaleFactor, ParkingDepthStrengthFactor };
                case 1: return new double[] { SidewalkUvSizePercent, SidewalkDepthStrengthPercent, SidewalkUvOffsetX, SidewalkUvOffsetY, SidewalkUvRotation, SidewalkCrackCount, SidewalkCrackSizePercent, SidewalkCrackWidthPercent, SidewalkCrackOpacityPercent, SidewalkCrackRandomnessPercent, SidewalkTextureTiling };
                case 2: return new double[] { AlleySidewalkUvSizePercent, AlleySidewalkDepthStrengthPercent, AlleySidewalkUvOffsetX, AlleySidewalkUvOffsetY, AlleySidewalkUvRotation, AlleySidewalkCrackCount, AlleySidewalkCrackSizePercent, AlleySidewalkCrackWidthPercent, AlleySidewalkCrackOpacityPercent, AlleySidewalkCrackRandomnessPercent, AlleySidewalkTextureTiling };
                case 3: return new double[] { RoadGravelUvSizePercent, RoadGravelDepthStrengthPercent, RoadGravelUvOffsetX, RoadGravelUvOffsetY, RoadGravelUvRotation };
                case 4: return new double[] { GravelShoulderUvSizePercent, GravelShoulderDepthStrengthPercent, GravelShoulderUvOffsetX, GravelShoulderUvOffsetY, GravelShoulderUvRotation };
                case 5: return new double[] { RailGravelUvSizePercent, RailGravelDepthStrengthPercent, RailGravelUvOffsetX, RailGravelUvOffsetY, RailGravelUvRotation };
                case 6: return new double[] { GrassUvSizePercent, GrassDepthStrengthPercent, GrassUvOffsetX, GrassUvOffsetY, GrassUvRotation };
                case 7: return new double[] { FootpathUvSizePercent, FootpathDepthStrengthPercent, FootpathUvOffsetX, FootpathUvOffsetY, FootpathUvRotation };
                default: throw new ArgumentOutOfRangeException(nameof(surface));
            }
        }

        private void WriteTextureSliders(int surface, double[] values)
        {
            switch (surface)
            {
                case 0:
                    RoadUvSizePercent = (int)values[0];
                    RoadDepthStrengthPercent = (int)values[1];
                    RoadUvOffsetX = (int)values[2];
                    RoadUvOffsetY = (int)values[3];
                    RoadUvRotation = (int)values[4];
                    RoadCrackCount = (int)values[5];
                    RoadCrackSizePercent = (int)values[6];
                    RoadCrackWidthPercent = (int)values[7];
                    RoadCrackOpacityPercent = (int)values[8];
                    RoadCrackRandomnessPercent = (int)values[9];
                    RoadTextureTiling = (float)values[10];
                    ParkingTextureScaleFactor = (float)values[11];
                    ParkingDepthStrengthFactor = (float)values[12];
                    break;
                case 1:
                    SidewalkUvSizePercent = (int)values[0];
                    SidewalkDepthStrengthPercent = (int)values[1];
                    SidewalkUvOffsetX = (int)values[2];
                    SidewalkUvOffsetY = (int)values[3];
                    SidewalkUvRotation = (int)values[4];
                    SidewalkCrackCount = (int)values[5];
                    SidewalkCrackSizePercent = (int)values[6];
                    SidewalkCrackWidthPercent = (int)values[7];
                    SidewalkCrackOpacityPercent = (int)values[8];
                    SidewalkCrackRandomnessPercent = (int)values[9];
                    SidewalkTextureTiling = (float)values[10];
                    break;
                case 2:
                    AlleySidewalkUvSizePercent = (int)values[0];
                    AlleySidewalkDepthStrengthPercent = (int)values[1];
                    AlleySidewalkUvOffsetX = (int)values[2];
                    AlleySidewalkUvOffsetY = (int)values[3];
                    AlleySidewalkUvRotation = (int)values[4];
                    AlleySidewalkCrackCount = (int)values[5];
                    AlleySidewalkCrackSizePercent = (int)values[6];
                    AlleySidewalkCrackWidthPercent = (int)values[7];
                    AlleySidewalkCrackOpacityPercent = (int)values[8];
                    AlleySidewalkCrackRandomnessPercent = (int)values[9];
                    AlleySidewalkTextureTiling = (float)values[10];
                    break;
                case 3:
                    RoadGravelUvSizePercent = (int)values[0];
                    RoadGravelDepthStrengthPercent = (int)values[1];
                    RoadGravelUvOffsetX = (int)values[2];
                    RoadGravelUvOffsetY = (int)values[3];
                    RoadGravelUvRotation = (int)values[4];
                    break;
                case 4:
                    GravelShoulderUvSizePercent = (int)values[0];
                    GravelShoulderDepthStrengthPercent = (int)values[1];
                    GravelShoulderUvOffsetX = (int)values[2];
                    GravelShoulderUvOffsetY = (int)values[3];
                    GravelShoulderUvRotation = (int)values[4];
                    break;
                case 5:
                    RailGravelUvSizePercent = (int)values[0];
                    RailGravelDepthStrengthPercent = (int)values[1];
                    RailGravelUvOffsetX = (int)values[2];
                    RailGravelUvOffsetY = (int)values[3];
                    RailGravelUvRotation = (int)values[4];
                    break;
                case 6:
                    GrassUvSizePercent = (int)values[0];
                    GrassDepthStrengthPercent = (int)values[1];
                    GrassUvOffsetX = (int)values[2];
                    GrassUvOffsetY = (int)values[3];
                    GrassUvRotation = (int)values[4];
                    break;
                case 7:
                    FootpathUvSizePercent = (int)values[0];
                    FootpathDepthStrengthPercent = (int)values[1];
                    FootpathUvOffsetX = (int)values[2];
                    FootpathUvOffsetY = (int)values[3];
                    FootpathUvRotation = (int)values[4];
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(surface));
            }
        }
    }
}



