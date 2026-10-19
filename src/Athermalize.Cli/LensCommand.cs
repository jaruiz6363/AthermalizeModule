using System.Globalization;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Athermalize.Core.Air;
using Athermalize.Core.Analysis;
using Athermalize.Core.Lens;
using Athermalize.Core.Materials;

namespace Athermalize.Cli;

/// <summary>
/// <c>atherm lens</c>, <c>atherm focus</c> and <c>atherm image</c>: a lens file at several temperatures, in its
/// mount, paraxially, as a first-order thermal report, and as image quality.
/// </summary>
internal static class LensCommand
{
    public const string Usage =
        "       atherm lens <file> --housing <material|α> [options]    the lens at each temperature, paraxially\n" +
        "       atherm focus <file> --housing <material|α> [options]   the first-order report: focus against the\n" +
        "                                         depth of focus, and each part's contribution to the focus shift\n" +
        "       atherm image <file> --housing <material|α> [options]   RMS wavefront error and MTF,\n" +
        "                                         at the fixed image plane and refocused\n" +
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
        "         --mount <file>                  the mount file (<lens>.mount.json beside the lens, read when it is\n" +
        "                                         there); the options given here override it\n" +
        "         --save-mount                    write the mount and the design conditions to the mount file\n" +
        "                                         (select: the best combination's housing and mount parts)\n" +
        "         --no-mount                      ignore the mount file\n" +
        "         --pressure <atm>                the air pressure, the design's and at each temperature (1)\n" +
        "         --catalogs, --air, --prefactor, ... as for materials\n" +
        "         --detail                        lens: the prescription at each temperature\n" +
        "         --frequencies <c/mm,...>        image: the MTF's frequencies (10,30)\n" +
        "         --mtf-limit <m>                 image: the temperatures over which the lowest MTF at the first\n" +
        "                                         frequency stays above m, searched over --range (-60,100)\n" +
        "         --mtf diffraction|geometric     image: which MTF (diffraction)\n" +
        "         --grid <n>                      image: the diffraction MTF's n×n pupil grid (64)\n" +
        "         --monochromatic                 image: the primary wavelength only\n" +
        "         --obscuration <ε>               image: a central obstruction, a fraction of the pupil radius\n" +
        "         --svg <file>                    focus: plot the focus change against temperature, with the depth\n" +
        "                                         of focus, and each part's contribution; image: plot the RMS\n" +
        "                                         and the lowest MTF at each frequency against temperature,\n" +
        "                                         fixed and refocused\n" +
        "         --plot-points <n>               image: the plot's temperatures, evenly over --temperatures (13)\n" +
        "       atherm select <file> --housing <material|α> [options]  every combination of candidate glasses\n" +
        "                                         and mount materials, scored on the lens at its shapes\n" +
        "         --glass <s>:<material>,...      candidates for the glass after surface s\n" +
        "         --near <s>[:<Δn>[:<Δν>]]        candidates near the glass after surface s (0.05, 5), from --catalog\n" +
        "         --catalog <name>                the catalog for --near (all)\n" +
        "         --housing-candidates <m>,...    candidates for the housing\n" +
        "         --part-candidates <k>:<m>,...   candidates for mount part k, counted from 0 in the order given\n" +
        "         --delta-t <K>                   the half-range the focus must hold over (60)\n" +
        "         --keep <n>                      how many of the best to print (10)\n" +
        "       atherm lens --mount-materials      list the mount materials carried, with their sources";

