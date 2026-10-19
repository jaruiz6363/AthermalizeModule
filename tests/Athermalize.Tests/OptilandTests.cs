using System.Text.Json;
using AberrationCalculator.Optiland;
using Athermalize.Core.Air;
using Athermalize.Core.Materials;
using Python.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace Athermalize.Tests;

/// <summary>
/// The index over temperature against Optiland's, an independent implementation of the same model (SCHOTT TIE-19)
/// in Python, run in process through AberrationCalculator's embedding (tools/setup-python.ps1). Optiland's glasses
/// come from the refractiveindex.info database it ships with.
/// </summary>
public class OptilandTests
{
    private readonly ITestOutputHelper _out;
    public OptilandTests(ITestOutputHelper output) => _out = output;

    /// <summary>Optiland's conventions, which the module's options can all take.</summary>
    private static readonly IndexOptions OptilandConventions = IndexOptions.Default with
    {
        Prefactor = Prefactor.Absolute,
        ThermalWavelength = ThermalWavelength.SystemAir,
        LambdaTkSign = LambdaTkSign.Ignore,
        AirWavelength = AirWavelength.Input,
    };

    private static readonly double[] Wavelengths = { 0.4047, 0.4861, 0.5876, 0.6563, 0.8521, 1.014, 1.5296, 2.3254 };
    private static readonly double[] Temperatures = { -40.0, 0.0, 20.0, 60.0, 100.0 };
    private static readonly double[] Pressures = { 1.0, 0.5 };

    private sealed record Glass(string Maker, string Name, string Path, double T0, double[] Coefficients, double[] Range);

    /// <summary>
    /// The same glass in both: Optiland's dispersion and its thermal constants, through the module's code with
    /// Optiland's conventions. Every database glass Optiland changes with temperature, at its wavelengths from 0.40
    /// to 2.33 µm, from −40 to +100 °C, at one atmosphere and at half of one.
    /// </summary>
    [OptilandFact]
    public void Same_glass_same_index()
    {
        var glasses = Glasses();
        double worst = 0.0;
        string where = "";
        int points = 0;
        foreach (var g in glasses)
        {
            var lambdas = Wavelengths.Where(l => l >= g.Range[0] && l <= g.Range[1]).ToArray();
            if (lambdas.Length == 0) continue;
            var c = g.Coefficients;
            var thermal = new ThermalData(c[0], c[1], c[2], c[3], c[4], c[5], g.T0, DataState.Present);
            var catalog = CatalogIndex(g, lambdas, g.T0);
            var m = new Tie19Material(g.Maker, g.Name, l => catalog.TryGetValue(l, out double n) ? n : CatalogIndex(g, new[] { l }, g.T0)[l],
                thermal, ExpansionData.None(), (g.Range[0], g.Range[1]), "Optiland database");
            foreach (double p in Pressures)
            {
                var theirs = Index(g, lambdas, Temperatures, p);
                for (int t = 0; t < Temperatures.Length; t++)
                    for (int w = 0; w < lambdas.Length; w++)
                    {
                        double ours = m.Index(lambdas[w], AirConditions.AtAtmospheres(Temperatures[t], p), Temperatures[t], OptilandConventions).Relative;
                        double d = Math.Abs(ours - theirs[t][w]);
                        points++;
                        if (d > worst) (worst, where) = (d, $"{g.Maker}/{g.Name} at {lambdas[w]} µm, {Temperatures[t]} °C, {p} atm");
                    }
            }
        }
        _out.WriteLine($"Optiland {PythonSession.OptilandVersion}: {glasses.Count} glasses, {points} indices; worst {worst:E2} ({where})");
        Assert.True(glasses.Count > 1000);
        Assert.True(worst < 1e-12, $"worst {worst:E2} at {where}");
    }

    /// <summary>
    /// What each convention the module defaults to differently from Optiland changes, over the same glasses: the
    /// largest change of the index at the d line from 20 °C to −40 and +100 °C.
    /// </summary>
    [OptilandFact]
    public void Each_convention_difference_is_small()
    {
        var glasses = Glasses();
        var cases = new (string Name, IndexOptions Options)[]
        {
            ("prefactor relative", OptilandConventions with { Prefactor = Prefactor.Relative }),
            ("E-term wavelength in reference air", OptilandConventions with { ThermalWavelength = ThermalWavelength.ReferenceAir }),
            ("lambda-tk sign agf", OptilandConventions with { LambdaTkSign = LambdaTkSign.Agf }),
            ("the module's defaults", IndexOptions.Default),
        };
        foreach (var (name, options) in cases)
        {
            double worst = 0.0;
            string where = "";
            foreach (var g in glasses.Where(g => g.Range[0] <= 0.5876 && g.Range[1] >= 0.5876))
            {
                var c = g.Coefficients;
                var thermal = new ThermalData(c[0], c[1], c[2], c[3], c[4], c[5], g.T0, DataState.Present);
                var catalog = new Dictionary<double, double>();
                double Reference(double l) => catalog.TryGetValue(l, out double n) ? n : catalog[l] = CatalogIndex(g, new[] { l }, g.T0)[l];
                var m = new Tie19Material(g.Maker, g.Name, Reference, thermal, ExpansionData.None(), (g.Range[0], g.Range[1]), "Optiland database");
                foreach (double t in new[] { -40.0, 100.0 })
                {
                    var air = AirConditions.AtAtmospheres(t, 1.0);
                    double d = Math.Abs(m.Index(0.5876, air, t, options).Relative - m.Index(0.5876, air, t, OptilandConventions).Relative);
                    if (d > worst) (worst, where) = (d, $"{g.Maker}/{g.Name} at {t} °C");
                }
            }
            _out.WriteLine($"{name}: largest change {worst:E2} ({where})");
            Assert.True(worst < 2e-5, $"{name}: {worst:E2} at {where}");
        }
    }

