namespace Kirie.Api.Sky;

/// <summary>Where something is in the sky, in degrees. Azimuth is measured from north, clockwise.</summary>
public readonly record struct SkyPosition(double Altitude, double Azimuth);

/// <summary>How much of the moon is lit, and where it is in its cycle (0 new, 0.5 full).</summary>
public readonly record struct MoonLight(double Illumination, double Phase)
{
    public bool Waxing => Phase < 0.5;
}

/// <summary>
/// Positions of the sun and moon, after the low-precision formulas in Jean Meeus'
/// Astronomical Algorithms as simplified by the Astronomy Answers articles. Good to
/// about a minute for sunrise and sunset, which is far better than a paper sun needs.
/// </summary>
public static class Astronomy
{
    private const double Rad = Math.PI / 180;
    private const double J1970 = 2440588;
    private const double J2000 = 2451545;
    private const double Obliquity = Rad * 23.4397;

    /// <summary>Altitude of the sun's centre at sunrise and sunset, allowing for refraction and its radius.</summary>
    public const double HorizonAltitude = -0.833;

    public static SkyPosition Sun(DateTimeOffset time, double latitude, double longitude)
    {
        var d = Days(time);
        var (declination, rightAscension) = SunCoordinates(d);
        var hourAngle = SiderealTime(d, Rad * -longitude) - rightAscension;
        var phi = Rad * latitude;
        return new(Altitude(hourAngle, phi, declination) / Rad, FromNorth(Azimuth(hourAngle, phi, declination)));
    }

    public static SkyPosition Moon(DateTimeOffset time, double latitude, double longitude)
    {
        var d = Days(time);
        var (rightAscension, declination, _) = MoonCoordinates(d);
        var hourAngle = SiderealTime(d, Rad * -longitude) - rightAscension;
        var phi = Rad * latitude;
        var altitude = Altitude(hourAngle, phi, declination);
        altitude += Refraction(altitude);
        return new(altitude / Rad, FromNorth(Azimuth(hourAngle, phi, declination)));
    }

    public static MoonLight MoonIllumination(DateTimeOffset time)
    {
        const double sunDistance = 149_598_000;
        var d = Days(time);
        var (sunDec, sunRa) = SunCoordinates(d);
        var (moonRa, moonDec, moonDistance) = MoonCoordinates(d);

        var elongation = Math.Acos(
            Math.Sin(sunDec) * Math.Sin(moonDec) + Math.Cos(sunDec) * Math.Cos(moonDec) * Math.Cos(sunRa - moonRa));
        var inclination = Math.Atan2(sunDistance * Math.Sin(elongation), moonDistance - sunDistance * Math.Cos(elongation));
        var angle = Math.Atan2(
            Math.Cos(sunDec) * Math.Sin(sunRa - moonRa),
            Math.Sin(sunDec) * Math.Cos(moonDec) - Math.Cos(sunDec) * Math.Sin(moonDec) * Math.Cos(sunRa - moonRa));

        var illumination = (1 + Math.Cos(inclination)) / 2;
        var phase = 0.5 + 0.5 * inclination * (angle < 0 ? -1 : 1) / Math.PI;
        return new(illumination, phase);
    }

    /// <summary>The point on Earth where the sun is directly overhead.</summary>
    public static (double Latitude, double Longitude) Subsolar(DateTimeOffset time)
    {
        var d = Days(time);
        var (declination, rightAscension) = SunCoordinates(d);
        var greenwichSidereal = Rad * (280.16 + 360.9856235 * d);
        var longitude = (rightAscension - greenwichSidereal) / Rad;
        return (declination / Rad, NormalizeLongitude(longitude));
    }

