using UnityEngine;

namespace Aaris.MapReview
{
    /// <summary>
    /// Copy authored texture references AND their UV transforms when an
    /// imported Standard/OBJ material is converted to URP Lit once per type.
    /// Never reimport/duplicate large textures or allocate on car frames.
    /// </summary>
    internal static class AarisVehicleSurfaceMaps
    {
        internal static Texture Transfer(Material source, Material target,
            string sourceProperty, string destinationProperty)
        {
            if (source == null || target == null ||
                !source.HasProperty(sourceProperty) ||
                !target.HasProperty(destinationProperty)) return null;
            Texture texture = source.GetTexture(sourceProperty);
            if (texture == null) return null;
            target.SetTexture(destinationProperty, texture);
            // Imported car textures may be atlas tiles; dropping the source
            // offset/scale changes which paint/glass/trim regions appear.
            target.SetTextureScale(destinationProperty,
                source.GetTextureScale(sourceProperty));
            target.SetTextureOffset(destinationProperty,
                source.GetTextureOffset(sourceProperty));
            return texture;
        }

        internal static void TransferStrength(Material source, Material target,
            string property, float fallback = 1f, float maximum = 1f)
        {
            if (source == null || target == null ||
                !target.HasProperty(property)) return;
            float value = source.HasProperty(property)
                ? source.GetFloat(property) : fallback;
            // A damaged imported scalar should never poison URP shading.
            if (float.IsNaN(value) || float.IsInfinity(value)) value = fallback;
            target.SetFloat(property, Mathf.Clamp(value, 0f, maximum));
        }
    }
}
