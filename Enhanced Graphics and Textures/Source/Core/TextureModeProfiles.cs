using System;
using System.Globalization;
using Game.Settings;

namespace Advanced_Graphics
{
    public sealed partial class Setting
    {
        // These strings are saved by the game's existing settings serializer.
        // Each surface stores three independent numeric profiles (Original/Generated/Custom).
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

        private bool m_TextureModeProfilesReady;
        private readonly double[][][] m_TextureModeProfiles = new double[6][][];

        // Called after LoadSettings and existing setting migrations. This keeps loading
        // order independent: mode setters must not swap values during deserialization.
        internal void InitializeTextureModeProfiles()
        {
            for (int surface = 0; surface < 6; surface++) EnsureTextureModeProfiles(surface);
            m_TextureModeProfilesReady = true;
            CaptureTextureModeProfiles();
        }

        private void SwitchTextureMode(int surface, ref int currentMode, int nextMode)
        {
            nextMode = Math.Max(0, Math.Min(2, nextMode));
            if (currentMode == nextMode) return;
            if (m_TextureModeProfilesReady && m_TextureModeProfiles[surface] != null)
            {
                m_TextureModeProfiles[surface][currentMode] = ReadTextureSliders(surface);
                WriteTextureSliders(surface, m_TextureModeProfiles[surface][nextMode]);
            }
            currentMode = nextMode;
            if (m_TextureModeProfilesReady && m_TextureModeProfiles[surface] != null)
                SetSerializedProfiles(surface, SerializeTextureProfiles(m_TextureModeProfiles[surface]));
        }

        private void CaptureTextureModeProfiles()
        {
            if (!m_TextureModeProfilesReady) return;
            for (int surface = 0; surface < 6; surface++)
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
                profiles[mode] = DefaultTextureSliders(surface);
                if (encoded.Length != 3) continue;
                var values = encoded[mode].Split(',');
                // Older asphalt profiles have eleven sliders. Append the new
                // parking factor without losing the three existing profiles.
                if (values.Length != profiles[mode].Length && !(surface == 0 && values.Length == 11)) continue;
                var parsed = DefaultTextureSliders(surface);
                bool valid = true;
                for (int i = 0; i < values.Length; i++)
                {
                    if (!double.TryParse(values[i], NumberStyles.Float, CultureInfo.InvariantCulture, out parsed[i]) ||
                        double.IsNaN(parsed[i]) || double.IsInfinity(parsed[i]) ||
                        !IsValidTextureSlider(i, parsed[i]))
                    { valid = false; break; }
                }
                if (valid) profiles[mode] = parsed;
            }
            // Current live values are authoritative when upgrading existing settings.
            profiles[GetTextureMode(surface)] = ReadTextureSliders(surface);
            m_TextureModeProfiles[surface] = profiles;
        }

        private static bool IsValidTextureSlider(int index, double value)
        {
            switch (index)
            {
                case 0: return value >= 50 && value <= 1000;
                case 1: return value >= 0 && value <= 1000;
                case 2: case 3: case 9: return value >= 0 && value <= 100;
                case 4: return value >= 0 && value <= 360;
                case 5: return value >= 0 && value <= 400;
                case 6: return value >= 25 && value <= 600;
                case 7: return value >= 25 && value <= 400;
                case 8: return value >= 0 && value <= 200;
                case 10: case 11: return value >= 0.1 && value <= 8;
                default: return false;
            }
        }

        private static double[] DefaultTextureSliders(int surface) => surface == 0
            ? new double[] { 100, 0, 0, 0, 0, 0, 100, 100, 100, 50, 1, 1 }
            : surface < 3
            ? new double[] { 100, 0, 0, 0, 0, 0, 100, 100, 100, 50, 1 }
            : new double[] { 100, 0, 0, 0, 0 };

        private static string SerializeTextureProfiles(double[][] profiles)
        {
            var modes = new string[3];
            for (int mode = 0; mode < 3; mode++)
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
            m_TextureModeProfiles[surface] = null;
            SetSerializedProfiles(surface, "");
        }

        private void ClearAllTextureModeProfiles()
        {
            for (int surface = 0; surface < 6; surface++) ClearTextureModeProfiles(surface);
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
                default: throw new ArgumentOutOfRangeException(nameof(surface));
            }
        }

        private double[] ReadTextureSliders(int surface)
        {
            switch (surface)
            {
                case 0: return new double[] { RoadUvSizePercent, RoadDepthStrengthPercent, RoadUvOffsetX, RoadUvOffsetY, RoadUvRotation, RoadCrackCount, RoadCrackSizePercent, RoadCrackWidthPercent, RoadCrackOpacityPercent, RoadCrackRandomnessPercent, RoadTextureTiling, ParkingTextureScaleFactor };
                case 1: return new double[] { SidewalkUvSizePercent, SidewalkDepthStrengthPercent, SidewalkUvOffsetX, SidewalkUvOffsetY, SidewalkUvRotation, SidewalkCrackCount, SidewalkCrackSizePercent, SidewalkCrackWidthPercent, SidewalkCrackOpacityPercent, SidewalkCrackRandomnessPercent, SidewalkTextureTiling };
                case 2: return new double[] { AlleySidewalkUvSizePercent, AlleySidewalkDepthStrengthPercent, AlleySidewalkUvOffsetX, AlleySidewalkUvOffsetY, AlleySidewalkUvRotation, AlleySidewalkCrackCount, AlleySidewalkCrackSizePercent, AlleySidewalkCrackWidthPercent, AlleySidewalkCrackOpacityPercent, AlleySidewalkCrackRandomnessPercent, AlleySidewalkTextureTiling };
                case 3: return new double[] { RoadGravelUvSizePercent, RoadGravelDepthStrengthPercent, RoadGravelUvOffsetX, RoadGravelUvOffsetY, RoadGravelUvRotation };
                case 4: return new double[] { GravelShoulderUvSizePercent, GravelShoulderDepthStrengthPercent, GravelShoulderUvOffsetX, GravelShoulderUvOffsetY, GravelShoulderUvRotation };
                case 5: return new double[] { RailGravelUvSizePercent, RailGravelDepthStrengthPercent, RailGravelUvOffsetX, RailGravelUvOffsetY, RailGravelUvRotation };
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
                default: throw new ArgumentOutOfRangeException(nameof(surface));
            }
        }
    }
}
