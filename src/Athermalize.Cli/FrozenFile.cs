using System.Globalization;
using System.Text;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using Athermalize.Core.Lens;

namespace Athermalize.Cli;

/// <summary>
/// Writes a <see cref="FrozenLens"/>: the lens file, and beside it a glass catalog of its frozen glasses, each the
/// Schott or Extended 2 formula fitted to the material's relative indices across the band, with no thermal data.
/// </summary>
internal static class FrozenFile
{
    /// <summary>The lens formats it writes: .zmx and .lhlt with an .agf catalog beside them, and Optiland's .json, which carries its glasses.</summary>
    public static readonly string[] Formats = { ".zmx", ".lhlt", ".json" };

    /// <param name="Glass">The frozen glass.</param>
    /// <param name="Formula">The dispersion formula fitted.</param>
    /// <param name="MaxError">The fit's largest index error at the sampled wavelengths.</param>
    public sealed record Fit(FrozenMaterial Glass, DispersionFormula Formula, double MaxError);

    /// <summary>Writes the lens to <paramref name="path"/> and the catalog beside it; returns the catalog's path, the fits and the writer's notes.</summary>
    public static (string CatalogPath, IReadOnlyList<Fit> Fits, IReadOnlyList<string> Notes) Write(FrozenLens frozen, string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (!Formats.Contains(ext))
            throw new ArgumentException($"--write: a frozen lens is written as {string.Join(" or ", Formats)}; other formats keep their glasses differently");
        string stem = Path.GetFileNameWithoutExtension(path);
        if (ext == ".json" && stem.EndsWith(".optiland", StringComparison.OrdinalIgnoreCase)) stem = stem[..^".optiland".Length];
        // Beside the lens, named as the lens was given, relative or not.
        string catalogPath = Path.Combine(Path.GetDirectoryName(path) ?? "", stem + ".agf");

        var inv = CultureInfo.InvariantCulture;
        var fits = new List<Fit>();
        var lines = new List<string>
        {
            "CC " + frozen.System.Title,
            "CC Frozen glasses: each material's index relative to the air at these conditions, fitted with the",
            "CC Schott or Extended 2 formula. No thermal data: use the lens at the program's reference conditions, unchanged.",
        };
        foreach (var m in frozen.Materials)
        {
            var (g, max) = m.Fit();
            fits.Add(new Fit(m, g.Formula, max));
            double lo = g.LambdaMin, hi = g.LambdaMax;
            // nd and νd when the band holds the visible lines, else the index at the band's middle and no νd.
            bool visible = lo <= IndexResolver.LambdaF && hi >= IndexResolver.LambdaC;
            double nd = visible ? g.IndexAt(IndexResolver.LambdaD) : g.IndexAt(0.5 * (lo + hi));
            double vd = visible ? (nd - 1) / (g.IndexAt(IndexResolver.LambdaF) - g.IndexAt(IndexResolver.LambdaC)) : 0.0;
            lines.Add(string.Format(inv, "NM {0} {1} 0 {2:F6} {3:F4} 0 0 0", m.Name, (int)g.Formula, nd, vd));
            lines.Add("GC " + m.Source);
            lines.Add("ED 0 0 0 0 1");
            lines.Add("CD " + string.Join(" ", g.Coefficients.Select(v => v.ToString("E15", inv))));
            lines.Add("TD 0 0 0 0 0 0 20");
            lines.Add("OD -1 -1 -1 -1 -1 -1");
            lines.Add(string.Format(inv, "LD {0:F6} {1:F6}", lo, hi));
        }
        File.WriteAllLines(catalogPath, lines, new UTF8Encoding(false));

        var catalog = new GlassCatalog();
        catalog.LoadFile(catalogPath);
        var system = frozen.System;
        system.GlassCatalogs.Clear();
        system.GlassCatalogs.Add(catalog.Catalogs[0]);
        var notes = LensFile.Write(system, path, catalog, installOptilandGlasses: false);
        return (catalogPath, fits, notes);
    }
}
