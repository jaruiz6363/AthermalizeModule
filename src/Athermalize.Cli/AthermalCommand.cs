using System.Globalization;
using System.Text;
using Athermalize.Core.Analysis;
using Athermalize.Core.Lens;
using Athermalize.Core.Materials;

namespace Athermalize.Cli;

/// <summary><c>atherm map</c> and <c>atherm solve</c>: the athermal map, and thin athermal achromatic doublets and triplets.</summary>
internal static class AthermalCommand
{
    public const string Usage =
        "       atherm map [<material>...] [options]   each material's ν, ω, γ and x_f over a band, and the housing's point\n" +
        "         --catalog <name>                every glass of a catalog with thermal and expansion data\n" +
        "         --band visible|visible-e|mwir|lwir|<short,center,long µm>   (visible: F, d, C)\n" +
        "         --housing <material|α>          the housing's point, (0, x_f = α_h)\n" +
        "         --temperature <°C>              (20)\n" +
        "         --svg <file>                    also plot x_f against ω = 1/ν\n" +
        "       atherm solve <a> <b> [<c>] --housing <material|α> [--focal <mm>] [--band ...]\n" +
        "                                         the thin athermal achromat: a doublet's powers and its thermal\n" +
        "                                         residual, or a triplet's powers\n" +
        "       atherm solve <a> --partners --catalog <name> --housing <material|α> [--top 10] [--max-power 4]\n" +
        "                                         the glasses that make the most nearly athermal achromat with a";

    public static int Run(string mode, string[] args, TextWriter output)
    {
        var names = new List<string>();
        string catalogs = Path.Combine(AppContext.BaseDirectory, "catalogs", "Glass");
        string? catalog = null, svg = null;
        var band = Band.Visible;
        MountMaterial? housing = null;
        double temperature = 20.0, focal = 100.0, maxPower = 4.0;
        int top = 10;
        bool partners = false;

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{a} needs a value");
            switch (a)
            {
                case "--catalogs": catalogs = Next(); break;
                case "--catalog": catalog = Next(); break;
                case "--band": band = BandOf(Next()); break;
                case "--housing": housing = LensCommand.Material(Next(), a); break;
                case "--temperature": temperature = Program.Numbers(Next(), a)[0]; break;
                case "--svg": svg = Next(); break;
                case "--focal": focal = Program.Numbers(Next(), a)[0]; break;
                case "--top": top = (int)Program.Numbers(Next(), a)[0]; break;
                case "--max-power": maxPower = Program.Numbers(Next(), a)[0]; break;
                case "--partners": partners = true; break;
                default:
                    if (a.StartsWith("--")) throw new ArgumentException($"unknown option '{a}'");
                    names.Add(a);
                    break;
            }
        }

        var library = MaterialLibrary.FromFolder(catalogs);
        Material Find(string n) => library.Find(n) ?? throw new ArgumentException($"no material '{n}'");
        MaterialConstants Of(Material m) => MaterialConstants.Of(m, band, temperature);
        double alpha = housing?.Cte ?? 0.0;

