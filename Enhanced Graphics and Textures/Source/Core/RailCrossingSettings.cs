using System;
using System.Diagnostics;
using System.IO;
using Game.Settings;

namespace Advanced_Graphics
{
    public sealed partial class Setting
    {
        internal const int CrossingThrough = 0;
        internal const int CrossingCover = 1;
        internal const int CrossingCustom = 2;

        [SettingsUIHidden] public int RailCrossingMode { get; set; } = CrossingCover;
        [SettingsUIHidden] public int RailCrossingDepth { get; set; } = 100;
        [SettingsUIHidden] public int RailCrossingGloss { get; set; } = 10;
        [SettingsUIHidden] public int RailCrossingUv { get; set; } = 100;
        [SettingsUIHidden] public int RailCrossingOffsetX { get; set; }
        [SettingsUIHidden] public int RailCrossingOffsetY { get; set; }
        [SettingsUIHidden] public int RailCrossingRotation { get; set; }
        [SettingsUIHidden] public int RailCrossingColor { get; set; } = 0xffffff;
        [SettingsUIHidden] public int RailCrossingColorOpacity { get; set; } = 100;

        internal void ResetRailCrossing()
        {
            RailCrossingMode = CrossingCover;
            RailCrossingDepth = RailCrossingUv = RailCrossingColorOpacity = 100;
            RailCrossingGloss = 10;
            RailCrossingOffsetX = RailCrossingOffsetY = RailCrossingRotation = 0;
            RailCrossingColor = 0xffffff;
        }

        internal void OpenRailCrossingFolder()
        {
            try
            {
                Directory.CreateDirectory(TextureStorage.RailCrossing);
                Process.Start(new ProcessStartInfo { FileName = TextureStorage.RailCrossing, UseShellExecute = true });
            }
            catch (Exception ex) { Mod.log.Error(ex, "Could not open rail crossing texture folder."); }
        }
    }
}
