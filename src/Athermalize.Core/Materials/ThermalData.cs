namespace Athermalize.Core.Materials;

/// <summary>What a catalog said about a glass's thermal or expansion data.</summary>
public enum DataState
{
    /// <summary>The line is there with every field.</summary>
    Present,

    /// <summary>The line is there with fewer fields than the format has. Missing fields take their defaults.</summary>
    Incomplete,

    /// <summary>The line is there with no fields at all.</summary>
    Empty,

    /// <summary>The glass has no such line.</summary>
    Missing,
}

/// <summary>
/// A glass's thermal coefficients of the refractive index, the AGF <c>TD</c> line:
/// <c>TD D0 D1 D2 E0 E1 λtk Tref</c>, the constants of SCHOTT's model (TIE-19, eq. 3).
/// </summary>
/// <param name="D0">D₀, 1/K.</param>
/// <param name="D1">D₁, 1/K².</param>
/// <param name="D2">D₂, 1/K³.</param>
/// <param name="E0">E₀, µm²/K.</param>
/// <param name="E1">E₁, µm²/K².</param>
/// <param name="LambdaTkUm">λtk, µm. Its sign sets the sign of λtk² in the denominator (see <see cref="LambdaTkSign"/>).</param>
/// <param name="ReferenceTemperatureC">The temperature, °C, at which the catalog's dispersion formula holds.</param>
/// <param name="State">Whether the catalog gave the line, and in full.</param>
public sealed record ThermalData(
    double D0, double D1, double D2, double E0, double E1, double LambdaTkUm,
    double ReferenceTemperatureC, DataState State)
{
    /// <summary>The reference temperature when a catalog gives none: 20 °C, which every maker's catalog uses.</summary>
    public const double DefaultReferenceTemperatureC = 20.0;

    /// <summary>No thermal coefficients, at the default reference temperature.</summary>
    public static ThermalData None(DataState state = DataState.Missing) =>
        new(0, 0, 0, 0, 0, 0, DefaultReferenceTemperatureC, state);

    /// <summary>True when at least one coefficient is nonzero, so the index changes with temperature.</summary>
    public bool HasCoefficients => D0 != 0 || D1 != 0 || D2 != 0 || E0 != 0 || E1 != 0;

    /// <summary>
    /// Reads the fields after <c>TD</c>. Fields that are absent take their defaults: zero for the coefficients and
    /// <see cref="DefaultReferenceTemperatureC"/> for Tref, never 0 °C.
    /// </summary>
    public static ThermalData FromFields(IReadOnlyList<double> f)
    {
        if (f.Count == 0) return None(DataState.Empty);
        double At(int i, double fallback) => i < f.Count ? f[i] : fallback;
        return new ThermalData(At(0, 0), At(1, 0), At(2, 0), At(3, 0), At(4, 0), At(5, 0),
            At(6, DefaultReferenceTemperatureC), f.Count >= 7 ? DataState.Present : DataState.Incomplete);
    }
}

/// <summary>
/// A glass's expansion data, the AGF <c>ED</c> line: <c>ED TCE(−30/+70) TCE(+100/+300) density ΔPgF ignore</c>.
/// </summary>
/// <param name="Cte">The mean coefficient of thermal expansion from −30 to +70 °C, 1/K.</param>
/// <param name="CteHigh">The mean coefficient from +100 to +300 °C, 1/K.</param>
/// <param name="Density">Density, g/cm³.</param>
/// <param name="DPgF">ΔPgF, the deviation of the relative partial dispersion from the normal line.</param>
/// <param name="IgnoreExpansion">The catalog's flag that the glass is not to be expanded with temperature.</param>
/// <param name="State">Whether the catalog gave the line, and in full.</param>
/// <param name="Curve">
/// The length over temperature, for a material whose source gives it; <paramref name="Cte"/> is then its coefficient
/// at 20 °C. Null when there is only the one coefficient.
/// </param>
public sealed record ExpansionData(
    double Cte, double CteHigh, double Density, double DPgF, bool IgnoreExpansion, DataState State,
    ExpansionCurve? Curve = null)
{
    /// <summary>The curve the material grows by: its own, or its one coefficient at every temperature.</summary>
    public ExpansionCurve Growth => Curve ?? new ConstantExpansion(Cte);

    public static ExpansionData None(DataState state = DataState.Missing) => new(0, 0, 0, 0, false, state);

    /// <summary>Reads the fields after <c>ED</c>. The catalog gives the expansion coefficients in 10⁻⁶/K.</summary>
    public static ExpansionData FromFields(IReadOnlyList<double> f)
    {
        if (f.Count == 0) return None(DataState.Empty);
        double At(int i) => i < f.Count ? f[i] : 0.0;
        return new ExpansionData(At(0) * 1e-6, At(1) * 1e-6, At(2), At(3), At(4) != 0.0,
            f.Count >= 5 ? DataState.Present : DataState.Incomplete);
    }
}
