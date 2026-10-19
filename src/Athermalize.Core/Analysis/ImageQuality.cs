using AberrationCalculator.Core.Glass;
using Athermalize.Core.Air;
using Athermalize.Core.Lens;
using WavefrontErrorCalculator.Core;

namespace Athermalize.Core.Analysis;

/// <summary>How the image is judged.</summary>
public sealed record QualityOptions
{
    /// <summary>
    /// The wavefront's conventions: Hopkins and Welford's by default. Its weighting is not used: the samples are
    /// weighted by the pupil quadrature below.
    /// </summary>
    public WavefrontOptions Wavefront { get; init; } = WavefrontOptions.Reference;

    /// <summary>
    /// Gauss quadrature over the pupil (Forbes 1988): Gauss-Legendre in ρ² on <see cref="Rings"/> rings, of
    /// <see cref="Arms"/> rays each, exact for a wavefront polynomial up to ρ^(4·Rings − 2) and an azimuthal order
    /// below <see cref="Arms"/>. 8 rings of 24 rays by default.
    /// </summary>
    public int Rings { get; init; } = 8;

    public int Arms { get; init; } = 24;

    /// <summary>
    /// A central obstruction, as a fraction of the pupil's radius: the quadrature then covers the annulus. The
    /// lens's chief ray is still traced through the pupil's center. 0 by default. It applies to the wavefront; the
    /// MTF, diffraction or geometric, is of the whole pupil.
    /// </summary>
    public double Obscuration { get; init; }

    /// <summary>Every wavelength, weighted by the lens's weights; the primary alone when false.</summary>
    public bool Polychromatic { get; init; } = true;

    /// <summary>Which MTF: the diffraction MTF by default, or the geometric one.</summary>
    public MtfKind MtfKind { get; init; } = MtfKind.Diffraction;

    /// <summary>
    /// The diffraction MTF's pupil grid, n×n (WavefrontErrorCalculator's autocorrelation with traced partners).
    /// 64 by default, within a few 10⁻³ of a converged MTF, which keeps a scan over temperature quick.
    /// </summary>
    public int DiffractionGrid { get; init; } = 64;

    /// <summary>The geometric MTF's sampling.</summary>
    public MtfOptions Mtf { get; init; } = new();

    internal AnnularQuadrature Sampling => new(Rings, Arms, Obscuration);

    internal WavefrontOptions EffectiveWavefront(double focusShift) =>
        Wavefront with { Weighting = Weighting.PerRay, FocusShift = focusShift };
}

/// <summary>
/// Gauss quadrature over the annulus ε ≤ ρ ≤ 1 of the unit pupil: Gauss-Legendre in t = ρ² on [ε², 1], and equally
/// spaced azimuths offset half a step from the meridian. Each point's area is its quadrature weight; they sum to
/// π(1 − ε²). With ε = 0 it is WavefrontErrorCalculator's Gauss quadrature.
/// </summary>
public sealed record AnnularQuadrature(int Rings, int Arms, double Obscuration) : Sampling
{
    public override IReadOnlyList<PupilPoint> Points()
    {
        if (Rings < 1) throw new ArgumentOutOfRangeException(nameof(Rings));
        if (Arms < 1) throw new ArgumentOutOfRangeException(nameof(Arms));
        if (!(Obscuration >= 0.0 && Obscuration < 1.0)) throw new ArgumentOutOfRangeException(nameof(Obscuration));
        double t0 = Obscuration * Obscuration, span = 1.0 - t0;
        var (nodes, weights) = GaussLegendre(Rings);
        var points = new List<PupilPoint>(Rings * Arms);
        for (int i = 0; i < Rings; i++)
        {
            double t = t0 + span * 0.5 * (nodes[i] + 1.0), rho = Math.Sqrt(t);
            double area = 0.5 * weights[i] * 0.5 * span * (2.0 * Math.PI / Arms);   // ½ dt dθ, dt = ½ span dx
            for (int j = 0; j < Arms; j++)
            {
                double a = 2.0 * Math.PI * (j + 0.5) / Arms;
                points.Add(new PupilPoint(rho * Math.Sin(a), rho * Math.Cos(a), area));
            }
        }
        return points;
    }

