using System;
using System.Globalization;
using Game.Settings;
using UnityEngine;

namespace Advanced_Graphics
{
    public sealed partial class Setting
    {
        [SettingsUIHidden]
        public int RoadFilterColor { get; set; } = 0xffffff;

        [SettingsUIHidden]
        public int RoadFilterOpacityPercent { get; set; } = 100;

        [SettingsUIHidden]
        public int SidewalkFilterColor { get; set; } = 0xffffff;

        [SettingsUIHidden]
        public int SidewalkFilterOpacityPercent { get; set; } = 100;

        [SettingsUIHidden]
        public int AlleySidewalkFilterColor { get; set; } = 0xffffff;

        [SettingsUIHidden]
        public int AlleySidewalkFilterOpacityPercent { get; set; } = 100;

        [SettingsUIHidden]
        public int RoadGravelFilterColor { get; set; } = 0xffffff;

        [SettingsUIHidden]
        public int RoadGravelFilterOpacityPercent { get; set; } = 100;

        [SettingsUIHidden]
        public int GravelShoulderFilterColor { get; set; } = 0xffffff;

        [SettingsUIHidden]
        public int GravelShoulderFilterOpacityPercent { get; set; } = 100;

        [SettingsUIHidden]
        public int GrassFilterColor { get; set; } = 0xffffff;

        [SettingsUIHidden]
        public int FootpathFilterColor { get; set; } = 0xffffff;

        [SettingsUIHidden]
        public int GrassFilterOpacityPercent { get; set; } = 100;

        [SettingsUIHidden]
        public int FootpathFilterOpacityPercent { get; set; } = 100;

        [SettingsUIHidden]
        public int RailFilterColor { get; set; } = 0xffffff;

        [SettingsUIHidden]
        public int RailFilterOpacityPercent { get; set; } = 100;

        [SettingsUIHidden]
        public float ColorWindowX { get; set; } = -1f;

        [SettingsUIHidden]
        public float ColorWindowY { get; set; } = -1f;

        [SettingsUIHidden]
        public string FilterModeProfiles { get; set; } = "";

        [SettingsUIHidden]
        public int SidewalkConcreteBaseColorVersion { get; set; }

        private bool m_FilterProfilesReady;
        private readonly int[][][] m_FilterProfiles = new int[8][][];

        private static int[][] NeutralFilterProfiles() => new[] {
            new[] { 0xffffff, 0 }, new[] { 0xffffff, 0 }, new[] { 0xffffff, 0 } };

        private int[] ReadSurfaceFilter(int surface)
        {
            switch (surface)
            {
                case 0: return new[] { RoadFilterColor, RoadFilterOpacityPercent };
                case 1: return new[] { SidewalkFilterColor, SidewalkFilterOpacityPercent };
                case 2: return new[] { AlleySidewalkFilterColor, AlleySidewalkFilterOpacityPercent };
                case 3: return new[] { RoadGravelFilterColor, RoadGravelFilterOpacityPercent };
                case 4: return new[] { GravelShoulderFilterColor, GravelShoulderFilterOpacityPercent };
                case 5: return new[] { RailFilterColor, RailFilterOpacityPercent };
                case 6: return new[] { GrassFilterColor, GrassFilterOpacityPercent };
                case 7: return new[] { FootpathFilterColor, FootpathFilterOpacityPercent };
                default: throw new ArgumentOutOfRangeException(nameof(surface));
            }
        }

        private void WriteSurfaceFilter(int surface, int[] values)
        {
            switch (surface)
            {
                case 0: RoadFilterColor = values[0]; RoadFilterOpacityPercent = values[1]; break;
                case 1: SidewalkFilterColor = values[0]; SidewalkFilterOpacityPercent = values[1]; break;
                case 2: AlleySidewalkFilterColor = values[0]; AlleySidewalkFilterOpacityPercent = values[1]; break;
                case 3: RoadGravelFilterColor = values[0]; RoadGravelFilterOpacityPercent = values[1]; break;
                case 4: GravelShoulderFilterColor = values[0]; GravelShoulderFilterOpacityPercent = values[1]; break;
                case 5: RailFilterColor = values[0]; RailFilterOpacityPercent = values[1]; break;
                case 6: GrassFilterColor = values[0]; GrassFilterOpacityPercent = values[1]; break;
                case 7: FootpathFilterColor = values[0]; FootpathFilterOpacityPercent = values[1]; break;
                default: throw new ArgumentOutOfRangeException(nameof(surface));
            }
        }

        private void InitializeFilterModeProfiles()
        {
            var surfaces = (FilterModeProfiles ?? "").Split('|');
            for (int surface = 0; surface < m_FilterProfiles.Length; surface++)
            {
                var profiles = NeutralFilterProfiles();
                bool valid = surfaces.Length == m_FilterProfiles.Length;
                var modes = valid ? surfaces[surface].Split(';') : new string[0];
                valid = valid && modes.Length == 3;
                for (int mode = 0; valid && mode < 3; mode++)
                {
                    var parts = modes[mode].Split(',');
                    int color = 0, opacity = 0;
                    valid = parts.Length == 2 &&
                        int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out color) &&
                        int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out opacity) &&
                        color >= 0 && color <= 0xffffff && opacity >= 0 && opacity <= 100;
                    if (valid) profiles[mode] = new[] { color, opacity };
                }
                // Old versions only saved one live filter per surface: keep it for the active mode.
                if (!valid)
                {
                    profiles = NeutralFilterProfiles();
                    profiles[GetTextureMode(surface)] = ReadSurfaceFilter(surface);
                }
                m_FilterProfiles[surface] = profiles;
                WriteSurfaceFilter(surface, profiles[GetTextureMode(surface)]);
            }