    public static int Run(string mode, string[] args, TextWriter output)
    {
        string? file = null;
        string catalogs = Path.Combine(AppContext.BaseDirectory, "catalogs", "Glass");
        double[] temperatures = { -20, 20, 60 };
        double[] frequencies = { 10, 30 };
        double[] range = { -60, 100 };
        double obscuration = 0.0;
        double? nominalGiven = null, atmospheresGiven = null;
        string? mountPath = null;
        bool noMount = false, saveMount = false;
        double? mtfLimit = null;
        double deltaT = 60.0;
        int keep = 10;
        string? searchCatalog = null;
        var glassCandidates = new Dictionary<int, List<string>>();
        var nearGlass = new List<(int Surface, double Dn, double Dnu)>();
        var housingCandidates = new List<MountMaterial>();
        var partCandidates = new Dictionary<int, List<MountMaterial>>();
        MountMaterial? housing = null;
        var segments = new List<MountSegment>();
        var mirrors = new Dictionary<int, MountMaterial>();
        var uses = new List<(string Name, string Material)>();
        var ctes = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        CementedSurface? cementedGiven = null;
        bool detail = false, monochromatic = false;
        var mtfKind = MtfKind.Diffraction;
        int grid = 64, plotPoints = 13;
        string? svg = null;
        var o = IndexOptions.Default;

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{a} needs a value");
            switch (a)
            {
                case "--mount-materials":
                    foreach (var m in MountMaterials.All)
                        output.WriteLine(FormattableString.Invariant($"{m.Name,-22} α {m.Cte * 1e6,6:0.0##}×10⁻⁶/K{(m.Curve != null ? " at 20 °C" : ""),-9}  {m.Source}"));
                    return 0;
                case "--catalogs": catalogs = Next(); break;
                case "--temperatures": temperatures = Program.Numbers(Next(), a); break;
                case "--nominal": nominalGiven = Program.Numbers(Next(), a)[0]; break;
                case "--pressure": atmospheresGiven = Program.Numbers(Next(), a)[0]; break;
                case "--mount": mountPath = Next(); break;
                case "--no-mount": noMount = true; break;
                case "--save-mount": saveMount = true; break;
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
                    cementedGiven = Program.Choose(Next(), a, ("following", CementedSurface.FollowingGlass), ("preceding", CementedSurface.PrecedingGlass));
                    break;
                case "--use": uses.Add(Split(Next(), '=', a)); break;
                case "--cte":
                {
                    var (name, value) = Split(Next(), '=', a);
                    ctes[name] = Program.Numbers(value, a)[0] * 1e-6;
                    break;
                }
                case "--detail": detail = true; break;
                case "--frequencies": frequencies = Program.Numbers(Next(), a); break;
                case "--mtf-limit": mtfLimit = Program.Numbers(Next(), a)[0]; break;
                case "--range":
                    range = Program.Numbers(Next(), a);
                    if (range.Length != 2 || range[0] >= range[1]) throw new ArgumentException("--range: give <low>,<high>");
                    break;
                case "--monochromatic": monochromatic = true; break;
                case "--mtf": mtfKind = Program.Choose(Next(), a, ("diffraction", MtfKind.Diffraction), ("geometric", MtfKind.Geometric)); break;
                case "--grid": grid = (int)Program.Numbers(Next(), a)[0]; break;
                case "--obscuration": obscuration = Program.Numbers(Next(), a)[0]; break;
                case "--svg": svg = Next(); break;
                case "--plot-points": plotPoints = (int)Program.Numbers(Next(), a)[0]; break;
                case "--glass":
                {
                    var (k, list) = Split(Next(), ':', a);
                    glassCandidates[Integer(k, a)] = list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                    break;
                }
                case "--near":
                {
                    var f = Next().Split(':');
                    nearGlass.Add((Integer(f[0], a), f.Length > 1 ? Program.Numbers(f[1], a)[0] : 0.05, f.Length > 2 ? Program.Numbers(f[2], a)[0] : 5.0));
                    break;
                }
                case "--catalog": searchCatalog = Next(); break;
                case "--housing-candidates":
                    housingCandidates.AddRange(Next().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(m => Material(m, a)));
                    break;
                case "--part-candidates":
                {
                    var (k, list) = Split(Next(), ':', a);
                    partCandidates[Integer(k, a)] = list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(m => Material(m, a)).ToList();
                    break;
                }
                case "--delta-t": deltaT = Program.Numbers(Next(), a)[0]; break;
                case "--keep": keep = (int)Program.Numbers(Next(), a)[0]; break;
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

        if (svg != null && mode is not ("focus" or "image")) throw new ArgumentException("--svg: only focus and image plot");
        if (plotPoints < 2) throw new ArgumentException("--plot-points: give at least 2");
        bool overrides = housing != null || segments.Count > 0 || mirrors.Count > 0 || uses.Count > 0 || ctes.Count > 0
                         || nominalGiven != null || atmospheresGiven != null || cementedGiven != null;
        bool HasOverrides() => overrides;

        // The mount file beside the lens, or the one named; the options given here override it.
        string mountFile = mountPath ?? MountFile.PathFor(file);
        MountFile? saved = null;
        if (!noMount && File.Exists(mountFile)) saved = MountFile.Read(mountFile);
        else if (!noMount && mountPath != null && !saveMount) throw new ArgumentException($"--mount: no file '{mountPath}'");
        var fromFile = saved?.ToMount();
        housing ??= fromFile?.Housing;
        if (segments.Count == 0 && fromFile != null) segments.AddRange(fromFile.Segments);
        if (fromFile != null)
            foreach (var (k, m) in fromFile.MirrorSubstrates) mirrors.TryAdd(k, m);
        var cemented = cementedGiven ?? saved?.CementedRule ?? CementedSurface.FollowingGlass;
        double nominal = nominalGiven ?? saved?.NominalTemperatureC ?? 20.0;
        double atmospheres = atmospheresGiven ?? saved?.PressureAtm ?? 1.0;
        if (saved != null)
        {
            foreach (var (k, v) in saved.Use) if (!uses.Any(u => string.Equals(u.Name, k, StringComparison.OrdinalIgnoreCase))) uses.Add((k, v));
            foreach (var (k, v) in saved.Cte) ctes.TryAdd(k, v * 1e-6);
        }
        void Save(Mount m)
        {
            var use = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, material) in uses) use[name] = material;
            MountFile.From(m, nominal, atmospheres, use, ctes.ToDictionary(c => c.Key, c => c.Value * 1e6, StringComparer.OrdinalIgnoreCase))
                .Write(mountFile);
            output.WriteLine();
            output.WriteLine($"Saved the mount to {mountFile}.");
        }
        if (housing == null)
            throw new ArgumentException($"give the housing's material with --housing, or in a mount file ({Path.GetFileName(mountFile)} beside the lens, or --mount)");

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
        if (saved != null) output.WriteLine($"Mount file: {mountFile}{(HasOverrides() ? ", with the options given here over it" : "")}.");
        output.WriteLine(Describe(mount));
        output.WriteLine("Materials:");
        for (int i = 1; i < system.Surfaces.Count - 1; i++)
        {
            var m = model.MaterialAfter(i);
            if (m == null || system.Surfaces[i].IsMirror) continue;
            output.WriteLine(FormattableString.Invariant(
                $"  after surface {i,2}  {m.Name,-24} α {model.CteAfter(i) * 1e6:0.###}×10⁻⁶/K"));
        }

