using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using Athermalize.Core.Air;
using Athermalize.Core.Materials;

namespace Athermalize.Core.Lens;

/// <summary>Settings for building a lens at a temperature.</summary>
/// <param name="Index">The index model's conventions.</param>
/// <param name="Substitutions">
/// Materials to use in place of the ones the lens names, by the name on the surface (as "NAME" or "CATALOG:NAME"):
/// a measured crystal in place of a catalog entry, say.
/// </param>
/// <param name="CteOverrides">Expansion coefficients, 1/K, to use in place of the data's, by material name.</param>
/// <param name="SurfaceMaterials">Materials for the medium after particular surfaces, by surface, before any other rule.</param>
public sealed record ThermalOptions(
    IndexOptions? Index = null,
    IReadOnlyDictionary<string, Material>? Substitutions = null,
    IReadOnlyDictionary<string, double>? CteOverrides = null,
    IReadOnlyDictionary<int, Material>? SurfaceMaterials = null)
{
    public static ThermalOptions Default { get; } = new();
}

/// <summary>What a part of the lens is.</summary>
public enum ThermalPartKind
{
    /// <summary>A glass: the medium after a surface, its index, its thickness and the surfaces that grow with it.</summary>
    Glass,

    /// <summary>A mirror's substrate, which sets the mirror's shape.</summary>
    Mirror,

    /// <summary>The housing: every air space no mount part holds, and the surfaces in air.</summary>
    Housing,

    /// <summary>One of the mount's parts.</summary>
    MountPart,
}

/// <summary>
/// A part of a lens that a temperature changes. <paramref name="Index"/> is the surface the glass follows, the
/// mirror's surface, or the mount part's place in <see cref="Mount.Segments"/>; 0 for the housing.
/// </summary>
public readonly record struct ThermalPart(ThermalPartKind Kind, int Index)
{
    public static ThermalPart Housing { get; } = new(ThermalPartKind.Housing, 0);
}

/// <summary>What set a surface's size and the thickness after it, at one temperature.</summary>
/// <param name="Index">The surface.</param>
/// <param name="ShapeSetBy">What the surface's radius, aspheric terms and apertures grew with.</param>
/// <param name="ShapeScale">The factor its lengths grew by: 1 + αΔT for one coefficient, the ratio of lengths for a curve.</param>
/// <param name="ThicknessSetBy">What set the thickness after the surface.</param>
/// <param name="NominalThickness">The thickness after it in the nominal design.</param>
/// <param name="Thickness">The thickness after it at the temperature.</param>
public readonly record struct SurfaceState(int Index, string ShapeSetBy, double ShapeScale, string ThicknessSetBy,
    double NominalThickness, double Thickness);

/// <summary>A lens at one temperature: its own copy of the prescription, and the indices to trace it with.</summary>
public sealed class ThermalLens
{
    internal ThermalLens(OpticalSystem system, AirConditions air, double deltaT, double[] vacuumWavelengths,
        double[][] indices, SurfaceState[] surfaces, List<string> notes)
    {
        System = system;
        Air = air;
        DeltaT = deltaT;
        VacuumWavelengthsUm = vacuumWavelengths;
        Indices = indices;
        Surfaces = surfaces;
        Notes = notes;
    }

    /// <summary>
    /// The lens at the temperature. Its wavelengths are those of the same light in the air at the temperature, and
    /// its aperture is the entrance pupil the expanded stop gives.
    /// </summary>
    public OpticalSystem System { get; }

    /// <summary>The air around the lens; every part of the lens is at its temperature.</summary>
    public AirConditions Air { get; }

    public double TemperatureC => Air.TemperatureC;

    /// <summary>The temperature less the nominal design's.</summary>
    public double DeltaT { get; }

    /// <summary>The vacuum wavelength of each of the lens's wavelengths, µm; the same at every temperature.</summary>
    public IReadOnlyList<double> VacuumWavelengthsUm { get; }

    /// <summary>
    /// For each wavelength, the index after each surface relative to the air, as AberrationCalculator's analyses
    /// take it: 1 in air, and a mirror keeps the index of the medium it sits in.
    /// </summary>
    public IReadOnlyList<double[]> Indices { get; }

