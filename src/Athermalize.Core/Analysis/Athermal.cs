namespace Athermalize.Core.Analysis;

/// <summary>A thin lens of a system: its material's constants over a band, its power, and its marginal-ray height.</summary>
/// <param name="Omega">ω = 1/ν.</param>
/// <param name="Gamma">γ = (dn/dT)/(n − 1) − α, 1/K.</param>
/// <param name="Power">φ, 1/mm.</param>
/// <param name="Height">h, the paraxial marginal ray's height at the lens, mm.</param>
/// <param name="Index">n, for the Petzval sum.</param>
/// <param name="PartialLong">The partial dispersion, for the secondary spectrum.</param>
public readonly record struct ThinLens(double Omega, double Gamma, double Power, double Height, double Index = 1.5,
    double PartialLong = 0.0)
{
    public static ThinLens Of(MaterialConstants c, double power, double height = 1.0) =>
        new(c.Omega, c.Gamma, power, height, c.Index, c.PartialLong);
}

/// <summary>A thin-lens system's residuals: zero for an achromatic, athermal, flat-field lens (Jiang and Liu 2024).</summary>
/// <param name="Power">φ_T = Σ φ′, φ′ = φ·h/h₁.</param>
/// <param name="Chromatic">R_ch = Σ ω φ″, φ″ = φ·(h/h₁)²: the change of power across the band.</param>
/// <param name="Thermal">R_th = Σ (γ + α_h) φ′, 1/(mm K): the power's change per kelvin less the housing's, which the focal length must follow.</param>
/// <param name="Petzval">R_ptz = Σ φ/n.</param>
/// <param name="SecondarySpectrum">R_pch = Σ ω φ″ P.</param>
/// <param name="Omega">The composite map's equivalent point, ω′_e = Σ wt sign(φ) ω.</param>
/// <param name="Gamma">Its γ′_e = Σ wt sign(φ)(γ + α_h), 1/K.</param>
public readonly record struct ThinResiduals(double Power, double Chromatic, double Thermal, double Petzval,
    double SecondarySpectrum, double Omega, double Gamma)
{
    /// <summary>
    /// The thin-lens rate of the focus less the image plane, mm/K, the image plane held by the housing at the focal
    /// distance: −R_th/φ_T². Negative when the image moves toward the lens.
    /// </summary>
    public double DefocusPerK => -Thermal / (Power * Power);
}

/// <summary>
/// Thin-lens athermalization: the athermal map, its feasibility test, the athermal achromatic doublet and
/// triplet, and Jiang and Liu's (2024) objective for ranking glass sets.
///
/// <para>On the map each material is a point (ω, γ). A lens of thin elements in contact, in a housing of α_h that
/// holds the image plane at the focal distance, is achromatic when Σ ω φ = 0 and athermal when
/// Σ (γ + α_h) φ = 0 (Jiang and Liu eqs. 5-7). An athermal achromatic doublet's two points therefore lie on a line
/// through the housing's point (0, −α_h).</para>
/// </summary>
public static class Athermal
{
    /// <summary>The residuals of a system of thin lenses in a housing of <paramref name="housingCte"/> (Jiang and Liu 2024, eqs. 3-16).</summary>
    public static ThinResiduals Residuals(IReadOnlyList<ThinLens> lenses, double housingCte)
    {
        if (lenses.Count == 0) throw new ArgumentException("no lenses");
        double h1 = lenses[0].Height;
        double power = 0, chromatic = 0, thermal = 0, petzval = 0, secondary = 0, absolute = 0;
        foreach (var l in lenses)
        {
            double p1 = l.Power * l.Height / h1, p2 = l.Power * (l.Height / h1) * (l.Height / h1);
            power += p1;
            chromatic += l.Omega * p2;
            thermal += (l.Gamma + housingCte) * p1;
            petzval += l.Power / l.Index;
            secondary += l.Omega * p2 * l.PartialLong;
            absolute += Math.Abs(p1);
        }
        double omega = 0, gamma = 0;
        foreach (var l in lenses)
        {
            double weight = Math.Abs(l.Power * l.Height / h1) / absolute, sign = Math.Sign(l.Power);
            omega += weight * sign * l.Omega;
            gamma += weight * sign * (l.Gamma + housingCte);
        }
        return new ThinResiduals(power, chromatic, thermal, petzval, secondary, omega, gamma);
    }