        var lenses = temperatures.Select(t => model.At(air with { TemperatureC = t })).ToList();
        if (mode == "select")
        {
            var glasses = new Dictionary<int, IReadOnlyList<Material>>();
            foreach (var (k, list) in glassCandidates)
                glasses[k] = list.Select(n => library.Find(n) ?? throw new ArgumentException($"--glass: no material '{n}'")).ToList();
            foreach (var (k, dn, dnu) in nearGlass)
            {
                var own = model.MaterialAfter(k) ?? throw new ArgumentException($"--near: surface {k} is not followed by glass");
                glasses[k] = MaterialSearch.Neighbors(library, own, dn, dnu, searchCatalog == null ? null : new[] { searchCatalog });
            }
            var candidates = new SearchCandidates
            {
                Glasses = glasses,
                Housing = housingCandidates,
                Parts = partCandidates.ToDictionary(p => p.Key, p => (IReadOnlyList<MountMaterial>)p.Value),
            };
            var best = Select(system, library, mount, air, new ThermalOptions(o, substitutions, ctes), candidates, deltaT, keep, output);
            if (saveMount)
            {
                var winner = new Mount(best.Housing, mount.Segments.Select((g, k) => g with { Material = best.Parts[k] }),
                    mount.MirrorSubstrates, mount.Cemented);
                Save(winner);
                if (best.Glasses.Count > 0)
                    output.WriteLine("The glasses it chose are not saved: a glass swap changes the lens, which the lens file holds.");
            }
            return 0;
        }
        if (mode == "focus") Focus(model, temperatures, p, svg, output);
        else if (mode == "image")
            Image(model, temperatures, frequencies, mtfLimit, range,
                new QualityOptions { Polychromatic = !monochromatic, Obscuration = obscuration, MtfKind = mtfKind, DiffractionGrid = grid },
                svg, plotPoints, output);
        else Paraxial(model, lenses, p, nominal, output);

