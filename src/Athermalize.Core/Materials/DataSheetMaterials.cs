using System.Reflection;
using System.Text.Json;

namespace Athermalize.Core.Materials;

/// <summary>
/// Glasses the module carries from their makers' data sheets, where no catalog file has them: a Sellmeier
/// formula at the sheet's reference temperature and the constants of TIE-19's thermal model, read as a
/// <see cref="Tie19Material"/>. See DataSheetMaterials.json for the sources.
/// </summary>
public static class DataSheetMaterials
{
    /// <summary>The glasses this library carries.</summary>
    public static IReadOnlyList<Tie19Material> BuiltIn => _builtIn.Value;

    private static readonly Lazy<IReadOnlyList<Tie19Material>> _builtIn = new(() =>
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Athermalize.Core.Materials.DataSheetMaterials.json")
            ?? throw new InvalidOperationException("The data-sheet materials resource is missing.");
        return Read(stream);
    });

    /// <summary>Reads glasses in the format of DataSheetMaterials.json.</summary>
    public static IReadOnlyList<Tie19Material> Read(Stream json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<Tie19Material>();
        foreach (var m in doc.RootElement.GetProperty("materials").EnumerateArray())
        {
            static double[] Row(JsonElement e) => e.EnumerateArray().Select(v => v.GetDouble()).ToArray();
            static (double, double) Pair(JsonElement e) => (e[0].GetDouble(), e[1].GetDouble());

            double[] b = Row(m.GetProperty("B")), c = Row(m.GetProperty("C"));
            if (b.Length != c.Length) throw new InvalidOperationException("B and C need the same number of terms.");
            double Sellmeier(double lambda)
            {
                double l2 = lambda * lambda, n2 = 1.0;
                for (int i = 0; i < b.Length; i++) n2 += b[i] * l2 / (l2 - c[i]);
                return n2 > 0.0 ? Math.Sqrt(n2) : double.NaN;
            }

            var t = m.GetProperty("thermal");
            double Get(string field) => t.TryGetProperty(field, out var v) ? v.GetDouble() : 0.0;
            var thermal = new ThermalData(Get("D0"), Get("D1"), Get("D2"), Get("E0"), Get("E1"), Get("lambdaTk"),
                m.GetProperty("referenceTemperatureC").GetDouble(), DataState.Present);

            var x = m.GetProperty("cte");
            var expansion = new ExpansionData(x.GetProperty("value").GetDouble(), 0, 0, 0, false, DataState.Incomplete);

            list.Add(new Tie19Material(m.GetProperty("catalog").GetString()!, m.GetProperty("name").GetString()!,
                Sellmeier, thermal, expansion, Pair(m.GetProperty("wavelengthRangeUm")),
                m.GetProperty("source").GetString()! + "; α " + x.GetProperty("range").GetString(),
                Pair(m.GetProperty("temperatureRangeC"))));
        }
        return list;
    }
}
