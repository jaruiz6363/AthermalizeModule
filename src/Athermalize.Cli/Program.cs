using System.Globalization;
using System.Text;
using Athermalize.Core.Air;
using Athermalize.Core.Materials;

namespace Athermalize.Cli;

public static class Program
{
    private const string Usage =
        "usage: atherm materials <name>... [options]   index and thermal constants over temperature\n" +
        "         --catalogs <dir>                .agf folder (the catalogs next to atherm)\n" +
        "         --wavelengths <µm,...>          in standard air, 20 °C and 1 atm (0.4861327,0.5875618,0.6562725)\n" +
        "         --vacuum                        the wavelengths are vacuum wavelengths\n" +
        "         --temperatures <°C,...>         material and air temperatures (-20,20,60)\n" +
        "         --pressure <atm>                air pressure in standard atmospheres; 0 is vacuum (1)\n" +
        "         --air tie19|ciddor              the air model (tie19)\n" +
        "         --humidity <%>                  relative humidity, ciddor only (0)\n" +
        "         --co2 <ppm>                     CO₂ content, ciddor only (450)\n" +
        "         --prefactor relative|absolute   the index in TIE-19's (n²−1)/2n (relative)\n" +
        "         --thermal-wavelength reference|vacuum   the wavelength in TIE-19's E term (reference)\n" +
        "         --lambda-tk-sign agf|ignore     a negative λtk adds to λ² (agf)\n" +
        "         --air-wavelength input|vacuum   the wavelength the air formulas take (input)\n" +
        "       atherm materials --measured        list the measured and data-sheet materials carried, with sources\n" +
        LensCommand.Usage + "\n" +
        AthermalCommand.Usage;

    /// <summary>
    /// Reports carry µ, ², λ and °, which a Windows console's code page has not all of: write UTF-8 to the console
    /// or a redirected file alike, and leave the console as it was found.
    /// </summary>
    public static int Main(string[] args)
    {
        var encoding = Console.OutputEncoding;
        try { Console.OutputEncoding = new UTF8Encoding(false); } catch (IOException) { }
        try { return Run(args, Console.Out); }
        finally { try { Console.OutputEncoding = encoding; } catch (IOException) { } }
    }