        var notes = new List<string>();
        foreach (var lens in lenses)
            foreach (var n in lens.Notes) if (!notes.Contains(n)) notes.Add(n);
        if (notes.Count > 0)
        {
            output.WriteLine();
            foreach (var n in notes) output.WriteLine("Note: " + n);
        }

        if (saveMount) Save(mount);

        if (detail && mode == "lens")
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

    private static void Paraxial(ThermalModel model, List<ThermalLens> lenses, int p, double nominal, TextWriter output)
    {
        var system = model.Nominal;
        output.WriteLine();
        output.WriteLine(FormattableString.Invariant(
            $"Paraxial, at {system.Wavelengths[p].Value:0.######} µm ({model.VacuumWavelengthsUm[p]:0.######} µm in vacuum):"));
        output.WriteLine(FormattableString.Invariant(
            $"    T °C        EFL mm        BFL mm   focus − image plane µm   change from {nominal:0.##} °C µm"));
        double nominalDefocus = FocusReport.Defocus(model.At(model.NominalAir));
        foreach (var lens in lenses)
        {
            var trace = ParaxialTrace.Trace(lens.System, lens.PrimaryIndices, 0.0);
            double defocus = FocusReport.Defocus(lens);
            output.WriteLine(FormattableString.Invariant(
                $"  {lens.TemperatureC,6:0.0}  {trace.Efl,12:0.000000}  {trace.Bfl,12:0.000000}  {defocus * 1e3,22:0.000}  {(defocus - nominalDefocus) * 1e3,22:0.000}"));
        }
    }

