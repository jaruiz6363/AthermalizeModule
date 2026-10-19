using Athermalize.Core.Materials;

namespace Athermalize.Core.Lens;

/// <summary>A mount material and its expansion coefficient, 1/K.</summary>
/// <param name="Name">The material's name, for reports.</param>
/// <param name="Cte">The mean expansion coefficient, 1/K; for a material with a curve, its coefficient at 20 °C.</param>
/// <param name="Source">Where the coefficient comes from.</param>
/// <param name="Curve">The length over temperature, when the source gives it; null for one coefficient at every temperature.</param>
public sealed record MountMaterial(string Name, double Cte, string Source = "", ExpansionCurve? Curve = null)
{
    /// <summary>The curve the material grows by: its own, or its one coefficient at every temperature.</summary>
    public ExpansionCurve Growth => Curve ?? new ConstantExpansion(Cte);

    /// <summary>A material whose length over temperature is <paramref name="curve"/>; its coefficient is the curve's at 20 °C.</summary>
    public static MountMaterial OfCurve(string name, ExpansionCurve curve, string source) =>
        new(name, curve.Alpha(20.0), source, curve);

    /// <summary>A material known only by its coefficient, 1/K.</summary>
    public static MountMaterial OfCte(double cte) =>
        new(FormattableString.Invariant($"α {cte * 1e6:G4}×10⁻⁶/K"), cte, "given");

    public override string ToString() => Name;
}

/// <summary>
/// A part of the mount that holds two surfaces a fixed length apart: a spacer, a barrel section between two lens
/// seats, or the housing between a lens and the image plane. The length is measured from surface
/// <paramref name="From"/> at height <paramref name="FromHeight"/> to surface <paramref name="To"/> at height
/// <paramref name="ToHeight"/>, and grows with the part's own expansion coefficient; so do the two contact heights.
/// The glass thicknesses inside the part grow with their glasses, the other air spaces inside it by their own
/// rules, and one air space, <paramref name="FreeSpace"/>, takes up the rest.
/// </summary>
/// <param name="From">The first surface the part touches.</param>
/// <param name="To">The last surface the part touches; the image surface for a housing that holds the image plane.</param>
/// <param name="Material">The part's material.</param>
/// <param name="FromHeight">The contact height on <paramref name="From"/>; 0 is the vertex.</param>
/// <param name="ToHeight">The contact height on <paramref name="To"/>; 0 is the vertex.</param>
/// <param name="FreeSpace">
/// The surface whose following air space takes up the rest. Null means the last air space in the part that no part
/// inside it holds.
/// </param>
public sealed record MountSegment(int From, int To, MountMaterial Material, double FromHeight = 0.0,
    double ToHeight = 0.0, int? FreeSpace = null)
{
    /// <summary>A spacer filling the air space after surface <paramref name="surface"/>, touching both surfaces at <paramref name="height"/>.</summary>
    public static MountSegment Spacer(int surface, MountMaterial material, double height = 0.0) =>
        new(surface, surface + 1, material, height, height);
}

/// <summary>Which glass sets the expansion of a surface between two glasses.</summary>
public enum CementedSurface
{
    /// <summary>The glass after the surface, the element whose front face it is.</summary>
    FollowingGlass,

    /// <summary>The glass before the surface.</summary>
    PrecedingGlass,
}

/// <summary>
/// How the lens is held: the housing, any parts that hold particular surfaces, and the substrates of the mirrors.
///
/// <para>Every air space no part holds grows with the housing, vertex to vertex, and so does the space to the image,
/// so the image plane is held by the housing at the last surface (Jamieson). A chain of parts, each with its own
/// material, moves the elements along the barrel by the sum of the parts' growths (Liu et al. 2025). A part that
/// touches the glass at a height away from the axis changes the air space by the change of the surfaces' sags there
/// as well (Shi et al. 2020). The space before the first surface, to the object, does not change unless a part holds
/// it.</para>
/// </summary>
public sealed class Mount
{
    public Mount(MountMaterial housing, IEnumerable<MountSegment>? segments = null,
        IReadOnlyDictionary<int, MountMaterial>? mirrorSubstrates = null,
        CementedSurface cemented = CementedSurface.FollowingGlass)
    {
        Housing = housing ?? throw new ArgumentNullException(nameof(housing));
        Segments = segments?.ToList() ?? new List<MountSegment>();
        MirrorSubstrates = mirrorSubstrates ?? new Dictionary<int, MountMaterial>();
        Cemented = cemented;
    }

    /// <summary>The housing's material; it holds every air space no segment holds.</summary>
    public MountMaterial Housing { get; }

    public IReadOnlyList<MountSegment> Segments { get; }

    /// <summary>The substrate of each mirror, by surface; a mirror not listed is taken to be of the housing's material.</summary>
    public IReadOnlyDictionary<int, MountMaterial> MirrorSubstrates { get; }

    public CementedSurface Cemented { get; }
}
