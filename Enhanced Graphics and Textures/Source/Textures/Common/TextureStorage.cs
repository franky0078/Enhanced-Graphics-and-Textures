using System;
using System.IO;
using UnityEngine;

namespace Advanced_Graphics
{
    internal static class TextureStorage
    {
        private static string Root => Path.Combine(Application.persistentDataPath, "ModsData", "Enhanced_Graphics_and_Textures", "Textures");
        internal static string Road => Path.Combine(Root, "Road");
        internal static string Sidewalk => Path.Combine(Root, "Sidewalk");
        internal static string AlleySidewalk => Path.Combine(Root, "AlleySidewalk");
        internal static string Rail => Path.Combine(Root, "Rail");
        internal static string RoadGravel => Path.Combine(Root, "Gravel");
        internal static string GravelShoulder => Path.Combine(Root, "GravelSidewalk");
        internal static void EnsureDirectories()
        {
            var folders = new[] { Road, Sidewalk, AlleySidewalk, Rail, RoadGravel, GravelShoulder };
            foreach (string folder in folders)
            {
                try { Directory.CreateDirectory(folder); }
                catch (Exception ex) { Mod.log.Error(ex, "Could not create texture folder."); }
            }
        }
    }
}