    /// <summary>Gauss-Legendre nodes and weights on [−1, 1], by Newton's method on Pₙ.</summary>
    private static (double[] Nodes, double[] Weights) GaussLegendre(int n)
    {
        var x = new double[n];
        var w = new double[n];
        for (int i = 0; i < n; i++)
        {
            double z = Math.Cos(Math.PI * (i + 0.75) / (n + 0.5)), dp = 0.0;
            for (int k = 0; k < 100; k++)
            {
                double p0 = 1.0, p1 = z;
                for (int m = 2; m <= n; m++) { double p2 = ((2 * m - 1) * z * p1 - (m - 1) * p0) / m; p0 = p1; p1 = p2; }
                double pn = n == 1 ? z : p1, pm = n == 1 ? 1.0 : p0;
                dp = n * (z * pn - pm) / (z * z - 1.0);
                double dz = pn / dp;
                z -= dz;
                if (Math.Abs(dz) < 1e-15) break;
            }
            x[i] = z;
            w[i] = 2.0 / ((1.0 - z * z) * dp * dp);
        }
        return (x, w);
    }
}

/// <summary>Which MTF is computed.</summary>
public enum MtfKind
{
    /// <summary>
    /// The diffraction MTF: the autocorrelation of the pupil function in the image's spatial frequencies, each
    /// wavelength's OTF summed by its weight (WavefrontErrorCalculator's DiffractionMtf). Needed wherever the
    /// wavelength is long against the blur, as in the thermal infrared.
    /// </summary>
    Diffraction,

    /// <summary>The geometric MTF, from where the rays land (Kidger): diffraction left out, so it overstates a nearly perfect lens.</summary>
    Geometric,
}

/// <summary>The image at one temperature, at a fixed image plane and refocused.</summary>
/// <param name="TemperatureC">The temperature.</param>
/// <param name="RmsFixed">The RMS wavefront error at the image plane the mount holds, waves.</param>
/// <param name="FocusShift">The move of the image plane that minimizes the RMS wavefront error, mm, along the axis.</param>
/// <param name="RmsRefocused">The RMS wavefront error there, waves.</param>
/// <param name="MtfFixed">For each field, the MTF at the fixed plane at each frequency, of the kind the options ask for.</param>
/// <param name="MtfRefocused">The same, refocused.</param>
public sealed record QualityState(double TemperatureC, double RmsFixed, double FocusShift, double RmsRefocused,
    IReadOnlyList<GeometricMtf.Modulation[]> MtfFixed, IReadOnlyList<GeometricMtf.Modulation[]> MtfRefocused);

/// <summary>
/// The image quality of a lens at a temperature: the polychromatic RMS wavefront error and the diffraction MTF
/// (or the geometric one), from WavefrontErrorCalculator, at the image plane the mount holds and with the focus compensated.
///
/// <para>The RMS over the lens's fields and wavelengths is √(Σ w_f w_λ W²_rms / Σ w_f w_λ) with the lens's own
/// weights. The focus compensator moves the image plane along the axis to the minimum of that RMS, found by
/// Brent's method from the paraxial focus.</para>
/// </summary>
public static class ImageQuality
{
    private static readonly GlassCatalog NoCatalog = new();

    /// <summary>WavefrontErrorCalculator's model of the lens at a temperature, with its index tables.</summary>
    public static LensModel Model(ThermalLens lens)
    {
        if (lens.System.Fields.Any(f => Math.Abs(f.X) > 1e-12))
            throw new NotSupportedException("a field off the y axis: for a rotationally symmetric lens, give it on the y axis");
        return new LensModel(lens.System, NoCatalog, w => lens.Indices[w]);
    }