    public IReadOnlyList<SurfaceState> Surfaces { get; }

    /// <summary>What the module assumed or could not do, in words.</summary>
    public IReadOnlyList<string> Notes { get; }

    /// <summary>The indices at the primary wavelength.</summary>
    public double[] PrimaryIndices => Indices[Math.Max(0, System.PrimaryWavelengthIndex)];
}

/// <summary>
/// A nominal lens with its materials and its mount, from which the lens at any temperature is built.
///
/// <para>The lens at a temperature is always built afresh from the nominal design, never by changing an earlier
/// one:</para>
/// <list type="bullet">
/// <item>every surface's radius, aspheric terms, Zernike terms and apertures grow by 1 + αΔT with its element's
/// expansion coefficient α, a mirror's with its substrate's; an even-asphere coefficient of r^(2p) is divided by
/// (1 + αΔT)^(2p−1), so the surface is the same shape at the larger size;</item>
/// <item>a glass thickness grows with its glass;</item>
/// <item>the air spaces, the image plane among them, are set by the <see cref="Mount"/>;</item>
/// <item>the indices are each material's at the temperature, relative to the air at the temperature and pressure,
/// at the same vacuum wavelengths as the nominal design's.</item>
/// </list>
/// </summary>
public sealed class ThermalModel
{
    private readonly Material?[] _after;
    private readonly double[] _cte;
    private readonly ExpansionCurve[] _growth;
    private readonly string[] _cteNote;

    /// <param name="nominal">The design, as its file gives it. It is never changed.</param>
    /// <param name="library">The materials to find the lens's glasses in.</param>
    /// <param name="mount">How the lens is held.</param>
    /// <param name="nominalAir">The conditions the design is for, its wavelengths in that air: 20 °C and one atmosphere when null.</param>
    /// <param name="options">The index conventions, and any substitutions.</param>
    public ThermalModel(OpticalSystem nominal, MaterialLibrary library, Mount mount, AirConditions? nominalAir = null,
        ThermalOptions? options = null)
    {
        Nominal = nominal ?? throw new ArgumentNullException(nameof(nominal));
        Mount = mount ?? throw new ArgumentNullException(nameof(mount));
        NominalAir = nominalAir ?? AirConditions.Standard;
        Options = options ?? ThermalOptions.Default;
        if (nominal.Surfaces.Count < 2) throw new ArgumentException("A lens needs an object and an image surface.", nameof(nominal));

        int count = nominal.Surfaces.Count;
        _after = new Material?[count];
        _cte = new double[count];
        _growth = new ExpansionCurve[count];
        _cteNote = new string[count];
        var unresolved = new List<string>();
        for (int i = 0; i < count; i++)
        {
            var s = nominal.Surfaces[i];
            if (s.IsMirror)
            {
                _after[i] = i > 0 ? _after[i - 1] : null;
                _cte[i] = i > 0 ? _cte[i - 1] : 0;
                _growth[i] = i > 0 ? _growth[i - 1] : new ConstantExpansion(0);
                _cteNote[i] = i > 0 ? _cteNote[i - 1] : "";
                continue;
            }
            var m = Options.SurfaceMaterials != null && Options.SurfaceMaterials.TryGetValue(i, out var given) ? given : Resolve(s, library);
            if (m == null && !string.IsNullOrWhiteSpace(s.Material)) unresolved.Add($"surface {i}: {s.Material}");
            _after[i] = m;
            _growth[i] = new ConstantExpansion(0);
            if (m != null) (_growth[i], _cteNote[i]) = ExpansionOf(m, s.Material);
            _cte[i] = _growth[i].Alpha(NominalAir.TemperatureC);
        }
        if (unresolved.Count > 0)
            throw new InvalidOperationException("No material found for " + string.Join("; ", unresolved));

        ValidateSegments();
        VacuumWavelengthsUm = nominal.Wavelengths.Select(w => VacuumWavelength(w.Value, NominalAir)).ToArray();
    }

