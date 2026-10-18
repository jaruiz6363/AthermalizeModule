using System.Globalization;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.RayTrace;
using Athermalize.Core.Air;
using Athermalize.Core.Lens;
using Athermalize.Core.Materials;

namespace Athermalize.Cli;

/// <summary><c>atherm lens</c>: a lens file at several temperatures, in its mount.</summary>
internal static class LensCommand
{
    public const string Usage =
        "       atherm lens <file> --housing <material|α> [options]   the lens at each temperature, paraxially\n" +
        "         --housing <material|α>          the housing, which holds every air space and the image plane;\n" +
        "                                         a name from --mount-materials, or α in 10⁻⁶/K\n" +
        "         --spacer <s>:<material|α>[:<h>] a spacer in the air space after surface s, touching both\n" +
        "                                         surfaces at height h mm (0, the vertices)\n" +
        "         --part <a>-<b>:<material|α>[:<ha>[:<hb>]]   a part holding surface a to surface b, the last\n" +
        "                                         air space inside it taking up the rest (b may be the image)\n" +
        "         --mirror <s>:<material|α>       the substrate of the mirror at surface s (the housing's)\n" +
        "         --cemented following|preceding  which glass a cemented surface grows with (following)\n" +
        "         --use <name>=<material>         a material in place of the one the file names\n" +
        "         --cte <name>=<α>                an expansion coefficient, 10⁻⁶/K, in place of the data's\n" +
        "         --temperatures <°C,...>         (-20,20,60)\n" +
        "         --nominal <°C>                  the temperature the design is for (20)\n" +
        "         --pressure <atm>                the air pressure, the design's and at each temperature (1)\n" +
        "         --catalogs, --air, --prefactor, ... as for materials\n" +
        "         --detail                        the prescription at each temperature\n" +
        "       atherm lens --mount-materials      list the mount materials carried, with their sources";

    public static int Run(string[] args, TextWriter output)
    {
        string? file = null;
        string catalogs = Path.Combine(AppContext.BaseDirectory, "catalogs", "Glass");
        double[] temperatures = { -20, 20, 60 };
        double nominal = 20.0, atmospheres = 1.0;
        MountMaterial? housing = null;
        var segments = new List<MountSegment>();
        var mirrors = new Dictionary<int, MountMaterial>();
        var uses = new List<(string Name, string Material)>();
        var ctes = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var cemented = CementedSurface.FollowingGlass;
        bool detail = false;
        var o = IndexOptions.Default;

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{a} needs a value");
            switch (a)
            {
                case "--mount-materials":
                    foreach (var m in MountMaterials.All)
                        output.WriteLine(FormattableString.Invariant($"{m.Name,-22} α {m.Cte * 1e6,6:0.0##}×10⁻⁶/K  {m.Source}"));
                    return 0;
                case "--catalogs": catalogs = Next(); break;
                case "--temperatures": temperatures = Program.Numbers(Next(), a); break;
                case "--nominal": nominal = Program.Numbers(Next(), a)[0]; break;
                case "--pressure": atmospheres = Program.Numbers(Next(), a)[0]; break;
                case "--housing": housing = Material(Next(), a); break;
                case "--spacer": segments.Add(Spacer(Next(), a)); break;
                case "--part": segments.Add(Part(Next(), a)); break;
                case "--mirror":
                {
                    var (s, rest) = Split(Next(), ':', a);
                    mirrors[Integer(s, a)] = Material(rest, a);
                    break;
                }
                case "--cemented":
                    cemented = Program.Choose(Next(), a, ("following", CementedSurface.FollowingGlass), ("preceding", CementedSurface.PrecedingGlass));
                    break;
                case "--use": uses.Add(Split(Next(), '=', a)); break;
                case "--cte":
                {
                    var (name, value) = Split(Next(), '=', a);
                    ctes[name] = Program.Numbers(value, a)[0] * 1e-6;
                    break;
                }
                case "--detail": detail = true; break;
                case "--air" or "--prefactor" or "--thermal-wavelength" or "--lambda-tk-sign" or "--air-wavelength":
                    o = Program.IndexOption(o, a, Next()); break;
                default:
                    if (a.StartsWith("--")) throw new ArgumentException($"unknown option '{a}'");
                    if (file != null) throw new ArgumentException("name one lens file");
                    file = a;
                    break;
            }
        }
        if (file == null) throw new ArgumentException("name a lens file");
        if (housing == null) throw new ArgumentException("give the housing's material with --housing");