    /// <summary>
    /// The thin achromatic doublet of power <paramref name="power"/> from materials a and b in contact:
    /// φ_a = φ ω_b/(ω_b − ω_a), φ_b = −φ ω_a/(ω_b − ω_a); and its residuals in the housing. Null when the two
    /// have the same ω, and no achromat can be made of them.
    /// </summary>
    public static (double PowerA, double PowerB, ThinResiduals Residuals)? Doublet(MaterialConstants a, MaterialConstants b,
        double power, double housingCte)
    {
        double d = b.Omega - a.Omega;
        if (Math.Abs(d) < 1e-12 * (Math.Abs(a.Omega) + Math.Abs(b.Omega))) return null;
        double pa = power * b.Omega / d, pb = -power * a.Omega / d;
        return (pa, pb, Residuals(new[] { ThinLens.Of(a, pa), ThinLens.Of(b, pb) }, housingCte));
    }

    /// <summary>
    /// The thin athermal achromatic triplet in contact: the powers with Σ φ = φ, Σ ω φ = 0 and Σ (γ + α_h) φ = 0
    /// (Zhang et al. 2012, eqs. 6-8, with γ + α_h for the housing). Null when the three points are collinear on
    /// the map, and no such triplet exists.
    /// </summary>
    public static double[]? Triplet(MaterialConstants a, MaterialConstants b, MaterialConstants c, double power,
        double housingCte)
    {
        double[] w = { a.Omega, b.Omega, c.Omega };
        double[] g = { a.Gamma + housingCte, b.Gamma + housingCte, c.Gamma + housingCte };
        // Σφ = φ, Σωφ = 0, Σgφ = 0: Cramer's rule.
        double det = Det(1, 1, 1, w[0], w[1], w[2], g[0], g[1], g[2]);
        double scale = Math.Abs(w[0] * g[1]) + Math.Abs(w[1] * g[2]) + Math.Abs(w[2] * g[0]) + 1e-300;
        if (Math.Abs(det) < 1e-12 * scale) return null;
        return new[]
        {
            power * Det(1, 1, 1, 0, w[1], w[2], 0, g[1], g[2]) / det,
            power * Det(1, 1, 1, w[0], 0, w[2], g[0], 0, g[2]) / det,
            power * Det(1, 1, 1, w[0], w[1], 0, g[0], g[1], 0) / det,
        };
    }

