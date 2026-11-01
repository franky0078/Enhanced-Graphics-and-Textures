using UnityEngine;

namespace Advanced_Graphics
{
    // Replaces only the RoadEU world-space asphalt detail maps.
    public partial class RoadAsphaltSystem : RoadSurfaceSystemBase
    {
        protected override void EncodeNormalPixels(Color32[] pixels)
        {
            // Keep the RGB layout consumed by the world-space material and atlas builder.
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = RoadNormalEncoding.Stabilize(pixels[i]);
        }
    }
}