        if (mode == "map")
        {
            var materials = names.Select(Find).ToList();
            if (catalog != null) materials.AddRange(CatalogGlasses(library, catalog));
            if (materials.Count == 0) throw new ArgumentException("name materials or a --catalog");
            var points = materials.Select(m => { try { return Of(m); } catch (ArithmeticException) { return null; } })
                .Where(c => c != null && double.IsFinite(c.Omega) && double.IsFinite(c.Gamma)).Select(c => c!).ToList();
            output.WriteLine(FormattableString.Invariant(
                $"The athermal map over {band.Name} at {temperature:0.##} °C: ω = (n_short − n_long)/(n_center − 1) = 1/ν, γ = (dn_rel/dT)/(n − 1) − α, x_f = −γ."));
            output.WriteLine("A thin athermal achromatic doublet's two points lie on a line through the housing's point.");
            output.WriteLine("  material                      n_center       ν       ω×10³    P_long   γ (10⁻⁶/K)  x_f (10⁻⁶/K)");
            foreach (var c in points.OrderBy(c => c.Omega))
                output.WriteLine(FormattableString.Invariant(
                    $"  {c.Material.Name,-28} {c.Index,9:0.000000} {c.Nu,8:0.00} {c.Omega * 1e3,10:0.0000} {c.PartialLong,9:0.0000} {c.Gamma * 1e6,11:0.000} {c.OptothermalExpansion * 1e6,12:0.000}{(c.InRange ? "" : "  outside the data's range")}"));
            if (housing != null)
                output.WriteLine(FormattableString.Invariant(
                    $"  {"housing " + housing.Name,-28} {"",9} {"",8} {0.0,10:0.0000} {"",9} {-alpha * 1e6,11:0.000} {alpha * 1e6,12:0.000}"));
            if (svg != null)
            {
                File.WriteAllText(svg, Svg(points, housing, band, names.Count > 0 && names.Count <= 30 ? names.Count : 0));
                output.WriteLine($"Plot written to {svg}.");
            }
            return 0;
        }

        if (housing == null) throw new ArgumentException("give the housing's material with --housing");
        if (partners)
        {
            if (names.Count != 1 || catalog == null) throw new ArgumentException("--partners needs one material and a --catalog");
            var a0 = Of(Find(names[0]));
            var list = new List<(MaterialConstants B, double Pa, double Pb, ThinResiduals R)>();
            foreach (var g in CatalogGlasses(library, catalog))
            {
                if (g.Name == a0.Material.Name) continue;
                MaterialConstants b;
                try { b = Of(g); } catch (ArithmeticException) { continue; }
                if (!double.IsFinite(b.Omega) || !double.IsFinite(b.Gamma)) continue;
                var d = Athermal.Doublet(a0, b, 1.0 / focal, alpha);
                if (d is not { } s || Math.Abs(s.PowerA) * focal > maxPower) continue;
                list.Add((b, s.PowerA, s.PowerB, s.Residuals));
            }
            output.WriteLine(FormattableString.Invariant(
                $"Thin achromatic doublets of {a0.Material.Name} and a {catalog} glass, f = {focal:0.###} mm over {band.Name}, in a housing of α_h = {alpha * 1e6:0.###}×10⁻⁶/K,"));
            output.WriteLine(FormattableString.Invariant(
                $"the image plane held at the focal distance; most nearly athermal first, the stronger element at most {maxPower:0.##}× the doublet's power:"));
            output.WriteLine("  partner                        ν       f_a mm     f_b mm   focus µm/K   R_th/φ (10⁻⁶/K)");
            foreach (var (b, pa, pb, r) in list.OrderBy(x => Math.Abs(x.R.Thermal)).Take(top))
                output.WriteLine(FormattableString.Invariant(
                    $"  {b.Material.Name,-28} {b.Nu,7:0.00} {1 / pa,10:0.000} {1 / pb,10:0.000} {r.DefocusPerK * 1e3,12:0.0000} {r.Thermal / r.Power * 1e6,14:0.000}"));
            return 0;
        }

        if (names.Count is < 2 or > 3) throw new ArgumentException("solve takes two or three materials");
        var c3 = names.Select(n => Of(Find(n))).ToList();
        output.WriteLine(FormattableString.Invariant(
            $"Thin, in contact, f = {focal:0.###} mm over {band.Name} at {temperature:0.##} °C, in a housing of α_h = {alpha * 1e6:0.###}×10⁻⁶/K that holds the image plane at the focal distance:"));
        foreach (var c in c3)
            output.WriteLine(FormattableString.Invariant($"  {c.Material.Name,-28} ν {c.Nu,8:0.00}   γ {c.Gamma * 1e6,9:0.000}×10⁻⁶/K"));
        if (c3.Count == 2)
        {
            var d = Athermal.Doublet(c3[0], c3[1], 1.0 / focal, alpha)
                    ?? throw new ArgumentException("the two materials have the same dispersion: no achromat");
            output.WriteLine(FormattableString.Invariant(
                $"Achromatic doublet: f_a = {1 / d.PowerA:0.000} mm, f_b = {1 / d.PowerB:0.000} mm."));
            output.WriteLine(FormattableString.Invariant(
                $"Thermal residual R_th/φ = Σ(γ + α_h)φ/φ = {d.Residuals.Thermal / d.Residuals.Power * 1e6:0.000}×10⁻⁶/K: the focus moves {d.Residuals.DefocusPerK * 1e3:0.0000} µm/K from the image plane."));
            output.WriteLine(Math.Abs(d.Residuals.Thermal / d.Residuals.Power) < 0.1e-6
                ? "It is athermal to within 0.1×10⁻⁶/K."
                : "Not athermal: no doublet of these two glasses is both achromatic and athermal in this housing; a third material is needed.");
        }
        else
        {
            var p = Athermal.Triplet(c3[0], c3[1], c3[2], 1.0 / focal, alpha);
            if (p == null) output.WriteLine("The three points lie on one line of the map: no athermal achromatic triplet of them exists.");
            else
            {
                output.WriteLine(FormattableString.Invariant(
                    $"Athermal achromatic triplet: f = {1 / p[0]:0.000}, {1 / p[1]:0.000} and {1 / p[2]:0.000} mm."));
                output.WriteLine(FormattableString.Invariant(
                    $"The strongest element has {p.Max(Math.Abs) * focal:0.00}× the triplet's power; weaker is easier to correct."));
            }
        }
        return 0;
    }

    private static IEnumerable<Material> CatalogGlasses(MaterialLibrary library, string catalog) =>
        library.CatalogGlasses().Where(g => string.Equals(g.Catalog, catalog, StringComparison.OrdinalIgnoreCase)
                                            && g.Thermal.HasCoefficients && g.Expansion.Cte != 0.0);

    private static Band BandOf(string text)
    {
        switch (text.ToLowerInvariant())
        {
            case "visible": return Band.Visible;
            case "visible-e": return Band.VisibleE;
            case "mwir": return Band.Mwir;
            case "lwir": return Band.Lwir;
        }
        var v = Program.Numbers(text, "--band");
        if (v.Length != 3 || !(v[0] < v[1] && v[1] < v[2])) throw new ArgumentException("--band: give short,center,long in µm");
        return new Band(FormattableString.Invariant($"{v[0]}-{v[2]} µm"), v[0], v[1], v[2]);
    }

    /// <summary>x_f against ω, with the housing's point; the first <paramref name="labeled"/> points labeled.</summary>
    private static string Svg(IReadOnlyList<MaterialConstants> points, MountMaterial? housing, Band band, int labeled)
    {
        const double w = 720, h = 520, left = 80, right = 30, topMargin = 40, bottom = 60;
        var xs = points.Select(p => p.Omega * 1e3).Append(0.0).ToList();
        var ys = points.Select(p => p.OptothermalExpansion * 1e6).ToList();
        if (housing != null) ys.Add(housing.Cte * 1e6);
        double x0 = xs.Min(), x1 = xs.Max(), y0 = ys.Min(), y1 = ys.Max();
        double padX = (x1 - x0) * 0.06 + 1e-9, padY = (y1 - y0) * 0.06 + 1e-9;
        x0 -= padX; x1 += padX; y0 -= padY; y1 += padY;
        string X(double v) => (left + (v - x0) / (x1 - x0) * (w - left - right)).ToString("0.0", CultureInfo.InvariantCulture);
        string Y(double v) => (h - bottom - (v - y0) / (y1 - y0) * (h - topMargin - bottom)).ToString("0.0", CultureInfo.InvariantCulture);
        string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        var s = new StringBuilder();
        s.Append(FormattableString.Invariant($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{w}\" height=\"{h}\" viewBox=\"0 0 {w} {h}\" font-family=\"sans-serif\" font-size=\"12\">\n"));
        s.Append("<rect width=\"100%\" height=\"100%\" fill=\"white\"/>\n");
        s.Append($"<text x=\"{w / 2}\" y=\"22\" text-anchor=\"middle\" font-size=\"14\">Athermal map, {band.Name}</text>\n");
        s.Append($"<line x1=\"{left}\" y1=\"{h - bottom}\" x2=\"{w - right}\" y2=\"{h - bottom}\" stroke=\"black\"/>\n");
        s.Append($"<line x1=\"{left}\" y1=\"{topMargin}\" x2=\"{left}\" y2=\"{h - bottom}\" stroke=\"black\"/>\n");
        for (int i = 0; i <= 5; i++)
        {
            double vx = x0 + (x1 - x0) * i / 5, vy = y0 + (y1 - y0) * i / 5;
            s.Append($"<text x=\"{X(vx)}\" y=\"{h - bottom + 18}\" text-anchor=\"middle\">{N(vx)}</text>\n");
            s.Append($"<text x=\"{left - 8}\" y=\"{Y(vy)}\" text-anchor=\"end\" dominant-baseline=\"middle\">{N(vy)}</text>\n");
        }
        s.Append($"<text x=\"{(left + w - right) / 2}\" y=\"{h - 18}\" text-anchor=\"middle\">ω = 1/ν (×10⁻³)</text>\n");
        s.Append($"<text x=\"18\" y=\"{(topMargin + h - bottom) / 2}\" text-anchor=\"middle\" transform=\"rotate(-90 18 {(topMargin + h - bottom) / 2})\">x_f (10⁻⁶/K)</text>\n");
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            s.Append($"<circle cx=\"{X(p.Omega * 1e3)}\" cy=\"{Y(p.OptothermalExpansion * 1e6)}\" r=\"3\" fill=\"#1f5fa8\"><title>{Escape(p.Material.Name)}</title></circle>\n");
            if (i < labeled)
                s.Append($"<text x=\"{X(p.Omega * 1e3)}\" y=\"{Y(p.OptothermalExpansion * 1e6)}\" dx=\"5\" dy=\"-5\">{Escape(p.Material.Glass)}</text>\n");
        }
        if (housing != null)
        {
            string hx = X(0.0), hy = Y(housing.Cte * 1e6);
            s.Append($"<rect x=\"{hx}\" y=\"{hy}\" width=\"8\" height=\"8\" transform=\"translate(-4 -4)\" fill=\"#c0392b\"><title>housing</title></rect>\n");
            s.Append($"<text x=\"{hx}\" y=\"{hy}\" dx=\"7\" dy=\"4\" fill=\"#c0392b\">housing {Escape(housing.Name)}</text>\n");
        }
        s.Append("</svg>\n");
        return s.ToString();
    }

    private static string Escape(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
