using UnityEngine;

/// <summary>
/// Provides a few utilities for modifying or creating colors.
/// </summary>
public static class ColorHelper {

    /// <summary>Modifies the alpha component of this color, and returns it.</summary>
    public static Color WithAlpha(this Color color, float alpha) {
        color.a = alpha;
        return color;
    }

    /// <summary>
    /// Multiplies two colors component-wise, but keeps the original alpha. Does not modify this color.
    /// </summary>
    public static Color MultiplyRGB(this Color a, Color b) {
        return new Color(a.r*b.r, a.g*b.g, a.b*b.b, a.a);
    }

    /// <summary>
    /// Multiplies all components of this color by a factor, except alpha. Does not modify this color.
    /// </summary>
    public static Color MultiplyRGB(this Color a, float b) {
        return new Color(a.r*b, a.g*b, a.b*b, a.a);
    }

    /// <summary>
    /// Clamps each floating point component of this color to the range 0-1. Does not modify this color.
    /// </summary>
    public static Color Clamp(this Color a) {
        return new Color(Mathf.Clamp01(a.r), Mathf.Clamp01(a.g), Mathf.Clamp01(a.b), Mathf.Clamp01(a.a));
    }

    /// <summary>
    /// Interpolates this color with another. Does not modify this color.
    /// </summary>
    public static Color LerpWith(this Color a, Color b, float amount, bool lerpAlpha = true) {
        Color ret = Vector4.Lerp(a, b, amount);
        return lerpAlpha ? ret : ret.WithAlpha(a.a);
    }
    
    /// <summary>
    /// Normalizes the RGB values of this color like a vector.
    /// </summary>
    /// <param name="a"></param>
    /// <returns></returns>
    public static Color Normalize(this Color a) { 
        Vector3 rgb = new(a.r, a.g, a.b);
        rgb.Normalize();
        return new Color(rgb.x, rgb.y, rgb.z, a.a);
    }
}
