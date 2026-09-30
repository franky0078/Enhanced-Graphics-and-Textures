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

        internal static void Migrate()
        {
            var oldFolders = new[] { "RoadTextures", "SidewalkTextures", "AlleySidewalkTextures", "RailTextures", "RoadGravelTextures", "GravelShoulderTextures" };
            var destinations = new[] { Road, Sidewalk, AlleySidewalk, Rail, RoadGravel, GravelShoulder };
            var roots = new[] {
                Path.Combine(Application.persistentDataPath, "ModsData", "Advanced_Graphics"),
                Path.Combine(Application.persistentDataPath, "Advanced_Graphics")
            };
            for (int i = 0; i < destinations.Length; i++)
            {
                try { Directory.CreateDirectory(destinations[i]); }
                catch (Exception ex) { Mod.log.Error(ex, "Could not create texture folder."); continue; }
                CopyFolderIfMissing(Path.Combine(Application.persistentDataPath, "ModsData", "Advanced_Graphics_and_Textures", "Textures", Path.GetFileName(destinations[i])), destinations[i]);
                foreach (string oldRoot in roots)
                    CopyFolderIfMissing(Path.Combine(oldRoot, oldFolders[i]), destinations[i]);
            }
            string alleyFile = Path.Combine(AlleySidewalk, "AlleySidewalk_BaseColor.png");
            string sidewalkFile = Path.Combine(Sidewalk, "Sidewalk_BaseColor.png");
            if (!File.Exists(alleyFile) && File.Exists(sidewalkFile))
            {
                try { File.Copy(sidewalkFile, alleyFile); }
                catch (Exception ex) { Mod.log.Error(ex, "Could not seed alley sidewalk texture."); }
            }
        }

        private static void CopyFolderIfMissing(string source, string destination)
        {
            try
            {
                if (!Directory.Exists(source)) return;
                foreach (string file in Directory.GetFiles(source, "*.png"))
                {
                    string target = Path.Combine(destination, Path.GetFileName(file));
                    if (!File.Exists(target)) File.Copy(file, target, false);
                }
            }
            catch (Exception ex) { Mod.log.Error(ex, "Could not migrate custom textures from " + source); }
        }
    }
}

