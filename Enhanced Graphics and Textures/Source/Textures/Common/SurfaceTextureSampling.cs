using UnityEngine;

namespace Advanced_Graphics
{
    internal static class SurfaceTextureSampling
    {
        internal static void Apply(Texture texture)
        {
            if (texture == null) return;
            float bias = Mod.ActiveSetting?.SurfaceTextureMipBias ?? 0f;
            if (texture.filterMode != FilterMode.Trilinear) texture.filterMode = FilterMode.Trilinear;
            if (texture.anisoLevel != 16) texture.anisoLevel = 16;
            if (!Mathf.Approximately(texture.mipMapBias, bias)) texture.mipMapBias = bias;
        }
    }
}