    /// <summary>
    /// Sunrise and sunset during the local calendar day that starts at <paramref name="localMidnight"/>.
    /// Either can be null: in the polar summer the sun never sets, in the polar winter it never rises.
    /// </summary>
    public static (DateTimeOffset? Rise, DateTimeOffset? Set) SunEvents(DateTimeOffset localMidnight, double latitude, double longitude)
    {
        DateTimeOffset? rise = null, set = null;
        var step = TimeSpan.FromMinutes(10);
        var previous = localMidnight;
        var previousAltitude = Sun(previous, latitude, longitude).Altitude - HorizonAltitude;

        for (var t = localMidnight + step; t <= localMidnight.AddDays(1); t += step)
        {
            var altitude = Sun(t, latitude, longitude).Altitude - HorizonAltitude;
            if (previousAltitude < 0 && altitude >= 0)
                rise ??= Crossing(previous, t, latitude, longitude);
            else if (previousAltitude >= 0 && altitude < 0)
                set ??= Crossing(previous, t, latitude, longitude);
            previous = t;
            previousAltitude = altitude;
        }
        return (rise, set);
    }

    private static DateTimeOffset Crossing(DateTimeOffset a, DateTimeOffset b, double latitude, double longitude)
    {
        var aAbove = Sun(a, latitude, longitude).Altitude >= HorizonAltitude;
        while (b - a > TimeSpan.FromSeconds(15))
        {
            var mid = a + (b - a) / 2;
            if (Sun(mid, latitude, longitude).Altitude >= HorizonAltitude == aAbove) a = mid;
            else b = mid;
        }
        return a + (b - a) / 2;
    }

    private static double Days(DateTimeOffset time) =>
        time.ToUnixTimeMilliseconds() / 86_400_000.0 - 0.5 + J1970 - J2000;

    private static double RightAscension(double l, double b) =>
        Math.Atan2(Math.Sin(l) * Math.Cos(Obliquity) - Math.Tan(b) * Math.Sin(Obliquity), Math.Cos(l));

    private static double Declination(double l, double b) =>
        Math.Asin(Math.Sin(b) * Math.Cos(Obliquity) + Math.Cos(b) * Math.Sin(Obliquity) * Math.Sin(l));

    // Measured from south, turning west, as the formulas produce it.
    private static double Azimuth(double h, double phi, double dec) =>
        Math.Atan2(Math.Sin(h), Math.Cos(h) * Math.Sin(phi) - Math.Tan(dec) * Math.Cos(phi));

    private static double Altitude(double h, double phi, double dec) =>
        Math.Asin(Math.Sin(phi) * Math.Sin(dec) + Math.Cos(phi) * Math.Cos(dec) * Math.Cos(h));

    private static double SiderealTime(double d, double lw) => Rad * (280.16 + 360.9856235 * d) - lw;

    private static double Refraction(double h)
    {
        h = Math.Max(h, 0);
        return 0.0002967 / Math.Tan(h + 0.00312536 / (h + 0.08901179));
    }

    private static (double Declination, double RightAscension) SunCoordinates(double d)
    {
        var m = Rad * (357.5291 + 0.98560028 * d);
        var center = Rad * (1.9148 * Math.Sin(m) + 0.02 * Math.Sin(2 * m) + 0.0003 * Math.Sin(3 * m));
        var eclipticLongitude = m + center + Rad * 102.9372 + Math.PI;
        return (Declination(eclipticLongitude, 0), RightAscension(eclipticLongitude, 0));
    }

    private static (double RightAscension, double Declination, double Distance) MoonCoordinates(double d)
    {
        var l = Rad * (218.316 + 13.176396 * d);
        var m = Rad * (134.963 + 13.064993 * d);
        var f = Rad * (93.272 + 13.229350 * d);
        var longitude = l + Rad * 6.289 * Math.Sin(m);
        var latitude = Rad * 5.128 * Math.Sin(f);
        var distance = 385001 - 20905 * Math.Cos(m);
        return (RightAscension(longitude, latitude), Declination(longitude, latitude), distance);
    }

    private static double FromNorth(double azimuthFromSouth) => (azimuthFromSouth / Rad + 180 + 360) % 360;

    private static double NormalizeLongitude(double longitude) => ((longitude + 180) % 360 + 360) % 360 - 180;
}
