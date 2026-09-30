using UnityEngine;

namespace Advanced_Graphics
{
    // Share the road's generated gravel color, authored normal and native
    // resolution. Only the target material and custom PNG folder differ.
    public partial class GravelShoulderSurfaceSystem : RoadGravelSystem
    {
        protected override string TextureFolder => TextureStorage.GravelShoulder;
        protected override string TextureFileName => "GravelShoulder_BaseColor.png";
        protected override string SurfaceName => "Gravel sidewalk";
        protected override bool ApplyToSourceMaterials => false;
        protected override bool KeepTileReadable => true;
        protected override bool CacheSourceColor => true;
        // Depth is applied in the shoulder's worker, without rebuilding tiles.
        protected override float AuthoredNormalStrength => 1f;
        protected override Texture OriginalNormalSource => Mod.RoadGravel?.OriginalGravelNormal;

        protected override Texture2D LoadOriginalColor()
        {
            return Mod.RoadGravel?.CopyOriginalGravelColor() ?? base.LoadOriginalColor();
        }

        protected override void EncodeNormalPixels(Color32[] pixels) { }
    }
}
