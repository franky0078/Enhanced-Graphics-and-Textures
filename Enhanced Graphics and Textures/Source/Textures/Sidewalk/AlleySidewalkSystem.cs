using System;
using UnityEngine;

namespace Advanced_Graphics
{
    public partial class AlleySidewalkSurfaceSystem : SidewalkSurfaceSystem
    {
        public static string AlleyTextureFolder => TextureStorage.AlleySidewalk;
        protected override string TextureFolder => AlleyTextureFolder;
        protected override string TextureFileName => "AlleySidewalk_BaseColor.png";
        protected override string SurfaceName => "Alley sidewalk surface";

        protected override Texture2D LoadOriginalColor()
        {
            var source = Mod.AlleySidewalkAtlas?.FindOriginalColorAtlas();
            if (source == null) return null;
            int size = Mathf.Max(1, Mathf.RoundToInt(source.width * 0.23f));
            var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Texture2D tile = null;
            try
            {
                UnityEngine.Graphics.Blit(source, target);
                RenderTexture.active = target;
                tile = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
                tile.ReadPixels(new Rect(0, 0, size, size), 0, 0, false);
                tile.Apply(false, false);
                return tile;
            }
            catch { if (tile != null) UnityEngine.Object.Destroy(tile); throw; }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
        }
    }

    public partial class AlleySidewalkAtlasSystem : SidewalkRoadAtlasSystem
    {
        protected override SidewalkSurfaceSystem Surface => Mod.AlleySidewalk;
        protected override bool IsAlleyAtlas => true;
    }
}