        var library = MaterialLibrary.FromFolder(catalogs);
        var substitutions = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, material) in uses)
            substitutions[name] = library.Find(material) ?? throw new ArgumentException($"--use: no material '{material}'");

        var system = LensFile.Read(file, library.Glasses);
        var mount = new Mount(housing, segments, mirrors, cemented);
        var air = new AirConditions(nominal, atmospheres * AirConditions.StandardPressurePa);
        var model = new ThermalModel(system, library, mount, air, new ThermalOptions(o, substitutions, ctes));

        int p = Math.Max(0, system.PrimaryWavelengthIndex);
        output.WriteLine($"{(string.IsNullOrWhiteSpace(system.Title) ? Path.GetFileName(file) : system.Title)} ({Path.GetFileName(file)}), {system.Surfaces.Count} surfaces");
        output.WriteLine(FormattableString.Invariant(
            $"Designed for {nominal:0.##} °C in {(air.IsVacuum ? "vacuum" : $"air at {atmospheres:0.####} atm")}; every part of the lens and the air at each temperature, the pressure held."));
        output.WriteLine(Describe(mount));
        output.WriteLine("Materials:");
        for (int i = 1; i < system.Surfaces.Count - 1; i++)
        {
            var m = model.MaterialAfter(i);
            if (m == null || system.Surfaces[i].IsMirror) continue;
            output.WriteLine(FormattableString.Invariant(
                $"  after surface {i,2}  {m.Name,-24} α {model.CteAfter(i) * 1e6:0.###}×10⁻⁶/K"));
        }
        output.WriteLine();
        output.WriteLine(FormattableString.Invariant(
            $"Paraxial, at {system.Wavelengths[p].Value:0.######} µm ({model.VacuumWavelengthsUm[p]:0.######} µm in vacuum):"));
        output.WriteLine(FormattableString.Invariant(
            $"    T °C        EFL mm        BFL mm   focus − image plane µm   change from {nominal:0.##} °C µm"));

        var notes = new List<string>();
        var lenses = temperatures.Select(t => model.At(air with { TemperatureC = t })).ToList();
        double nominalDefocus = Defocus(model.At(air));
        foreach (var lens in lenses)
        {
            var trace = ParaxialTrace.Trace(lens.System, lens.PrimaryIndices, 0.0);
            double defocus = Defocus(lens);
            output.WriteLine(FormattableString.Invariant(
                $"  {lens.TemperatureC,6:0.0}  {trace.Efl,12:0.000000}  {trace.Bfl,12:0.000000}  {defocus * 1e3,22:0.000}  {(defocus - nominalDefocus) * 1e3,22:0.000}"));
            foreach (var n in lens.Notes) if (!notes.Contains(n)) notes.Add(n);
        }
        if (notes.Count > 0)
        {
            output.WriteLine();
            foreach (var n in notes) output.WriteLine("Note: " + n);
        }

        if (detail)
            foreach (var lens in lenses)
            {
                output.WriteLine();
                output.WriteLine(FormattableString.Invariant($"At {lens.TemperatureC:0.##} °C:"));
                output.WriteLine("  surf        radius mm     thickness mm   n_rel        shape grows with / thickness set by");
                var n = lens.PrimaryIndices;
                for (int i = 1; i < lens.System.Surfaces.Count; i++)
                {
                    var s = lens.System.Surfaces[i];
                    var st = lens.Surfaces[i];
                    string radius = s.Curvature == 0.0 ? "infinity" : (1.0 / s.Curvature).ToString("0.000000", CultureInfo.InvariantCulture);
                    output.WriteLine(FormattableString.Invariant(
                        $"  {i,4}  {radius,15}  {s.Thickness,15:0.000000}  {n[i],10:0.0000000}   {st.ShapeSetBy}{(st.ThicknessSetBy != "" ? " / " + st.ThicknessSetBy : "")}"));
                }
            }
        return 0;
    }

    /// <summary>The paraxial focus at the primary wavelength less the distance to the image plane, mm.</summary>
    private static double Defocus(ThermalLens lens)
    {
        var trace = ParaxialTrace.Trace(lens.System, lens.PrimaryIndices, 0.0);
        return trace.ParaxialFocusDistance - lens.System.Surfaces[lens.System.LastOpticalSurface()].Thickness;
    }

    private static string Describe(Mount mount)
    {
        var parts = new List<string> { $"housing, {Show(mount.Housing)}" };
        foreach (var g in mount.Segments)
            parts.Add(g.To == g.From + 1
                ? FormattableString.Invariant($"spacer after surface {g.From}, {Show(g.Material)}{(g.FromHeight != 0 ? $", touching at {g.FromHeight:0.###} mm" : "")}")
                : FormattableString.Invariant($"part from surface {g.From} to {g.To}, {Show(g.Material)}"));
        foreach (var (s, m) in mount.MirrorSubstrates) parts.Add($"mirror {s} substrate, {Show(m)}");
        return "Mount: " + string.Join("; ", parts) + ".";
    }

    private static string Show(MountMaterial m) => m.Source == "given"
        ? m.Name
        : FormattableString.Invariant($"{m.Name} (α {m.Cte * 1e6:0.###}×10⁻⁶/K)");

    private static MountMaterial Material(string text, string option)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double cte))
            return MountMaterial.OfCte(cte * 1e-6);
        return MountMaterials.Find(text)
               ?? throw new ArgumentException($"{option}: '{text}' is neither a number nor a mount material (atherm lens --mount-materials)");
    }

    private static MountSegment Spacer(string text, string option)
    {
        var f = text.Split(':');
        if (f.Length is < 2 or > 3) throw new ArgumentException($"{option}: write <surface>:<material|α>[:<height>]");
        double h = f.Length == 3 ? Program.Numbers(f[2], option)[0] : 0.0;
        return MountSegment.Spacer(Integer(f[0], option), Material(f[1], option), h);
    }

    private static MountSegment Part(string text, string option)
    {
        var f = text.Split(':');
        if (f.Length is < 2 or > 4) throw new ArgumentException($"{option}: write <a>-<b>:<material|α>[:<ha>[:<hb>]]");
        var (a, b) = Split(f[0], '-', option);
        double ha = f.Length >= 3 ? Program.Numbers(f[2], option)[0] : 0.0;
        double hb = f.Length == 4 ? Program.Numbers(f[3], option)[0] : 0.0;
        return new MountSegment(Integer(a, option), Integer(b, option), Material(f[1], option), ha, hb);
    }

    private static (string, string) Split(string text, char at, string option)
    {
        int k = text.IndexOf(at);
        if (k <= 0 || k == text.Length - 1) throw new ArgumentException($"{option}: '{text}' needs the form x{at}y");
        return (text[..k], text[(k + 1)..]);
    }

    private static int Integer(string text, string option) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
            ? v : throw new ArgumentException($"{option}: '{text}' is not a surface number");
}
