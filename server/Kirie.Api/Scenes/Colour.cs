using System.Globalization;

namespace Kirie.Api.Scenes;

/// <summary>
/// An sRGB colour. Mixing happens in OKLab, so a dusk sky fading into overcast
/// grey goes through believable colours instead of the muddy midpoints plain RGB
/// interpolation gives.
/// </summary>
public readonly record struct Colour(double R, double G, double B)
{
    public static Colour Hex(string hex)
    {
        var value = int.Parse(hex.AsSpan(hex[0] == '#' ? 1 : 0), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new(((value >> 16) & 0xff) / 255.0, ((value >> 8) & 0xff) / 255.0, (value & 0xff) / 255.0);
    }

    public string ToHex() =>
        $"#{Channel(R):x2}{Channel(G):x2}{Channel(B):x2}";

    public static Colour Mix(Colour a, Colour b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        var (l1, a1, b1) = a.ToOkLab();
        var (l2, a2, b2) = b.ToOkLab();
        return FromOkLab(l1 + (l2 - l1) * t, a1 + (a2 - a1) * t, b1 + (b2 - b1) * t);
    }

    private static int Channel(double c) => (int)Math.Round(Math.Clamp(c, 0, 1) * 255);

    private static double ToLinear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

    private static double ToSrgb(double c) => c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;

    private (double L, double A, double B) ToOkLab()
    {
        double r = ToLinear(R), g = ToLinear(G), b = ToLinear(B);
        var l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        var m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        var s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
        return (
            0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
    }

    private static Colour FromOkLab(double lightness, double a, double b)
    {
        var l = Math.Pow(lightness + 0.3963377774 * a + 0.2158037573 * b, 3);
        var m = Math.Pow(lightness - 0.1055613458 * a - 0.0638541728 * b, 3);
        var s = Math.Pow(lightness - 0.0894841775 * a - 1.2914855480 * b, 3);
        return new(
            ToSrgb(4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s),
            ToSrgb(-1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s),
            ToSrgb(-0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s));
    }
}
