using Athermalize.Core.Air;

namespace Athermalize.Core.Materials;

/// <summary>
/// A material known by its index relative to the air at a reference temperature, a single dn/dT relative to the
/// air, and an expansion coefficient, as a data sheet or a paper gives them:
/// n_rel(λ, T) = n_rel(λ, T₀) + (dn_rel/dT)·(T − T₀), the air at T and the material's own pressure.
/// The air's own change is folded into the given dn/dT, so the material does not depend on the air model.
/// </summary>
public sealed class LinearMaterial : Material
{
    private readonly Func<double, double> _index;

    /// <param name="catalog">A catalog name, for reports.</param>
    /// <param name="glass">The material's name.</param>
    /// <param name="index">The index relative to the air at <paramref name="referenceTemperatureC"/>, at a vacuum wavelength in µm.</param>
    /// <param name="dnDtRelative">dn_rel/dT, 1/K.</param>
    /// <param name="cte">The expansion coefficient, 1/K.</param>
    /// <param name="referenceTemperatureC">T₀, °C.</param>
    /// <param name="source">Where the values come from.</param>
    public LinearMaterial(string catalog, string glass, Func<double, double> index, double dnDtRelative, double cte,
        double referenceTemperatureC = 20.0, string source = "given")
    {
        Catalog = catalog;
        Glass = glass;
        _index = index;
        DnDtRelative = dnDtRelative;
        ReferenceTemperatureC = referenceTemperatureC;
        Source = source;
        Expansion = cte != 0.0 ? new ExpansionData(cte, 0, 0, 0, false, DataState.Present) : ExpansionData.None();
    }

    /// <summary>A material of one index at every wavelength.</summary>
    public static LinearMaterial Constant(string name, double index, double dnDtRelative, double cte,
        double referenceTemperatureC = 20.0, string source = "given") =>
        new("GIVEN", name, _ => index, dnDtRelative, cte, referenceTemperatureC, source);

    public override string Catalog { get; }
    public override string Glass { get; }
    public override string Source { get; }
    public override ExpansionData Expansion { get; }
    public double DnDtRelative { get; }
    public double ReferenceTemperatureC { get; }
    public override (double Min, double Max) WavelengthRangeUm => (0, 0);
    public override (double Min, double Max) TemperatureRangeC => (double.NegativeInfinity, double.PositiveInfinity);

    public override MaterialIndex Index(double lambdaUm, AirConditions air, double temperatureC, IndexOptions? options = null)
    {
        var o = options ?? IndexOptions.Default;
        var (nAir, lambdaVac) = SystemAir(lambdaUm, air, o);
        double n = _index(lambdaVac) + DnDtRelative * (temperatureC - ReferenceTemperatureC);
        return new MaterialIndex(n, n * nAir, nAir, lambdaVac, true);
    }
}
