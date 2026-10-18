using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Athermalize.Core.Air;
using Athermalize.Core.Lens;
using Athermalize.Core.Materials;

namespace Athermalize.Core.Analysis;

/// <summary>The candidates of a search: for each glass and each part of the mount, the materials it may be made of.</summary>
public sealed record SearchCandidates
{
    /// <summary>For the medium after a surface, the glasses it may be; a surface not listed keeps its own.</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<Material>> Glasses { get; init; } = new Dictionary<int, IReadOnlyList<Material>>();

    /// <summary>The housing's candidates; the mount's own housing when empty.</summary>
    public IReadOnlyList<MountMaterial> Housing { get; init; } = Array.Empty<MountMaterial>();

    /// <summary>For a mount part, by its place in <see cref="Mount.Segments"/>, its candidates; a part not listed keeps its own.</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<MountMaterial>> Parts { get; init; } = new Dictionary<int, IReadOnlyList<MountMaterial>>();

    /// <summary>The number of combinations.</summary>
    public double Count(Mount mount) =>
        Glasses.Values.Aggregate(1.0, (p, l) => p * Math.Max(1, l.Count))
        * Math.Max(1, Housing.Count)
        * Parts.Values.Aggregate(1.0, (p, l) => p * Math.Max(1, l.Count));
}

/// <summary>How the search scores and how far it goes.</summary>
public sealed record SearchOptions
{
    /// <summary>The design's conditions: 20 °C and one atmosphere when null.</summary>
    public AirConditions? NominalAir { get; init; }

    /// <summary>The index conventions, and any substitutions, for every combination.</summary>
    public ThermalOptions? Thermal { get; init; }

    /// <summary>The half-width of the temperature range the lens must hold focus over, K.</summary>
    public double TemperatureRangeK { get; init; } = 60.0;

    /// <summary>The weight of the focal-length change, as Jiang and Liu's W_power.</summary>
    public double WPower { get; init; } = 2.0;

    /// <summary>How many of the best combinations to return.</summary>
    public int Keep { get; init; } = 10;

    /// <summary>The most combinations the search will try.</summary>
    public double MaxCombinations { get; init; } = 200_000;
}

/// <summary>One combination of materials, scored.</summary>
/// <param name="Glasses">The glass after each searched surface.</param>
/// <param name="Housing">The housing's material.</param>
/// <param name="Parts">Each mount part's material, in the mount's order.</param>
/// <param name="DefocusPerK">The rate of the focus less the image plane at the design's temperature, mm/K.</param>
/// <param name="AxialColor">The paraxial focus at the shortest wavelength less the longest's, mm.</param>
/// <param name="EflChange">The focal length less the design's, at fixed shapes, mm.</param>
/// <param name="Score">(|rate|·ΔT + |color|)/depth of focus × (1 + W_power·|ΔEFL|/EFL).</param>
public sealed record SearchResult(IReadOnlyDictionary<int, Material> Glasses, MountMaterial Housing,
    IReadOnlyList<MountMaterial> Parts, double DefocusPerK, double AxialColor, double EflChange, double Score);

