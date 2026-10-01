using System;
using System.IO;
using UnityEngine;

namespace Advanced_Graphics
{
    public partial class SidewalkSurfaceSystem : RoadSurfaceSystemBase
    {
        public static string SidewalkTextureFolder => TextureStorage.Sidewalk;

        protected override string TextureFolder => SidewalkTextureFolder;
        protected override string TextureFileName => "Sidewalk_BaseColor.png";
        protected override string SurfaceName => "Sidewalk surface";
        protected override bool ApplyToSourceMaterials => false;
        protected override int MaximumTileResolution => 4096;
        protected override bool KeepTileReadable => true;
        protected override bool CacheSourceColor => true;
        protected override bool ReuseColorForDepthChanges => true;
        internal bool UsesCustomTexture => CurrentLevel == 2;
        protected override int SeamBlendDivisor => UsesCustomTexture ? 64 : 8;

        protected override Texture2D LoadOriginalColor()
        {
            var atlas = Mod.SidewalkAtlas?.FindOriginalColorAtlas();
            if (atlas == null) return null;
            int left = Mathf.RoundToInt(atlas.width * 318f / 1024f);
            int right = Mathf.RoundToInt(atlas.width * 759f / 1024f);
            int size = right - left;
            if (size <= 0 || atlas.height < size || atlas.width > 4096 || atlas.height > 4096) return null;
            var target = RenderTexture.GetTemporary(atlas.width, atlas.height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Texture2D tile = null;
            try
            {
                UnityEngine.Graphics.Blit(atlas, target);
                RenderTexture.active = target;
                tile = new Texture2D(size, size, TextureFormat.RGBA32, true, false);
                tile.ReadPixels(new Rect(left, 0, size, size), 0, 0, false);
                tile.Apply(false, false);
                return tile;
            }
            catch
            {
                if (tile != null) UnityEngine.Object.Destroy(tile);
                throw;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        protected override Texture2D GenerateNeutralAsphalt()
        {
            const int size = 1024;
            const int slab = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int column = x / slab, row = y / slab;
                uint hash = (uint)(column * 73856093 ^ row * 19349663);
                float variation = (int)(hash % 13) - 6;
                float grain = ((x * 17 + y * 31 + x * y * 3) % 9) - 4;
                bool joint = x % slab < 2 || y % slab < 2;
                byte shade = (byte)Mathf.Clamp((joint ? 73f : 110f) + variation + grain, 0f, 255f);
                pixels[y * size + x] = new Color32(shade, shade, shade, 255);
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        protected override bool MatchesColor(Texture texture) => Matches(texture, "BaseColor");
        protected override bool MatchesNormal(Texture texture) => Matches(texture, "Normal");
        protected override bool MatchesShader(Material material) => material.shader != null &&
            (Contains(material.shader.name, "NetComposition") || IsSidewalk(material.name));

        protected override bool TryGetOriginal(Material material, out string colorSlot, out string normalSlot,
            out Texture color, out Texture normal)
        {
            colorSlot = normalSlot = null;
            color = normal = null;
            string[] properties;
            try { properties = material.GetTexturePropertyNames(); }
            catch (Exception) { return false; }
            bool namedSidewalk = IsSidewalk(material.name);
            foreach (var property in properties)
            {
                if (!Contains(property, "Worldspace") ||
                    (!Contains(property, "Albedo") && !Contains(property, "BaseColor"))) continue;
                Texture candidate = material.GetTexture(property);
                if (candidate == null || candidate.name.StartsWith("Advanced Graphics", StringComparison.Ordinal) ||
                    candidate.width != candidate.height || candidate.width > 4096 ||
                    (!IsSidewalk(candidate.name) && !namedSidewalk)) continue;
                colorSlot = property;
                color = candidate;
                break;
            }
            if (colorSlot == null) return false;
            foreach (var property in properties)
            {
                if (!Contains(property, "Worldspace") || !Contains(property, "Normal")) continue;
                normalSlot = property;
                normal = material.GetTexture(property);
                break;
            }
            return true;
        }

        private static bool IsSidewalk(string name) => Contains(name, "Sidewalk") ||
            Contains(name, "Pavement") || Contains(name, "Footpath") || Contains(name, "Pedestrian");

        private static bool Contains(string text, string term) => text != null &&
            text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool Matches(Texture texture, string map)
        {
            if (texture == null) return false;
            var name = texture.name;
            return name.IndexOf(map, StringComparison.OrdinalIgnoreCase) >= 0 &&
                (name.IndexOf("Sidewalk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("Pavement", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("Footpath", StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