    /// <summary>The combined RMS wavefront error, waves, with the image plane moved by <paramref name="focusShift"/> mm; +∞ when a field cannot be traced.</summary>
    public static double Rms(LensModel lens, double focusShift, QualityOptions? options = null) =>
        Combine(lens, options, (f, w, o) =>
            Variance(WavefrontCalculator.Compute(lens, f, w, o.EffectiveWavefront(focusShift), o.Sampling).Samples, null, o));

    /// <summary>
    /// The RMS of the wavefront's change from <paramref name="nominal"/> to <paramref name="lens"/>, waves: at each
    /// pupil sample, the difference of the two wavefronts, its standard deviation over the pupil for each field and
    /// wavelength, combined as <see cref="Rms"/> combines. It leaves out the design's own aberrations, which the
    /// total RMS includes. The two lenses must have the same fields and wavelengths; +∞ when a field cannot be
    /// traced in either.
    /// </summary>
    public static double RmsChange(LensModel nominal, LensModel lens, double focusShift = 0.0, QualityOptions? options = null) =>
        Combine(lens, options, (f, w, o) => Variance(
            WavefrontCalculator.Compute(lens, f, w, o.EffectiveWavefront(focusShift), o.Sampling).Samples,
            WavefrontCalculator.Compute(nominal, f, w, o.EffectiveWavefront(0.0), o.Sampling).Samples, o));

    /// <summary>√(Σ w_f w_λ σ² / Σ w_f w_λ) over the fields and wavelengths; +∞ when one cannot be traced.</summary>
    private static double Combine(LensModel lens, QualityOptions? options, Func<int, int, QualityOptions, double> variance)
    {
        var o = options ?? new QualityOptions();
        var system = lens.System;
        var waves = o.Polychromatic ? Enumerable.Range(0, system.Wavelengths.Count) : new[] { lens.PrimaryWavelength };
        double sum = 0.0, weights = 0.0;
        try
        {
            for (int f = 0; f < system.Fields.Count; f++)
                foreach (int w in waves)
                {
                    double weight = system.Fields[f].Weight * system.Wavelengths[w].Weight;
                    if (weight <= 0.0) continue;
                    double v = variance(f, w, o);
                    if (!double.IsFinite(v)) return double.PositiveInfinity;
                    sum += weight * v;
                    weights += weight;
                }
        }
        catch (Exception e) when (e is InvalidOperationException or ArithmeticException)
        {
            return double.PositiveInfinity;
        }
        return weights > 0.0 ? Math.Sqrt(sum / weights) : double.PositiveInfinity;
    }

    /// <summary>
    /// The variance over the pupil of W, or of W less <paramref name="less"/>'s W at the same samples, each sample
    /// weighted by its quadrature area; the samples that arrive in both count. NaN when none does.
    /// </summary>
    private static double Variance(IReadOnlyList<WavefrontSample> samples, IReadOnlyList<WavefrontSample>? less, QualityOptions o)
    {
        var points = o.Sampling.Points();
        if (samples.Count != points.Count || (less != null && less.Count != points.Count)) return double.NaN;
        double total = 0.0, mean = 0.0, square = 0.0;
        for (int k = 0; k < samples.Count; k++)
        {
            if (samples[k].Vignetted || (less != null && less[k].Vignetted) || !double.IsFinite(samples[k].W)) continue;
            double d = samples[k].W - (less?[k].W ?? 0.0), a = points[k].Area;
            total += a;
            mean += a * d;
            square += a * d * d;
        }
        if (!(total > 0.0)) return double.NaN;
        mean /= total;
        return Math.Max(0.0, square / total - mean * mean);
    }

