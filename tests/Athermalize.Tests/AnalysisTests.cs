using AberrationCalculator.Core.Models;
using Athermalize.Core.Air;
using Athermalize.Core.Analysis;
using Athermalize.Core.Lens;
using Athermalize.Core.Materials;
using Xunit;
using Xunit.Abstractions;

namespace Athermalize.Tests;

/// <summary>The focus report and the image quality against closed forms.</summary>
public class AnalysisTests
{
    private readonly ITestOutputHelper _out;
    public AnalysisTests(ITestOutputHelper output) => _out = output;

    private static readonly MountMaterial Aluminum = new("aluminum", 23.6e-6, "Liu et al. 2025");

    /// <summary>A doublet and a singlet at f/4, focused at 20 °C: a spacer touching 9 mm off the axis between them, and a part from the singlet to the image.</summary>
    private static (OpticalSystem System, Mount Mount) Triplet()
    {
        var system = Lenses.AtInfinity(25, 0.5875618, 0.4861327, 0.6562725)
            .Surface(61.07, 6.0, "N-BK7", stop: true, semiDiameter: 14)
            .Surface(-44.17, 2.5, "N-SF6", semiDiameter: 14)
            .Surface(-128.89, 4.0, semiDiameter: 14)
            .Surface(150.0, 3.0, "N-BK7", semiDiameter: 13)
            .Surface(-300.0, 80.0, semiDiameter: 13)
            .Image();
        var mount = new Mount(Aluminum, new[]
        {
            MountSegment.Spacer(3, new MountMaterial("brass", 19e-6), 9.0),
            new MountSegment(4, 6, new MountMaterial("titanium", 9.2e-6)),
        });
        var model = new ThermalModel(system, Catalogs.Library, mount);
        system.Surfaces[5].Thickness += FocusReport.Defocus(model.At(20.0));
        return (system, mount);
    }

    /// <summary>
    /// The parts' shares add up to the whole lens's rate, and the rate times ΔT gives the change to 3 % over 60 K
    /// (2.6 % warm, 0.4 % cold), the rest being the rise of the glasses' dn/dT with temperature.
    /// </summary>
    [Fact]
    public void The_parts_shares_add_up_to_the_whole_lens()
    {
        var (system, mount) = Triplet();
        var model = new ThermalModel(system, Catalogs.Library, mount);
        var report = FocusReport.Compute(model, new[] { -40.0, 20.0, 80.0 });
        foreach (var c in report.Contributions) _out.WriteLine($"{c.Name,-36} {c.DefocusPerK * 1e3,9:F4} µm/K  EFL {c.EflPerK * 1e3,9:F4} µm/K");
        _out.WriteLine($"{"total",-36} {report.DefocusPerK * 1e3,9:F4} µm/K  EFL {report.EflPerK * 1e3,9:F4} µm/K");
        Assert.Equal(6, report.Contributions.Count);   // three glasses, the housing, two parts
        Assert.InRange(report.Contributions.Sum(c => c.DefocusPerK) / report.DefocusPerK - 1.0, -1e-4, 1e-4);
        Assert.InRange(report.Contributions.Sum(c => c.EflPerK) / report.EflPerK - 1.0, -1e-4, 1e-4);
        Assert.Equal(0.0, report.States[1].Change);
        foreach (var s in report.States)
            Assert.InRange(s.Change - s.Linear, -3e-2 * Math.Abs(s.Linear) - 1e-12, 3e-2 * Math.Abs(s.Linear) + 1e-12);
    }

    /// <summary>A thin singlet's shares: the glass's f·x_f, the housing's −BFL·α_h (Jamieson 1981).</summary>
    [Fact]
    public void A_thin_singlets_shares_are_its_glass_and_its_housing()
    {
        var m = Catalogs.Find("SCHOTT:N-BK7");
        double nd = m.Index(0.5875618, AirConditions.Standard).Relative, r = 2.0 * 100.0 * (nd - 1.0);
        var system = Lenses.AtInfinity(10).Surface(r, 0.01, "N-BK7", stop: true).Surface(-r, 100.0).Image();
        var model = new ThermalModel(system, Catalogs.Library, new Mount(Aluminum));
        var report = FocusReport.Compute(model, new[] { 20.0 });
        double f = report.States[0].Efl, bfl = report.States[0].Bfl;
        double xf = ThermoOptic.At(m, model.VacuumWavelengthsUm[0], 20.0).OptothermalExpansion;
        var glass = report.Contributions.Single(c => c.Part.Kind == ThermalPartKind.Glass);
        var housing = report.Contributions.Single(c => c.Part.Kind == ThermalPartKind.Housing);
        Assert.InRange(glass.DefocusPerK / (f * xf) - 1.0, -1e-3, 1e-3);
        Assert.InRange(glass.EflPerK / (f * xf) - 1.0, -1e-3, 1e-3);
        Assert.Equal(-100.0 * Aluminum.Cte, housing.DefocusPerK, 12);
        Assert.Equal(0.0, housing.EflPerK, 12);
        _ = bfl;
    }

