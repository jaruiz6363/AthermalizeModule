using AberrationCalculator.Core.Glass;
using Athermalize.Core.Air;

namespace Athermalize.Core.Materials;

/// <summary>
/// A glass whose catalog dispersion formula holds in air at its reference temperature and one atmosphere, and whose
/// index changes with temperature by SCHOTT's model (TIE-19). The steps, for a wavelength λ in the system air:
/// <list type="number">
/// <item>the air index at the system conditions, n_sys, and at the reference temperature and one atmosphere, n_ref;</item>
/// <item>the same light's wavelength in the reference air, λ_ref = λ·n_sys/n_ref;</item>
/// <item>the catalog index there, n_rel0, and the absolute index, n_abs0 = n_rel0·n_ref;</item>
/// <item>the change of the absolute index, TIE-19 eq. (3), with ΔT = T − Tref:
/// Δn_abs = (n² − 1)/(2n)·[D₀ΔT + D₁ΔT² + D₂ΔT³ + (E₀ΔT + E₁ΔT²)/(λ² − λtk²)];</item>
/// <item>the index relative to the system air, (n_abs0 + Δn_abs)/n_sys.</item>
/// </list>
/// A glass without thermal coefficients goes through the same steps with Δn_abs = 0: only the air changes.
/// </summary>
public sealed class Tie19Material : Material
{
    private readonly Func<double, double> _referenceIndex;

    /// <param name="catalog">The catalog's name.</param>
    /// <param name="glass">The glass's name.</param>
    /// <param name="referenceIndex">The catalog index at a wavelength in µm, in air at the reference conditions.</param>
    /// <param name="thermal">The thermal coefficients.</param>
    /// <param name="expansion">The expansion data.</param>
    /// <param name="wavelengthRangeUm">The catalog's wavelength range; (0, 0) when it gives none.</param>
    /// <param name="source">Where the data come from.</param>
    public Tie19Material(string catalog, string glass, Func<double, double> referenceIndex, ThermalData thermal,
        ExpansionData expansion, (double Min, double Max) wavelengthRangeUm, string source)
    {
        Catalog = catalog;
        Glass = glass;
        _referenceIndex = referenceIndex;
        Thermal = thermal;
        Expansion = expansion;
        WavelengthRangeUm = wavelengthRangeUm;
        Source = source;
    }

    /// <summary>A catalog glass with its thermal and expansion data.</summary>
    public static Tie19Material FromCatalog(GlassData glass, ThermalCatalog thermal) =>
        new(glass.Catalog, glass.Name, glass.IndexAt, thermal.Thermal(glass), thermal.Expansion(glass),
            (glass.LambdaMin, glass.LambdaMax), glass.Catalog + ".AGF");

    /// <summary>
    /// A model glass from nd, Vd and ΔPgF (AberrationCalculator's model). It has no thermal coefficients unless
    /// given: by default only the air changes. <paramref name="cte"/> is its expansion coefficient, 1/K.
    /// </summary>
    public static Tie19Material Model(double nd, double vd, double dPgF = 0.0, ThermalData? thermal = null, double cte = 0.0)
    {
        string name = FormattableString.Invariant($"{nd:F6}/{vd:F2}/{dPgF:F4}");
        var expansion = cte != 0.0 ? new ExpansionData(cte, 0, 0, dPgF, false, DataState.Incomplete) : ExpansionData.None();
        return new Tie19Material("MODEL", name, l => IndexResolver.ModelIndex(nd, vd, dPgF, l),
            thermal ?? ThermalData.None(), expansion, (0, 0), "model glass");
    }

    public override string Catalog { get; }
    public override string Glass { get; }
    public override string Source { get; }
    public override ExpansionData Expansion { get; }
    public override (double Min, double Max) WavelengthRangeUm { get; }

    /// <summary>The range TIE-19 states for the model, −100 to +140 °C.</summary>
    public override (double Min, double Max) TemperatureRangeC => (-100.0, 140.0);

    public ThermalData Thermal { get; }

    public override MaterialIndex Index(double lambdaUm, AirConditions air, double temperatureC, IndexOptions? options = null)
    {
        var o = options ?? IndexOptions.Default;
        double tRef = Thermal.ReferenceTemperatureC;

        var (nSys, lambdaVac) = SystemAir(lambdaUm, air, o);
        var reference = new AirConditions(tRef);
        double nRef = AirIndex.Of(o.AirModel, o.AirWavelength == AirWavelength.Vacuum ? lambdaVac : lambdaUm, reference);
        double lambdaRef = lambdaUm * nSys / nRef;

        double nRel0 = _referenceIndex(lambdaRef);
        double nAbs0 = nRel0 * nRef;
        double nAbs = nAbs0 + DeltaAbsolute(nRel0, nAbs0, lambdaRef, lambdaVac, temperatureC - tRef, o);

        return new MaterialIndex(nAbs / nSys, nAbs, nSys, lambdaVac, InRange(lambdaRef, temperatureC));
    }

    /// <summary>TIE-19 eq. (3).</summary>
    private double DeltaAbsolute(double nRel0, double nAbs0, double lambdaRef, double lambdaVac, double dT, IndexOptions o)
    {
        var t = Thermal;
        if (!t.HasCoefficients || dT == 0.0) return 0.0;

        double n = o.Prefactor == Prefactor.Relative ? nRel0 : nAbs0;
        double l = o.ThermalWavelength == ThermalWavelength.ReferenceAir ? lambdaRef : lambdaVac;
        double sign = o.LambdaTkSign == LambdaTkSign.Agf && t.LambdaTkUm < 0 ? -1.0 : 1.0;
        double denominator = l * l - sign * t.LambdaTkUm * t.LambdaTkUm;

        return (n * n - 1.0) / (2.0 * n)
             * (t.D0 * dT + t.D1 * dT * dT + t.D2 * dT * dT * dT + (t.E0 * dT + t.E1 * dT * dT) / denominator);
    }
}
