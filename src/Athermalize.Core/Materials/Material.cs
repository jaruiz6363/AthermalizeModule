using Athermalize.Core.Air;

namespace Athermalize.Core.Materials;

/// <summary>A material's index at one wavelength, temperature and pressure.</summary>
/// <param name="Relative">The index relative to the surrounding air, the one a ray trace uses.</param>
/// <param name="Absolute">The index relative to vacuum.</param>
/// <param name="Air">The index of the surrounding air.</param>
/// <param name="VacuumWavelengthUm">The vacuum wavelength of the light, µm.</param>
/// <param name="InRange">False when the wavelength or temperature is outside the material data's stated range.</param>
public readonly record struct MaterialIndex(
    double Relative, double Absolute, double Air, double VacuumWavelengthUm, bool InRange);

/// <summary>An optical material whose index depends on temperature and on the air around it.</summary>
public abstract class Material
{
    /// <summary>"CATALOG:NAME".</summary>
    public string Name => Catalog + ":" + Glass;

    public abstract string Catalog { get; }
    public abstract string Glass { get; }

    /// <summary>Where the data come from.</summary>
    public abstract string Source { get; }

    public abstract ExpansionData Expansion { get; }

    /// <summary>The wavelengths, µm, over which the data hold.</summary>
    public abstract (double Min, double Max) WavelengthRangeUm { get; }

    /// <summary>The temperatures, °C, over which the data hold.</summary>
    public abstract (double Min, double Max) TemperatureRangeC { get; }

    /// <summary>
    /// The index at <paramref name="lambdaUm"/>, a wavelength in the air <paramref name="air"/> (in vacuum, the
    /// vacuum wavelength), with the material at <paramref name="temperatureC"/>.
    /// </summary>
    public abstract MaterialIndex Index(double lambdaUm, AirConditions air, double temperatureC, IndexOptions? options = null);

    /// <summary>The index with the material at the air's temperature.</summary>
    public MaterialIndex Index(double lambdaUm, AirConditions air, IndexOptions? options = null) =>
        Index(lambdaUm, air, air.TemperatureC, options);

    /// <summary>The index at a fixed vacuum wavelength, the wavelength the air then sees being worked out from it.</summary>
    public MaterialIndex IndexAtVacuumWavelength(double lambdaVacuumUm, AirConditions air, double temperatureC,
        IndexOptions? options = null)
    {
        var o = options ?? IndexOptions.Default;
        double lambda = lambdaVacuumUm;
        if (o.AirWavelength == AirWavelength.Vacuum)
            lambda = lambdaVacuumUm / AirIndex.Of(o.AirModel, lambdaVacuumUm, air);
        else
            for (int i = 0; i < 4; i++) lambda = lambdaVacuumUm / AirIndex.Of(o.AirModel, lambda, air);
        return Index(lambda, air, temperatureC, o);
    }

    protected bool InRange(double lambdaUm, double temperatureC)
    {
        var (wMin, wMax) = WavelengthRangeUm;
        var (tMin, tMax) = TemperatureRangeC;
        const double slack = 1e-9;
        return (wMax <= 0 || (lambdaUm >= wMin - slack && lambdaUm <= wMax + slack))
            && temperatureC >= tMin - slack && temperatureC <= tMax + slack;
    }

    /// <summary>
    /// The index of the air at the system conditions, and the vacuum wavelength, for a wavelength given in that air.
    /// </summary>
    protected static (double Air, double VacuumWavelengthUm) SystemAir(double lambdaUm, AirConditions air, IndexOptions o)
    {
        if (air.IsVacuum) return (1.0, lambdaUm);
        if (o.AirWavelength == AirWavelength.Input)
        {
            double n = AirIndex.Of(o.AirModel, lambdaUm, air);
            return (n, lambdaUm * n);
        }

        double vac = lambdaUm, nAir = 1.0;
        for (int i = 0; i < 4; i++)
        {
            nAir = AirIndex.Of(o.AirModel, vac, air);
            vac = lambdaUm * nAir;
        }
        return (nAir, vac);
    }

    public override string ToString() => Name;
}
