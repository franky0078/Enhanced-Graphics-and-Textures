using UnityEngine;

namespace Advanced_Graphics
{
    internal static class RoadNormalEncoding
    {
        // Leave room for RGBA32 quantization and filtering. Near-tangent normals can
        // otherwise decode outside the unit disk (or with an effectively zero Z).
        private const float MinimumZ = 0.15f;
        private const float MaximumXY2 = 1f - MinimumZ * MinimumZ;

        internal static Color32 Stabilize(Color32 pixel)
        {
            float x = pixel.r / 127.5f - 1f;
            float y = pixel.g / 127.5f - 1f;
            float xy2 = x * x + y * y;
            if (xy2 <= MaximumXY2) return pixel;
            float scale = Mathf.Sqrt(MaximumXY2 / xy2);
            x *= scale;
            y *= scale;
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt((x + 1f) * 127.5f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt((y + 1f) * 127.5f), 0, 255),
                (byte)Mathf.RoundToInt((MinimumZ + 1f) * 127.5f), pixel.a);
        }
    }
}
