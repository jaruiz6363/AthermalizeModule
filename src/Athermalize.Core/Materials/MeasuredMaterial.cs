using System.Reflection;
using System.Text.Json;
using Athermalize.Core.Air;

namespace Athermalize.Core.Materials;

/// <summary>
/// A material whose index is given over temperature by a published model fitted to measurements. The models are
/// in <see cref="IndexModel"/>: a Sellmeier model with coefficients polynomial in temperature, and the two forms of
/// Li's critical reviews. The index relative to the air is the absolute index over the air's.
/// </summary>
public sealed class MeasuredMaterial : Material
{
    private readonly IndexModel _model;

    public MeasuredMaterial(string catalog, string glass, string description, string source,
        double[][] s, double[][] lambda, (double Min, double Max) temperatureRangeK, (double Min, double Max) wavelengthRangeUm,
        double cte = 0.0, string cteSource = "", ExpansionCurve? expansion = null)
        : this(catalog, glass, description, source, new SellmeierModel(s, lambda), temperatureRangeK, wavelengthRangeUm, cte, cteSource, expansion)
    {
    }

    public MeasuredMaterial(string catalog, string glass, string description, string source, IndexModel model,
        (double Min, double Max) temperatureRangeK, (double Min, double Max) wavelengthRangeUm,
        double cte = 0.0, string cteSource = "", ExpansionCurve? expansion = null)
    {
        Catalog = catalog;
        Glass = glass;
        Description = description;
        Source = source;
        _model = model;
        TemperatureRangeC = (temperatureRangeK.Min - 273.15, temperatureRangeK.Max - 273.15);
        WavelengthRangeUm = wavelengthRangeUm;
        Expansion = cte != 0.0 ? new ExpansionData(cte, 0, 0, 0, false, DataState.Present, expansion) : ExpansionData.None();
        CteSource = cteSource;
    }

    public override string Catalog { get; }
    public override string Glass { get; }
    public string Description { get; }
    public override string Source { get; }
    public override ExpansionData Expansion { get; }

    /// <summary>The model of the index, and what it is relative to.</summary>
    public IndexModel Model => _model;

    /// <summary>Where the expansion coefficient comes from, and what range it is for.</summary>
    public string CteSource { get; }
    public override (double Min, double Max) WavelengthRangeUm { get; }
    public override (double Min, double Max) TemperatureRangeC { get; }

    /// <summary>The absolute index at a vacuum wavelength, µm, and a temperature, K.</summary>
    public double AbsoluteIndex(double lambdaVacuumUm, double temperatureK) => _model.Absolute(lambdaVacuumUm, temperatureK);

    public override MaterialIndex Index(double lambdaUm, AirConditions air, double temperatureC, IndexOptions? options = null)
    {
        var o = options ?? IndexOptions.Default;
        var (nAir, lambdaVac) = SystemAir(lambdaUm, air, o);
        double nAbs = AbsoluteIndex(lambdaVac, temperatureC + 273.15);
        return new MaterialIndex(nAbs / nAir, nAbs, nAir, lambdaVac, InRange(lambdaVac, temperatureC));
    }

    /// <summary>The measured materials this library carries (see MeasuredMaterials.json for their sources).</summary>
    public static IReadOnlyList<MeasuredMaterial> BuiltIn => _builtIn.Value;

    private static readonly Lazy<IReadOnlyList<MeasuredMaterial>> _builtIn = new(() =>
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Athermalize.Core.Materials.MeasuredMaterials.json")
            ?? throw new InvalidOperationException("The measured-materials resource is missing.");
        return Read(stream);
    });

    /// <summary>Reads materials in the format of MeasuredMaterials.json.</summary>
    public static IReadOnlyList<MeasuredMaterial> Read(Stream json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<MeasuredMaterial>();
        foreach (var m in doc.RootElement.GetProperty("materials").EnumerateArray())
        {
            static (double, double) Pair(JsonElement e) => (e[0].GetDouble(), e[1].GetDouble());

            double cte = 0.0;
            string cteSource = "";
            ExpansionCurve? curve = null;
            if (m.TryGetProperty("cte", out var c))
            {
                cte = c.GetProperty("value").GetDouble();
                cteSource = c.GetProperty("source").GetString() + "; " + c.GetProperty("range").GetString();
                if (c.TryGetProperty("curve", out var k)) curve = ExpansionCurve.Read(k);
            }
            list.Add(new MeasuredMaterial(
                m.GetProperty("catalog").GetString()!, m.GetProperty("name").GetString()!,
                m.GetProperty("description").GetString()!, m.GetProperty("source").GetString()!,
                IndexModel.Read(m), Pair(m.GetProperty("temperatureRangeK")), Pair(m.GetProperty("wavelengthRangeUm")),
                cte, cteSource, curve));
        }
        return list;
    }
}