    public OpticalSystem Nominal { get; }
    public Mount Mount { get; }
    public AirConditions NominalAir { get; }
    public ThermalOptions Options { get; }
    public double NominalTemperatureC => NominalAir.TemperatureC;
    public IReadOnlyList<double> VacuumWavelengthsUm { get; }

    private IndexOptions IndexOptions => Options.Index ?? IndexOptions.Default;

    /// <summary>The material after each surface; null for air. A mirror has the material it sits in.</summary>
    public Material? MaterialAfter(int surface) => _after[surface];

    /// <summary>The expansion coefficient the material after <paramref name="surface"/> grows with at the nominal temperature, 1/K.</summary>
    public double CteAfter(int surface) => _cte[surface];

    /// <summary>The length over temperature the material after <paramref name="surface"/> grows by.</summary>
    public ExpansionCurve GrowthAfter(int surface) => _growth[surface];

    /// <summary>The lens at <paramref name="temperatureC"/>, in the nominal air's pressure, humidity and CO₂.</summary>
    public ThermalLens At(double temperatureC) => At(NominalAir with { TemperatureC = temperatureC });

    /// <summary>Every part of the lens a temperature changes.</summary>
    public IReadOnlyList<ThermalPart> Parts()
    {
        var parts = new List<ThermalPart>();
        int image = Nominal.Surfaces.Count - 1;
        for (int i = 1; i < image; i++)
            if (Nominal.Surfaces[i].IsMirror) parts.Add(new ThermalPart(ThermalPartKind.Mirror, i));
            else if (_after[i] != null) parts.Add(new ThermalPart(ThermalPartKind.Glass, i));
        parts.Add(ThermalPart.Housing);
        for (int k = 0; k < Mount.Segments.Count; k++) parts.Add(new ThermalPart(ThermalPartKind.MountPart, k));
        return parts;
    }

    /// <summary>A part's name, for reports.</summary>
    public string Describe(ThermalPart part) => part.Kind switch
    {
        ThermalPartKind.Glass => $"{_after[part.Index]!.Name} after surface {part.Index}",
        ThermalPartKind.Mirror => $"mirror {part.Index} ({(Mount.MirrorSubstrates.TryGetValue(part.Index, out var m) ? m : Mount.Housing).Name})",
        ThermalPartKind.Housing => $"housing ({Mount.Housing.Name})",
        _ => $"mount part {Mount.Segments[part.Index].From}-{Mount.Segments[part.Index].To} ({Mount.Segments[part.Index].Material.Name})",
    };

    /// <summary>The lens, every part of it at the air's temperature.</summary>
    public ThermalLens At(AirConditions air) => At(air, null);