            if (SidewalkConcreteBaseColorVersion < 1)
            {
                m_FilterProfiles[1][1] = new[] { 0xffffff, 0 };
                if (SidewalkDetail == 1) WriteSurfaceFilter(1, m_FilterProfiles[1][1]);
                SidewalkConcreteBaseColorVersion = 1;
            }
            m_FilterProfilesReady = true;
            SaveFilterModeProfiles();
        }

        private void SaveFilterModeProfiles()
        {
            var surfaces = new string[m_FilterProfiles.Length];
            for (int surface = 0; surface < surfaces.Length; surface++)
            {
                var modes = new string[3];
                for (int mode = 0; mode < 3; mode++)
                {
                    var pair = m_FilterProfiles[surface][mode];
                    modes[mode] = pair[0].ToString(CultureInfo.InvariantCulture) + "," +
                        pair[1].ToString(CultureInfo.InvariantCulture);
                }
                surfaces[surface] = string.Join(";", modes);
            }
            FilterModeProfiles = string.Join("|", surfaces);
        }

        private void CaptureFilterModeProfiles()
        {
            if (!m_FilterProfilesReady || m_RestoringTextureMode) return;
            for (int surface = 0; surface < m_FilterProfiles.Length; surface++)
                m_FilterProfiles[surface][GetTextureMode(surface)] = ReadSurfaceFilter(surface);
            SaveFilterModeProfiles();
        }

        private void RestoreFilterModeProfile(int surface, int mode)
        {
            if (m_FilterProfilesReady) WriteSurfaceFilter(surface, m_FilterProfiles[surface][mode]);
        }

        private void ClearFilterModeProfiles(int surface)
        {
            if (!m_FilterProfilesReady)
            {
                m_FilterProfiles[surface] = null;
                FilterModeProfiles = "";
                return;
            }
            m_FilterProfiles[surface] = NeutralFilterProfiles();
            WriteSurfaceFilter(surface, m_FilterProfiles[surface][GetTextureMode(surface)]);
            SaveFilterModeProfiles();
        }

        private static int FilterSurfaceIndex(string surface)
        {
            switch (surface)
            {
                case "road": return 0;
                case "sidewalk": return 1;
                case "alley": return 2;
                case "gravel": return 3;
                case "shoulder": return 4;
                case "rail": return 5;
                case "grass": return 6;
                case "footpath": return 7;
                default: return -1;
            }
        }

        internal bool SetFilterColor(string surface, int color, int opacityPercent, int expectedMode = -1)
        {
            int index = FilterSurfaceIndex(surface);
            if (index < 0 || (expectedMode >= 0 && expectedMode != GetTextureMode(index))) return false;
            color = System.Math.Max(0, System.Math.Min(0xffffff, color));
            opacityPercent = System.Math.Max(0, System.Math.Min(100, opacityPercent));
            switch (surface)
            {
                case "road": RoadFilterColor = color; RoadFilterOpacityPercent = opacityPercent; break;
                case "sidewalk": SidewalkFilterColor = color; SidewalkFilterOpacityPercent = opacityPercent; break;
                case "alley": AlleySidewalkFilterColor = color; AlleySidewalkFilterOpacityPercent = opacityPercent; break;
                case "gravel": RoadGravelFilterColor = color; RoadGravelFilterOpacityPercent = opacityPercent; break;
                case "shoulder": GravelShoulderFilterColor = color; GravelShoulderFilterOpacityPercent = opacityPercent; break;
                case "grass": GrassFilterColor = color; GrassFilterOpacityPercent = opacityPercent; break;
                case "footpath": FootpathFilterColor = color; FootpathFilterOpacityPercent = opacityPercent; break;
                case "rail": RailFilterColor = color; RailFilterOpacityPercent = opacityPercent; break;
                default: return false;
            }
            CaptureFilterModeProfiles();
            return true;
        }
    }

    internal static class TextureColorFilter
    {
        internal static bool IsActive(int color, int opacityPercent) => color != 0xffffff && opacityPercent > 0;

        // Blend the RGB multiplier with neutral. Never alter the material's alpha masks.
        internal static Color32 Apply(Color32 pixel, int color, int opacityPercent)
        {
            int neutral = 255 * (100 - opacityPercent);
            pixel.r = (byte)((pixel.r * (neutral + ((color >> 16) & 255) * opacityPercent) + 12750) / 25500);
            pixel.g = (byte)((pixel.g * (neutral + ((color >> 8) & 255) * opacityPercent) + 12750) / 25500);
            pixel.b = (byte)((pixel.b * (neutral + (color & 255) * opacityPercent) + 12750) / 25500);
            return pixel;
        }

        internal static void Apply(Texture2D texture, int color, int opacityPercent)
        {
            if (!IsActive(color, opacityPercent)) return;
            var pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Apply(pixels[i], color, opacityPercent);
            texture.SetPixels32(pixels);
        }
    }
}


