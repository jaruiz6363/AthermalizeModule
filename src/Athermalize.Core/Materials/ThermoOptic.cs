using Athermalize.Core.Air;

namespace Athermalize.Core.Materials;

/// <summary>A material's thermal constants at one wavelength and temperature.</summary>
/// <param name="Relative">The index relative to the air.</param>
/// <param name="Absolute">The absolute index.</param>
/// <param name="DnDtRelative">dn_rel/dT, 1/K: the material and the air both at the temperature, the pressure held.</param>
/// <param name="DnDtAbsolute">dn_abs/dT, 1/K.</param>
/// <param name="Cte">The material's expansion coefficient, 1/K; 0 when the data give none.</param>
/// <param name="OptothermalExpansion">
/// x_f = α − (dn_rel/dT)/(n_rel − 1), 1/K: the relative change of a thin lens's focal length per kelvin, in air
/// at the same temperature (Jamieson 1981). Positive when the focal length grows.
/// NaN when the material has no expansion coefficient.
/// </param>
/// <param name="InRange">False when the wavelength or temperature is outside the data's stated range.</param>
public readonly record struct ThermalConstants(
    double Relative, double Absolute, double DnDtRelative, double DnDtAbsolute, double Cte,
    double OptothermalExpansion, bool InRange);

/// <summary>
/// The change of a material's index with temperature. Every derivative here holds the vacuum wavelength fixed,
/// since that is what stays the same when a lens warms; the wavelength in the air changes with the air.
/// </summary>
public static class ThermoOptic
{
    /// <summary>
    /// The vacuum wavelength of light whose wavelength in standard air (20 °C, one atmosphere, dry) is
    /// <paramref name="lambdaStandardAirUm"/>. Data sheets give their spectral lines this way.
    /// </summary>
    public static double VacuumWavelength(double lambdaStandardAirUm, AirModel model = AirModel.Tie19) =>
        lambdaStandardAirUm * AirIndex.Of(model, lambdaStandardAirUm, AirConditions.Standard);

    /// <summary>
    /// The mean dn/dT over [<paramref name="t1"/>, <paramref name="t2"/>], °C, as data sheets tabulate it:
    /// (n(T₂) − n(T₁))/(T₂ − T₁), the material and the air at each temperature. The air's pressure, humidity and
    /// CO₂ are <paramref name="air"/>'s (standard air when null); its temperature is ignored.
    /// </summary>
    public static (double Relative, double Absolute) MeanDnDt(Material m, double lambdaVacuumUm, double t1, double t2,
        AirConditions? air = null, IndexOptions? options = null)
    {
        air ??= AirConditions.Standard;
        var a = m.IndexAtVacuumWavelength(lambdaVacuumUm, air with { TemperatureC = t1 }, t1, options);
        var b = m.IndexAtVacuumWavelength(lambdaVacuumUm, air with { TemperatureC = t2 }, t2, options);
        return ((b.Relative - a.Relative) / (t2 - t1), (b.Absolute - a.Absolute) / (t2 - t1));
    }

    /// <summary>
    /// The index and its thermal constants with the material and the air at <paramref name="temperatureC"/>, the
    /// derivatives by a central difference of ±0.5 K. The air is <paramref name="air"/> otherwise (standard air when null).
    /// </summary>
    public static ThermalConstants At(Material m, double lambdaVacuumUm, double temperatureC, AirConditions? air = null,
        IndexOptions? options = null)
    {
        const double h = 0.5;
        air ??= AirConditions.Standard;
        var n = m.IndexAtVacuumWavelength(lambdaVacuumUm, air with { TemperatureC = temperatureC }, temperatureC, options);
        var (rel, abs) = MeanDnDt(m, lambdaVacuumUm, temperatureC - h, temperatureC + h, air, options);
        double cte = m.Expansion.Cte;
        double xf = cte != 0.0 ? cte - rel / (n.Relative - 1.0) : double.NaN;
        return new ThermalConstants(n.Relative, n.Absolute, rel, abs, cte, xf, n.InRange);
    }
}