    /// <summary>
    /// The module's catalog glasses against Optiland's database glasses of the same name and maker: dn/dT at the d
    /// line and 20 °C, each with its own conventions. Where the two carry the same thermal constants the difference
    /// is the conventions' alone; the rest differ because the catalogs are different editions, and are only counted.
    /// </summary>
    [OptilandFact]
    public void Catalog_glasses_agree_with_Optilands_database()
    {
        var makers = new Dictionary<string, string>
        {
            ["schott"] = "SCHOTT", ["ohara"] = "OHARA", ["hoya"] = "HOYA", ["cdgm"] = "CDGM", ["sumita"] = "SUMITA",
        };
        int same = 0, differ = 0, absent = 0;
        double worstSame = 0.0;
        var spread = new List<double>();
        foreach (var g in Glasses().Where(g => makers.ContainsKey(g.Maker) && g.Range[0] <= 0.5876 && g.Range[1] >= 0.5876))
        {
            if (Catalogs.Library.Find(makers[g.Maker] + ":" + g.Name) is not Tie19Material ours || !ours.Thermal.HasCoefficients)
            {
                absent++;
                continue;
            }
            double D(Func<double, double> n) => n(20.5) - n(19.5);
            double mine = D(t => ours.Index(0.5876, new AirConditions(t), t).Relative);
            var theirs = Index(g, new[] { 0.5876 }, new[] { 19.5, 20.5 }, 1.0);
            double diff = Math.Abs(mine - (theirs[1][0] - theirs[0][0]));
            var a = ours.Thermal;
            var c = g.Coefficients;
            bool equal = new[] { a.D0, a.D1, a.D2, a.E0, a.E1, Math.Abs(a.LambdaTkUm) }.Zip(new[] { c[0], c[1], c[2], c[3], c[4], Math.Abs(c[5]) })
                .All(p => Math.Abs(p.First - p.Second) <= 1e-9 * Math.Max(Math.Abs(p.First), 1e-12)) && a.ReferenceTemperatureC == g.T0;
            if (equal) { same++; worstSame = Math.Max(worstSame, diff); }
            else { differ++; spread.Add(diff); }
        }
        spread.Sort();
        double median = spread.Count > 0 ? spread[spread.Count / 2] : 0.0;
        _out.WriteLine($"same constants: {same} glasses, dn/dT within {worstSame:E2}/K; other constants: {differ} glasses, median {median:E2}/K, largest {(spread.Count > 0 ? spread[^1] : 0):E2}/K; not in the module's catalogs or without constants there: {absent}");
        Assert.True(same > 100);
        Assert.True(worstSame < 2e-8, $"{worstSame:E2}");
    }

    // ── Optiland, through Python.NET ─────────────────────────────────────────────────

    private static readonly Lazy<List<Glass>> _glasses = new(() => PythonSession.WithGil(() =>
    {
        string json = Helper().glasses().ToString();
        return JsonSerializer.Deserialize<List<JsonElement>>(json)!.Select(e => new Glass(
            e.GetProperty("maker").GetString()!, e.GetProperty("name").GetString()!, e.GetProperty("path").GetString()!,
            e.GetProperty("t0").GetDouble(), e.GetProperty("coefficients").EnumerateArray().Select(x => x.GetDouble()).ToArray(),
            e.GetProperty("range").EnumerateArray().Select(x => x.GetDouble()).ToArray())).ToList();
    }));

    private static List<Glass> Glasses() => _glasses.Value;

    /// <summary>Optiland's index for each temperature (rows) and wavelength (columns), relative to the air there.</summary>
    private static double[][] Index(Glass g, double[] lambdas, double[] temperatures, double atmospheres) =>
        PythonSession.WithGil(() =>
        {
            string json = Helper().index(g.Path, JsonSerializer.Serialize(lambdas), JsonSerializer.Serialize(temperatures),
                atmospheres).ToString();
            return JsonSerializer.Deserialize<double[][]>(json)!;
        });

    /// <summary>
    /// Optiland's catalog index at the wavelengths in the reference air that the module will ask for: each of
    /// <paramref name="lambdas"/> in the air at each test temperature and pressure, taken to the reference air.
    /// </summary>
    private static Dictionary<double, double> CatalogIndex(Glass g, double[] lambdas, double t0)
    {
        var wanted = new List<double>();
        foreach (double l in lambdas)
        {
            wanted.Add(l);
            foreach (double t in Temperatures)
                foreach (double p in Pressures)
                    wanted.Add(l * AirIndex.Tie19(l, t, p) / AirIndex.Tie19(l, t0, 1.0));
        }
        var n = PythonSession.WithGil(() =>
            JsonSerializer.Deserialize<double[]>(Helper().catalog_index(g.Path, JsonSerializer.Serialize(wanted)).ToString())!);
        var map = new Dictionary<double, double>();
        for (int i = 0; i < wanted.Count; i++) map[wanted[i]] = n[i];
        return map;
    }

    private static dynamic Helper()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "python");
        dynamic sys = Py.Import("sys");
        bool present = false;
        foreach (PyObject p in sys.path) if (p.ToString() == dir) { present = true; break; }
        if (!present) sys.path.insert(0, dir);
        return Py.Import("athermalize_optiland");
    }
}

/// <summary>A test that needs the embedded Python with Optiland: skipped, with the setup hint, when it is not there.</summary>
public sealed class OptilandFactAttribute : FactAttribute
{
    public OptilandFactAttribute()
    {
        if (!PythonEnvironment.IsReady) Skip = PythonEnvironment.SetupHint;
    }
}