    /// <summary>
    /// A paraboloid is perfect on axis, so moving its image plane by Δz gives a pure defocus, whose RMS is
    /// Δz/(8√12 N²) in lengths (the footnote to Itek 1972, Table G-3), or that times 1 − ε² over an annulus of
    /// obstruction ε; the compensator finds the focus again.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    public void A_pure_defocus_has_its_closed_form_RMS(double obscuration)
    {
        const double f = 500.0, epd = 50.0, dz = 0.05, lambda = 0.5875618;
        var system = Lenses.AtInfinity(epd, lambda).Surface(-2 * f, -f + dz, "MIRROR", stop: true, conic: -1.0).Image();
        system.Fields.RemoveAt(1);
        var model = new ThermalModel(system, Catalogs.Library, new Mount(Aluminum), AirConditions.Vacuum(20));
        var lens = ImageQuality.Model(model.At(AirConditions.Vacuum(20)));
        var options = new QualityOptions { Obscuration = obscuration };
        double rms = ImageQuality.Rms(lens, 0.0, options);
        double n = f / epd, expected = dz / (8.0 * Math.Sqrt(12.0) * n * n) / (lambda * 1e-3) * (1 - obscuration * obscuration);
        _out.WriteLine($"ε {obscuration}: RMS {rms:F6} waves against {expected:F6}");
        Assert.InRange(rms / expected - 1.0, -2e-3, 2e-3);

        var (shift, best) = ImageQuality.BestFocus(lens, 0.0, 2.0 * lambda * 1e-3 * n * n, options);
        Assert.InRange(shift + dz, -1e-5, 1e-5);   // the plane is dz nearer the mirror than the focus at −f
        Assert.InRange(best, 0.0, 1e-4);
    }

    /// <summary>
    /// First order against real rays: the paraxial focus's change with temperature against the change of the best
    /// focus by the polychromatic RMS wavefront. For this doublet and singlet they agree to a few percent; Liu et
    /// al. (2025) found a factor of 2 on a seven-element lens between their paraxial model and a ray-traced best
    /// focus, so the module reports both.
    /// </summary>
    [Fact]
    public void The_first_order_focus_change_against_the_best_focus()
    {
        var (system, mount) = Triplet();
        var model = new ThermalModel(system, Catalogs.Library, mount);
        var states = ImageQuality.Compute(model, new[] { -40.0, 20.0, 80.0 });
        var report = FocusReport.Compute(model, new[] { -40.0, 20.0, 80.0 });
        for (int i = 0; i < 3; i += 2)
        {
            double best = states[i].FocusShift - states[1].FocusShift, paraxial = report.States[i].Change;
            _out.WriteLine($"{states[i].TemperatureC} °C: paraxial {paraxial * 1e3:F2} µm, best focus {best * 1e3:F2} µm; " +
                           $"RMS fixed {states[i].RmsFixed:F3}, refocused {states[i].RmsRefocused:F3} (nominal {states[1].RmsRefocused:F3})");
            Assert.InRange(best / paraxial - 1.0, -0.05, 0.05);
            Assert.True(states[i].RmsRefocused <= states[i].RmsFixed + 1e-9);
        }
    }

    /// <summary>
    /// The temperatures over which the geometric MTF stays above a limit (Schuster and Franks 2012), against a closed form. A
    /// paraboloid is perfect on axis, so defocused by Δz at f/N its geometric MTF is that of a uniform blur circle of
    /// diameter Δz/N, 2J₁(x)/x with x = πνΔz/N. On a substrate of α_m in a housing of α_h that holds its image plane,
    /// Δz = f·(α_m − α_h)·ΔT, so the MTF at ν falls to a limit at ΔT = x₀N/(πν f |α_m − α_h|), 2J₁(x₀)/x₀ = limit.
    /// Refocused, it is perfect at every temperature.
    /// </summary>
    [Fact]
    public void The_MTF_temperature_range()
    {
        const double f = 200.0, epd = 50.0, frequency = 40.0, limit = 0.8;
        var system = Lenses.AtInfinity(epd, 0.55).Surface(-2 * f, -f, "MIRROR", stop: true, conic: -1.0).Image();
        system.Fields.RemoveAt(1);
        var substrate = new MountMaterial("substrate", 7.1e-6);
        var model = new ThermalModel(system, Catalogs.Library,
            new Mount(Aluminum, mirrorSubstrates: new Dictionary<int, MountMaterial> { [1] = substrate }));

        static double Jinc(double x)
        {
            double sum = 0.0, term = 1.0;                    // 2J₁(x)/x = Σ (−x²/4)^k / (k!(k+1)!)
            for (int k = 0; k < 40; k++)
            {
                sum += term;
                term *= -x * x / 4.0 / ((k + 1) * (k + 2));
            }
            return sum;
        }
        double lo = 0.0, hi = 3.0;
        for (int i = 0; i < 60; i++) { double mid = 0.5 * (lo + hi); if (Jinc(mid) > limit) lo = mid; else hi = mid; }
        double n = f / epd, dT = lo * n / (Math.PI * frequency * f * Math.Abs(substrate.Cte - Aluminum.Cte));

        var geometric = new QualityOptions { MtfKind = MtfKind.Geometric };
        var range = ImageQuality.Range(model, frequency, limit, -100, 140, 2.0, options: geometric);
        var refocused = ImageQuality.Range(model, frequency, limit, -100, 140, 20.0, refocused: true, options: geometric);
        _out.WriteLine($"closed form 20 ± {dT:F3} °C; fixed plane {range}; refocused {refocused}");
        Assert.NotNull(range);
        Assert.InRange(range!.Value.Low - (20.0 - dT), -0.1, 0.1);
        Assert.InRange(range.Value.High - (20.0 + dT), -0.1, 0.1);
        Assert.Equal((-100.0, 140.0), refocused);
    }

