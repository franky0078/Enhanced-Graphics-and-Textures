using UnityEngine;

namespace Advanced_Graphics
{
    // Replaces only the RoadEU world-space asphalt detail maps.
    public partial class RoadAsphaltSystem : RoadSurfaceSystemBase
    {
        protected override bool CacheSourceColor => TexturePerformance.CacheAsphaltSource;

        protected override bool KeepTileReadable => TexturePerformance.KeepAsphaltReadable;

        protected override void EncodeNormalPixels(Color32[] pixels)
        {
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = RoadNormalEncoding.Stabilize(pixels[i]);
        }
    }
}