    /// <summary>
    /// The lens with only the parts <paramref name="changes"/> accepts at the air's temperature, the rest as
    /// designed: a glass not accepted keeps its nominal index. All of them when it is null.
    /// </summary>
    public ThermalLens At(AirConditions air, Func<ThermalPart, bool>? changes)
    {
        ArgumentNullException.ThrowIfNull(air);
        double dT = air.TemperatureC - NominalTemperatureC;
        double DT(ThermalPart p) => changes == null || changes(p) ? dT : 0.0;
        double Grow(ExpansionCurve c, ThermalPart p) =>
            DT(p) == 0.0 ? 1.0 : c.Scale(NominalTemperatureC, NominalTemperatureC + DT(p));
        var notes = new List<string>();
        var copy = LensCopy.Of(Nominal);
        var surfaces = copy.Surfaces;
        int count = surfaces.Count, image = count - 1;

        // The surfaces' shapes.
        var shapeBy = new string[count];
        var scale = new double[count];
        for (int i = 0; i < count; i++)
        {
            (shapeBy[i], var growth, var owner) = ShapeCte(i);
            scale[i] = Grow(growth, owner);
            if (i == 0 || i == image || Nominal.Surfaces[i].Type == SurfaceType.Paraxial)
            {
                scale[i] = 1.0;
                shapeBy[i] = i == 0 ? "object" : i == image ? "image" : "paraxial surface";
                if (i != 0 && i != image) notes.Add($"Surface {i} is a paraxial surface; its focal length does not change.");
                continue;
            }
            Expand(surfaces[i], scale[i]);
        }

        // The thicknesses: glass with its glass, air by the housing, then the mount's parts.
        var thicknessBy = new string[count];
        for (int i = 0; i < image; i++)
        {
            double t = Nominal.Surfaces[i].Thickness;
            if (i == 0)
            {
                thicknessBy[i] = "object distance, held";
                continue;
            }
            if (_after[i] != null)
            {
                surfaces[i].Thickness = t * Grow(_growth[i], GlassPart(i));
                thicknessBy[i] = _after[i]!.Name;
            }
            else
            {
                surfaces[i].Thickness = t * Grow(Mount.Housing.Growth, ThermalPart.Housing);
                thicknessBy[i] = "housing " + Mount.Housing.Name;
            }
        }
        foreach (var (seg, part) in Mount.Segments.Select((g, j) => (g, j)).OrderBy(p => p.g.To - p.g.From))
        {
            int free = FreeSpace(seg);
            double s = Grow(seg.Material.Growth, new ThermalPart(ThermalPartKind.MountPart, part));
            var nFrom = Nominal.Surfaces[seg.From];
            var nTo = Nominal.Surfaces[seg.To];
            double length = 0.0, others = 0.0;
            for (int k = seg.From; k < seg.To; k++)
            {
                length += Nominal.Surfaces[k].Thickness;
                if (k != free) others += surfaces[k].Thickness;
            }
            length += SagAt(nTo, seg.ToHeight, seg.To) - SagAt(nFrom, seg.FromHeight, seg.From);
            double grown = length * s
                - SagAt(surfaces[seg.To], seg.ToHeight * s, seg.To) + SagAt(surfaces[seg.From], seg.FromHeight * s, seg.From);
            surfaces[free].Thickness = grown - others;
            thicknessBy[free] = FormattableString.Invariant(
                $"mount part {seg.From}-{seg.To}, {seg.Material.Name}");
            if (surfaces[free].Thickness * Nominal.Surfaces[free].Thickness < 0)
                notes.Add($"The air space after surface {free} changes sign: the mount part {seg.From}-{seg.To} is shorter than what it holds.");
        }

        // A solve would refocus the lens, which is a compensator's job, not the temperature's.
        foreach (var s in surfaces) s.HasMarginalRaySolve = false;
        copy.Pickups.Clear();

        // The indices, and the wavelengths in the air at the temperature.
        var o = IndexOptions;
        var indices = new double[VacuumWavelengthsUm.Count][];
        bool outOfRange = false;
        for (int w = 0; w < indices.Length; w++)
        {
            double vac = VacuumWavelengthsUm[w];
            var n = new double[count];
            for (int i = 0; i < count; i++)
            {
                if (surfaces[i].IsMirror) { n[i] = i > 0 ? n[i - 1] : 1.0; continue; }
                var m = _after[i];
                if (m == null) { n[i] = 1.0; continue; }
                var index = DT(GlassPart(i)) != 0.0 || dT == 0.0
                    ? m.IndexAtVacuumWavelength(vac, air, air.TemperatureC, o)
                    : m.IndexAtVacuumWavelength(vac, NominalAir, NominalTemperatureC, o);
                n[i] = index.Relative;
                if (!index.InRange && !outOfRange)
                {
                    outOfRange = true;
                    notes.Add(FormattableString.Invariant(
                        $"{m.Name} is outside its data's range at {vac:F4} µm (vacuum) and {air.TemperatureC:G4} °C."));
                }
            }
            indices[w] = n;
            copy.Wavelengths[w].Value = WavelengthInAir(vac, air);
        }

        for (int i = 1; i < image; i++)
            if (_after[i] != null && _cteNote[i] != "" && !notes.Contains(_cteNote[i])) notes.Add(_cteNote[i]);
        if (dT != 0.0) ExpansionRangeNotes(air.TemperatureC, notes);
        foreach (var (i, _) in Mirrors().Where(m => !Mount.MirrorSubstrates.ContainsKey(m.Index)))
            notes.Add($"Mirror {i} has no substrate given; it is taken to be {Mount.Housing.Name}, the housing's material.");

        SetAperture(copy, indices, scale, notes);

        var states = new SurfaceState[count];
        for (int i = 0; i < count; i++)
            states[i] = new SurfaceState(i, shapeBy[i], scale[i], i < image ? thicknessBy[i] : "",
                Nominal.Surfaces[i].Thickness, surfaces[i].Thickness);
        return new ThermalLens(copy, air, dT, VacuumWavelengthsUm.ToArray(), indices, states, notes);
    }