/// <summary>A model of a material's index over wavelength and temperature.</summary>
public abstract class IndexModel
{
    /// <summary>The absolute index at a vacuum wavelength, µm, and a temperature, K.</summary>
    public abstract double Absolute(double lambdaVacuumUm, double temperatureK);

    /// <summary>What the model's own index is relative to, in words.</summary>
    public abstract string Reference { get; }

    internal static IndexModel Read(JsonElement m)
    {
        static double[] Row(JsonElement e) => e.EnumerateArray().Select(v => v.GetDouble()).ToArray();
        static double[][] Rows(JsonElement e) => e.EnumerateArray().Select(Row).ToArray();

        string model = m.TryGetProperty("model", out var kind) ? kind.GetString()! : "sellmeier";
        switch (model)
        {
            case "sellmeier":
                return new SellmeierModel(Rows(m.GetProperty("S")), Rows(m.GetProperty("lambda")));
            case "li-germanium":
            {
                var x = m.GetProperty("expansion");
                return new LiGermaniumModel(Row(m.GetProperty("epsilon")), Row(m.GetProperty("A")),
                    x.GetProperty("splitK").GetDouble(), x.GetProperty("belowOriginK").GetDouble(),
                    Row(x.GetProperty("below")), Row(x.GetProperty("above")));
            }
            case "li-zinc":
                return new LiZincModel(Row(m.GetProperty("E")), Row(m.GetProperty("A")), Row(m.GetProperty("B")),
                    Row(m.GetProperty("lambdaU")), Row(m.GetProperty("lambdaI")));
            default:
                throw new InvalidOperationException($"Unknown index model \"{model}\".");
        }
    }

    internal static double Polynomial(double[] c, double x)
    {
        double y = 0.0;
        for (int j = c.Length - 1; j >= 0; j--) y = y * x + c[j];
        return y;
    }

    /// <summary>
    /// The index of the air Li's indices are taken as relative to: dry air at 20 °C and one atmosphere, by the
    /// TIE-19 formula, at the vacuum wavelength.
    /// </summary>
    internal static double ReferenceAir(double lambdaVacuumUm) => AirIndex.Tie19(lambdaVacuumUm, 20.0, 1.0);
}

/// <summary>
/// An absolute-index Sellmeier model whose coefficients are polynomials in the temperature in kelvin:
/// n²(λ, T) − 1 = Σᵢ Sᵢ(T)·λ²/(λ² − λᵢ(T)²), Sᵢ(T) = Σⱼ Sᵢⱼ Tʲ, λᵢ(T) = Σⱼ λᵢⱼ Tʲ,
/// with λ the vacuum wavelength in µm. The form of the CHARMS measurements at NASA Goddard.
/// </summary>
public sealed class SellmeierModel : IndexModel
{
    private readonly double[][] _s, _lambda;

    public SellmeierModel(double[][] s, double[][] lambda)
    {
        if (s.Length != lambda.Length) throw new ArgumentException("S and λ need the same number of terms.");
        _s = s;
        _lambda = lambda;
    }

    public override string Reference => "absolute index";

    public override double Absolute(double lambdaVacuumUm, double temperatureK)
    {
        double l2 = lambdaVacuumUm * lambdaVacuumUm, n2 = 1.0;
        for (int i = 0; i < _s.Length; i++)
        {
            double li = Polynomial(_lambda[i], temperatureK);
            n2 += Polynomial(_s[i], temperatureK) * l2 / (l2 - li * li);
        }
        return n2 > 0.0 ? Math.Sqrt(n2) : double.NaN;
    }
}