    /// <summary>
    /// The same with the diffraction MTF, in the thermal infrared: a paraboloid of 200 mm at F/8 and 10 µm, on a
    /// substrate that does not expand, in a housing of α_h = 100×10⁻⁶/K, so that Δz = f·α_h·ΔT is a pure defocus.
    /// In focus its MTF is the perfect lens's, circle(ν/ν_c), ν_c = 2 sin θ/λ, while the geometric MTF says 1. At
    /// 5 cycles/mm it falls to 0.8 of that where Hopkins's defocused OTF, with W₂₀ = Δz sin²θ/(2λ), does: the ends
    /// of the range come within 1 K of it, the rest being the steps' linear interpolation and terms of order Δz/f.
    /// </summary>
    [Fact]
    public void The_diffraction_MTF_temperature_range_in_the_infrared()
    {
        const double f = 200.0, epd = 25.0, lambda = 10e-3, frequency = 5.0, housing = 100e-6;
        var system = Lenses.AtInfinity(epd, 10.0).Surface(-2 * f, -f, "MIRROR", stop: true, conic: -1.0).Image();
        system.Fields.RemoveAt(1);
        var model = new ThermalModel(system, Catalogs.Library,
            new Mount(MountMaterial.OfCte(housing), mirrorSubstrates: new Dictionary<int, MountMaterial> { [1] = MountMaterial.OfCte(0.0) }));

        double a = epd / 2, sine = a / (f + a * a / (4 * f)), cutoff = 2 * sine / lambda;
        static double Circle(double v) => v >= 1 ? 0 : 2 / Math.PI * (Math.Acos(v) - v * Math.Sqrt(1 - v * v));
        static double Hopkins(double s, double w20)
        {
            double h = s / 2, upper = 1 - h, sum = 0;
            const int n = 20_000;
            for (int k = 0; k < n; k++)
            {
                double x = (k + 0.5) * upper / n;
                sum += Math.Sqrt(Math.Max(0, 1 - (x + h) * (x + h))) * Math.Cos(4 * Math.PI * w20 * s * x);
            }
            return Math.Abs(4 / Math.PI * sum * upper / n);
        }
        double s0 = 2 * frequency / cutoff, perfect = Circle(frequency / cutoff), limit = 0.8 * perfect;
        double lo = 0, hi = 2;
        for (int i = 0; i < 60; i++) { double w = 0.5 * (lo + hi); if (Hopkins(s0, w) > limit) lo = w; else hi = w; }
        double dT = 2 * lambda * lo / (sine * sine) / (f * housing);

        var lens = ImageQuality.Model(model.At(20.0));
        var diffraction = ImageQuality.Mtf(lens, new[] { frequency }, 0.0)[0][0];
        var geometric = ImageQuality.Mtf(lens, new[] { frequency }, 0.0, new QualityOptions { MtfKind = MtfKind.Geometric })[0][0];
        var range = ImageQuality.Range(model, frequency, limit, -100, 140, 2.0);
        _out.WriteLine($"in focus: diffraction {diffraction.Tangential:F4} against {perfect:F4}, geometric {geometric.Tangential:F4}; " +
                       $"range {range} against 20 ± {dT:F2} °C");
        Assert.InRange(diffraction.Sagittal - perfect, -3e-3, 3e-3);
        Assert.InRange(diffraction.Tangential - perfect, -3e-3, 3e-3);
        Assert.Equal(1.0, geometric.Tangential, 6);
        Assert.NotNull(range);
        Assert.InRange(range!.Value.Low - (20 - dT), -1.0, 1.0);
        Assert.InRange(range.Value.High - (20 + dT), -1.0, 1.0);
    }
}
