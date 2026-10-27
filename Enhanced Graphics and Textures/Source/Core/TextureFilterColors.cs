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

        internal bool SetFilterColor(string surface, int color, int opacityPercent)
        {
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
