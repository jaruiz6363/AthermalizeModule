using Athermalize.Core.Air;
using Athermalize.Core.Materials;

namespace Athermalize.Core.Analysis;

/// <summary>
/// A spectral band by three wavelengths, µm, in standard air (20 °C, one atmosphere, as data sheets give spectral
/// lines): the short and long ends, which set the dispersion, and the center, which sets the index.
/// </summary>
public sealed record Band(string Name, double ShortUm, double CenterUm, double LongUm)
{
    /// <summary>F, d and C: the Abbe number ν_d.</summary>
    public static Band Visible { get; } = new("visible (F, d, C)", 0.4861327, 0.5875618, 0.6562725);

    /// <summary>F′, e and C′: the Abbe number ν_e.</summary>
    public static Band VisibleE { get; } = new("visible (F′, e, C′)", 0.4799914, 0.5460740, 0.6438469);

    /// <summary>3, 4 and 5 µm.</summary>
    public static Band Mwir { get; } = new("3-5 µm", 3.0, 4.0, 5.0);

    /// <summary>8, 10 and 12 µm.</summary>
    public static Band Lwir { get; } = new("8-12 µm", 8.0, 10.0, 12.0);

    public static IReadOnlyList<Band> Presets { get; } = new[] { Visible, VisibleE, Mwir, Lwir };
}

/// <summary>A material's optical and thermal constants over a band, at one temperature.</summary>
/// <param name="Material">The material.</param>
/// <param name="Index">n_rel at the band's center.</param>
/// <param name="Omega">The chromatic power ω = (n_short − n_long)/(n_center − 1) = 1/ν.</param>
/// <param name="PartialLong">The partial dispersion of the long half, (n_center − n_long)/(n_short − n_long): P_d,C in the visible.</param>
/// <param name="Gamma">γ = (dn_rel/dT)/(n_rel − 1) − α at the center, 1/K: the relative change of a thin lens's power.</param>
/// <param name="Cte">α, 1/K.</param>
/// <param name="InRange">False when a wavelength or the temperature is outside the material data's range.</param>
public sealed record MaterialConstants(Material Material, double Index, double Omega, double PartialLong, double Gamma,
    double Cte, bool InRange)
{
    /// <summary>The Abbe number over the band, 1/ω.</summary>
    public double Nu => 1.0 / Omega;

    /// <summary>x_f = −γ, the relative change of a thin lens's focal length (Jamieson 1981).</summary>
    public double OptothermalExpansion => -Gamma;

    /// <summary>
    /// The constants of <paramref name="material"/> over <paramref name="band"/> at <paramref name="temperatureC"/>,
    /// in air of the given pressure at that temperature (standard air when null). The band's wavelengths are held at
    /// their vacuum wavelengths. A material without an expansion coefficient has α = 0.
    /// </summary>
    public static MaterialConstants Of(Material material, Band band, double temperatureC = 20.0, AirConditions? air = null,
        IndexOptions? options = null, double? cteOverride = null)
    {
        var o = options ?? IndexOptions.Default;
        var a = (air ?? AirConditions.Standard) with { TemperatureC = temperatureC };
        MaterialIndex At(double lambda) =>
            material.IndexAtVacuumWavelength(ThermoOptic.VacuumWavelength(lambda, o.AirModel), a, temperatureC, o);
        var s = At(band.ShortUm);
        var c = At(band.CenterUm);
        var l = At(band.LongUm);
        double cte = cteOverride ?? material.Expansion.Cte;
        var t = ThermoOptic.At(material, ThermoOptic.VacuumWavelength(band.CenterUm, o.AirModel), temperatureC, a, o);
        double gamma = t.DnDtRelative / (t.Relative - 1.0) - cte;
        return new MaterialConstants(material, c.Relative, (s.Relative - l.Relative) / (c.Relative - 1.0),
            (c.Relative - l.Relative) / (s.Relative - l.Relative), gamma, cte, s.InRange && c.InRange && l.InRange && t.InRange);
    }
}