    /// <summary>
    /// The image-plane move, mm, that minimizes the RMS wavefront error, and that RMS: Brent's method in a bracket
    /// around <paramref name="start"/>, a few depths of focus wide, widened while the minimum sits on its edge.
    /// </summary>
    public static (double Shift, double Rms) BestFocus(LensModel lens, double start, double depthOfFocus,
        QualityOptions? options = null)
    {
        double F(double z) => Rms(lens, z, options);
        double half = Math.Max(4.0 * Math.Abs(depthOfFocus), 1e-6);
        for (int widen = 0; widen < 12; widen++, half *= 3.0)
        {
            var (x, fx) = Brent(F, start - half, start + half, 1e-7 * Math.Max(1.0, Math.Abs(start)) + 1e-9);
            bool onEdge = Math.Abs(x - (start - half)) < 0.02 * half || Math.Abs(x - (start + half)) < 0.02 * half;
            if (!onEdge || !double.IsFinite(fx)) return (x, fx);
            start = x;
        }
        return (start, F(start));
    }

    /// <summary>
    /// The MTF of every field at <paramref name="frequencies"/>, cycles/mm, with the image plane moved by
    /// <paramref name="focusShift"/>: the diffraction MTF, or the geometric one, as <see cref="QualityOptions.MtfKind"/> says.
    /// </summary>
    public static GeometricMtf.Modulation[][] Mtf(LensModel lens, IReadOnlyList<double> frequencies, double focusShift,
        QualityOptions? options = null)
    {
        var o = options ?? new QualityOptions();
        var wavefront = o.Wavefront with { FocusShift = focusShift };
        if (o.MtfKind == MtfKind.Geometric)
        {
            var mtf = o.Polychromatic ? o.Mtf : o.Mtf with { Wavelength = lens.PrimaryWavelength };
            return Enumerable.Range(0, lens.System.Fields.Count)
                .Select(f => GeometricMtf.Compute(lens, f, frequencies, wavefront, mtf).Mtf.ToArray())
                .ToArray();
        }
        // The autocorrelation fixes the pupil coordinates to the stop.
        if (wavefront.ChiefRay == ChiefRayDefinition.VignettedCenter) wavefront = wavefront with { ChiefRay = ChiefRayDefinition.StopCenter };
        var d = new DiffractionMtfOptions { Grid = o.DiffractionGrid, Wavelength = o.Polychromatic ? null : lens.PrimaryWavelength };
        return Enumerable.Range(0, lens.System.Fields.Count)
            .Select(f => DiffractionMtf.Compute(lens, f, frequencies, wavefront, d).Mtf.ToArray())
            .ToArray();
    }

    /// <summary>
    /// The image at each temperature: the RMS wavefront error and the MTF, at the fixed image plane and refocused; in
    /// <paramref name="at"/>'s air (its pressure, humidity and CO₂) at each temperature, or the design's when it is null.
    /// </summary>
    public static IReadOnlyList<QualityState> Compute(ThermalModel model, IEnumerable<double> temperatures,
        IReadOnlyList<double>? frequencies = null, QualityOptions? options = null, AirConditions? at = null)
    {
        var freqs = frequencies ?? Array.Empty<double>();
        var list = new List<QualityState>();
        foreach (double t in temperatures)
        {
            var lens = model.At((at ?? model.NominalAir) with { TemperatureC = t });
            var lm = Model(lens);
            var focus = FocusReport.Compute(model, new[] { t }, 0.5, contributions: false, at).States[0];
            double fixedRms = Rms(lm, 0.0, options);
            var (shift, best) = BestFocus(lm, focus.Defocus, focus.DepthOfFocus, options);
            var fixedMtf = freqs.Count > 0 ? Mtf(lm, freqs, 0.0, options) : Array.Empty<GeometricMtf.Modulation[]>();
            var bestMtf = freqs.Count > 0 ? Mtf(lm, freqs, shift, options) : Array.Empty<GeometricMtf.Modulation[]>();
            list.Add(new QualityState(t, fixedRms, shift, best, fixedMtf, bestMtf));
        }
        return list;
    }

