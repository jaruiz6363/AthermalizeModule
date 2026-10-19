using AberrationCalculator.Core.RayTrace;
using Athermalize.Core.Lens;

namespace Athermalize.Core.Analysis;

/// <summary>The paraxial state of a lens at one temperature, at the primary wavelength.</summary>
/// <param name="TemperatureC">The temperature.</param>
/// <param name="Efl">The effective focal length.</param>
/// <param name="Bfl">The back focal length, from the last surface.</param>
/// <param name="Defocus">The paraxial focus less the image plane, along the axis: positive when the image falls beyond the plane.</param>
/// <param name="Change">The defocus less the nominal temperature's.</param>
/// <param name="Linear">The change the first-order coefficient predicts, its rate at the nominal temperature times ΔT.</param>
/// <param name="FNumber">The working F-number, 1/(2n′|u′|) of the marginal ray in image space.</param>
/// <param name="DepthOfFocus">The quarter-wave depth of focus, ±2λN², λ in the image space.</param>
public sealed record FocusState(double TemperatureC, double Efl, double Bfl, double Defocus, double Change, double Linear,
    double FNumber, double DepthOfFocus)
{
    /// <summary>The change in depths of focus.</summary>
    public double ChangeInDepths => Change / DepthOfFocus;
}

/// <summary>One part's contribution to the thermal change, at the nominal temperature.</summary>
/// <param name="Part">The part.</param>
/// <param name="Name">Its name.</param>
/// <param name="DefocusPerK">d(defocus)/dT with only this part warmed, mm/K.</param>
/// <param name="EflPerK">dEFL/dT with only this part warmed, mm/K.</param>
public sealed record Contribution(ThermalPart Part, string Name, double DefocusPerK, double EflPerK);

/// <summary>
/// The first-order thermal report: the focal length, back focal length and defocus at each temperature, and each
/// part's contribution to their rates at the nominal temperature.
///
/// <para>A part's contribution is the rate of change with that part alone warmed: its glass's index and size, a mirror's
/// substrate, the housing, or a mount part. To first order the contributions add up to the whole lens's rate, which the
/// report gives beside them; the difference is second order in the step.</para>
/// </summary>
public sealed class FocusReport
{
    private FocusReport(IReadOnlyList<FocusState> states, IReadOnlyList<Contribution> contributions,
        double defocusPerK, double eflPerK)
    {
        States = states;
        Contributions = contributions;
        DefocusPerK = defocusPerK;
        EflPerK = eflPerK;
    }

    public IReadOnlyList<FocusState> States { get; }
    public IReadOnlyList<Contribution> Contributions { get; }

    /// <summary>d(defocus)/dT of the whole lens at the nominal temperature, mm/K.</summary>
    public double DefocusPerK { get; }

    /// <summary>dEFL/dT of the whole lens at the nominal temperature, mm/K.</summary>
    public double EflPerK { get; }

    /// <summary>The depth of focus at the nominal temperature, ±2λN², mm.</summary>
    public double DepthOfFocus { get; private init; }

    /// <summary>
    /// The temperature change that moves the focus by one depth of focus at the nominal rate,
    /// 2λN²/|d(defocus)/dT|, K: Schuster and Franks's (2012, eq. 4) first-order athermal range,
    /// 2λN²/(f(α_H + γ_T)) for a thin lens.
    /// </summary>
    public double TemperatureForOneDepthK => DepthOfFocus / Math.Abs(DefocusPerK);

    /// <summary>
    /// The report at <paramref name="temperatures"/>, °C, the rates by central differences of ±<paramref name="stepK"/>;
    /// without the parts' contributions when <paramref name="contributions"/> is false.
    /// </summary>
    public static FocusReport Compute(ThermalModel model, IEnumerable<double> temperatures, double stepK = 0.5, bool contributions = true)
    {
        double t0 = model.NominalTemperatureC;
        var air = model.NominalAir;
        (double Defocus, double Efl) Rate(Func<ThermalPart, bool>? only)
        {
            var hot = Paraxial(model.At(air with { TemperatureC = t0 + stepK }, only));
            var cold = Paraxial(model.At(air with { TemperatureC = t0 - stepK }, only));
            return ((hot.Defocus - cold.Defocus) / (2 * stepK), (hot.Efl - cold.Efl) / (2 * stepK));
        }

        var (defocusRate, eflRate) = Rate(null);
        var byPart = (contributions ? model.Parts() : Array.Empty<ThermalPart>()).Select(p =>
        {
            var (d, e) = Rate(q => q == p);
            return new Contribution(p, model.Describe(p), d, e);
        }).ToList();

        var nominal = Paraxial(model.At(air));
        var states = temperatures.Select(t =>
        {
            var lens = model.At(air with { TemperatureC = t });
            var p = Paraxial(lens);
            double lambdaMm = lens.System.Wavelengths[Math.Max(0, lens.System.PrimaryWavelengthIndex)].Value * 1e-3;
            return new FocusState(t, p.Efl, p.Bfl, p.Defocus, p.Defocus - nominal.Defocus, defocusRate * (t - t0),
                p.FNumber, 2.0 * lambdaMm * p.FNumber * p.FNumber);
        }).ToList();
        var lens0 = model.At(air);
        var p0 = Paraxial(lens0);
        double lambda0 = lens0.System.Wavelengths[Math.Max(0, lens0.System.PrimaryWavelengthIndex)].Value * 1e-3;
        return new FocusReport(states, byPart, defocusRate, eflRate) { DepthOfFocus = 2.0 * lambda0 * p0.FNumber * p0.FNumber };
    }

    /// <summary>The paraxial focus less the image plane, mm, at the primary wavelength.</summary>
    public static double Defocus(ThermalLens lens) => Paraxial(lens).Defocus;

    private static (double Efl, double Bfl, double Defocus, double FNumber) Paraxial(ThermalLens lens)
    {
        var system = lens.System;
        var trace = ParaxialTrace.Trace(system, lens.PrimaryIndices, 0.0);
        int last = system.LastOpticalSurface();
        double image = 0.0;
        for (int i = last; i < system.Surfaces.Count - 1; i++) image += system.Surfaces[i].Thickness;
        double u = Math.Abs(trace.N[last] * trace.U[last]);
        return (trace.Efl, trace.Bfl, trace.ParaxialFocusDistance - image, u > 0 ? 1.0 / (2.0 * u) : double.PositiveInfinity);
    }
}
