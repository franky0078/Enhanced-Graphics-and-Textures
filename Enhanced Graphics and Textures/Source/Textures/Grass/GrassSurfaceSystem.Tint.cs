using System.Collections.Generic;
using UnityEngine;

namespace Advanced_Graphics
{
    public partial class GrassSurfaceSystem
    {
        // Depth-only updates read the existing color pixels again. Keep their
        // CPU copy alive until the shared worker destroys/replaces the texture.
        protected override bool KeepTileReadable => true;

        private sealed class GrassTint
        {
            public Color Original;
            public Color Applied;
        }

        private readonly Dictionary<Material, GrassTint> m_GrassTints =
            new Dictionary<Material, GrassTint>();

        protected override void OnSurfaceMaterialApplied(Material material)
        {
            if (CurrentLevel == 0 || !material.HasProperty("_BaseColor"))
                return;

            Color current = material.GetColor("_BaseColor");
            if (!m_GrassTints.TryGetValue(material, out var tint))
            {
                tint = new GrassTint
                {
                    Original = current,
                    Applied = current
                };
                m_GrassTints.Add(material, tint);
            }

            if (!current.Equals(tint.Original) && !current.Equals(tint.Applied))
                return;

            tint.Applied = new Color(1f, 1f, 1f, tint.Original.a);
            material.SetColor("_BaseColor", tint.Applied);
        }

        protected override void OnSurfaceMaterialRestored(Material material)
        {
            if (!m_GrassTints.TryGetValue(material, out var tint))
                return;

            if (material.HasProperty("_BaseColor") &&
                material.GetColor("_BaseColor").Equals(tint.Applied))
            {
                material.SetColor("_BaseColor", tint.Original);
            }

            m_GrassTints.Remove(material);
        }
    }
}
