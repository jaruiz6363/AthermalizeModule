using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Athermalize.Core.Lens;

namespace Athermalize.Tests;

/// <summary>Lenses written for the tests, surface by surface.</summary>
internal sealed class Lenses
{
    private readonly OpticalSystem _system = new();

    private Lenses(double epd, double[] wavelengthsUm)
    {
        _system.Aperture = new Aperture(ApertureType.EPD, epd);
        for (int i = 0; i < wavelengthsUm.Length; i++)
            _system.Wavelengths.Add(new Wavelength(wavelengthsUm[i], 1.0, i == 0));
        _system.Fields.Add(new Field(0.0));
        _system.Fields.Add(new Field(1.0));
        _system.Surfaces.Add(new Surface { Index = 0, Thickness = double.PositiveInfinity });
    }

    /// <summary>An object at infinity, an entrance pupil of <paramref name="epd"/>, the first wavelength primary.</summary>
    public static Lenses AtInfinity(double epd, params double[] wavelengthsUm) =>
        new(epd, wavelengthsUm.Length == 0 ? new[] { 0.5875618 } : wavelengthsUm);

    /// <summary>A surface; a radius of 0 is a plane.</summary>
    public Lenses Surface(double radius, double thickness, string? material = null, bool stop = false,
        double semiDiameter = 10.0, double conic = 0.0, double[]? aspheric = null)
    {
        var s = new Surface
        {
            Index = _system.Surfaces.Count,
            Curvature = radius == 0.0 ? 0.0 : 1.0 / radius,
            Thickness = thickness,
            Material = material,
            IsStop = stop,
            SemiDiameter = semiDiameter,
            SemiDiameterMode = SemiDiameterMode.Fixed,
            Conic = conic,
        };
        if (aspheric != null)
        {
            s.Type = SurfaceType.EvenAsphere;
            Array.Copy(aspheric, s.AsphericCoefficients, aspheric.Length);
        }
        _system.Surfaces.Add(s);
        return this;
    }

    public OpticalSystem Image()
    {
        _system.Surfaces.Add(new Surface { Index = _system.Surfaces.Count });
        return _system;
    }

    /// <summary>The paraxial focus at the primary wavelength less the distance to the image plane, mm.</summary>
    public static double Defocus(ThermalLens lens) => Defocus(lens.System, lens.PrimaryIndices);

    public static double Defocus(OpticalSystem system, double[] indices)
    {
        var trace = ParaxialTrace.Trace(system, indices, 0.0);
        return trace.ParaxialFocusDistance - system.Surfaces[system.LastOpticalSurface()].Thickness;
    }

    public static ParaxialResult Paraxial(ThermalLens lens) => ParaxialTrace.Trace(lens.System, lens.PrimaryIndices, 0.0);

    /// <summary>The position of each surface's vertex along the axis from surface 1.</summary>
    public static double[] Vertices(OpticalSystem system)
    {
        var z = new double[system.Surfaces.Count];
        for (int i = 2; i < z.Length; i++) z[i] = z[i - 1] + system.Surfaces[i - 1].Thickness;
        return z;
    }
}