    private static void Focus(ThermalModel model, double[] temperatures, int p, string? svg, TextWriter output)
    {
        var system = model.Nominal;
        var report = FocusReport.Compute(model, temperatures);
        double t0 = model.NominalTemperatureC;
        output.WriteLine();
        output.WriteLine(FormattableString.Invariant(
            $"First order, at {system.Wavelengths[p].Value:0.######} µm. Focus change from {t0:0.##} °C, positive when the image falls beyond the image plane:"));
        output.WriteLine("    T °C        EFL mm        BFL mm   change µm   first order µm   F/#    depth of focus ±µm   in depths");
        foreach (var s in report.States)
            output.WriteLine(FormattableString.Invariant(
                $"  {s.TemperatureC,6:0.0}  {s.Efl,12:0.000000}  {s.Bfl,12:0.000000}  {s.Change * 1e3,10:0.000}  {s.Linear * 1e3,15:0.000}  {s.FNumber,6:0.00}  {s.DepthOfFocus * 1e3,19:0.000}  {s.ChangeInDepths,10:0.00}"));

        output.WriteLine();
        output.WriteLine(FormattableString.Invariant($"Each part's contribution at {t0:0.##} °C, that part alone warmed:"));
        output.WriteLine("  part                                     focus µm/K      EFL µm/K");
        foreach (var c in report.Contributions)
            output.WriteLine(FormattableString.Invariant($"  {c.Name,-38} {c.DefocusPerK * 1e3,13:0.0000} {c.EflPerK * 1e3,13:0.0000}"));
        output.WriteLine(FormattableString.Invariant(
            $"  {"sum of the contributions",-38} {report.Contributions.Sum(c => c.DefocusPerK) * 1e3,13:0.0000} {report.Contributions.Sum(c => c.EflPerK) * 1e3,13:0.0000}"));
        output.WriteLine(FormattableString.Invariant(
            $"  {"whole lens",-38} {report.DefocusPerK * 1e3,13:0.0000} {report.EflPerK * 1e3,13:0.0000}"));

        output.WriteLine();
        output.WriteLine(FormattableString.Invariant(
            $"Glasses at {t0:0.##} °C and {model.VacuumWavelengthsUm[p]:0.######} µm (vacuum); x_f = α − (dn_rel/dT)/(n − 1), γ = −x_f, ν_T = −1/x_f:"));
        output.WriteLine("  part                                       n_rel   dn_rel/dT      α        x_f        γ  (10⁻⁶/K)      ν_T (K)");
        foreach (var part in report.Contributions.Where(c => c.Part.Kind == ThermalPartKind.Glass))
        {
            var m = model.MaterialAfter(part.Part.Index)!;
            var c = ThermoOptic.At(m, model.VacuumWavelengthsUm[p], t0, model.NominalAir, model.Options.Index);
            double xf = model.CteAfter(part.Part.Index) - c.DnDtRelative / (c.Relative - 1.0);
            output.WriteLine(FormattableString.Invariant(
                $"  {part.Name,-38} {c.Relative,9:0.000000} {c.DnDtRelative * 1e6,11:0.000} {model.CteAfter(part.Part.Index) * 1e6,8:0.00} {xf * 1e6,10:0.000} {-xf * 1e6,8:0.000} {-1.0 / xf,20:0}"));
        }

        if (svg == null) return;
        // The focus over the whole span, paraxially, which is quick: 60 steps.
        double lo = temperatures.Append(t0).Min(), hi = temperatures.Append(t0).Max();
        var curve = FocusReport.Compute(model, Enumerable.Range(0, 61).Select(i => lo + (hi - lo) * i / 60.0), contributions: false).States;
        var focus = new SvgChart
        {
            Title = FormattableString.Invariant($"{Name(system)}: focus change from {t0:0.##} °C"),
            XLabel = "temperature (°C)",
            YLabel = "focus change (µm), + beyond the image plane",
        };
        focus.Bands.Add(new SvgChart.Band("± depth of focus", curve.Select(s => (s.TemperatureC, -s.DepthOfFocus * 1e3, s.DepthOfFocus * 1e3)).ToList(), SvgChart.Green));
        focus.Lines.Add(new SvgChart.Line("first order", curve.Select(s => (s.TemperatureC, s.Linear * 1e3)).ToList(), SvgChart.Gray, Dashed: true));
        focus.Lines.Add(new SvgChart.Line("focus change", curve.Select(s => (s.TemperatureC, s.Change * 1e3)).ToList(), SvgChart.Blue));
        focus.Lines.Add(new SvgChart.Line("", report.States.Select(s => (s.TemperatureC, s.Change * 1e3)).ToList(), SvgChart.Blue, Markers: true));
        var contributions = new SvgChart { Title = FormattableString.Invariant($"Each part's contribution to the focus drift at {t0:0.##} °C"), XLabel = "µm/K" };
        foreach (var c in report.Contributions)
            contributions.Bars.Add((c.Name, c.DefocusPerK * 1e3, c.Part.Kind switch
            {
                ThermalPartKind.Glass => SvgChart.Blue,
                ThermalPartKind.Mirror => SvgChart.Purple,
                ThermalPartKind.Housing => SvgChart.Red,
                _ => SvgChart.Orange,
            }));
        contributions.Bars.Add(("whole lens", report.DefocusPerK * 1e3, "#333333"));
        File.WriteAllText(svg, SvgChart.Document(focus, contributions));
        output.WriteLine();
        output.WriteLine($"Plot written to {svg}.");
    }

    /// <summary>The lens's title, or its file's name when it has none.</summary>
    private static string Name(OpticalSystem system) => string.IsNullOrWhiteSpace(system.Title) ? "The lens" : system.Title.Trim();

    /// <summary>
    /// <paramref name="points"/> temperatures evenly over the span of <paramref name="temperatures"/> and the nominal,
    /// with those themselves, in order.
    /// </summary>
    private static double[] PlotTemperatures(ThermalModel model, double[] temperatures, int points)
    {
        double lo = temperatures.Append(model.NominalTemperatureC).Min(), hi = temperatures.Append(model.NominalTemperatureC).Max();
        return Enumerable.Range(0, points).Select(i => lo + (hi - lo) * i / (points - 1))
            .Concat(temperatures).Append(model.NominalTemperatureC).Distinct().OrderBy(t => t).ToArray();
    }

