using AberrationCalculator.Core.Models;

namespace Athermalize.Tests;

/// <summary>
/// The B4 Cassegrain of Itek Corporation, "Outer Planets Mission Television Subsystem Optics Study," Final Report
/// 72-9320-1 for the Jet Propulsion Laboratory (NASA NAS7-100), July 28, 1972, NASA N72-33381, Table 2-7: a
/// 1000 mm f/4.4 Cassegrain with two BK7 correctors, in inches in the report and in millimeters here.
/// Surface 1 is the entrance pupil.
/// </summary>
internal static class ItekCassegrain
{
    public const double Inch = 25.4;

    /// <summary>The central obstruction, as a fraction of the pupil's radius (Table 2-6).</summary>
    public const double Obscuration = 0.52;

    /// <summary>The lens, in mm. Surfaces 3 and 4 are the primary and the secondary.</summary>
    public static OpticalSystem Lens()
    {
        // Even-asphere coefficients of r^6 and r^8, converted from inches.
        static double[] Aspheric(double a6, double a8) =>
            new[] { 0.0, 0.0, a6 / Math.Pow(Inch, 5), a8 / Math.Pow(Inch, 7) };

        var system = Lenses.AtInfinity(9.0 * Inch, 0.55, 0.35, 0.45, 0.65, 0.70)
            .Surface(0.0, 9.75 * Inch, stop: true, semiDiameter: 4.6596 * Inch)
            .Surface(0.0, 0.25 * Inch, semiDiameter: 4.545 * Inch)
            .Surface(-41.2762 * Inch, -10.8 * Inch, "MIRROR", semiDiameter: 4.545 * Inch, conic: -2.141034,
                aspheric: Aspheric(2.930676E-09, 1.983164E-11))
            .Surface(-41.2762 * Inch, 12.4 * Inch, "MIRROR", semiDiameter: 2.326 * Inch, conic: -34.70414,
                aspheric: Aspheric(4.343248E-07, -5.855673E-10))
            .Surface(10.8872 * Inch, 0.2 * Inch, "N-BK7", semiDiameter: 1.08 * Inch)
            .Surface(37.7249 * Inch, 0.2 * Inch, semiDiameter: 1.064 * Inch)
            .Surface(-8.5294 * Inch, 0.2 * Inch, "N-BK7", semiDiameter: 1.047 * Inch)
            .Surface(-20.5730 * Inch, 0.0, semiDiameter: 1.036 * Inch)
            .Surface(0.0, 0.0, semiDiameter: 1.033 * Inch)
            .Surface(0.0, 5.8517492 * Inch, semiDiameter: 1.033 * Inch)
            .Image();
        system.Title = "Itek B4 Cassegrain";
        system.GlassCatalogs.Add("SCHOTT");
        system.Fields.Clear();
        foreach (double h in new[] { 0.0, 0.7, 1.0 }) system.Fields.Add(new Field(h * 40.0 / 60.0));
        return system;
    }
}