    /// <summary>The indices of the nominal design at its own conditions, for each wavelength.</summary>
    public double[][] NominalIndices() => At(NominalAir).Indices.ToArray();

    // ── Materials ────────────────────────────────────────────────────────────────────

    private Material? Resolve(Surface s, MaterialLibrary library)
    {
        var subs = Options.Substitutions;
        string? name = s.Material;
        if (subs != null && !string.IsNullOrWhiteSpace(name))
        {
            if (!string.IsNullOrEmpty(s.CatalogName) && subs.TryGetValue(s.CatalogName + ":" + name, out var q)) return q;
            if (subs.TryGetValue(name!, out var b)) return b;
        }
        if (s.ModelIndexEnabled && s.ModelNd > 0.0) return Tie19Material.Model(s.ModelNd, s.ModelVd, s.ModelDPgF);
        if (string.IsNullOrWhiteSpace(name)) return null;

        var found = !string.IsNullOrEmpty(s.CatalogName)
            ? library.Find(s.CatalogName + ":" + name) ?? library.Find(name!, Nominal.GlassCatalogs)
            : library.Find(name!, Nominal.GlassCatalogs);
        if (found != null) return found;
        return GlassCode.TryParse(name, out double nd, out double vd) ? Tie19Material.Model(nd, vd) : null;
    }

    private (ExpansionCurve Growth, string Note) ExpansionOf(Material m, string? surfaceName)
    {
        var overrides = Options.CteOverrides;
        if (overrides != null)
        {
            if (overrides.TryGetValue(m.Name, out double c)) return (new ConstantExpansion(c), "");
            if (overrides.TryGetValue(m.Glass, out c)) return (new ConstantExpansion(c), "");
            if (surfaceName != null && overrides.TryGetValue(surfaceName, out c)) return (new ConstantExpansion(c), "");
        }
        var e = m.Expansion;
        if (e.IgnoreExpansion) return (new ConstantExpansion(0), $"{m.Name}: the catalog says not to expand it; it is held at its nominal size.");
        if (e.Cte == 0.0 && e.Curve == null) return (new ConstantExpansion(0), $"{m.Name} has no expansion coefficient; it is held at its nominal size.");
        return (e.Growth, "");
    }

    /// <summary>A note for each material whose expansion data don't reach the temperature, or the nominal one.</summary>
    private void ExpansionRangeNotes(double temperatureC, List<string> notes)
    {
        var used = new List<(string Name, ExpansionCurve Curve)>();
        for (int i = 1; i < Nominal.Surfaces.Count - 1; i++)
            if (_after[i] != null && !Nominal.Surfaces[i].IsMirror) used.Add((_after[i]!.Name, _growth[i]));
        used.Add((Mount.Housing.Name, Mount.Housing.Growth));
        used.AddRange(Mount.Segments.Select(g => (g.Material.Name, g.Material.Growth)));
        used.AddRange(Mirrors().Select(m => Mount.MirrorSubstrates.TryGetValue(m.Index, out var sub) ? sub : Mount.Housing)
            .Select(sub => (sub.Name, sub.Growth)));
        foreach (var (name, curve) in used)
        {
            if (curve.InRange(temperatureC) && curve.InRange(NominalTemperatureC)) continue;
            string note = FormattableString.Invariant(
                $"{name}'s expansion data cover {curve.RangeC.Min:0.#} to {curve.RangeC.Max:0.#} °C; beyond them its length is extrapolated.");
            if (!notes.Contains(note)) notes.Add(note);
        }
    }