    internal static int Run(string[] args, TextWriter output)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            if (args.Length == 0 || args[0] is "-h" or "--help") { output.WriteLine(Usage); return args.Length == 0 ? 1 : 0; }
            return args[0] switch
            {
                "materials" => Materials(args.Skip(1).ToArray(), output),
                "lens" or "focus" or "image" or "select" => LensCommand.Run(args[0], args.Skip(1).ToArray(), output),
                "map" or "solve" => AthermalCommand.Run(args[0], args.Skip(1).ToArray(), output),
                _ => Fail($"unknown command '{args[0]}'"),
            };
        }
        catch (ArgumentException e) { return Fail(e.Message); }
        catch (Exception e) when (e is InvalidOperationException or IOException or NotSupportedException)
        {
            Console.Error.WriteLine("atherm: " + e.Message);
            return 1;
        }

        int Fail(string message)
        {
            Console.Error.WriteLine("atherm: " + message);
            Console.Error.WriteLine(Usage);
            return 1;
        }
    }

    private static int Materials(string[] args, TextWriter output)
    {
        var names = new List<string>();
        string catalogs = Path.Combine(AppContext.BaseDirectory, "catalogs", "Glass");
        double[] wavelengths = { 0.4861327, 0.5875618, 0.6562725 };
        double[] temperatures = { -20, 20, 60 };
        bool vacuum = false, listMeasured = false;
        double atmospheres = 1.0, humidity = 0.0, co2 = 450.0;
        var o = IndexOptions.Default;

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{a} needs a value");
            switch (a)
            {
                case "--catalogs": catalogs = Next(); break;
                case "--wavelengths": wavelengths = Numbers(Next(), a); break;
                case "--vacuum": vacuum = true; break;
                case "--temperatures": temperatures = Numbers(Next(), a); break;
                case "--pressure": atmospheres = Numbers(Next(), a)[0]; break;
                case "--humidity": humidity = Numbers(Next(), a)[0] / 100.0; break;
                case "--co2": co2 = Numbers(Next(), a)[0]; break;
                case "--air" or "--prefactor" or "--thermal-wavelength" or "--lambda-tk-sign" or "--air-wavelength":
                    o = IndexOption(o, a, Next()); break;
                case "--measured": listMeasured = true; break;
                default:
                    if (a.StartsWith("--")) throw new ArgumentException($"unknown option '{a}'");
                    names.Add(a);
                    break;
            }
        }

        var library = MaterialLibrary.FromFolder(catalogs);
        if (listMeasured)
        {
            foreach (var m in library.Measured)
                output.WriteLine(FormattableString.Invariant(
                    $"{m.Name,-22} {m.Description}; {m.WavelengthRangeUm.Min}-{m.WavelengthRangeUm.Max} µm, {m.TemperatureRangeC.Min + 273.15:0}-{m.TemperatureRangeC.Max + 273.15:0} K\n{"",-22} {m.Source}"));
            output.WriteLine();
            output.WriteLine("From makers' data sheets (TIE-19 model):");
            foreach (var g in library.DataSheet)
                output.WriteLine(FormattableString.Invariant(
                    $"{g.Name,-22} {g.WavelengthRangeUm.Min}-{g.WavelengthRangeUm.Max} µm, {g.TemperatureRangeC.Min:0} to {g.TemperatureRangeC.Max:0} °C\n{"",-22} {g.Source}"));
            return 0;
        }
        if (names.Count == 0) throw new ArgumentException("name at least one material");

        var air = new AirConditions(20.0, atmospheres * AirConditions.StandardPressurePa, humidity, co2);
        output.WriteLine(Conditions(air, o, vacuum));

        foreach (var name in names)
        {
            var m = library.Find(name) ?? throw new ArgumentException($"no material '{name}' in {catalogs}, the measured materials or the data-sheet glasses");
            output.WriteLine();
            output.WriteLine(Header(m));
            output.WriteLine("    T °C      λ µm      n_rel        n_abs     dn_rel/dT  dn_abs/dT      x_f   (10⁻⁶/K)");
            foreach (double t in temperatures)
                foreach (double l in wavelengths)
                {
                    double lv = vacuum ? l : ThermoOptic.VacuumWavelength(l, o.AirModel);
                    var c = ThermoOptic.At(m, lv, t, air, o);
                    output.WriteLine(FormattableString.Invariant(
                        $"  {t,6:0.0}  {l,8:0.000000}  {c.Relative,10:0.0000000}  {c.Absolute,10:0.0000000}  {c.DnDtRelative * 1e6,9:0.000}  {c.DnDtAbsolute * 1e6,9:0.000}  {(double.IsNaN(c.OptothermalExpansion) ? "     -" : (c.OptothermalExpansion * 1e6).ToString("0.000", CultureInfo.InvariantCulture)),8}{(c.InRange ? "" : "   outside the data's range")}"));
                }
        }
        return 0;
    }

    /// <summary>One of the index model's convention switches.</summary>
    internal static IndexOptions IndexOption(IndexOptions o, string option, string value) => option switch
    {
        "--air" => o with { AirModel = Choose(value, option, ("tie19", AirModel.Tie19), ("ciddor", AirModel.Ciddor1996)) },
        "--prefactor" => o with { Prefactor = Choose(value, option, ("relative", Prefactor.Relative), ("absolute", Prefactor.Absolute)) },
        "--thermal-wavelength" => o with { ThermalWavelength = Choose(value, option, ("reference", ThermalWavelength.ReferenceAir), ("vacuum", ThermalWavelength.Vacuum)) },
        "--lambda-tk-sign" => o with { LambdaTkSign = Choose(value, option, ("agf", LambdaTkSign.Agf), ("ignore", LambdaTkSign.Ignore)) },
        "--air-wavelength" => o with { AirWavelength = Choose(value, option, ("input", AirWavelength.Input), ("vacuum", AirWavelength.Vacuum)) },
        _ => throw new ArgumentException($"unknown option '{option}'"),
    };

    private static string Conditions(AirConditions air, IndexOptions o, bool vacuum)
    {
        var s = new StringBuilder();
        s.Append(air.IsVacuum
            ? "Vacuum"
            : FormattableString.Invariant($"Air at {air.Atmospheres:0.####} atm"));
        if (o.AirModel == AirModel.Ciddor1996)
            s.Append(FormattableString.Invariant($", {air.RelativeHumidity * 100:0.#} % RH, {air.Co2Ppm:0} ppm CO₂, Ciddor (1996)"));
        else if (!air.IsVacuum)
            s.Append(", dry, TIE-19 air");
        s.Append("; material and air at each temperature. Wavelengths ");
        s.Append(vacuum ? "in vacuum." : "in standard air (20 °C, 1 atm); each is held at its vacuum wavelength.");
        s.Append(FormattableString.Invariant(
            $"\nPrefactor {o.Prefactor.ToString().ToLowerInvariant()}; E-term wavelength {(o.ThermalWavelength == ThermalWavelength.ReferenceAir ? "in reference air" : "vacuum")}; λtk sign {o.LambdaTkSign.ToString().ToLowerInvariant()}; air formulas at the {(o.AirWavelength == AirWavelength.Input ? "input" : "vacuum")} wavelength."));
        s.Append("\nx_f = α − (dn_rel/dT)/(n_rel − 1), the relative change of a thin lens's focal length per kelvin.");
        return s.ToString();
    }

    private static string Header(Material m)
    {
        string text = m switch
        {
            Tie19Material t => FormattableString.Invariant(
                $"{m.Name}: thermal data {Describe(t.Thermal.State)}{(t.Thermal.State is DataState.Present or DataState.Incomplete ? $", Tref {t.Thermal.ReferenceTemperatureC:0.##} °C" : "")}{(t.Thermal.HasCoefficients ? "" : "; no thermal coefficients, so only the air changes")}"),
            MeasuredMaterial mm => $"{m.Name}: {mm.Description}, {mm.Model.Reference} from a model fitted to measurements; {mm.Source}",
            _ => m.Name,
        };
        text += m.Expansion.Cte != 0.0
            ? FormattableString.Invariant($"; α {m.Expansion.Cte * 1e6:0.###}×10⁻⁶/K")
            : "; no expansion coefficient";
        return text;
    }

    private static string Describe(DataState s) => s switch
    {
        DataState.Present => "present",
        DataState.Incomplete => "incomplete (missing fields take their defaults)",
        DataState.Empty => "empty",
        _ => "missing",
    };

    internal static double[] Numbers(string text, string option)
    {
        var values = new List<double>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                throw new ArgumentException($"{option}: '{part}' is not a number");
            values.Add(v);
        }
        if (values.Count == 0) throw new ArgumentException($"{option} needs a value");
        return values.ToArray();
    }

    internal static T Choose<T>(string text, string option, params (string Name, T Value)[] choices)
    {
        foreach (var (name, value) in choices)
            if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase)) return value;
        throw new ArgumentException($"{option}: '{text}' is not one of {string.Join(", ", choices.Select(c => c.Name))}");
    }
}
