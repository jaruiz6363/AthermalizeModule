using System.Reflection;
using System.Text.Json;
using Athermalize.Core.Air;

namespace Athermalize.Core.Materials;

/// <summary>
/// A material measured over temperature, given as an absolute-index Sellmeier model whose coefficients are
/// polynomials in the temperature in kelvin:
/// n²(λ, T) − 1 = Σᵢ Sᵢ(T)·λ²/(λ² − λᵢ(T)²), Sᵢ(T) = Σⱼ Sᵢⱼ Tʲ, λᵢ(T) = Σⱼ λᵢⱼ Tʲ,
/// with λ the vacuum wavelength in µm. The index relative to the air is the absolute index over the air's.
/// </summary>
public sealed class MeasuredMaterial : Material
{
    private readonly double[][] _s, _lambda;

    public MeasuredMaterial(string catalog, string glass, string description, string source,
        double[][] s, double[][] lambda, (double Min, double Max) temperatureRangeK, (double Min, double Max) wavelengthRangeUm,
        double cte = 0.0, string cteSource = "")
    {
        if (s.Length != lambda.Length) throw new ArgumentException("S and λ need the same number of terms.");
        Catalog = catalog;
        Glass = glass;
        Description = description;
        Source = source;
        _s = s;
        _lambda = lambda;
        TemperatureRangeC = (temperatureRangeK.Min - 273.15, temperatureRangeK.Max - 273.15);
        WavelengthRangeUm = wavelengthRangeUm;
        Expansion = cte != 0.0 ? new ExpansionData(cte, 0, 0, 0, false, DataState.Present) : ExpansionData.None();
        CteSource = cteSource;
    }

    public override string Catalog { get; }
    public override string Glass { get; }
    public string Description { get; }
    public override string Source { get; }
    public override ExpansionData Expansion { get; }

    /// <summary>Where the expansion coefficient comes from, and what range it is for.</summary>
    public string CteSource { get; }
    public override (double Min, double Max) WavelengthRangeUm { get; }
    public override (double Min, double Max) TemperatureRangeC { get; }

    /// <summary>The absolute index at a vacuum wavelength, µm, and a temperature, K.</summary>
    public double AbsoluteIndex(double lambdaVacuumUm, double temperatureK)
    {
        double l2 = lambdaVacuumUm * lambdaVacuumUm, n2 = 1.0;
        for (int i = 0; i < _s.Length; i++)
        {
            double li = Polynomial(_lambda[i], temperatureK);
            n2 += Polynomial(_s[i], temperatureK) * l2 / (l2 - li * li);
        }
        return n2 > 0.0 ? Math.Sqrt(n2) : double.NaN;
    }

    public override MaterialIndex Index(double lambdaUm, AirConditions air, double temperatureC, IndexOptions? options = null)
    {
        var o = options ?? IndexOptions.Default;
        var (nAir, lambdaVac) = SystemAir(lambdaUm, air, o);
        double nAbs = AbsoluteIndex(lambdaVac, temperatureC + 273.15);
        return new MaterialIndex(nAbs / nAir, nAbs, nAir, lambdaVac, InRange(lambdaVac, temperatureC));
    }

    private static double Polynomial(double[] c, double x)
    {
        double y = 0.0;
        for (int j = c.Length - 1; j >= 0; j--) y = y * x + c[j];
        return y;
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
            static double[][] Rows(JsonElement e) =>
                e.EnumerateArray().Select(r => r.EnumerateArray().Select(v => v.GetDouble()).ToArray()).ToArray();
            static (double, double) Pair(JsonElement e) => (e[0].GetDouble(), e[1].GetDouble());

            double cte = 0.0;
            string cteSource = "";
            if (m.TryGetProperty("cte", out var c))
            {
                cte = c.GetProperty("value").GetDouble();
                cteSource = c.GetProperty("source").GetString() + "; " + c.GetProperty("range").GetString();
            }
            list.Add(new MeasuredMaterial(
                m.GetProperty("catalog").GetString()!, m.GetProperty("name").GetString()!,
                m.GetProperty("description").GetString()!, m.GetProperty("source").GetString()!,
                Rows(m.GetProperty("S")), Rows(m.GetProperty("lambda")),
                Pair(m.GetProperty("temperatureRangeK")), Pair(m.GetProperty("wavelengthRangeUm")), cte, cteSource));
        }
        return list;
    }
}