    /// <summary>What a surface's shape grows with: its mirror's substrate, its glass, or the housing; and that part.</summary>
    private (string By, ExpansionCurve Growth, ThermalPart Owner) ShapeCte(int i)
    {
        var s = Nominal.Surfaces[i];
        if (s.IsMirror)
        {
            var sub = Mount.MirrorSubstrates.TryGetValue(i, out var m) ? m : Mount.Housing;
            return ("mirror substrate " + sub.Name, sub.Growth, new ThermalPart(ThermalPartKind.Mirror, i));
        }
        int before = i - 1, after = i;
        bool glassBefore = i > 0 && _after[before] != null, glassAfter = _after[after] != null;
        if (glassBefore && glassAfter && !ReferenceEquals(_after[before], _after[after]))
            return Mount.Cemented == CementedSurface.FollowingGlass
                ? (_after[after]!.Name, _growth[after], GlassPart(after))
                : (_after[before]!.Name, _growth[before], GlassPart(before));
        if (glassAfter) return (_after[after]!.Name, _growth[after], GlassPart(after));
        if (glassBefore) return (_after[before]!.Name, _growth[before], GlassPart(before));
        return ("housing " + Mount.Housing.Name, Mount.Housing.Growth, ThermalPart.Housing);
    }

    /// <summary>The glass part the medium after <paramref name="surface"/> belongs to: the glass itself, or the one a mirror continues.</summary>
    private ThermalPart GlassPart(int surface)
    {
        int i = surface;
        while (i > 0 && Nominal.Surfaces[i].IsMirror) i--;
        return new ThermalPart(ThermalPartKind.Glass, i);
    }

    private IEnumerable<(int Index, Surface Surface)> Mirrors() =>
        Nominal.Surfaces.Select((s, i) => (i, s)).Where(p => p.s.IsMirror);

    // ── Geometry ─────────────────────────────────────────────────────────────────────

    /// <summary>Every length of the surface times <paramref name="s"/>: the same surface, larger.</summary>
    internal static void Expand(Surface surface, double s)
    {
        if (s == 1.0) return;
        surface.Curvature /= s;
        var a = surface.AsphericCoefficients;
        for (int k = 0; k < a.Length; k++) a[k] *= Math.Pow(s, -(2 * k + 1));
        var z = surface.FringeZernike;
        for (int j = 0; j < z.Length; j++) z[j] *= s;
        surface.ZernikeRadius *= s;
        surface.SemiDiameter *= s;
        surface.ObscurationRadius *= s;
        surface.InnerRadius *= s;
        surface.ClapOuterRadius *= s;
        surface.FloatingApertureRadius *= s;
    }

    private static double SagAt(Surface s, double height, int index)
    {
        if (height == 0.0) return 0.0;
        double z = s.Sag(height);
        if (double.IsNaN(z))
            throw new InvalidOperationException(FormattableString.Invariant(
                $"A mount part touches surface {index} at height {height:G6}, beyond the surface."));
        return z;
    }

    private void ValidateSegments()
    {
        int image = Nominal.Surfaces.Count - 1;
        var segs = Mount.Segments;
        foreach (var g in segs)
        {
            if (g.From < 0 || g.To > image || g.From >= g.To)
                throw new ArgumentException($"Mount part {g.From}-{g.To} does not run forward between two surfaces of the lens.");
            for (int k = g.From; k < g.To; k++)
                if (double.IsInfinity(Nominal.Surfaces[k].Thickness))
                    throw new ArgumentException($"Mount part {g.From}-{g.To} holds an infinite distance.");
        }
        for (int a = 0; a < segs.Count; a++)
            for (int b = a + 1; b < segs.Count; b++)
            {
                var (p, q) = (segs[a], segs[b]);
                bool disjoint = p.To <= q.From || q.To <= p.From;
                bool nested = (p.From >= q.From && p.To <= q.To) || (q.From >= p.From && q.To <= p.To);
                if (p.From == q.From && p.To == q.To)
                    throw new ArgumentException($"Two mount parts hold surfaces {p.From}-{p.To}.");
                if (!disjoint && !nested)
                    throw new ArgumentException($"Mount parts {p.From}-{p.To} and {q.From}-{q.To} overlap without one holding the other.");
            }
        foreach (var g in segs) FreeSpace(g);
    }