    private static double Det(double a, double b, double c, double d, double e, double f, double g, double h, double i) =>
        a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);

    /// <summary>
    /// Whether lenses of these materials and power signs can be made achromatic and athermal at all: the origin
    /// must lie in the convex hull of their composite points (sign(φ) ω, sign(φ)(γ + α_h)) (Jiang and Liu 2024).
    /// A necessary condition, not a sufficient one.
    /// </summary>
    public static bool CanAthermalize(IReadOnlyList<(MaterialConstants Material, int PowerSign)> lenses, double housingCte)
    {
        var p = lenses.Select(l => (X: l.PowerSign * l.Material.Omega, Y: l.PowerSign * (l.Material.Gamma + housingCte))).ToList();
        // Scale both axes to comparable sizes so the geometry is well conditioned.
        double sx = p.Max(q => Math.Abs(q.X)) + 1e-300, sy = p.Max(q => Math.Abs(q.Y)) + 1e-300;
        var pts = p.Select(q => (q.X / sx, q.Y / sy)).ToList();
        return InConvexHull(pts, (0.0, 0.0));
    }

    /// <summary>Whether <paramref name="point"/> lies in the convex hull of <paramref name="points"/>, its boundary included.</summary>
    internal static bool InConvexHull(IReadOnlyList<(double X, double Y)> points, (double X, double Y) point)
    {
        var hull = ConvexHull(points);
        const double eps = 1e-12;
        if (hull.Count == 1) return Math.Abs(hull[0].X - point.X) < eps && Math.Abs(hull[0].Y - point.Y) < eps;
        if (hull.Count == 2)
        {
            var (a, b) = (hull[0], hull[1]);
            double cross = (b.X - a.X) * (point.Y - a.Y) - (b.Y - a.Y) * (point.X - a.X);
            double dot = (point.X - a.X) * (b.X - a.X) + (point.Y - a.Y) * (b.Y - a.Y);
            double len = (b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y);
            return Math.Abs(cross) < eps && dot >= -eps && dot <= len + eps;
        }
        for (int i = 0; i < hull.Count; i++)
        {
            var a = hull[i];
            var b = hull[(i + 1) % hull.Count];
            if ((b.X - a.X) * (point.Y - a.Y) - (b.Y - a.Y) * (point.X - a.X) < -eps) return false;
        }
        return true;
    }

    /// <summary>The convex hull, counterclockwise (Andrew's monotone chain).</summary>
    private static List<(double X, double Y)> ConvexHull(IReadOnlyList<(double X, double Y)> points)
    {
        var p = points.Distinct().OrderBy(q => q.X).ThenBy(q => q.Y).ToList();
        if (p.Count < 3) return p;
        static double Cross((double X, double Y) o, (double X, double Y) a, (double X, double Y) b) =>
            (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        var hull = new List<(double X, double Y)>();
        foreach (var q in p)
        {
            while (hull.Count >= 2 && Cross(hull[^2], hull[^1], q) <= 0) hull.RemoveAt(hull.Count - 1);
            hull.Add(q);
        }
        int lower = hull.Count + 1;
        for (int i = p.Count - 2; i >= 0; i--)
        {
            while (hull.Count >= lower && Cross(hull[^2], hull[^1], p[i]) <= 0) hull.RemoveAt(hull.Count - 1);
            hull.Add(p[i]);
        }
        hull.RemoveAt(hull.Count - 1);
        return hull;
    }
}

/// <summary>
/// Jiang and Liu's (2024) objective for a glass set at fixed powers (eqs. 15-19):
/// f = g·(1 + W_power Σ|Δφ′|/φ_T), g = Σ (τ/r)|R| over the thermal, chromatic, Petzval and secondary-spectrum
/// residuals, each divided by its acceptable size r and weighted by τ, for the system scaled to a focal length of 1.
/// </summary>
public sealed record JiangObjective
{
    public double TauThermal { get; init; } = 1.0;
    public double TauChromatic { get; init; } = 1.0;
    public double TauSecondary { get; init; } = 1.0;
    public double TauPetzval { get; init; } = 0.1;
    public double WPower { get; init; } = 2.0;

    /// <summary>The acceptable residuals at a focal length of 1 (the paper's: 7.8×10⁻⁶/K, 0.05×10⁻³, 0.05×10⁻³, 0.05).</summary>
    public double RThermal { get; init; } = 7.8e-6;
    public double RChromatic { get; init; } = 0.05e-3;
    public double RSecondary { get; init; } = 0.05e-3;
    public double RPetzval { get; init; } = 0.05;

    /// <summary>
    /// g·(1 + W_power·powerDeviation), from the residuals of the system scaled to φ_T = 1 and the relative power
    /// deviation Σ|Δφ′|/φ_T of the glass swap, Δφ′ = ((n_new − n_old)/(n_old − 1))·φ′ (eq. 18).
    /// </summary>
    public double Evaluate(ThinResiduals r, double powerDeviation)
    {
        // R_ch, R_th, R_pch scale with φ; R_ptz with φ too; at φ_T = 1 divide each by φ_T.
        double s = 1.0 / r.Power;
        double g = TauThermal * Math.Abs(r.Thermal * s) / RThermal
                 + TauChromatic * Math.Abs(r.Chromatic * s) / RChromatic
                 + TauSecondary * Math.Abs(r.SecondarySpectrum * s) / RSecondary
                 + TauPetzval * Math.Abs(r.Petzval * s) / RPetzval;
        return g * (1.0 + WPower * powerDeviation);
    }

    /// <summary>The same from the normalized residuals as the paper tabulates them, |R/r| each.</summary>
    public double Evaluate(double thermal, double chromatic, double secondary, double petzval, double powerDeviation) =>
        (TauThermal * thermal + TauChromatic * chromatic + TauSecondary * secondary + TauPetzval * petzval)
        * (1.0 + WPower * powerDeviation);
}