/// <summary>
/// An exhaustive search over materials for the glasses and the mount, every combination of the candidates
/// (Liu et al. 2025's Cartesian product), each scored on the full lens model at the design's shapes.
///
/// <para>The score adds the defocus over the temperature range, the rate times the half-range ΔT, and the axial
/// color, both in depths of focus, and multiplies by 1 + W_power·|ΔEFL|/EFL for the change of focal length a glass
/// swap brings at fixed shapes, as Jiang and Liu (2024) weight their power deviation. A glass swap changes the
/// design, so the best combinations are starting points to optimize, not finished lenses.</para>
/// </summary>
public static class MaterialSearch
{
    /// <summary>The best combinations, lowest score first.</summary>
    public static IReadOnlyList<SearchResult> Run(OpticalSystem nominal, MaterialLibrary library, Mount mount,
        SearchCandidates candidates, SearchOptions? options = null)
    {
        var o = options ?? new SearchOptions();
        double count = candidates.Count(mount);
        if (count > o.MaxCombinations)
            throw new ArgumentException($"{count:G3} combinations, more than the {o.MaxCombinations:G3} allowed: shorten the candidate lists");

        var thermal = o.Thermal ?? ThermalOptions.Default;
        var air = o.NominalAir ?? AirConditions.Standard;
        var design = new ThermalModel(nominal, library, mount, air, thermal);
        var designLens = design.At(air);
        double efl0 = ParaxialTrace.Trace(designLens.System, designLens.PrimaryIndices, 0.0).Efl;
        double depth = FocusReport.Compute(design, Array.Empty<double>(), shares: false).DepthOfFocus;
        var (shortest, longest) = Extremes(nominal);

        var glassSlots = candidates.Glasses.Where(g => g.Value.Count > 0).OrderBy(g => g.Key).ToList();
        var housings = candidates.Housing.Count > 0 ? candidates.Housing : new[] { mount.Housing };
        var partSlots = Enumerable.Range(0, mount.Segments.Count)
            .Select(k => candidates.Parts.TryGetValue(k, out var l) && l.Count > 0 ? l : new[] { mount.Segments[k].Material })
            .ToList();

        var radices = glassSlots.Select(g => g.Value.Count).Append(housings.Count).Concat(partSlots.Select(p => p.Count)).ToArray();
        var digits = new int[radices.Length];
        var best = new List<SearchResult>();
        while (true)
        {
            var glasses = new Dictionary<int, Material>();
            for (int i = 0; i < glassSlots.Count; i++) glasses[glassSlots[i].Key] = glassSlots[i].Value[digits[i]];
            var housing = housings[digits[glassSlots.Count]];
            var parts = partSlots.Select((p, k) => p[digits[glassSlots.Count + 1 + k]]).ToList();
            var trial = new Mount(housing, mount.Segments.Select((g, k) => g with { Material = parts[k] }),
                mount.MirrorSubstrates, mount.Cemented);
            var surfaceMaterials = new Dictionary<int, Material>(thermal.SurfaceMaterials ?? new Dictionary<int, Material>());
            foreach (var (k, m) in glasses) surfaceMaterials[k] = m;
            var model = new ThermalModel(nominal, library, trial, air, thermal with { SurfaceMaterials = surfaceMaterials });

            var report = FocusReport.Compute(model, Array.Empty<double>(), shares: false);
            var lens = model.At(air);
            double efl = ParaxialTrace.Trace(lens.System, lens.PrimaryIndices, 0.0).Efl;
            double color = shortest == longest ? 0.0
                : ParaxialTrace.Trace(lens.System, lens.Indices[shortest], 0.0).ParaxialFocusDistance
                  - ParaxialTrace.Trace(lens.System, lens.Indices[longest], 0.0).ParaxialFocusDistance;
            double score = (Math.Abs(report.DefocusPerK) * o.TemperatureRangeK + Math.Abs(color)) / depth
                           * (1.0 + o.WPower * Math.Abs(efl - efl0) / Math.Abs(efl0));
            Insert(best, new SearchResult(glasses, housing, parts, report.DefocusPerK, color, efl - efl0, score), o.Keep);

            int d = 0;
            while (d < radices.Length && ++digits[d] == radices[d]) digits[d++] = 0;
            if (d == radices.Length) break;
        }
        return best;
    }

    /// <summary>
    /// The glasses of the catalogs within |Δn_d| ≤ <paramref name="maxDn"/> and |Δν_d| ≤ <paramref name="maxDnu"/>
    /// of <paramref name="glass"/>, with thermal and expansion data, nearest first (Jiang and Liu 2024 filter so).
    /// </summary>
    public static IReadOnlyList<Material> Neighbors(MaterialLibrary library, Material glass, double maxDn, double maxDnu,
        IEnumerable<string>? catalogs = null, int max = int.MaxValue)
    {
        var at = MaterialConstants.Of(glass, Band.Visible);
        var allowed = catalogs?.Select(c => c.ToUpperInvariant()).ToHashSet();
        var list = new List<(Material M, double D)>();
        foreach (var g in library.CatalogGlasses())
        {
            if (allowed != null && !allowed.Contains(g.Catalog.ToUpperInvariant())) continue;
            if (!g.Thermal.HasCoefficients || g.Expansion.Cte == 0.0) continue;
            MaterialConstants c;
            try { c = MaterialConstants.Of(g, Band.Visible); }
            catch (ArithmeticException) { continue; }
            if (!double.IsFinite(c.Index) || !double.IsFinite(c.Nu)) continue;
            double dn = c.Index - at.Index, dnu = c.Nu - at.Nu;
            if (Math.Abs(dn) <= maxDn && Math.Abs(dnu) <= maxDnu)
                list.Add((g, Math.Sqrt(dn / maxDn * (dn / maxDn) + dnu / maxDnu * (dnu / maxDnu))));
        }
        return list.OrderBy(p => p.D).Take(max).Select(p => p.M).ToList();
    }

    private static (int Shortest, int Longest) Extremes(OpticalSystem system)
    {
        int s = 0, l = 0;
        for (int w = 1; w < system.Wavelengths.Count; w++)
        {
            if (system.Wavelengths[w].Value < system.Wavelengths[s].Value) s = w;
            if (system.Wavelengths[w].Value > system.Wavelengths[l].Value) l = w;
        }
        return (s, l);
    }

    private static void Insert(List<SearchResult> best, SearchResult r, int keep)
    {
        int i = best.FindIndex(b => b.Score > r.Score);
        if (i < 0) best.Add(r); else best.Insert(i, r);
        if (best.Count > keep) best.RemoveAt(best.Count - 1);
    }
}