    /// <summary>The air space that takes up the rest of a mount part's length.</summary>
    private int FreeSpace(MountSegment g)
    {
        bool Inner(int k) => Mount.Segments.Any(o => !ReferenceEquals(o, g) && o.From >= g.From && o.To <= g.To
                                                    && k >= o.From && k < o.To);
        bool Air(int k) => _after[k] == null;
        if (g.FreeSpace is int f)
        {
            if (f < g.From || f >= g.To)
                throw new ArgumentException($"The free space {f} is not inside mount part {g.From}-{g.To}.");
            if (!Air(f)) throw new ArgumentException($"The free space after surface {f} is not air.");
            if (Inner(f)) throw new ArgumentException($"The free space after surface {f} is held by a part inside {g.From}-{g.To}.");
            return f;
        }
        for (int k = g.To - 1; k >= g.From; k--)
            if (Air(k) && !Inner(k)) return k;
        throw new ArgumentException($"Mount part {g.From}-{g.To} holds no air space of its own to take up its length.");
    }

    // ── Aperture and wavelengths ─────────────────────────────────────────────────────

    /// <summary>
    /// The stop is a physical aperture, so it grows with its surface. The entrance pupil at the temperature is the one
    /// that fills it, whatever way the file stated the aperture.
    /// </summary>
    private void SetAperture(OpticalSystem copy, double[][] indices, double[] scale, List<string> notes)
    {
        int p = Math.Max(0, Nominal.PrimaryWavelengthIndex);
        var nominalIndices = NominalIndexTable(p);
        var nominalTrace = ParaxialTrace.Trace(Nominal, nominalIndices, 0.0);
        double epd = nominalTrace.Epd;
        int stop = Nominal.StopSurfaceIndex;
        int last = Nominal.LastOpticalSurface();
        if (stop < 1 || stop > last) stop = last;

        copy.Aperture = new Aperture(ApertureType.EPD, epd);
        var trace = ParaxialTrace.Trace(copy, indices[p], 0.0);
        double yNominal = nominalTrace.Y[stop], y = trace.Y[stop];
        copy.Aperture = new Aperture(ApertureType.EPD,
            Math.Abs(y) > 1e-15 && Math.Abs(yNominal) > 1e-15 ? epd * scale[stop] * yNominal / y : epd * scale[stop]);
        if (Nominal.Aperture.Type != ApertureType.EPD)
            notes.Add("The aperture is given as the entrance pupil the expanded stop admits, not as the file's "
                      + Nominal.Aperture.Type + ".");
    }

    private double[] NominalIndexTable(int wavelength)
    {
        var o = IndexOptions;
        int count = Nominal.Surfaces.Count;
        var n = new double[count];
        for (int i = 0; i < count; i++)
        {
            if (Nominal.Surfaces[i].IsMirror) { n[i] = i > 0 ? n[i - 1] : 1.0; continue; }
            n[i] = _after[i] == null ? 1.0
                : _after[i]!.IndexAtVacuumWavelength(VacuumWavelengthsUm[wavelength], NominalAir, NominalTemperatureC, o).Relative;
        }
        return n;
    }

    private double VacuumWavelength(double lambdaInAir, AirConditions air)
    {
        if (air.IsVacuum) return lambdaInAir;
        var o = IndexOptions;
        if (o.AirWavelength == AirWavelength.Input) return lambdaInAir * AirIndex.Of(o.AirModel, lambdaInAir, air);
        double vac = lambdaInAir;
        for (int i = 0; i < 4; i++) vac = lambdaInAir * AirIndex.Of(o.AirModel, vac, air);
        return vac;
    }

    private double WavelengthInAir(double vacuum, AirConditions air)
    {
        if (air.IsVacuum) return vacuum;
        var o = IndexOptions;
        double lambda = vacuum;
        if (o.AirWavelength == AirWavelength.Vacuum) return vacuum / AirIndex.Of(o.AirModel, vacuum, air);
        for (int i = 0; i < 4; i++) lambda = vacuum / AirIndex.Of(o.AirModel, lambda, air);
        return lambda;
    }
}