/// <summary>
/// Li's model of germanium (J. Phys. Chem. Ref. Data 9, 561 (1980), eq. (28)), with λ in µm and T in kelvin:
/// n² = ε(T) + (L(T)/λ²)·(A₀ + A₁T + A₂T²), ε a cubic in T, and L(T) = exp(−3ΔL(T)/L₂₉₃) carrying the lattice's
/// expansion from 293 K, ΔL/L₂₉₃ a cubic in (T − origin) on each side of 293 K. The index is relative to air,
/// taken as dry air at 20 °C and one atmosphere (<see cref="IndexModel.ReferenceAir"/>).
/// </summary>
public sealed class LiGermaniumModel : IndexModel
{
    private readonly double[] _epsilon, _a, _below, _above;
    private readonly double _splitK, _belowOriginK;

    /// <param name="epsilon">ε(T), constant term first.</param>
    /// <param name="a">A₀, A₁, A₂.</param>
    /// <param name="splitK">The temperature, K, at which the expansion's two cubics meet; the upper one is in (T − splitK).</param>
    /// <param name="belowOriginK">The origin of the lower cubic, which is in (T − belowOriginK).</param>
    /// <param name="below">ΔL/L₂₉₃ below <paramref name="splitK"/>, as a fraction, constant term first.</param>
    /// <param name="above">ΔL/L₂₉₃ above it.</param>
    public LiGermaniumModel(double[] epsilon, double[] a, double splitK, double belowOriginK, double[] below, double[] above)
    {
        _epsilon = epsilon;
        _a = a;
        _splitK = splitK;
        _belowOriginK = belowOriginK;
        _below = below;
        _above = above;
    }

    public override string Reference => "index relative to air (20 °C, 1 atm)";

    /// <summary>ΔL/L₂₉₃, the lattice's relative change of length from 293 K.</summary>
    public double Expansion(double temperatureK) => temperatureK < _splitK
        ? Polynomial(_below, temperatureK - _belowOriginK)
        : Polynomial(_above, temperatureK - _splitK);

    /// <summary>The index relative to air, as the paper gives it.</summary>
    public double Relative(double lambdaUm, double temperatureK)
    {
        double l = Math.Exp(-3.0 * Expansion(temperatureK));
        double n2 = Polynomial(_epsilon, temperatureK) + l / (lambdaUm * lambdaUm) * Polynomial(_a, temperatureK);
        return n2 > 0.0 ? Math.Sqrt(n2) : double.NaN;
    }

    public override double Absolute(double lambdaVacuumUm, double temperatureK) =>
        Relative(lambdaVacuumUm, temperatureK) * ReferenceAir(lambdaVacuumUm);
}

/// <summary>
/// Li's model of the zinc chalcogenides (J. Phys. Chem. Ref. Data 13, 103 (1984), eq. (13)), with λ in µm and
/// t = T − 293 K: n² = E(t) + A(t)/(λ² − λu(t)²) + B(t)/(λ²/λI(t)² − 1), E, A and B quartics in t, and the
/// ultraviolet and infrared absorption wavelengths λu and λI linear in t. The index is relative to air, taken as dry
/// air at 20 °C and one atmosphere (<see cref="IndexModel.ReferenceAir"/>).
/// </summary>
public sealed class LiZincModel : IndexModel
{
    private readonly double[] _e, _a, _b, _lambdaU, _lambdaI;

    /// <param name="e">E(t), constant term first.</param>
    /// <param name="a">A(t).</param>
    /// <param name="b">B(t).</param>
    /// <param name="lambdaU">λu(t), µm.</param>
    /// <param name="lambdaI">λI(t), µm.</param>
    public LiZincModel(double[] e, double[] a, double[] b, double[] lambdaU, double[] lambdaI)
    {
        _e = e;
        _a = a;
        _b = b;
        _lambdaU = lambdaU;
        _lambdaI = lambdaI;
    }

    public override string Reference => "index relative to air (20 °C, 1 atm)";

    /// <summary>The index relative to air, as the paper gives it.</summary>
    public double Relative(double lambdaUm, double temperatureK)
    {
        double t = temperatureK - 293.0, l2 = lambdaUm * lambdaUm;
        double lu = Polynomial(_lambdaU, t), li = Polynomial(_lambdaI, t);
        double n2 = Polynomial(_e, t) + Polynomial(_a, t) / (l2 - lu * lu) + Polynomial(_b, t) / (l2 / (li * li) - 1.0);
        return n2 > 0.0 ? Math.Sqrt(n2) : double.NaN;
    }

    public override double Absolute(double lambdaVacuumUm, double temperatureK) =>
        Relative(lambdaVacuumUm, temperatureK) * ReferenceAir(lambdaVacuumUm);
}