    /// <summary>The RMS wavefront error and the lowest MTF at each frequency against temperature, fixed and refocused.</summary>
    private static string ImagePlot(ThermalModel model, IReadOnlyList<QualityState> states, double[] frequencies, double? mtfLimit,
        QualityOptions options)
    {
        var rms = new SvgChart
        {
            Title = $"{Name(model.Nominal)}: RMS wavefront error",
            XLabel = "temperature (°C)",
            YLabel = "RMS wavefront error (waves)",
            YMin = 0.0,
        };
        rms.Lines.Add(new SvgChart.Line("fixed image plane", states.Select(s => (s.TemperatureC, s.RmsFixed)).ToList(), SvgChart.Blue, Markers: true));
        rms.Lines.Add(new SvgChart.Line("refocused", states.Select(s => (s.TemperatureC, s.RmsRefocused)).ToList(), SvgChart.Green, Dashed: true, Markers: true));
        if (frequencies.Length == 0) return SvgChart.Document(rms);

        string kind = options.MtfKind == MtfKind.Diffraction ? "diffraction" : "geometric";
        var mtf = new SvgChart
        {
            Title = $"Lowest {kind} MTF over the fields and both azimuths",
            XLabel = "temperature (°C)",
            YLabel = "MTF",
            YMin = 0.0,
            YMax = 1.0,
        };
        static double Lowest(IReadOnlyList<WavefrontErrorCalculator.Core.GeometricMtf.Modulation[]> m, int k) => m.Min(f => Math.Min(f[k].Sagittal, f[k].Tangential));
        for (int k = 0; k < frequencies.Length; k++)
        {
            string color = SvgChart.Palette[k % SvgChart.Palette.Length];
            string at = FormattableString.Invariant($"{frequencies[k]:0.#} c/mm");
            mtf.Lines.Add(new SvgChart.Line(at + ", fixed", states.Select(s => (s.TemperatureC, Lowest(s.MtfFixed, k))).ToList(), color, Markers: true));
            mtf.Lines.Add(new SvgChart.Line(at + ", refocused", states.Select(s => (s.TemperatureC, Lowest(s.MtfRefocused, k))).ToList(), color, Dashed: true));
        }
        if (mtfLimit is double limit) mtf.Levels.Add(new SvgChart.Level(FormattableString.Invariant($"limit {limit:0.###}"), limit, "#000000"));
        return SvgChart.Document(rms, mtf);
    }

