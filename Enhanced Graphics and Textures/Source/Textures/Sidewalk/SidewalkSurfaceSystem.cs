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
        protected override int MinimumUvSize => GetType() == typeof(SidewalkSurfaceSystem) ? 10 : base.MinimumUvSize;
        // Generated 100% matches the supplied custom texture at 50% UV size.
        protected override float UvSizeMultiplier => GetType() == typeof(SidewalkSurfaceSystem) && CurrentLevel == 1 ? 0.5f : 1f;

        protected override Texture2D BuildSurfaceNormal(Texture2D color, int depth, int uvSize, byte[] crackMask) =>
            base.BuildSurfaceNormal(color, depth, Mathf.RoundToInt(uvSize * UvSizeMultiplier), crackMask);

        protected override void ApplyIntrinsicColor(Texture2D color)
        {
            if (GetType() == typeof(SidewalkSurfaceSystem) && CurrentLevel == 1)
                TextureColorFilter.Apply(color, 0x8c8a8d, 100);
        }

        protected override void EncodeNormalPixels(Color32[] pixels)
        {
            // Keep RGB for the atlas builders, with the same extreme-depth guard as asphalt.
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = RoadNormalEncoding.Stabilize(pixels[i]);
        }

        protected override bool ReuseColorForDepthChanges => false;
        // Bundled generated concrete uses the same atlas sampling as a custom PNG.
        internal bool UsesCustomTexture => CurrentLevel != 0;
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
            const string resource = "Advanced_Graphics.SidewalkConcrete_BaseColor.png";
            Texture2D texture = null;
            try
            {
                using (var stream = typeof(SidewalkSurfaceSystem).Assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null) throw new InvalidDataException("Missing embedded texture: " + resource);
                    using (var buffer = new MemoryStream())
                    {
                        stream.CopyTo(buffer);
                        texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                        if (!texture.LoadImage(buffer.ToArray(), false) || texture.width != 1254 || texture.height != 1254)
                            throw new InvalidDataException("Invalid embedded sidewalk concrete texture: " + resource);
                    }
                }
                texture.name = "Advanced Graphics Generated Sidewalk Concrete Color";
                texture.wrapMode = TextureWrapMode.Repeat;
                return texture;
            }
            catch
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
                throw;
            }
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



