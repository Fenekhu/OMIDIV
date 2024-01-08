using UnityEngine;

public static class ColorHelper {
    public static Color WithAlpha(this Color color, float alpha) {
        color.a = alpha;
        return color;
    }
    public static Color MultiplyRGB(this Color a, Color b) {
        return new Color(a.r*b.r, a.g*b.g, a.b*b.b, a.a);
    }
    public static Color MultiplyRGB(this Color a, float b) {
        return new Color(a.r*b, a.g*b, a.b*b, a.a);
    }
    public static Color Clamp(this Color a) {
        return new Color(Mathf.Clamp01(a.r), Mathf.Clamp01(a.g), Mathf.Clamp01(a.b), Mathf.Clamp01(a.a));
    }
    public static Color LerpWith(this Color a, Color b, float amount, bool lerpAlpha = true) {
        Color ret = Vector4.Lerp(a, b, amount);
        return lerpAlpha ? ret : ret.WithAlpha(a.a);
    }
    public static Color Normalize(this Color a) { return Vector4.Normalize(a); }
}