    private static void Image(ThermalModel model, double[] temperatures, double[] frequencies, double? mtfLimit,
        double[] range, QualityOptions options, string? svg, int plotPoints, TextWriter output)
    {
        // With a plot, the report's temperatures are among the plotted ones, computed once.
        var sampled = ImageQuality.Compute(model, svg != null ? PlotTemperatures(model, temperatures, plotPoints) : temperatures, frequencies, options);
        var states = temperatures.Select(t => sampled.First(s => s.TemperatureC == t)).ToList();
        output.WriteLine();
        output.WriteLine($"Image quality, {(options.Polychromatic ? "every wavelength, by its weight" : "the primary wavelength")}" +
                         $"{(options.Obscuration > 0 ? FormattableString.Invariant($", obstruction {options.Obscuration:0.###}") : "")}; " +
                         "RMS wavefront error in waves over the fields by their weights; refocused, the image plane moved to the least RMS:");
        output.WriteLine("    T °C   RMS fixed   focus shift µm   RMS refocused");
        foreach (var s in states)
            output.WriteLine(FormattableString.Invariant(
                $"  {s.TemperatureC,6:0.0}  {s.RmsFixed,10:0.0000}  {s.FocusShift * 1e3,15:0.000}  {s.RmsRefocused,14:0.0000}"));
        if (frequencies.Length > 0)
        {
            output.WriteLine();
            output.WriteLine($"{(options.MtfKind == MtfKind.Diffraction ? "Diffraction" : "Geometric")} MTF, sagittal/tangential, fixed | refocused:");
            for (int f = 0; f < model.Nominal.Fields.Count; f++)
                for (int k = 0; k < frequencies.Length; k++)
                {
                    output.Write(FormattableString.Invariant($"  field {model.Nominal.Fields[f].Y,8:0.###}  {frequencies[k],5:0.#} c/mm:"));
                    foreach (var s in states)
                        output.Write(FormattableString.Invariant(
                            $"  {s.TemperatureC:0.#} °C {s.MtfFixed[f][k].Sagittal:0.000}/{s.MtfFixed[f][k].Tangential:0.000} | {s.MtfRefocused[f][k].Sagittal:0.000}/{s.MtfRefocused[f][k].Tangential:0.000}"));
                    output.WriteLine();
                }
        }
        if (mtfLimit is double limit && frequencies.Length > 0)
        {
            output.WriteLine();
            foreach (bool refocused in new[] { false, true })
            {
                var r = ImageQuality.Range(model, frequencies[0], limit, range[0], range[1], 5.0, refocused, options);
                output.WriteLine(FormattableString.Invariant(
                    $"The lowest {(options.MtfKind == MtfKind.Diffraction ? "diffraction" : "geometric")} MTF at {frequencies[0]:0.#} c/mm stays at or above {limit:0.###} {(refocused ? "refocused" : "at the fixed plane")}: ") +
                    (r is { } v ? FormattableString.Invariant($"{v.Low:0.0} to {v.High:0.0} °C (searched {range[0]:0} to {range[1]:0} °C)") : "not even at the nominal temperature"));
            }
        }
        if (svg != null)
        {
            File.WriteAllText(svg, ImagePlot(model, sampled, frequencies, mtfLimit, options));
            output.WriteLine();
            output.WriteLine(FormattableString.Invariant($"Plot written to {svg}, at {sampled.Count} temperatures."));
        }
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

    private static SearchResult Select(AberrationCalculator.Core.Models.OpticalSystem system, MaterialLibrary library, Mount mount,
        AirConditions air, ThermalOptions thermal, SearchCandidates candidates, double deltaT, int keep, TextWriter output)
    {
        double count = candidates.Count(mount);
        output.WriteLine();
        output.WriteLine(FormattableString.Invariant(
            $"{count:0} combinations. Score = (|focus rate|·{deltaT:0.#} K + |axial color|)/depth of focus × (1 + 2|ΔEFL|/EFL), at the design's shapes:"));
        var results = MaterialSearch.Run(system, library, mount, candidates,
            new SearchOptions { NominalAir = air, Thermal = thermal, TemperatureRangeK = deltaT, Keep = keep });
        var design = MaterialSearch.Run(system, library, mount, new SearchCandidates(),
            new SearchOptions { NominalAir = air, Thermal = thermal, TemperatureRangeK = deltaT, Keep = 1 })[0];
        void Row(string label, SearchResult r) => output.WriteLine(FormattableString.Invariant(
            $"  {label,-5} {r.Score,8:0.000} {r.DefocusPerK * 1e3,11:0.0000} {r.AxialColor * 1e3,11:0.000} {r.EflChange,10:0.0000}   {Choice(r)}"));
        output.WriteLine("  rank     score   focus µm/K   color µm   ΔEFL mm   materials");
        Row("design", design);
        for (int i = 0; i < results.Count; i++) Row((i + 1).ToString(CultureInfo.InvariantCulture), results[i]);
        output.WriteLine("A glass swap changes the lens: optimize the best before taking them further.");
        return results.Count > 0 ? results[0] : design;
    }

    private static string Choice(SearchResult r)
    {
        var parts = r.Glasses.OrderBy(g => g.Key).Select(g => $"after {g.Key}: {g.Value.Name}").ToList();
        parts.Add($"housing {r.Housing.Name}");
        parts.AddRange(r.Parts.Select((m, k) => $"part {k}: {m.Name}"));
        return string.Join("; ", parts);
    }

    internal static MountMaterial Material(string text, string option)
    {
        try { return MountFile.Material(text); }
        catch (ArgumentException e) { throw new ArgumentException($"{option}: {e.Message}"); }
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