    /// <summary>
    /// The temperatures, around the nominal one, over which the lowest MTF of every field and both azimuths at
    /// <paramref name="frequency"/> stays at or above <paramref name="limit"/> (Schuster and Franks 2012), searched
    /// from <paramref name="min"/> to <paramref name="max"/> °C in steps of <paramref name="step"/> K, the ends found
    /// by interpolation. At the fixed image plane, or refocused at each temperature. Null when the nominal lens
    /// itself falls below the limit. In <paramref name="at"/>'s air at each temperature, or the design's when it is null.
    /// </summary>
    public static (double Low, double High)? Range(ThermalModel model, double frequency, double limit, double min,
        double max, double step = 5.0, bool refocused = false, QualityOptions? options = null, AirConditions? at = null)
    {
        double Lowest(double t)
        {
            var lens = model.At((at ?? model.NominalAir) with { TemperatureC = t });
            var lm = Model(lens);
            double shift = 0.0;
            if (refocused)
            {
                var focus = FocusReport.Compute(model, new[] { t }, 0.5, contributions: false, at).States[0];
                shift = BestFocus(lm, focus.Defocus, focus.DepthOfFocus, options).Shift;
            }
            return Mtf(lm, new[] { frequency }, shift, options)
                .Min(m => Math.Min(m[0].Sagittal, m[0].Tangential));
        }

        double t0 = model.NominalTemperatureC;
        double m0 = Lowest(t0);
        if (!(m0 >= limit)) return null;
        double Edge(double direction, double bound)
        {
            double t = t0, m = m0;
            while (direction * (bound - t) > 1e-9)
            {
                double next = direction > 0 ? Math.Min(t + step, bound) : Math.Max(t - step, bound);
                double mn = Lowest(next);
                if (!(mn >= limit)) return t + (next - t) * (m - limit) / (m - mn);
                (t, m) = (next, mn);
            }
            return bound;
        }
        return (Edge(-1, min), Edge(+1, max));
    }

    /// <summary>Brent's method for a minimum of <paramref name="f"/> in [a, b].</summary>
    internal static (double X, double Fx) Brent(Func<double, double> f, double a, double b, double tolerance)
    {
        const double golden = 0.3819660112501051;
        double x = a + golden * (b - a), w = x, v = x;
        double fx = f(x), fw = fx, fv = fx;
        double d = 0.0, e = 0.0;
        for (int iteration = 0; iteration < 100; iteration++)
        {
            double m = 0.5 * (a + b), tol = tolerance + 1e-10 * Math.Abs(x), tol2 = 2.0 * tol;
            if (Math.Abs(x - m) <= tol2 - 0.5 * (b - a)) break;
            bool golden_step = true;
            if (Math.Abs(e) > tol)
            {
                double r = (x - w) * (fx - fv), q = (x - v) * (fx - fw), p = (x - v) * q - (x - w) * r;
                q = 2.0 * (q - r);
                if (q > 0) p = -p; else q = -q;
                if (Math.Abs(p) < Math.Abs(0.5 * q * e) && p > q * (a - x) && p < q * (b - x))
                {
                    e = d;
                    d = p / q;
                    double u0 = x + d;
                    if (u0 - a < tol2 || b - u0 < tol2) d = x < m ? tol : -tol;
                    golden_step = false;
                }
            }
            if (golden_step)
            {
                e = (x < m ? b : a) - x;
                d = golden * e;
            }
            double u = Math.Abs(d) >= tol ? x + d : x + (d > 0 ? tol : -tol);
            double fu = f(u);
            if (fu <= fx)
            {
                if (u < x) b = x; else a = x;
                (v, fv, w, fw, x, fx) = (w, fw, x, fx, u, fu);
            }
            else
            {
                if (u < x) a = u; else b = u;
                if (fu <= fw || w == x) { (v, fv, w, fw) = (w, fw, u, fu); }
                else if (fu <= fv || v == x || v == w) { v = u; fv = fu; }
            }
        }
        return (x, fx);
    }
}
